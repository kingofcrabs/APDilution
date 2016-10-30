using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace APDilution
{
    public class worklist
    {

        public void DoJob(List<DilutionInfo> dilutionInfos,
           ref List<string> bufferPipettingsStrs,
           ref List<string> samplePipettingStrs)
        {

           var  bufferPipettings = GenerateBufferPipettingInfos(dilutionInfos);
           var  samplePipettings = GenerateSamplePipettingInfos(dilutionInfos);
           
        }

        private List<PipettingInfo> GenerateSamplePipettingInfos(List<DilutionInfo> dilutionInfos)
        {
            throw new NotImplementedException();
        }

        private List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> dilutionInfos)
        {
            var firstPlateDilutions = dilutionInfos.Take(48).ToList();
            var secondPlateDilutions = dilutionInfos.Skip(48).ToList();
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
            pipettingInfos.AddRange(GenerateBufferPipettingInfos(firstPlateDilutions, "Dilution1"));
            pipettingInfos.AddRange(GenerateBufferPipettingInfos(secondPlateDilutions, "Dilution2"));
            return pipettingInfos;
        }

        private List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> dilutionInfos, string destPlateLabel)
        {
            int columnIndex = 0;
            List<List<PipettingInfo>> bufferPipettingInfos = new List<List<PipettingInfo>>();
            while(dilutionInfos.Count > 0)
            {
                var thisColumnPipettingInfos = dilutionInfos.Take(8).ToList();
                dilutionInfos = dilutionInfos.Except(thisColumnPipettingInfos).ToList();
                if(dilutionInfos.Exists(x=>x.dilutionTimes !=0))
                    bufferPipettingInfos.AddRange(GenerateBufferPipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex));
                columnIndex++;
            }
            return bufferPipettingInfos;
        }

        private List<List<PipettingInfo>> GenerateBufferPipettingInfos(List<DilutionInfo> thisColumnPipettingInfos, 
            string destPlateLabel, int columnIndex)
        {
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
           for(int i = 0; i< thisColumnPipettingInfos.Count;i++)
           {
               //GenerateBufferPipettingInfos()
               int indexInColumn = i;
               List<PipettingInfo> thisSamplePipettingInfos = GenerateBufferPipettingInfos(thisColumnPipettingInfos[i], destPlateLabel, columnIndex, indexInColumn);
               pipettingInfos.Add(thisSamplePipettingInfos);
           }
           return pipettingInfos;
        }

        private List<PipettingInfo> GenerateBufferPipettingInfos(DilutionInfo dilutionInfo, string destPlateLabel, 
            int columnIndex, int indexInColumn)
        {
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            double times = dilutionInfo.dilutionTimes;
            List<double> dilutionVolumes = GetEachStepDilutionVolume(times);
            string srcLabware = "Buffer";
            foreach(double vol in dilutionVolumes)
            {
                PipettingInfo pipettingInfo = new PipettingInfo(srcLabware, indexInColumn + 1, destPlateLabel, GetWellID(columnIndex, indexInColumn), vol);
                pipettingInfos.Add(pipettingInfo);
            }
            return pipettingInfos;
        }

        private int GetWellID(int columnIndex, int indexInColumn)
        {
            return columnIndex * 16 + indexInColumn + 1;
        }

        private List<double> GetEachStepDilutionVolume(double times)
        {
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
                double dilutionVol = (thisStepTimes - 1) * 20;
                vols.Add(dilutionVol);
                double realTimes = dilutionVol/20.0;
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
