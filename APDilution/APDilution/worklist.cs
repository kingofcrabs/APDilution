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
        public List<string> DoJob(List<DilutionInfo> dilutionInfos)
        {
            plateName_LastDilutionPositions.Clear();
            int parallelHoleCount = CountParallelHole(dilutionInfos);
            //from buffer & sample to dilution
            var bufferPipettings = GenerateBufferPipettingInfos(dilutionInfos,parallelHoleCount);
            var samplePipettings = GenerateSamplePipettingInfos(dilutionInfos,parallelHoleCount);
           
            //from dilution to reaction plate
            List<PipettingInfo> transferPipettings = GenerateTransferPipettingInfos(dilutionInfos,0);
            transferPipettings.AddRange(GenerateTransferPipettingInfos(dilutionInfos, 1));

            List<string> strs = new List<string>();
            strs.AddRange(Format(bufferPipettings));
            strs.Add("B;");
            strs.AddRange(Format(samplePipettings,Configurations.Instance.SampleLiquidClass));
            strs.Add("B;");
            strs.AddRange(Format(transferPipettings, Configurations.Instance.TransferLiquidClass));
            strs.Add("B;");
            return strs;
        }

        private int CountParallelHole(List<DilutionInfo> dilutionInfos)
        {
            int seqID = dilutionInfos[0].seqIDinThisType;
            int parallelCnt = 1;
            for(int columnID = 2; columnID<= 12; columnID++)
            {
                if (dilutionInfos.Count <= (columnID - 1) * 8)
                    break;
                
                if (dilutionInfos[(columnID-1) * 8].seqIDinThisType != seqID)
                    break;
                parallelCnt = columnID;
            }
            if( dilutionInfos.Count > 8)
            {
                if (parallelCnt < 2 || parallelCnt > 6)
                    throw new Exception("Parallel Hole Count must be between 2 and 6!");
            }
            
            return parallelCnt;
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

        internal List<PipettingInfo> GenerateTransferPipettingInfos(List<DilutionInfo> dilutionInfos, int dilutionPlateIndex)
        {
            var vol = Configurations.Instance.ReactionVolume;
            if(IsGradualPipetting())
            {
                return GenerateGradualTransferPipettingInfos(dilutionInfos, dilutionPlateIndex, vol);
            }
            int needSrcWellCnt = dilutionInfos.Count;
            int parallelHoleCount = CountParallelHole(dilutionInfos);
            if (dilutionPlateIndex != 0)//without considering parallel count
            {
                needSrcWellCnt = dilutionInfos.Count - 24 * parallelHoleCount;
                if (needSrcWellCnt < 0)
                    throw new Exception("need transfer count < 0!");
            }
            else
            {
                needSrcWellCnt = Math.Min(24 * parallelHoleCount,needSrcWellCnt);
            }

          
            needSrcWellCnt = needSrcWellCnt/parallelHoleCount;
            string srcLabware = string.Format("Dilution{0}", dilutionPlateIndex + 1);
            int columnCnt = (needSrcWellCnt + 7) / 8;
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            int srcIndex = 0;
          
            for (int column = 0; column < columnCnt; column++)
            {
                int thisColumnCnt = needSrcWellCnt > (column + 1) * 8 ? 8 : needSrcWellCnt - column * 8;
                for (int indexInColumn = 0; indexInColumn < thisColumnCnt; indexInColumn++)
                {
                    int dstWellID = dilutionPlateIndex * 24 * parallelHoleCount + GetWellID(column*parallelHoleCount, indexInColumn);
                    if(dilutionInfos[dstWellID-1].dilutionTimes == 0)//no need transfer
                    {
                        srcIndex++;
                        continue;
                    }
                    PipettingInfo firstColInfo = new PipettingInfo(srcLabware,
                                                                plateName_LastDilutionPositions[srcLabware][srcIndex++],
                                                                Configurations.Instance.ReactionPlateName,
                                                                dstWellID,
                                                                Configurations.Instance.ReactionVolume);
                    
                    for (int parallelColID = 1; parallelColID < parallelHoleCount; parallelColID++ )
                    {
                        PipettingInfo parallelColInfo = new PipettingInfo(firstColInfo);
                        parallelColInfo.dstWellID = dstWellID + parallelColID*8;
                        pipettingInfos.Add(parallelColInfo);
                    }
                    pipettingInfos.Insert(0, firstColInfo);
                  
                }
            }
            return pipettingInfos;
        }

        private List<PipettingInfo> GenerateGradualTransferPipettingInfos(List<DilutionInfo> dilutionInfos, int dilutionPlateIndex, int vol)
        {
            int parallelHoleCount = CountParallelHole(dilutionInfos);
            string srcLabware = string.Format("Dilution{0}", dilutionPlateIndex + 1);
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            if(dilutionPlateIndex == 0)
            {
                dilutionInfos = dilutionInfos.Take(4).ToList();
            }
            else
            {
                dilutionInfos = dilutionInfos.Skip(4).ToList();
            }
            for(int i = 0; i< dilutionInfos.Count;i++)
            {
                int firstColumnDstWellID = dilutionPlateIndex * 4 + i + 1;
                if (dilutionInfos[firstColumnDstWellID - 1].dilutionTimes == 0)//no need transfer
                    continue;
                PipettingInfo firstColInfo = new PipettingInfo(srcLabware,
                                                            plateName_LastDilutionPositions[srcLabware][i],
                                                            Configurations.Instance.ReactionPlateName,
                                                            firstColumnDstWellID,
                                                            Configurations.Instance.ReactionVolume);
                for (int parallelColID = 1; parallelColID < parallelHoleCount; parallelColID++)
                {
                    PipettingInfo parallelColInfo = new PipettingInfo(firstColInfo);
                    parallelColInfo.dstWellID = firstColumnDstWellID + parallelColID * 8;
                    pipettingInfos.Add(parallelColInfo);
                }
                pipettingInfos.Insert(0, firstColInfo);
            }
            return pipettingInfos;
        }

        #region sample
        internal List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> dilutionInfos, int parallelCnt)
        {
            var firstPlateSamples = dilutionInfos.Take(24*parallelCnt).ToList();
            var secondPlateSamples = dilutionInfos.Skip(24*parallelCnt).ToList();
            firstPlateSamples = GetRegionFirstColumnDilutions(firstPlateSamples,parallelCnt);
            secondPlateSamples = GetRegionFirstColumnDilutions(secondPlateSamples, parallelCnt);
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateSamplePipettingInfos(firstPlateSamples, "Dilution1"));
            pipettingInfos.AddRange(GenerateSamplePipettingInfos(secondPlateSamples, "Dilution2"));
            return pipettingInfos;
        }

        private List<DilutionInfo> GetRegionFirstColumnDilutions(List<DilutionInfo> dilutionInfos, int parallelCnt)
        {
            List<DilutionInfo> oddDilutionInfos = new List<DilutionInfo>();
            if (dilutionInfos.Count <= 8)
                return dilutionInfos;
            for (int i = 0; i < dilutionInfos.Count; i++)
            {
                int columnID = (i + 8) / 8;
                if (columnID % parallelCnt == 1)
                {
                    oddDilutionInfos.Add(dilutionInfos[i]);
                }
            }
            return oddDilutionInfos;
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

        internal List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> dilutionInfos,int parallelHoleCount)
        {
            int maxSampleCntPerDilutionPlate = 24 * parallelHoleCount;
            var firstPlateDilutions = dilutionInfos.Take(maxSampleCntPerDilutionPlate).ToList();
            var secondPlateDilutions = dilutionInfos.Skip(maxSampleCntPerDilutionPlate).ToList();
            firstPlateDilutions = GetRegionFirstColumnDilutions(firstPlateDilutions,parallelHoleCount);
            secondPlateDilutions = GetRegionFirstColumnDilutions(secondPlateDilutions, parallelHoleCount);
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
            pipettingInfos.AddRange(GenerateBufferPipettingInfos(firstPlateDilutions, "Dilution1"));
            plateName_LastDilutionPositions.Add("Dilution1", GetLastPositions(pipettingInfos));
            var secondPlatePipettings = GenerateBufferPipettingInfos(secondPlateDilutions, "Dilution2");
            plateName_LastDilutionPositions.Add("Dilution2",GetLastPositions(secondPlatePipettings));
            pipettingInfos.AddRange(secondPlatePipettings);
            return pipettingInfos;
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
