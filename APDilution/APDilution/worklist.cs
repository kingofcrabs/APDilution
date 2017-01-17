﻿using System;
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
        const string firstDilutionPlateName = "Dilution1";
        const string secondDilutionPlateName = "Dilution2";

        const string breakPrefix = "B;";
        GradualDilutionInfo gradualDilutionInfo = new GradualDilutionInfo();
        //Dictionary<DilutionInfo, List<int>> dilutionInfo_WellID = new Dictionary<DilutionInfo, List<int>>();
        public List<string> DoJob(List<DilutionInfo> dilutionInfos, List<DilutionInfo> rawDilutionInfos,
            out List<PipettingInfo> firstPlateBufferFlat, out List<PipettingInfo> secondPlateBufferFlat)
        {
            //plateName_LastDilutionPositions.Clear();
            //int parallelHoleCount = CountParallelHole(dilutionInfos);
            //this.dilutionInfos = FilterParallel(dilutionInfos);
            firstPlateBufferFlat = null;
            secondPlateBufferFlat = null;
            this.rawDilutionInfos = rawDilutionInfos;
            this.dilutionInfos = dilutionInfos;
            
            List<List<PipettingInfo>> firstPlateBuffer = new List<List<PipettingInfo>>();
            List<List<PipettingInfo>> secondPlateBuffer = new List<List<PipettingInfo>>();
 
            //List<string> gradualPipettingStrs = ProcessGradualDilution4StandardAndQC(rawDilutionInfos);
            //from buffer & sample to dilution
            var bufferPipettings = GenerateBufferPipettingInfos(ref firstPlateBuffer, ref secondPlateBuffer);
            var samplePipettings = GenerateSamplePipettingInfos();

            //convert to flat
            List<PipettingInfo> firstBufferFlat = new List<PipettingInfo>();
            firstPlateBuffer.ForEach(x => firstBufferFlat.AddRange(x));
            List<PipettingInfo> secondBufferFlat = new List<PipettingInfo>();
            secondPlateBuffer.ForEach(x => secondBufferFlat.AddRange(x));
            firstPlateBufferFlat = firstBufferFlat;
            secondPlateBufferFlat = secondBufferFlat;
            Save2Excel(firstBufferFlat, secondBufferFlat);

            //from dilution to reaction plate
            List<PipettingInfo> transferPipettings = GenerateTransferPipettingInfos();
            List<string> strs = new List<string>();
            strs.Add(GetComment("buffer"));
            strs.AddRange(Format(bufferPipettings,Configurations.Instance.BufferLiquidClass));
            
            //strs.Add(breakPrefix);
            strs.Add(GetComment("sample"));
            strs.AddRange(Format(samplePipettings,Configurations.Instance.SampleLiquidClass));
            
            //strs.Add(breakPrefix);
            strs.Add(GetComment("transfer"));
            strs.AddRange(Format(transferPipettings, Configurations.Instance.TransferLiquidClass));
            //strs.Add(breakPrefix);
            return strs;
        }

        protected string GetComment(string sComment)
        {
            return string.Format(breakPrefix + "Comment(\"{0}\");", sComment);
        }
        private List<string> ProcessGradualDilution4StandardAndQC(List<DilutionInfo> rawDilutionInfos)
        {
            int currentWellID = 1;
            var empty = new List<string>(); 
            if (!bool.Parse(ConfigurationManager.AppSettings["StandardGradual"]))
                return empty;

            List<PipettingInfo> standardStrs = GenerateGradualDilution(rawDilutionInfos, true, ref currentWellID);
            List<PipettingInfo> QCStrs = GenerateGradualDilution(rawDilutionInfos, true, ref currentWellID);
            gradualDilutionInfo.sampleCnt = standardStrs.Count;
            gradualDilutionInfo.HQCWellID = standardStrs.Count + 1;
            gradualDilutionInfo.MQCWellID = standardStrs.Count + 2;
            gradualDilutionInfo.LQCWellID = standardStrs.Count + 3;
            gradualDilutionInfo.plateName = Configurations.Instance.GradualPlateName;
            List<string> strs = new List<string>();
            strs.AddRange(Format(standardStrs, Configurations.Instance.SampleLiquidClass));
            //strs.AddRange(Format(standardStrs, Configurations.Instance.SampleLiquidClass));
            return strs;
        }

        private List<PipettingInfo> GenerateGradualDilution(List<DilutionInfo> rawDilutionInfos, bool isStandard, ref int currentWellID)
        {
            var empty = new List<PipettingInfo>();
            List<DilutionInfo> dilutionInfos = null;
            if (isStandard)
                dilutionInfos = rawDilutionInfos.Where(x => x.type == SampleType.STD).ToList();
            else
            {
                dilutionInfos.AddRange(rawDilutionInfos.Where(x => x.type == SampleType.HQC));
                dilutionInfos.AddRange(rawDilutionInfos.Where(x => x.type == SampleType.MQC));
                dilutionInfos.AddRange(rawDilutionInfos.Where(x => x.type == SampleType.LQC));
            }
            if (dilutionInfos.Count == 1)
                return empty;
            int ratio = (int)(dilutionInfos[1].dilutionTimes / dilutionInfos[0].dilutionTimes);
            if (dilutionInfos[1].dilutionTimes != (ratio * dilutionInfos[0].dilutionTimes))
                throw new Exception(string.Format("Cannot do gradual dilution because the ratio is:", ratio));
            for (int i = 1; i < dilutionInfos.Count - 1; i++)
            {
                double beforeTime = dilutionInfos[i].dilutionTimes;
                double afterTime = dilutionInfos[i + 1].dilutionTimes;
                if (afterTime / beforeTime != ratio)
                    throw new Exception(string.Format("Gradual dilutions' ratios are not equal: {0} vs {1}", ratio, afterTime / beforeTime));
            }
            //move standard to 1st well
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            SampleType firstWellType = isStandard ? SampleType.STD : SampleType.HQC;
            pipettingInfos.Add(new PipettingInfo(dilutionInfos.First().type.ToString(), 1,
                Configurations.Instance.GradualPlateName, currentWellID++, Configurations.Instance.DilutionVolume, currentWellID, firstWellType, dilutionInfos[0].animalNo));
            for (int i = 1; i < dilutionInfos.Count; i++)
            {
                pipettingInfos.Add(new PipettingInfo(Configurations.Instance.GradualPlateName, currentWellID - 1,
                 Configurations.Instance.GradualPlateName, currentWellID, Configurations.Instance.DilutionVolume / ratio, ratio, dilutionInfos[i].type, dilutionInfos[0].animalNo));
                pipettingInfos.Add(new PipettingInfo(Configurations.Instance.BufferLabwareName, 1,
                Configurations.Instance.GradualPlateName, currentWellID, Configurations.Instance.DilutionVolume * (ratio - 1) / ratio, ratio, dilutionInfos[i].type, dilutionInfos[0].animalNo));
                currentWellID++;
            }
            return pipettingInfos;
        }

        private void Save2Excel(List<PipettingInfo> firstBufferFlat,
            List<PipettingInfo> secondBufferFlat)
        {
            ExcelWriter excelWriter = new ExcelWriter();
            string outputFolder = Utility.GetOutputFolder();
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
                commands.Add("W;");
            }
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
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(firstDilutionPlateName, firstPlateSamples));
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(secondDilutionPlateName, secondPlateSamples));
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
                            pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, "Reaction", dstWellID, vol, 1, current.type,current.animalNo));
                        }
                    }
                }
                else
                {
                    foreach (var dstWellID in destWells)
                    {
                        pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, "Reaction", dstWellID, vol, 1, current.type, current.animalNo));
                    }
                    index++;
                }
            }
            return pipettingInfos;
        }

       

       

        #region sample
        internal List<PipettingInfo> GenerateSamplePipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            var firstPlateSamples = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateSamples = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            var firstPlate = GenerateSamplePipettingInfos(firstPlateSamples, firstDilutionPlateName);
            var secondPlate = GenerateSamplePipettingInfos(secondPlateSamples, secondDilutionPlateName);
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
            int maxDilutionTiems = thisColumnPipettingInfos.Select(x => x.dilutionTimes).Max();
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
            int times = dilutionInfo.dilutionTimes;
            List<int> sampleVolumes = GetEachStepVolume(times, false);
            

            PipettingInfo pipettingInfo = new PipettingInfo(dilutionInfo.type.ToString(),
                indexInColumn + 1, 
                destPlateLabel, 
                GetWellID(columnIndex, indexInColumn),
                sampleVolumes[0], 
                Configurations.Instance.DilutionVolume/sampleVolumes[0],
                dilutionInfo.type,dilutionInfo.animalNo);
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
                                                dilutionInfo.type,dilutionInfo.animalNo);
                columnIndex++;
                pipettingInfos.Add(pipettingInfo);
            }
            return pipettingInfos;
        }



        #endregion
        #region buffer

        internal List<List<PipettingInfo>> GenerateBufferPipettingInfos(ref List<List<PipettingInfo>> firstPlate,ref List<List<PipettingInfo>>secondPlate)
        {
            if(Configurations.Instance.IsGradualPipetting && rawDilutionInfos.Count > 8)
                throw new Exception("sample count > 8!");
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            List<DilutionInfo> firstPlateDilutions = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateDilutions = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            List<List<PipettingInfo>> pipettingInfos = new List<List<PipettingInfo>>();
            firstPlate = GenerateBufferPipettingInfos(firstPlateDilutions, firstDilutionPlateName);
            pipettingInfos.AddRange(firstPlate);
            plateName_LastDilutionPositions.Add(firstDilutionPlateName, GetLastPositions(firstPlate));
            secondPlate = GenerateBufferPipettingInfos(secondPlateDilutions, secondDilutionPlateName);
            plateName_LastDilutionPositions.Add(secondDilutionPlateName, GetLastPositions(secondPlate));
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
                return 4;
            else return 24;
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
            foreach(var sameRowPipettingInfos in pipettingInfos)
            {
                if (sameRowPipettingInfos.Count == 0)
                {
                    vals.Add(-1);
                    continue;
                }
                    
                if (Configurations.Instance.IsGradualPipetting && sameRowPipettingInfos.First().type == SampleType.Norm)//add whole row for gradual pipettings
                    vals.AddRange(sameRowPipettingInfos.Select(x=>x.dstWellID).ToList());
                else
                    vals.Add(sameRowPipettingInfos.Max(x => x.dstWellID));
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
            int times = dilutionInfo.dilutionTimes;
            int startWellID = 1 + index*2;
            int wellsNeeded = (int)Math.Ceiling(Math.Log(times, gradualTimes));
            List<int> destWellIDs = new List<int>();
            if (times == 1)
                destWellIDs.Add(startWellID);
            if (wellsNeeded > 24)
                throw new Exception(string.Format("Need {0} wells to do gradual dilution!",wellsNeeded));
            if(wellsNeeded < 12)
            {
                for (int i = 0; i < wellsNeeded; i++)
                    destWellIDs.Add(startWellID + i * 8);
            }
            else
            {
                for (int i = 0; i < 12; i++)
                    destWellIDs.Add(startWellID + i * 8);
                int remainWells = wellsNeeded - 12;
                for (int i = 0; i < remainWells; i++)
                    destWellIDs.Add(startWellID + 1 + i * 8);
            }
            


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
                        wellTimes, dilutionInfo.type,dilutionInfo.animalNo));
                else
                {
                    string srcLabware = destPlateLabel;
                    int srcWellID = dstWellID - 8;
                    if(isFirstColumn)
                    {
                        srcLabware = GetFirstColumnLabwareName(dilutionInfo.type);
                        srcWellID = GetFirstColumnSrcWellID(srcWellIndex,dilutionInfo);
                        isFirstColumn = false;
                    }
                    pipettingInfo.Add(new PipettingInfo(srcLabware,
                        srcWellID, 
                        destPlateLabel,
                        dstWellID,
                        vol, wellTimes, dilutionInfo.type,dilutionInfo.animalNo));
                }
                srcWellIndex++;
            }
            return pipettingInfo;
        }

        private int GetFirstColumnSrcWellID(int srcWellIndex, DilutionInfo dilutionInfo)
        {
            var sampleType = dilutionInfo.type;
            if (sampleType == SampleType.HQC ||
                sampleType == SampleType.MQC ||
                sampleType == SampleType.LQC)
            {
                if (sampleType == SampleType.HQC)
                    return 1;
                if (sampleType == SampleType.MQC)
                    return 2;
                if (sampleType == SampleType.LQC)
                    return 3;
            }
            return Configurations.Instance.StandardQCSameTrough ? srcWellIndex % 8 + 1 : dilutionInfo.seqIDinThisType;
        }

        private string GetFirstColumnLabwareName(SampleType sampleType)
        {
            if (sampleType == SampleType.HQC ||
                sampleType == SampleType.MQC ||
                sampleType == SampleType.LQC)
            {
                return "QC";
            }
            return sampleType.ToString();    
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
            int times = dilutionInfo.dilutionTimes;
            List<int> dilutionVolumes = GetEachStepVolume(times,true);
            string srcLabware = Configurations.Instance.BufferLabwareName;
            foreach(double vol in dilutionVolumes)
            {
                double thisWellTimes = Configurations.Instance.DilutionVolume / (Configurations.Instance.DilutionVolume - vol);
                PipettingInfo pipettingInfo = new PipettingInfo(srcLabware, indexInColumn + 1,
                    destPlateLabel, GetWellID(columnIndex++, indexInColumn), vol,
                    thisWellTimes, dilutionInfo.type,dilutionInfo.animalNo);
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

        private List<int> GetEachStepVolume(int times, bool isBuffer)
        {
            FactorFinder factorFinder = new FactorFinder();
            List<int> eachStepTimes = factorFinder.GetBestFactors(times);
            List<int> vols = new List<int>();
            foreach(var thisStepTimes in eachStepTimes)
            {
                int bufferVol = (int)(Configurations.Instance.DilutionVolume * (thisStepTimes - 1) / thisStepTimes);
                int sampleVol = (int)(Configurations.Instance.DilutionVolume - bufferVol);
                vols.Add(isBuffer ? bufferVol : sampleVol);
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


    public class GradualDilutionInfo
    {
        public int sampleCnt;
        public int HQCWellID;
        public int MQCWellID;
        public int LQCWellID;
        public string plateName;
        
    }

    public class PipettingInfo
    {

        public string srcLabware;
        public int srcWellID;
        public string dstLabware;
        public int dstWellID;
        public double vol;
        public double dilutionTimes;
        public SampleType type;
        public string animalNo;
        public PipettingInfo(string srcLabware,
            int srcWell, string dstLabware, int dstWell, double v, double dilutionTimes, SampleType type, string animalNo)
        {
            this.srcLabware = srcLabware;
            this.dstLabware = dstLabware;
            this.srcWellID = srcWell;
            this.dstWellID = dstWell;
            this.vol = v;
            this.dilutionTimes = dilutionTimes;
            this.type = type;
            this.animalNo = animalNo;
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
            animalNo = pipettingInfo.animalNo;
        }
    }
}
