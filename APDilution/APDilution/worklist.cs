using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace APDilution
{
    public class worklist
    {

        Dictionary<string, List<int>> plateName_LastDilutionPositions = new Dictionary<string, List<int>>();
        public List<string> DoJob(List<DilutionInfo> dilutionInfos)
        {
            plateName_LastDilutionPositions.Clear();
            //from buffer & sample to dilution
            var  bufferPipettings = GenerateBufferPipettingInfos(dilutionInfos);
            var  samplePipettings = GenerateSamplePipettingInfos(dilutionInfos);
           
            //from dilution to reaction plate
            List<PipettingInfo> transferPipettings = GenerateTransferPipettingInfos(dilutionInfos,0);
            transferPipettings.AddRange(GenerateTransferPipettingInfos(dilutionInfos, 1));

            List<string> strs = new List<string>();
            strs.AddRange(Format(bufferPipettings));
            strs.AddRange(Format(samplePipettings,Configurations.Instance.SampleLiquidClass));
            strs.AddRange(Format(transferPipettings, Configurations.Instance.TransferLiquidClass));
            return strs;
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

        private List<PipettingInfo> GenerateTransferPipettingInfos(List<DilutionInfo> dilutionInfos, int dilutionPlateIndex)
        {
            var vol = Configurations.Instance.ReactionVolume;

            int needTransferCnt = dilutionInfos.Count;
            if (dilutionPlateIndex != 0)
                needTransferCnt = dilutionInfos.Count - 48;
            needTransferCnt = Math.Min(48, needTransferCnt)/2;//max 24
            string srcLabware = string.Format("Dilution{0}", dilutionPlateIndex + 1);
            int columnCnt = (needTransferCnt + 7) / 8;
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            int srcIndex = 0;
            for (int column = 0; column < columnCnt; column++)
            {
                int thisColumnCnt = needTransferCnt > (column + 1) * 8 ? 8 : needTransferCnt - column * 8;
                for (int indexInColumn = 0; indexInColumn < thisColumnCnt; indexInColumn++)
                {
                    int dstWellID = dilutionPlateIndex * 48 + GetWellID(column*2, indexInColumn);
                    if(dilutionInfos[dstWellID].dilutionTimes == 0)//no need transfer
                    {
                        srcIndex++;
                        continue;
                    }
                    PipettingInfo leftInfo = new PipettingInfo(srcLabware,
                                                                plateName_LastDilutionPositions[srcLabware][srcIndex++],
                                                                Configurations.Instance.ReactionPlateName,
                                                                dstWellID,
                                                                Configurations.Instance.ReactionVolume);
                    PipettingInfo rightInfo = new PipettingInfo(leftInfo);
                    rightInfo.dstWellID = dstWellID + 8;
                    pipettingInfos.Add(leftInfo);
                    pipettingInfos.Add(rightInfo);
                }
            }
            return pipettingInfos;
        }

        #region sample
        internal List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> dilutionInfos)
        {
            var firstPlateSamples = dilutionInfos.Take(48).ToList();
            var secondPlateSamples = dilutionInfos.Skip(48).ToList();
            firstPlateSamples = GetOddColumnDilutions(firstPlateSamples);
            secondPlateSamples = GetOddColumnDilutions(secondPlateSamples);
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateSamplePipettingInfos(firstPlateSamples, "Dilution1"));
            pipettingInfos.AddRange(GenerateSamplePipettingInfos(secondPlateSamples, "Dilution2"));
            return pipettingInfos;
        }

        private List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> dilutionInfos, string destPlateLabel)
        {
            int columnIndex = 0;
            List<PipettingInfo> samplePipettingInfos = new List<PipettingInfo>();
            while (dilutionInfos.Count > 0)
            {
                var thisColumnPipettingInfos = dilutionInfos.Take(8).ToList();

                if (dilutionInfos.Exists(x => x.dilutionTimes != 0))
                    samplePipettingInfos.AddRange(GenerateSamplePipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex));
                dilutionInfos = dilutionInfos.Skip(thisColumnPipettingInfos.Count).ToList();
                columnIndex += 4;
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
            List<double> sampleVolumes = GetEachStepVolume(times, false);
            
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

        internal List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> dilutionInfos)
        {
            var firstPlateDilutions = dilutionInfos.Take(48).ToList();
            var secondPlateDilutions = dilutionInfos.Skip(48).ToList();
            firstPlateDilutions = GetOddColumnDilutions(firstPlateDilutions);
            secondPlateDilutions = GetOddColumnDilutions(secondPlateDilutions);
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
            pipettingInfos.AddRange(GenerateBufferPipettingInfos(firstPlateDilutions, "Dilution1"));
            plateName_LastDilutionPositions.Add("Dilution1", GetLastPositions(pipettingInfos));
            var secondPlatePipettings = GenerateBufferPipettingInfos(secondPlateDilutions, "Dilution2");
            plateName_LastDilutionPositions.Add("Dilution2",GetLastPositions(secondPlatePipettings));
            pipettingInfos.AddRange(secondPlatePipettings);
            return pipettingInfos;
        }

        private List<DilutionInfo> GetOddColumnDilutions(List<DilutionInfo> dilutionInfos)
        {
            List<DilutionInfo> oddDilutionInfos = new List<DilutionInfo>();
            for(int i =0; i< dilutionInfos.Count; i++)
            {
                int columnID = (i + 8) / 8;
                if(columnID % 2 == 1)
                {
                    oddDilutionInfos.Add(dilutionInfos[i]);
                }
            }
            return oddDilutionInfos;
        }

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
            while(dilutionInfos.Count > 0)
            {
                var thisColumnPipettingInfos = dilutionInfos.Take(8).ToList();
                
                if(dilutionInfos.Exists(x=>x.dilutionTimes !=0))
                    bufferPipettingInfos.AddRange(GenerateBufferPipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex));
                dilutionInfos = dilutionInfos.Skip(thisColumnPipettingInfos.Count).ToList();
                columnIndex++;
            }
            return bufferPipettingInfos;
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
            List<double> dilutionVolumes = GetEachStepVolume(times,true);
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

        private List<double> GetEachStepVolume(double times, bool isBuffer)
        {
            if (times == 0)
                return new List<double>() { 0 };
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
            double currentTimes = 1;
            List<double> vols = new List<double>();
            for(int i = 0; i< neededSteps; i++)
            {
                double thisStepTimes = eachStepTimes;
                if( i == neededSteps-1)
                {
                    thisStepTimes = times / currentTimes;
                }
                double bufferVol = (int)(Configurations.Instance.DilutionVolume * (thisStepTimes - 1) / thisStepTimes);
                double sampleVol = (int)(Configurations.Instance.DilutionVolume - bufferVol);
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
