﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
        List<PipettingInfo> firstSample2Dilution = new List<PipettingInfo>();
        int transferVol = 0;
        //Dictionary<DilutionInfo, List<int>> dilutionInfo_WellID = new Dictionary<DilutionInfo, List<int>>();
        public void DoJob(string assayName, string reactionBarcode,
            List<DilutionInfo> dilutionInfos, List<DilutionInfo> rawDilutionInfos,
            out List<PipettingInfo> firstPlateBufferFlat, out List<PipettingInfo> secondPlateBufferFlat, int transferVol)
        {
            this.transferVol = transferVol;

            string subOutputFolder = Utility.GetSubOutputFolder();
            var header = "AnalysisNo,Src Labware,Src WellID,Volume,Dst Labware,Dst WellID";
            
            firstPlateBufferFlat = null;
            secondPlateBufferFlat = null;
            this.rawDilutionInfos = rawDilutionInfos;
            this.dilutionInfos = dilutionInfos;
            List<PipettingInfo> allPipettings = new List<PipettingInfo>();
            List<string> strs = new List<string>();

            if (Configurations.Instance.StandardGradual)
            {
                strs.Add(GetComment("standard gradual"));
                List<string> gradualPipettingStrs = ProcessGradualDilution4StandardAndQC(this.rawDilutionInfos, allPipettings);
                var gradualReadableStrs = FormatReadable(allPipettings);
                File.WriteAllLines(subOutputFolder + "gradualSTDQC.csv", strs);
                strs.AddRange(gradualPipettingStrs);
            }
            

            List<List<PipettingInfo>> firstPlateBuffer = new List<List<PipettingInfo>>();
            List<List<PipettingInfo>> secondPlateBuffer = new List<List<PipettingInfo>>();
          
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
            //Save2Excel(firstBufferFlat, secondBufferFlat);
            List<string> readableCommands = new List<string>();
            
            readableCommands.Add(header);
          
            strs.Add(GetComment("buffer"));
            //var flatBufferPipettings = Flatten(bufferPipettings);
            string bufferFile = subOutputFolder + "buffer.gwl";
            strs.AddRange(FormatBuffer(bufferPipettings, Configurations.Instance.BufferLiquidClass));
            File.WriteAllLines(bufferFile, strs);

            allPipettings.AddRange(bufferPipettings);
            
            strs.Add(GetComment("sample"));
            allPipettings.AddRange(samplePipettings);
          
            //after each column's pipetting, shake 
            //get first plate
            List<string> firstColumnStrs = new List<string>();
            var firstPlateSamplePipettings = samplePipettings.Where(x => x.dstLabware == firstDilutionPlateName).ToList();
            //extract first column pipettings
            var firstColumnPipettings = firstPlateSamplePipettings.Where(x => x.dstWellID <= 8).ToList();
            firstPlateSamplePipettings = firstPlateSamplePipettings.Except(firstColumnPipettings).ToList();
            firstColumnStrs.AddRange(GetStringForEachColumn(firstColumnPipettings, Configurations.Instance.SampleLiquidClass));
            strs.AddRange(GetStringForEachColumn(firstPlateSamplePipettings, Configurations.Instance.SampleLiquidClass));
            
            var secondPlateSamplePipettings = samplePipettings.Except(firstPlateSamplePipettings).ToList();
            //extract first column pipettings
            firstColumnPipettings = secondPlateSamplePipettings.Where(x => x.dstWellID <= 8).ToList();
            firstColumnStrs.AddRange(GetStringForEachColumn(firstColumnPipettings, Configurations.Instance.SampleLiquidClass));
            secondPlateSamplePipettings = secondPlateSamplePipettings.Except(firstColumnPipettings).ToList();
            strs.AddRange(GetStringForEachColumn(secondPlateSamplePipettings, Configurations.Instance.SampleLiquidClass));
            string firstColumnFile = subOutputFolder + "firstColumn.gwl";
            File.WriteAllLines(firstColumnFile, firstColumnStrs);

            //from dilution to reaction plate
            List<PipettingInfo> transferPipettings = GenerateTransferPipettingInfos();
            strs.Add(GetComment("transfer"));
            allPipettings.AddRange(transferPipettings);
            strs.AddRange(FormatTransfer(transferPipettings, Configurations.Instance.TransferLiquidClass));
            allPipettings = allPipettings.OrderBy(x => x.analysisNo).ToList();
            readableCommands.AddRange(FormatReadable(allPipettings));


            
            string dilutionFile = subOutputFolder + "dilution.gwl";
            var sReactionBarcodeFile = Utility.GetOutputFolder() + "currentBarcode.txt";
            File.WriteAllText(sReactionBarcodeFile, reactionBarcode);
            string tplFile = subOutputFolder + "current.tpl";
            TPLFile.Generate(tplFile, dilutionInfos);
            var sReadableFile = subOutputFolder + "readable.csv";
            File.WriteAllLines(dilutionFile, strs);

            var rCommands = GenerateRCommands(Helper.GetConfigFolder() + assayName + ".csv", dilutionInfos.Select(x => x.destWellID).ToList());
            for (int i = 0; i < rCommands.Count; i++)
            {
                File.WriteAllText(subOutputFolder + string.Format("r{0}.gwl", i + 1), rCommands[i]);
            }

            File.WriteAllLines(sReadableFile, readableCommands, Encoding.Default);
            Copy2AssayFolder(tplFile, sReadableFile, reactionBarcode);
        }

    

        private void Copy2AssayFolder(string tplFile, string sReadableFile, string reactionBarcode)
        {
            string dstFolder = Utility.GetAssaysFolder();
            File.Copy(sReadableFile, dstFolder + string.Format("{0}.csv", reactionBarcode), true);
            File.Copy(tplFile, dstFolder + string.Format("{0}.tpl", reactionBarcode), true);
        }


        private List<string> ProcessGradualDilution4StandardAndQC(
            List<DilutionInfo> rawDilutionInfos,
            List<PipettingInfo> allPipettings)
        {
            int currentWellID = 1;
            var empty = new List<string>();
            if (!Configurations.Instance.StandardGradual)
                return empty;

            List<PipettingInfo> standardDilutions = GenerateGradualDilution(rawDilutionInfos, true, ref currentWellID);
            List<PipettingInfo> QCDilutions = GenerateGradualDilution(rawDilutionInfos, false, ref currentWellID);
            standardDilutions = SplitByVolume(standardDilutions);
            QCDilutions = SplitByVolume(QCDilutions);
            allPipettings.AddRange(standardDilutions);
            allPipettings.AddRange(QCDilutions);

            gradualDilutionInfo.sampleCnt = standardDilutions.Count;
            gradualDilutionInfo.HQCWellID = standardDilutions.Count + 1;
            gradualDilutionInfo.MQCWellID = standardDilutions.Count + 2;
            gradualDilutionInfo.LQCWellID = standardDilutions.Count + 3;
            gradualDilutionInfo.plateName = Configurations.Instance.GradualPlateName;
            List<string> strs = new List<string>();
            strs.AddRange(Format(standardDilutions, Configurations.Instance.SampleLiquidClass));
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
                dilutionInfos = new List<DilutionInfo>();
                dilutionInfos.AddRange(rawDilutionInfos.Where(x => x.type == SampleType.HQC));
                dilutionInfos.AddRange(rawDilutionInfos.Where(x => x.type == SampleType.MQC));
                dilutionInfos.AddRange(rawDilutionInfos.Where(x => x.type == SampleType.LQC));
            }
            if (dilutionInfos.Count <= 1)
                return empty;
            int ratio = (int)(dilutionInfos[1].dilutionTimes / dilutionInfos[0].dilutionTimes);
            if (dilutionInfos[1].dilutionTimes != (ratio * dilutionInfos[0].dilutionTimes))
                throw new Exception(string.Format("不能做梯度稀释应为样品之间的稀释倍数不能整除。"));
            for (int i = 1; i < dilutionInfos.Count - 1; i++)
            {
                double beforeTime = dilutionInfos[i].dilutionTimes;
                double afterTime = dilutionInfos[i + 1].dilutionTimes;
                if (afterTime / beforeTime != ratio)
                    throw new Exception(string.Format("梯度稀释倍数不等: {0}-{1}", ratio, afterTime / beforeTime));
            }
            //move standard to 1st well
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            SampleType firstWellType = isStandard ? SampleType.STD : SampleType.HQC;
            string sLabware = isStandard ? "STD" : "QC";
        
            for (int i = 0; i < dilutionInfos.Count-1; i++)
            {
                int curWellID = i + 1;
                int maxSampleAvailable = (int)(dilutionInfos[i].orgVolume * (ratio - 1) / ratio);
                int sampleVol = Math.Min(maxSampleAvailable, Configurations.Instance.DilutionVolume / ratio);
                int bufferVol = (ratio - 1) * sampleVol;
                var nextSampleDilutionInfo = dilutionInfos[i + 1];
                nextSampleDilutionInfo.orgVolume = (uint)(sampleVol * ratio);
                dilutionInfos[i + 1] = nextSampleDilutionInfo;
                var curSampleIndex = rawDilutionInfos.FindIndex(x => x.analysisNo == dilutionInfos[i].analysisNo);
                var curSampleDilutionInfo = rawDilutionInfos[curSampleIndex];
                curSampleDilutionInfo.orgVolume -= (uint)sampleVol;
                rawDilutionInfos[curSampleIndex] = curSampleDilutionInfo;

                pipettingInfos.Add(new PipettingInfo(sLabware,
                                                     curWellID,
                                                     sLabware,
                                                     curWellID + 1,
                                                     sampleVol,
                                                     ratio,
                                                     dilutionInfos[i].type,
                                                     dilutionInfos[i].analysisNo));

                pipettingInfos.Add(new PipettingInfo(
                    Configurations.Instance.Buffer1LabwareName, 1,
                    sLabware,
                    curWellID + 1,
                    bufferVol,
                    ratio,
                    dilutionInfos[i].type,
                    dilutionInfos[i].analysisNo));
            }
            return pipettingInfos;
        }

        //private string GetLabware(SampleType sampleType)
        //{
        //    switch(sampleType)
        //    {
        //        case SampleType.HQC:
        //        case SampleType.MQC:
        //        case SampleType.LQC:
        //            return "QC";
        //        default:
        //            return sampleType.ToString();
        //    }
        //}
        private List<PipettingInfo> Flatten(List<List<PipettingInfo>> pipettingInfos)
        {
            List<PipettingInfo> oneDPipettingInfos = new List<PipettingInfo>();
            foreach (var pipettings in pipettingInfos)
            {
                foreach (var pipetting in pipettings)
                {
                    oneDPipettingInfos.Add(pipetting);
                }
            }
            return oneDPipettingInfos;
        }

        private IEnumerable<string> FormatReadable(List<PipettingInfo> pipettingInfos)
        {
            List<string> strs = new List<string>();
            foreach (var pipetting in pipettingInfos)
            {
                strs.Add(FormatReadable(pipetting));
            }
            return strs;
        }

        private List<string> FormatReadable(List<List<PipettingInfo>> bufferPipettings)
        {
            List<string> strs = new List<string>();
            foreach(var pipettings in bufferPipettings)
            {
                foreach(var pipetting in pipettings)
                {
                    strs.Add(FormatReadable(pipetting));
                }
            }
            return strs;
        }

        private List<string> GetShakeCommands()
        {
            List<string> strs =
             new List<string>()
            {
                "B;FACTS(\"Shaker\",\"Shaker_Start\",\"1\",\"0\",\"\");",
                "B;StartTimer(\"10\");",
               
            };
            strs.Add(string.Format("B;WaitTimer(\"10\",\"{0}\");",ConfigurationManager.AppSettings["ShakeSeconds"]));
            strs.Add("B;FACTS(\"Shaker\",\"Shaker_Stop\",\"\",\"0\",\"\")");
            return strs;
        }
        private string FormatReadable(PipettingInfo pipettingInfo)
        {
            return string.Format("{0},{1},{2},{3},{4},{5}",
                pipettingInfo.analysisNo,
                pipettingInfo.srcLabware,
                pipettingInfo.srcWellID, pipettingInfo.vol, pipettingInfo.dstLabware, pipettingInfo.dstWellID);
        }

        private List<string> GetStringForEachColumn(List<PipettingInfo> firstPlateSamplePipettings, string liquidClass)
        {
            List<string> strs = new List<string>();
            for(int col = 0; col< 12; col++)
            {
                int startWellID = col * 8 + 1;
                int endWellID = col *8 + 8;
                var thisColumnPipetting = firstPlateSamplePipettings.Where(x => x.dstWellID >= startWellID && x.dstWellID <= endWellID).ToList();
                if (thisColumnPipetting.Count == 0)
                    continue;
                strs.AddRange(Format(thisColumnPipetting, liquidClass));
                strs.AddRange(GetShakeCommands());
            }
            return strs;
        }

        protected string GetComment(string sComment)
        {
            return string.Format(breakPrefix + "Comment(\"{0}\");", sComment);
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

        private IEnumerable<string> FormatSingle(PipettingInfo pipettingInfo, string liquidClass)
        {
            List<string> commands = new List<string>();
            commands.Add(GetAspirate(pipettingInfo.srcLabware, pipettingInfo.srcWellID, pipettingInfo.vol, liquidClass));
            commands.Add(GetDispense(pipettingInfo.dstLabware, pipettingInfo.dstWellID, pipettingInfo.vol, liquidClass));
            commands.Add("W;");
            return commands;
        }


        private IEnumerable<string> FormatBuffer(List<PipettingInfo> pipettingInfos, string liquidClass)
        {
             List<PipettingInfo> optimizedPipetting = new List<PipettingInfo>();
            var firstBuffer = pipettingInfos.Where(x => x.srcLabware == Configurations.Instance.Buffer1LabwareName).ToList();
            var secondBuffer = pipettingInfos.Where(x => x.srcLabware == Configurations.Instance.Buffer2LabwareName).ToList();
            List<List<PipettingInfo>> pipettingEachBuffer = new List<List<PipettingInfo>>()
            {
                firstBuffer,secondBuffer
            };
            List<string> commands = new List<string>();
            for (int plateIndex = 0; plateIndex < 2; plateIndex++)
            {
                var curPipettingInfos = pipettingEachBuffer[plateIndex];
                for (int row = 0; row < 8; row++ )
                {
                    var thisColumnPipettings = curPipettingInfos.Where(x => IsSameRow(x,row)).ToList();
                    foreach (var pipettingInfo in thisColumnPipettings)
                    {
                        commands.Add(GetAspirate(pipettingInfo.srcLabware, row+1, pipettingInfo.vol, liquidClass));
                        commands.Add(GetDispense(pipettingInfo.dstLabware, pipettingInfo.dstWellID, pipettingInfo.vol, liquidClass));
                    }
                    commands.Add("W;");
                }
            }
            return commands;
        }

        private bool IsSameRow(PipettingInfo x, int rowIndex)
        {
            int firstWellID = rowIndex + 1;
            return (x.dstWellID - firstWellID) % 8 == 0;
        }

        private IEnumerable<string> FormatTransfer(List<PipettingInfo> transferPipettings, string liquidClass)
        {
            List<string> commands = new List<string>();
            while(transferPipettings.Count > 0)
            {
                var first = transferPipettings.First();
                var sameSrcPipettings = transferPipettings.Where(x => x.srcLabware == first.srcLabware && x.srcWellID == first.srcWellID).ToList();
                foreach (var pipettingInfo in sameSrcPipettings)
                {
                    commands.Add(GetAspirate(pipettingInfo.srcLabware, pipettingInfo.srcWellID, pipettingInfo.vol, liquidClass));
                    commands.Add(GetDispense(pipettingInfo.dstLabware, pipettingInfo.dstWellID, pipettingInfo.vol, liquidClass));
                    commands.Add("W;");
                }
            }
            return commands;
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

        internal List<PipettingInfo> GenerateTransferPipettingInfos()
        {
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            var firstPlateSamples = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateSamples = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(firstDilutionPlateName, firstPlateSamples));
            pipettingInfos.AddRange(GenerateTransferPipettingInfos(secondDilutionPlateName, secondPlateSamples));
            pipettingInfos = SplitByVolume(pipettingInfos);
            return pipettingInfos;
        }

        private DilutionInfo Clone(DilutionInfo x)
        {
            DilutionInfo newInfo = new DilutionInfo(x.type,x.orgVolume,
                x.dilutionTimes, x.seqIDinThisType, x.destWellID,x.gradualStep);
            return newInfo;
        }

        private List<PipettingInfo> GenerateTransferPipettingInfos(string plateName, List<DilutionInfo> dilutionInfosWithoutParallel)
        {
            var srcPositionOnDilutionPlate = plateName_LastDilutionPositions[plateName];
            int index = 0;
            //Configurations.Instance.ReactionVolume;
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            foreach (var current in dilutionInfosWithoutParallel)
            {
                int vol = GetTransferVolume(current); 

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
                    var remainVol = vol * (Configurations.Instance.GradualTimes - 1) / Configurations.Instance.GradualTimes;
                    for (int i = 0; i < gradualWellsNeeded; i++)
                    {
                        var tmpVol = i == gradualWellsNeeded - 1 ? vol : remainVol;
                        int startWellID = current.destWellID + i * 2;
                        srcWellID = srcPositionOnDilutionPlate[index++];
                        for(int parallel = 0; parallel < 2; parallel++)
                        {
                            int dstWellID = startWellID + parallel;
                            pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, Configurations.Instance.ReactionPlateName, dstWellID, tmpVol, 1, current.type, current.analysisNo));
                        }
                    }
                }
                else
                {
                    foreach (var dstWellID in destWells)
                    {
                        pipettingInfos.Add(new PipettingInfo(plateName, srcWellID, Configurations.Instance.ReactionPlateName, dstWellID, vol, 1, current.type, current.analysisNo));
                    }
                    index++;
                }
            }
            return pipettingInfos;
        }

        private int GetTransferVolume(DilutionInfo current)
        {
           int vol = Math.Min(Configurations.Instance.DilutionVolume,
                    (int)(current.orgVolume * current.dilutionTimes));
           int parallelCnt = current.type == SampleType.Norm ? ExcelReader.SampleParallelCnt : ExcelReader.STDParallelCnt;
           if (vol < parallelCnt * transferVol)
               throw new Exception(string.Format("Sample with analysis no: {0}'s volume is not enough for transfering.", current.analysisNo));
           return vol / parallelCnt;
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
            pipettingInfos = SplitByVolume(pipettingInfos);
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
                    {
                        var thisColumnSampleInfos = GenerateSamplePipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex);
                        samplePipettingInfos.AddRange(thisColumnSampleInfos);
                    }
                    dilutionInfos = dilutionInfos.Skip(thisColumnPipettingInfos.Count).ToList();
                    columnIndex += GetMaxDilutionWellsNeeded(thisColumnPipettingInfos);
                }
            }
            return samplePipettingInfos;
        }

        private int GetMaxDilutionWellsNeeded(List<DilutionInfo> thisColumnPipettingInfos)
        {
            int maxWellsNeed = 1;
            for (int i = 0; i < thisColumnPipettingInfos.Count; i++)
            {
                int times = thisColumnPipettingInfos[i].dilutionTimes;
                int mrdTimes = ExcelReader.MRDTimes;
                int remainTimes = times / mrdTimes;
                FactorFinder factorFinder = new FactorFinder();
                int mrdWellsNeed = factorFinder.GetBestFactors(mrdTimes,true).Count;
                int remainWellsNeed = factorFinder.GetBestFactors(remainTimes).Count;
                int totalWellsNeed = mrdWellsNeed + remainWellsNeed;
                if (mrdTimes == 1)
                    totalWellsNeed = remainWellsNeed;
                if (totalWellsNeed > maxWellsNeed)
                    maxWellsNeed = totalWellsNeed;
            }

            //if (maxWellsNeed > Configurations.Instance.DilutionWells)
            //    throw new Exception("稀释孔数量不足以实现稀释！");
            return maxWellsNeed;
        }


        public List<string> GenerateRCommands(string assayFile,List<int> dstWells)
        {
            List<string> contents = File.ReadAllLines(assayFile).ToList();
            List<string> rCommands = new List<string>();
            for(int i = 1; i< contents.Count; i++)
            {
                List<string> tmpStrs = contents[i].Split(',').ToList();
                string srcLabware = tmpStrs[0];
                int vol = int.Parse(tmpStrs[1]);
                rCommands.Add(GenerateRCommand(srcLabware, Configurations.Instance.ReactionPlateName, dstWells, vol));
            }
            return rCommands;
        }


        private string GenerateRCommand(string srcLabel,string destLabel,List<int> dstWells, int vol)
        {
            //R;AspirateParameters;DispenseParameters;Volume;LiquidClass;NoOfDitiRe
            //uses;NoOfMultiDisp;Direction[;ExcludeDestWell]*
            //AspirateParameters =
            //SrcRackLabel; SrcRackID; SrcRackType; SrcPosStart; SrcPosEnd;
            //and
            //DispenseParameters =
            //DestRackLabel; DestRackID; DestRackType; DestPosStart; DestPosEnd;
            
            string aspParameters = string.Format("{0};;;{1};{2};", srcLabel, 1, 8);
            int maxWell = dstWells.Max();
            int minWell = dstWells.Min();
            string exclude = "";
            for(int wellID = minWell; wellID < maxWell; wellID++)
            {
                if(!dstWells.Contains(wellID))
                {
                    exclude += ";";
                    exclude += wellID;
                }
            }
            string dspParameters = string.Format("{0};;;{1};{2};", destLabel, minWell, maxWell);
            //R;AspirateParameters;DispenseParameters;Volume;LiquidClass;NoOfDitiRe
            //uses;NoOfMultiDisp;Direction[;ExcludeDestWell]*
            int noOfMultiDisp = 0;
            if(vol <= 50)
            {
                noOfMultiDisp = 12;
            }
            else if(vol <= 110)
            {
                noOfMultiDisp = 6;
            }
            else if(vol <= 200)
            {
                noOfMultiDisp = 4;
            }
            else if( vol <= 300)
            {
                noOfMultiDisp = 3;
            }
            else
            {
                noOfMultiDisp = 2;
            }


            string rCommand =  string.Format("R;{0};{1};{2};{3};{4};{5};{6}{7}",
                aspParameters,
                dspParameters,
                vol,
                Configurations.Instance.ReagentLiquidClass,
                1,
                noOfMultiDisp,
                0,
                exclude);

            return rCommand;
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
            List<int> eachStepTimes = new List<int>();
            int mrdSteps = 0;
            List<int> sampleVolumes = GetEachStepVolume(times, 
                                                        dilutionInfo.orgVolume, false, ref eachStepTimes,ref mrdSteps);

            bool isQC = IsQC(dilutionInfo.type);
            string sLabware =  isQC? "QC" : dilutionInfo.type.ToString();
            int srcWell = GetSourceWell(dilutionInfo);
            PipettingInfo pipettingInfo = new PipettingInfo(sLabware,
                srcWell, 
                destPlateLabel, 
                GetWellID(columnIndex, indexInColumn),
                sampleVolumes[0],
                eachStepTimes[0],
                dilutionInfo.type,dilutionInfo.analysisNo);
            pipettingInfos.Add(pipettingInfo);
            sampleVolumes = sampleVolumes.Skip(1).ToList();
            for (int i = 0; i < sampleVolumes.Count; i++)
            {
                double vol = sampleVolumes[i];
                int dstWellID = GetWellID(columnIndex + 1, indexInColumn);
                if(dstWellID > 96)
                    throw new Exception(string.Format("样品：{0}需要用到第{1}个孔", dilutionInfo.analysisNo,dstWellID));
                pipettingInfo = new PipettingInfo(destPlateLabel, 
                                                GetWellID(columnIndex, indexInColumn),
                                                destPlateLabel,
                                                dstWellID,
                                                vol,
                                                eachStepTimes[i],
                                                dilutionInfo.type,dilutionInfo.analysisNo);
                columnIndex++;
                pipettingInfos.Add(pipettingInfo);
            }
            return pipettingInfos;
        }

        private int GetSourceWell(DilutionInfo dilutionInfo)
        {
            if (IsQC(dilutionInfo.type))
            {
                if (dilutionInfo.type == SampleType.HQC)
                    return 1;
                else if (dilutionInfo.type == SampleType.MQC)
                    return 2;
                else
                    return 3;
            }
            if (dilutionInfo.type == SampleType.STD || dilutionInfo.type == SampleType.Norm)
            {
                return dilutionInfo.seqIDinThisType;
            }
            else
                return 1;
        }

        private bool IsQC(SampleType sampleType)
        {
            return sampleType == SampleType.HQC ||
                sampleType == SampleType.MQC ||
                sampleType == SampleType.LQC;
        }

        #endregion
        #region buffer

        internal List<PipettingInfo> GenerateBufferPipettingInfos(ref List<List<PipettingInfo>> firstPlate,ref List<List<PipettingInfo>>secondPlate)
        {
            if(Configurations.Instance.IsGradualPipetting && rawDilutionInfos.Count > 8)
                throw new Exception("样品数不得大于8!");
            int firstPlateCnt = GetMaxSampleCntFirstDilutionPlate();
            List<DilutionInfo> firstPlateDilutions = rawDilutionInfos.Take(firstPlateCnt).ToList();
            var secondPlateDilutions = rawDilutionInfos.Skip(firstPlateCnt).ToList();
            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            firstPlate = GenerateBufferPipettingInfos(firstPlateDilutions, firstDilutionPlateName);
           
            plateName_LastDilutionPositions.Add(firstDilutionPlateName, GetLastPositions(firstPlate));
            secondPlate = GenerateBufferPipettingInfos(secondPlateDilutions, secondDilutionPlateName);
            plateName_LastDilutionPositions.Add(secondDilutionPlateName, GetLastPositions(secondPlate));
            var flatFirstPlate = Flatten(firstPlate);
            var flatSecondPlate = Flatten(secondPlate);
          
            pipettingInfos.AddRange(flatFirstPlate);
            pipettingInfos.AddRange(flatSecondPlate);
            pipettingInfos = SplitByVolume(pipettingInfos);
            //OptimizeSourcePipetting(ref pipettingInfos);
            
            return pipettingInfos;
        }

        internal List<PipettingInfo> SplitByVolume(List<PipettingInfo> pipettingInfos)
        {
            List<PipettingInfo> processedInfos = new List<PipettingInfo>();
            int safeVolume = (int)(Configurations.Instance.TipVolume * 0.9);
            foreach(var pipettingInfo in pipettingInfos)
            {
                List<PipettingInfo> subInfos = new List<PipettingInfo>(); 

                if(pipettingInfo.vol > safeVolume)
                {
                    double vol = pipettingInfo.vol;
                    while(vol > safeVolume)
                    {
                        
                        var tmpInfo = new PipettingInfo(pipettingInfo);
                        tmpInfo.vol = safeVolume;
                        vol -= safeVolume;
                        subInfos.Add(tmpInfo);
                    }
                    if(vol > 0)
                    {
                        var tmpInfo = new PipettingInfo(pipettingInfo);
                        tmpInfo.vol = vol;
                        subInfos.Add(tmpInfo);
                    }
                    processedInfos.AddRange(subInfos);   
                }
                else
                {
                    processedInfos.Add(pipettingInfo);
                }
            }
            return processedInfos;
        }

        private void OptimizeSourcePipetting(ref List<PipettingInfo> pipettingInfos)
        {
            List<PipettingInfo> optimizedPipetting = new List<PipettingInfo>();
            var firstBuffer = pipettingInfos.Where(x => x.srcLabware == Configurations.Instance.Buffer1LabwareName).ToList();
            var secondBuffer = pipettingInfos.Where(x => x.srcLabware == Configurations.Instance.Buffer2LabwareName).ToList();
            List<List<PipettingInfo>> pipettingEachBuffer = new List<List<PipettingInfo>>()
            {
                firstBuffer,secondBuffer
            };


            for (int plateIndex = 0; plateIndex < 2; plateIndex++ )
            {
                for (int i = 0; i < pipettingEachBuffer[plateIndex].Count; i++)
                {
                    var curPipetting = pipettingEachBuffer[plateIndex];
                    var tmpInfo = new PipettingInfo(curPipetting[i]);
                    tmpInfo.srcWellID = i % 8 + 1;
                    optimizedPipetting.Add(tmpInfo);
                }
            }
            pipettingInfos = optimizedPipetting;
        }



        private int GetMaxSampleCntFirstDilutionPlate()
        {
            if (Configurations.Instance.IsGradualPipetting)
                return 4;
            else return 8*(12/Configurations.Instance.DilutionWells);
        }

     
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
                    {
                        var thisColumnBufferInfos = GenerateBufferPipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex);
                        bufferPipettingInfos.AddRange(GenerateBufferPipettingInfos(thisColumnPipettingInfos, destPlateLabel, columnIndex));
                        columnIndex += thisColumnBufferInfos.Max(x => x.Count);
                    }
                        
                    dilutionInfos = dilutionInfos.Skip(thisColumnPipettingInfos.Count).ToList();
                    //columnIndex += GetMaxDilutionWellsNeeded(thisColumnPipettingInfos);
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
            List<int> eachStepTimes = new List<int>();
            int mrdSteps = 0;
            List<int> volumes = GetEachStepVolume(times, dilutionInfo.orgVolume, isBuffer, ref eachStepTimes, ref mrdSteps);
            bool isFirstColumn = true;
            for (int i = 0; i < destWellIDs.Count; i++ )
            {
                var dstWellID = destWellIDs[i];
                var vol = volumes[srcWellIndex];
                double wellTimes = eachStepTimes[i];
                if (isBuffer)
                    pipettingInfo.Add(new PipettingInfo("Buffer",
                        srcWellIndex % 8 + 1,
                        destPlateLabel,
                        dstWellID,
                        vol,
                        wellTimes, dilutionInfo.type, dilutionInfo.analysisNo));
                else
                {
                    string srcLabware = destPlateLabel;
                    int srcWellID = dstWellID - 8;
                    if (isFirstColumn)
                    {
                        srcLabware = GetFirstColumnLabwareName(dilutionInfo.type);
                        srcWellID = GetFirstColumnSrcWellID(srcWellIndex, dilutionInfo);
                        isFirstColumn = false;
                    }
                    pipettingInfo.Add(new PipettingInfo(srcLabware,
                        srcWellID,
                        destPlateLabel,
                        dstWellID,
                        vol, wellTimes, dilutionInfo.type, dilutionInfo.analysisNo));
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
            //return Configurations.Instance.StandardQCSameTrough ? srcWellIndex % 8 + 1 : dilutionInfo.seqIDinThisType;
            return dilutionInfo.seqIDinThisType;
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
       
            List<int> eachStepTimes = new List<int>();
            int mrdSteps = 0;
            List<int> dilutionVolumes = GetEachStepVolume(times, dilutionInfo.orgVolume, true, ref eachStepTimes,ref mrdSteps);
                
            for(int i = 0; i < dilutionVolumes.Count; i++)
            {
                string srcLabware = i >= mrdSteps ? Configurations.Instance.Buffer2LabwareName : Configurations.Instance.Buffer1LabwareName;
                var vol = dilutionVolumes[i];
                double thisWellTimes = eachStepTimes[i];
                int dstWellID = GetWellID(columnIndex++, indexInColumn);
                PipettingInfo pipettingInfo = new PipettingInfo(srcLabware, indexInColumn + 1,
                    destPlateLabel, dstWellID, vol,
                    thisWellTimes, dilutionInfo.type,dilutionInfo.analysisNo);
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


        internal List<int> GetEachStepVolume(int times, uint sampleVol, bool isBuffer,ref  List<int> eachStepTimes, ref int mrdSteps)
        {
            int mrdTimes = ExcelReader.MRDTimes;
            FactorFinder factorFinder = new FactorFinder();
            if(mrdTimes != 1)
            {
                times = times / mrdTimes;
                eachStepTimes = factorFinder.GetBestFactors(mrdTimes,true);
                mrdSteps = eachStepTimes.Count;
            }
            bool processedInMRD = times == 1 && mrdTimes != 1; //has been processed in mrd
            if(!processedInMRD)
                eachStepTimes.AddRange(factorFinder.GetBestFactors(times));
            List<int> vols = new List<int>();
            foreach (var thisStepTimes in eachStepTimes)
            {
                int totalVol = (int)sampleVol * thisStepTimes;
                int bufferVol = 0;
                
                if( totalVol > Configurations.Instance.DilutionVolume)
                {
                    bufferVol = (int)(Configurations.Instance.DilutionVolume * (thisStepTimes - 1) / thisStepTimes);
                    sampleVol = (uint)(Configurations.Instance.DilutionVolume - bufferVol);
                }
                else
                {
                    bufferVol = (int)(sampleVol * (thisStepTimes - 1));
                }
                vols.Add(isBuffer ? bufferVol : (int)sampleVol);
                sampleVol = (uint)totalVol;
            }
            return vols;
        }


        //private List<int> GetEachStepVolume(int times, bool isBuffer)
        //{
        //    FactorFinder factorFinder = new FactorFinder();
        //    List<int> eachStepTimes = factorFinder.GetBestFactors(times);
        //    List<int> vols = new List<int>();
        //    foreach(var thisStepTimes in eachStepTimes)
        //    {

        //        int bufferVol = (int)(Configurations.Instance.DilutionVolume * (thisStepTimes - 1) / thisStepTimes);
        //        int sampleVol = (int)(Configurations.Instance.DilutionVolume - bufferVol);
        //        vols.Add(isBuffer ? bufferVol : sampleVol);
        //    }
        //    return vols;
        //}

        //private string FormatReadable(PipettingInfo pipettingInfo)
        //{
        //    return string.Format("{0},{1},{2},{3},{4}",
        //        pipettingInfo.srcLabware,
        //        pipettingInfo.srcWellID,
        //        pipettingInfo.dstLabware,
        //        pipettingInfo.dstWellID, pipettingInfo.vol);
        //}

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
        public string analysisNo;
        public PipettingInfo(string srcLabware,
            int srcWell, string dstLabware, int dstWell, double v, double dilutionTimes, SampleType type, string analysisNo)
        {
            this.srcLabware = srcLabware;
            this.dstLabware = dstLabware;
            this.srcWellID = srcWell;
            this.dstWellID = dstWell;
            this.vol = v;
            this.dilutionTimes = dilutionTimes;
            this.type = type;
            this.analysisNo = analysisNo;
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
            analysisNo = pipettingInfo.analysisNo;
        }
    }
}
