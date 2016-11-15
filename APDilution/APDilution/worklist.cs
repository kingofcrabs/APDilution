using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace APDilution
{
    public class worklist
    {
        internal bool isTesting = false;
        internal bool isGradual = false;
        Dictionary<string, List<int>> plateName_LastDilutionPositions = new Dictionary<string, List<int>>();
        Dictionary<DilutionInfo, List<int>> dilutionInfo_WellID = new Dictionary<DilutionInfo, List<int>>();
        public List<string> DoJob(List<DilutionInfo> dilutionInfos)
        {
            //plateName_LastDilutionPositions.Clear();
            //int parallelHoleCount = CountParallelHole(dilutionInfos);
        
            //from buffer & sample to dilution
            var bufferPipettings = GenerateBufferPipettingInfos();
            var samplePipettings = GenerateSamplePipettingInfos();
           
            //from dilution to reaction plate
            List<PipettingInfo> transferPipettings = GenerateTransferPipettingInfos();
            //transferPipettings.AddRange(GenerateTransferPipettingInfos(1));

            List<string> strs = new List<string>();
            strs.AddRange(Format(bufferPipettings));
            strs.Add("B;");
            strs.AddRange(Format(samplePipettings,Configurations.Instance.SampleLiquidClass));
            strs.Add("B;");
            strs.AddRange(Format(transferPipettings, Configurations.Instance.TransferLiquidClass));
            strs.Add("B;");
            return strs;
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
            string sampleLiquidClass = Configurations.Instance.SampleLiquidClass;
            List<string> commands = new List<string>();
            foreach (var pipettingInfo in pipettingInfos)
            {
                commands.Add(GetAspirate(pipettingInfo.srcLabware, pipettingInfo.srcWellID, pipettingInfo.vol, sampleLiquidClass));
                commands.Add(GetDispense(pipettingInfo.dstLabware, pipettingInfo.dstWellID, pipettingInfo.vol, sampleLiquidClass));
            }
            commands.Add("W");
            return commands;
        }

        private IEnumerable<string> Format(List<List<PipettingInfo>> bufferPipettings)
        {
            string bufferLiquidClass = Configurations.Instance.BufferLiquidClass;
            List<string> commands = new List<string>();
            foreach (var sameSamplePipettings in bufferPipettings)
            {
                
                foreach (var pipettingInfo in sameSamplePipettings)
                {
                    commands.Add(GetAspirate(pipettingInfo.srcLabware, pipettingInfo.srcWellID, pipettingInfo.vol,bufferLiquidClass));
                    commands.Add(GetDispense(pipettingInfo.dstLabware, pipettingInfo.dstWellID, pipettingInfo.vol,bufferLiquidClass));
                }
                commands.Add("W");
            }
            return commands;
        }

        internal List<PipettingInfo> GenerateTransferPipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            var firstPlateSamples = GetDilutionInfo(dilutionInfo_WellID.Take(firstPlateCnt));
            var secondPlateSamples = GetDilutionInfo(dilutionInfo_WellID.Skip(firstPlateCnt));
            var firstPlateName = "Dilution1";
            var secondPlateName = "Dilution2";
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(firstPlateName, firstPlateSamples));
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(secondPlateName, secondPlateSamples));
            return pipettingInfos;
        }

        private List<PipettingInfo> GenerateTransferPipettingInfos(string plateName, List<DilutionInfo> dilutionInfos)
        {
            var srcPositionOnDilutionPlate = plateName_LastDilutionPositions[plateName];
            int index = 0;
            var vol = Configurations.Instance.ReactionVolume;
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            foreach(var dilutionInfo in dilutionInfos)
            {
                var destWells = dilutionInfo_WellID[dilutionInfo];
                var srcWellID = srcPositionOnDilutionPlate[index];
                foreach(var dstWellID in destWells)
                {
                    pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, "Reaction", dstWellID, vol));
                }
                index++;
            }
            return pipettingInfos;

        }

       

        #region sample
        internal List<PipettingInfo> GenerateSamplePipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            var firstPlateSamples = GetDilutionInfo(dilutionInfo_WellID.Take(firstPlateCnt));
            var secondPlateSamples = GetDilutionInfo(dilutionInfo_WellID.Skip(firstPlateCnt));
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateSamplePipettingInfos(firstPlateSamples, "Dilution1"));
            pipettingInfos.AddRange(GenerateSamplePipettingInfos(secondPlateSamples, "Dilution2"));
            return pipettingInfos;
        }

     


        bool IsGradualPipetting()
        {
            if (isTesting)
                return isGradual;
            var cmdLines = Environment.GetCommandLineArgs();
            return cmdLines.Count() > 1 && cmdLines[1] == "G"; // gradual pipetting
        }

        private List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> dilutionInfos, string destPlateLabel)
        {
            int columnIndex = 0;
            List<PipettingInfo> samplePipettingInfos = new List<PipettingInfo>();
            if(IsGradualPipetting())
            {
                if (dilutionInfos.Count > 4) //max 4 samples
                    throw new Exception(string.Format("Samples to dilution is: {0}, cannot > 4.", dilutionInfos.Count));
                
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
                    columnIndex += 4;
                }
            }
            return samplePipettingInfos;
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
            List<SampleType> types = new List<SampleType>(){SampleType.Normal,SampleType.HQC,SampleType.LQC,SampleType.MQC,SampleType.STD};
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
            
            PipettingInfo pipettingInfo = new PipettingInfo(dilutionInfo.type.ToString(), indexInColumn + 1, destPlateLabel, GetWellID(columnIndex, indexInColumn), sampleVolumes[0]);
            pipettingInfos.Add(pipettingInfo);
            sampleVolumes = sampleVolumes.Skip(1).ToList();
            foreach (double vol in sampleVolumes)
            {
                pipettingInfo = new PipettingInfo(destPlateLabel, 
                                                GetWellID(columnIndex, indexInColumn),
                                                destPlateLabel, 
                                                GetWellID(columnIndex+1, indexInColumn), vol);
                columnIndex++;
                pipettingInfos.Add(pipettingInfo);
            }
            return pipettingInfos;
        }



        #endregion
        #region buffer

        internal List<List<PipettingInfo>> GenerateBufferPipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            List<DilutionInfo> firstPlateDilutions = GetDilutionInfo(dilutionInfo_WellID.Take(firstPlateCnt));
            var secondPlateDilutions = GetDilutionInfo(dilutionInfo_WellID.Skip(firstPlateCnt));
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
            pipettingInfos.AddRange(GenerateBufferPipettingInfos(firstPlateDilutions, "Dilution1"));
            plateName_LastDilutionPositions.Add("Dilution1", GetLastPositions(pipettingInfos));
            var secondPlatePipettings = GenerateBufferPipettingInfos(secondPlateDilutions, "Dilution2");
            plateName_LastDilutionPositions.Add("Dilution2",GetLastPositions(secondPlatePipettings));
            pipettingInfos.AddRange(secondPlatePipettings);
            return pipettingInfos;
        }

        private List<DilutionInfo> GetDilutionInfo(IEnumerable<KeyValuePair<DilutionInfo, List<int>>> pairs)
        {
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            foreach(var pair in pairs)
            {
                dilutionInfos.Add(pair.Key);
            }
            return dilutionInfos;
        }

        private int GetMaxSampleCntFirstDilutionPlate()
        {
            if (IsGradualPipetting())
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
                vals.Add(sampeRowPipettingInfos.Max(x => x.dstWellID));
            }
            return vals;
        }

        private List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> dilutionInfos, string destPlateLabel)
        {
            int columnIndex = 0;
            List<List<PipettingInfo>> bufferPipettingInfos = new List<List<PipettingInfo>>();
           
            if(IsGradualPipetting()) // gradual pipetting
            {
                if (dilutionInfos.Count > 4) //max 4 samples
                    throw new Exception(string.Format("Samples to dilution is: {0}, cannot > 4.", dilutionInfos.Count));
                
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
                    columnIndex++;
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
            int startWellID = 1 + 2 * index;
            int wellsNeeded = (int)Math.Ceiling(Math.Log(times, gradualTimes));
            List<int> destWellIDs = new List<int>();
            for (int i = 0; i < Math.Min(wellsNeeded,12); i++)
                destWellIDs.Add(startWellID + i * 8);
            int remainCnt = wellsNeeded - 12;
            if(remainCnt > 0)
            {
                for (int i = 0; i < remainCnt; i++)
                {
                    destWellIDs.Add(destWellIDs[i] + 1);
                }
            }
            
            int srcWellIndex = 0;
            List<int> volumes = GetEachStepVolume(times,isBuffer);
            bool isFirstColumn = true;
            foreach(var dstWellID in destWellIDs)
            {
                if(isBuffer)
                    pipettingInfo.Add(new PipettingInfo("Buffer", srcWellIndex % 8 + 1, destPlateLabel, dstWellID, volumes[srcWellIndex]));
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
                        volumes[srcWellIndex]));
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
                PipettingInfo pipettingInfo = new PipettingInfo(srcLabware, indexInColumn + 1, destPlateLabel, GetWellID(columnIndex++, indexInColumn), vol);
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
            
            if (IsGradualPipetting())
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

        public PipettingInfo(string srcLabware,
            int srcWell, string dstLabware, int dstWell, double v)
        {
            this.srcLabware = srcLabware;
            this.dstLabware = dstLabware;
            this.srcWellID = srcWell;
            this.dstWellID = dstWell;
            this.vol = v;

        }

        public PipettingInfo(PipettingInfo pipettingInfo)
        {
            srcLabware = pipettingInfo.srcLabware;
            dstLabware = pipettingInfo.dstLabware;
            srcWellID = pipettingInfo.srcWellID;
            dstWellID = pipettingInfo.dstWellID;
            vol = pipettingInfo.vol;
        }
    }
}
