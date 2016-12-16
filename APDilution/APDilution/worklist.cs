using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace APDilution
{
    public class worklist
    {
    
        List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
        List<DilutionInfo> rawDilutionInfos = new List<DilutionInfo>();
        Dictionary<string, List<int>> plateName_LastDilutionPositions = new Dictionary<string, List<int>>();
        //Dictionary<DilutionInfo, List<int>> dilutionInfo_WellID = new Dictionary<DilutionInfo, List<int>>();
        public List<string> DoJob(List<DilutionInfo> dilutionInfos, List<DilutionInfo> rawDilutionInfos)
        {
            //plateName_LastDilutionPositions.Clear();
            //int parallelHoleCount = CountParallelHole(dilutionInfos);
            //this.dilutionInfos = FilterParallel(dilutionInfos);
            this.rawDilutionInfos = rawDilutionInfos;
            this.dilutionInfos = dilutionInfos;

            List<List<PipettingInfo>> firstPlateBuffer = new List<List<PipettingInfo>>();
            List<List<PipettingInfo>> secondPlateBuffer = new List<List<PipettingInfo>>();
            //List<PipettingInfo> firstPlateSample = new List<PipettingInfo>();
            //List<PipettingInfo> secondPlateSample = new List<PipettingInfo>();
            //from buffer & sample to dilution
            var bufferPipettings = GenerateBufferPipettingInfos(ref firstPlateBuffer, ref secondPlateBuffer);
            var samplePipettings = GenerateSamplePipettingInfos();
            Save2Excel(firstPlateBuffer, secondPlateBuffer);

            //from dilution to reaction plate
            List<PipettingInfo> transferPipettings = GenerateTransferPipettingInfos();
            
            List<string> strs = new List<string>();
            strs.AddRange(Format(bufferPipettings,Configurations.Instance.BufferLiquidClass));
            strs.Add("B;");
            strs.AddRange(Format(samplePipettings,Configurations.Instance.SampleLiquidClass));
            strs.Add("B;");
            strs.AddRange(Format(transferPipettings, Configurations.Instance.TransferLiquidClass));
            strs.Add("B;");
            return strs;
        }

        private void Save2Excel(List<List<PipettingInfo>> firstPlateBuffer, 
            List<List<PipettingInfo>> secondPlateBuffer)
        {
            ExcelWriter excelWriter = new ExcelWriter();
            string outputFolder = Utility.GetOutputFolder();
            List<PipettingInfo> firstBufferFlat =   new List<PipettingInfo> ();
            firstPlateBuffer.ForEach(x => firstBufferFlat.AddRange(x));
            List<PipettingInfo> secondBufferFlat = new List<PipettingInfo>();
            secondPlateBuffer.ForEach(x => secondBufferFlat.AddRange(x));
            excelWriter.PrepareSave2File("firstBuffer", firstBufferFlat);
            //excelWriter.PrepareSave2File("firstPlateSample", firstPlateSample);
            excelWriter.PrepareSave2File("secondPlateBuffer", secondBufferFlat);
            //excelWriter.PrepareSave2File("secondPlateSample", secondPlateSample);
            excelWriter.Save();
        }

       

        private void Parse(List<DilutionInfo> dilutionInfos, Dictionary<DilutionInfo, List<int>> dilutionInfo_WellID)
        {
            for(int i = 0; i< dilutionInfos.Count; i++)
            {
                var info = dilutionInfos[i];
                int wellID = i + 1;
                if (dilutionInfo_WellID.ContainsKey(info))
                    dilutionInfo_WellID[info].Add(wellID);
                else
                    dilutionInfo_WellID.Add(info, new List<int>());
            }
        }


        private IEnumerable<string> Format(List<PipettingInfo> pipettingInfos,string liquidClass)
        {
            List<string> commands = new List<string>();
            foreach (var pipettingInfo in pipettingInfos)
            {
                commands.Add(GetAspirate(pipettingInfo.srcLabware, pipettingInfo.srcWellID, pipettingInfo.vol, liquidClass));
                commands.Add(GetDispense(pipettingInfo.dstLabware, pipettingInfo.dstWellID, pipettingInfo.vol, liquidClass));
            }
            commands.Add("W");
            return commands;
        }

        private IEnumerable<string> Format(List<List<PipettingInfo>> pipettings,string liquidClass)
        {
            List<string> commands = new List<string>();
            foreach (var samePlatePipettings in pipettings)
            {
                commands.AddRange(Format(samePlatePipettings, liquidClass));
            }
            return commands;
        }

        internal List<PipettingInfo> GenerateTransferPipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            var firstPlateSamples = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateSamples = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            var firstPlateName = "Dilution1";
            var secondPlateName = "Dilution2";
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(firstPlateName, firstPlateSamples));
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(secondPlateName, secondPlateSamples));
            return pipettingInfos;
        }

        private DilutionInfo Clone(DilutionInfo x)
        {
            DilutionInfo newInfo = new DilutionInfo(x.type, x.dilutionTimes, x.seqIDinThisType, x.destWellID,x.gradualStep);
            return newInfo;
        }

        private List<PipettingInfo> GenerateTransferPipettingInfos(string plateName, List<DilutionInfo> dilutionInfosWithoutParallel)
        {
            var srcPositionOnDilutionPlate = plateName_LastDilutionPositions[plateName];
            int index = 0;
            var vol = Configurations.Instance.ReactionVolume;
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            foreach (var current in dilutionInfosWithoutParallel)
            {
                var parallelDilutions = dilutionInfos.Where(x => x.type == current.type &&
                    x.seqIDinThisType == current.seqIDinThisType &&
                    x.dilutionTimes == current.dilutionTimes).ToList();
                var destWells = parallelDilutions.Select(x => x.destWellID).ToList();
                var srcWellID = srcPositionOnDilutionPlate[index];
                if(Configurations.Instance.IsGradualPipetting && current.type == SampleType.Norm)
                {
                    //closely layout 24*N normal samples
                    Console.WriteLine(current.destWellID);
                    int gradualWellsNeeded = Utility.GetNeededGradualWellsCount(current.dilutionTimes);
                    for (int i = 0; i < gradualWellsNeeded; i++)
                    {
                        int startWellID = current.destWellID + i * 2;
                        srcWellID = srcPositionOnDilutionPlate[index++];
                        for(int parallel = 0; parallel < 2; parallel++)
                        {
                            int dstWellID = startWellID + parallel;
                            pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, "Reaction", dstWellID, vol, 1, current.type));
                        }
                    }
                }
                else
                {
                    foreach (var dstWellID in destWells)
                    {
                        pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, "Reaction", dstWellID, vol, 1, current.type));
                    }
                    index++;
                }
            }
            //foreach(var dilutionInfo in dilutionInfos)
            //{
            //    var destWells = dilutionInfos.Where(x=>x.destWellID == dilutionInfo.destWellID).Select(x=>x.destWellID).ToList();
            //    var srcWellID = srcPositionOnDilutionPlate[index];
            //    foreach(var dstWellID in destWells)
            //    {
            //        pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, "Reaction", dstWellID, vol,1,dilutionInfo.type));
            //    }
            //    index++;
            //}
            return pipettingInfos;

        }

       

       

        #region sample
        internal List<PipettingInfo> GenerateSamplePipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            
            var firstPlateSamples = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateSamples = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            var firstPlate = GenerateSamplePipettingInfos(firstPlateSamples, "Dilution1");
            var secondPlate = GenerateSamplePipettingInfos(secondPlateSamples, "Dilution2");
            pipettingInfos.AddRange(firstPlate);
            pipettingInfos.AddRange(secondPlate);
            return pipettingInfos;
        }

        private List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> dilutionInfos, string destPlateLabel)
        {
            int columnIndex = 0;
            List<PipettingInfo> samplePipettingInfos = new List<PipettingInfo>();
            if(Configurations.Instance.IsGradualPipetting)
            {
                if (dilutionInfos.Count > 8) //max 8 samples per plate
                    throw new Exception(string.Format("Samples to dilution is: {0}, cannot > 8 per plate.", dilutionInfos.Count));
                
                for(int i = 0; i< dilutionInfos.Count; i++)
                {
                    samplePipettingInfos.AddRange(GenerateGradualPipettingInfos(dilutionInfos[i], destPlateLabel, i,false));
                }

            }
            else
            {

                while (dilutionInfos.Count > 0)
                {
                    var thisColumnPipettingInfos = dilutionInfos.Take(8).ToList();

                    if (dilutionInfos.Exists(x => x.dilutionTimes != 0))
                        samplePipettingInfos.AddRange(GenerateSamplePipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex));
                    dilutionInfos = dilutionInfos.Skip(thisColumnPipettingInfos.Count).ToList();
                    columnIndex += GetMaxDilutionWellsNeeded(thisColumnPipettingInfos);
                }
            }
            return samplePipettingInfos;
        }

        private int GetMaxDilutionWellsNeeded(List<DilutionInfo> thisColumnPipettingInfos)
        {
            double maxDilutionTiems = thisColumnPipettingInfos.Select(x => x.dilutionTimes).Max();
            return GetEachStepVolume(maxDilutionTiems, true).Count;
        }

        private List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> thisColumnDilutionInfos, string destPlateLabel, int columnIndex)
        {
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            for (int i = 0; i < thisColumnDilutionInfos.Count; i++)
            {
                //GenerateBufferPipettingInfos()
                int indexInColumn = i;
                List<PipettingInfo> thisSamplePipettingInfos = GenerateSamplePipettingInfos(thisColumnDilutionInfos[i], destPlateLabel, columnIndex, indexInColumn);
                pipettingInfos.AddRange(thisSamplePipettingInfos);
            }
            var fromSourceLabwarePipettingInfos = pipettingInfos.Where(x => IsFromSourceLabware(x)).ToList();
            pipettingInfos = pipettingInfos.Except(fromSourceLabwarePipettingInfos).ToList();
            pipettingInfos.OrderBy(x => x.srcWellID);
            pipettingInfos = fromSourceLabwarePipettingInfos.Union(pipettingInfos).ToList();
            return pipettingInfos;
        }

        private bool IsFromSourceLabware(PipettingInfo pipettingInfo)
        {
            List<SampleType> types = new List<SampleType>(){SampleType.Norm,SampleType.HQC,SampleType.LQC,SampleType.MQC,SampleType.STD};
            return types.Exists(x => x.ToString() == pipettingInfo.srcLabware);
            
        }

        private List<PipettingInfo> GenerateSamplePipettingInfos(DilutionInfo dilutionInfo,
            string destPlateLabel,
            int columnIndex, 
            int indexInColumn)
        {
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            double times = dilutionInfo.dilutionTimes;
            List<int> sampleVolumes = GetEachStepVolume(times, false);
            

            PipettingInfo pipettingInfo = new PipettingInfo(dilutionInfo.type.ToString(),
                indexInColumn + 1, 
                destPlateLabel, 
                GetWellID(columnIndex, indexInColumn),
                sampleVolumes[0], 
                Configurations.Instance.DilutionVolume/sampleVolumes[0],
                dilutionInfo.type);
            pipettingInfos.Add(pipettingInfo);
            sampleVolumes = sampleVolumes.Skip(1).ToList();
            foreach (double vol in sampleVolumes)
            {
                pipettingInfo = new PipettingInfo(destPlateLabel, 
                                                GetWellID(columnIndex, indexInColumn),
                                                destPlateLabel, 
                                                GetWellID(columnIndex+1, indexInColumn),
                                                vol,
                                                Configurations.Instance.DilutionVolume/vol,
                                                dilutionInfo.type);
                columnIndex++;
                pipettingInfos.Add(pipettingInfo);
            }
            return pipettingInfos;
        }



        #endregion
        #region buffer

        internal List<List<PipettingInfo>> GenerateBufferPipettingInfos(ref List<List<PipettingInfo>> firstPlate,ref List<List<PipettingInfo>>secondPlate)
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            List<DilutionInfo> firstPlateDilutions = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateDilutions = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
            firstPlate = GenerateBufferPipettingInfos(firstPlateDilutions, "Dilution1");
            pipettingInfos.AddRange(firstPlate);
            plateName_LastDilutionPositions.Add("Dilution1", GetLastPositions(firstPlate));
            secondPlate = GenerateBufferPipettingInfos(secondPlateDilutions, "Dilution2");
            plateName_LastDilutionPositions.Add("Dilution2", GetLastPositions(secondPlate));
            pipettingInfos.AddRange(secondPlate);
            return pipettingInfos;
        }

        //private List<DilutionInfo> GetDilutionInfo(IEnumerable<KeyValuePair<DilutionInfo, List<int>>> pairs)
        //{
        //    List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
        //    foreach(var pair in pairs)
        //    {
        //        dilutionInfos.Add(pair.Key);
        //    }
        //    return dilutionInfos;
        //}

        private int GetMaxSampleCntFirstDilutionPlate()
        {
            if (Configurations.Instance.IsGradualPipetting)
                return 8;
            else
                return 24;
        }

        //private List<DilutionInfo> GetOddColumnDilutions(List<DilutionInfo> dilutionInfos, int parallelHoleCount)
        //{
        //    List<DilutionInfo> oddDilutionInfos = new List<DilutionInfo>();
        //    for(int i =0; i< dilutionInfos.Count; i++)
        //    {
        //        int columnID = (i + 8) / 8;
        //        if (columnID % parallelHoleCount == 1)
        //        {
        //            oddDilutionInfos.Add(dilutionInfos[i]);
        //        }
        //    }
        //    return oddDilutionInfos;
        //}

        private List<int> GetLastPositions(List<List<PipettingInfo>> pipettingInfos)
        {
            List<int> vals = new List<int>();
            foreach(var sampeRowPipettingInfos in pipettingInfos)
            {
                if (Configurations.Instance.IsGradualPipetting && sampeRowPipettingInfos.First().type == SampleType.Norm)//add whole row for gradual pipettings
                    vals.AddRange(sampeRowPipettingInfos.Select(x=>x.dstWellID).ToList());
                else
                    vals.Add(sampeRowPipettingInfos.Max(x => x.dstWellID));
            }
            return vals;
        }

        private List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> dilutionInfos, string destPlateLabel)
        {
            int columnIndex = 0;
            List<List<PipettingInfo>> bufferPipettingInfos = new List<List<PipettingInfo>>();
           
            if(Configurations.Instance.IsGradualPipetting) // gradual pipetting
            {
                for(int i = 0; i< dilutionInfos.Count; i++)
                {
                    bufferPipettingInfos.Add(GenerateGradualPipettingInfos(dilutionInfos[i], destPlateLabel, i,true));
                }
            }
            else
            {
                while (dilutionInfos.Count > 0)
                {
                    var thisColumnPipettingInfos = dilutionInfos.Take(8).ToList();
                    if (dilutionInfos.Exists(x => x.dilutionTimes != 0))
                        bufferPipettingInfos.AddRange(GenerateBufferPipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex));
                    dilutionInfos = dilutionInfos.Skip(thisColumnPipettingInfos.Count).ToList();
                    columnIndex += GetMaxDilutionWellsNeeded(thisColumnPipettingInfos);
                }
            }
            
            return bufferPipettingInfos;
        }

        private List<PipettingInfo> GenerateGradualPipettingInfos(DilutionInfo dilutionInfo, 
            string destPlateLabel, 
            int index,bool isBuffer)
        {
            List<PipettingInfo> pipettingInfo = new List<PipettingInfo>();
            int gradualTimes = Configurations.Instance.GradualTimes;
            double times = dilutionInfo.dilutionTimes;
            int startWellID = 1 + index;
            int wellsNeeded = (int)Math.Ceiling(Math.Log(times, gradualTimes));
            List<int> destWellIDs = new List<int>();
            if (wellsNeeded > 24)
                throw new Exception(string.Format("Need {0} wells to do gradual dilution!",wellsNeeded));
            for (int i = 0; i < wellsNeeded; i++)
                destWellIDs.Add(startWellID + i * 8);
            int srcWellIndex = 0;
            List<int> volumes = GetEachStepVolume(times,isBuffer);
            bool isFirstColumn = true;
            foreach(var dstWellID in destWellIDs)
            {
                var vol = volumes[srcWellIndex];
                double wellTimes = isBuffer ?
                    ((double)Configurations.Instance.DilutionVolume) / (Configurations.Instance.DilutionVolume - vol)
                    : ((double)Configurations.Instance.DilutionVolume) / vol;
                if(isBuffer)
                    pipettingInfo.Add(new PipettingInfo("Buffer", 
                        srcWellIndex % 8 + 1,
                        destPlateLabel, 
                        dstWellID, 
                        vol,
                        wellTimes, dilutionInfo.type));
                else
                {
                    string srcLabware = destPlateLabel;
                    int srcWellID = dstWellID - 8;
                    if(isFirstColumn)
                    {
                        srcLabware = dilutionInfo.type.ToString();
                        srcWellID = index % 8 + 1;
                        isFirstColumn = false;
                    }
                    pipettingInfo.Add(new PipettingInfo(srcLabware,
                        srcWellID, 
                        destPlateLabel,
                        dstWellID,
                        vol, wellTimes, dilutionInfo.type));
                }
                srcWellIndex++;
            }
            return pipettingInfo;
        }

        private List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> thisColumnDilutionInfos, 
            string destPlateLabel, int columnIndex)
        {
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
           for(int i = 0; i< thisColumnDilutionInfos.Count;i++)
           {
               //GenerateBufferPipettingInfos()
               int indexInColumn = i;
               List<PipettingInfo> thisSamplePipettingInfos = GenerateBufferPipettingInfos(thisColumnDilutionInfos[i], destPlateLabel, columnIndex, indexInColumn);
               pipettingInfos.Add(thisSamplePipettingInfos);
           }
           return pipettingInfos;
        }

        private List<PipettingInfo> GenerateBufferPipettingInfos(DilutionInfo dilutionInfo, string destPlateLabel, 
            int columnIndex, int indexInColumn)
        {
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            double times = dilutionInfo.dilutionTimes;
            List<int> dilutionVolumes = GetEachStepVolume(times,true);
            string srcLabware = "Buffer";
            foreach(double vol in dilutionVolumes)
            {
                double thisWellTimes = Configurations.Instance.DilutionVolume / (Configurations.Instance.DilutionVolume - vol);
                PipettingInfo pipettingInfo = new PipettingInfo(srcLabware, indexInColumn + 1,
                    destPlateLabel, GetWellID(columnIndex++, indexInColumn), vol,
                    thisWellTimes, dilutionInfo.type);
                pipettingInfos.Add(pipettingInfo);
            }
            return pipettingInfos;
        }
        #endregion

        private string GetAspirateOrDispense(string sLabware, int srcWellID, double vol, string liquidClass, bool isAsp)
        {
            char type = isAsp ? 'A' : 'D';
            string sCommand = string.Format("{0};{1};;;{2};;{3};{4};",
                         type,
                         sLabware,
                         srcWellID,
                         vol,
                         liquidClass);
            return sCommand;
        }

        private string GetAspirate(string sLabware, int srcWellID, double vol, string liquidClass)
        {
            return GetAspirateOrDispense(sLabware, srcWellID, vol, liquidClass, true);
        }

        private string GetDispense(string sLabware, int dstWellID, double vol, string liquidClass)
        {
            return GetAspirateOrDispense(sLabware, dstWellID, vol, liquidClass, false);
        }
     
        private int GetWellID(int columnIndex, int indexInColumn)
        {
            return columnIndex * 8 + indexInColumn + 1;
        }

        private List<int> GetEachStepVolume(double times, bool isBuffer)
        {
            if (times == 0)
                return new List<int>() { 0 };
            List<int> eachStepMaxTimes = new List<int>(){
                50,2500,125000,6250000};
            int neededSteps = 0;
            for(int i = 0; i< eachStepMaxTimes.Count; i++)
            {
                if(eachStepMaxTimes[i] > times)
                {
                    neededSteps = i + 1;
                    break;
                }
            }
            double eachStepTimes = Math.Pow(times, 1.0 / neededSteps);

            if (Configurations.Instance.IsGradualPipetting)
            {
                eachStepTimes = Configurations.Instance.GradualTimes;
                neededSteps = (int)Math.Ceiling(Math.Log(times, eachStepTimes));
            }

            double currentTimes = 1;
            List<int> vols = new List<int>();
            for(int i = 0; i< neededSteps; i++)
            {
                double thisStepTimes = eachStepTimes;
                if( i == neededSteps-1)
                {
                    thisStepTimes = times / currentTimes;
                }
                int bufferVol = (int)(Configurations.Instance.DilutionVolume * (thisStepTimes - 1) / thisStepTimes);
                int sampleVol = (int)(Configurations.Instance.DilutionVolume - bufferVol);
                vols.Add(isBuffer ? bufferVol : sampleVol);
                double realTimes = Configurations.Instance.DilutionVolume / sampleVol;
                currentTimes *= realTimes;
            }
            return vols;
        }


        private string FormatReadable(PipettingInfo pipettingInfo)
        {
            return string.Format("{0},{1},{2},{3},{4}",
                pipettingInfo.srcLabware,
                pipettingInfo.srcWellID,
                pipettingInfo.dstLabware,
                pipettingInfo.dstWellID, pipettingInfo.vol);
        }

        public int needSrcWellCnt { get; set; }
    }


    class PipettingInfo
    {

        public string srcLabware;
        public int srcWellID;
        public string dstLabware;
        public int dstWellID;
        public double vol;
        public double dilutionTimes;
        public SampleType type;
        public PipettingInfo(string srcLabware,
            int srcWell, string dstLabware, int dstWell, double v, double dilutionTimes, SampleType type)
        {
            this.srcLabware = srcLabware;
            this.dstLabware = dstLabware;
            this.srcWellID = srcWell;
            this.dstWellID = dstWell;
            this.vol = v;
            this.dilutionTimes = dilutionTimes;
            this.type = type;
        }

        public PipettingInfo(PipettingInfo pipettingInfo)
        {
            srcLabware = pipettingInfo.srcLabware;
            dstLabware = pipettingInfo.dstLabware;
            srcWellID = pipettingInfo.srcWellID;
            dstWellID = pipettingInfo.dstWellID;
            vol = pipettingInfo.vol;
            dilutionTimes = pipettingInfo.dilutionTimes;
            type = pipettingInfo.type;
        }
    }
}
