using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace APDilution
{
    public class ExcelReader
    {
        const int startRowNo = 4;
        const int startColNo = 9;
        const int rowCnt = 8;
        const int colCnt = 12;
        static public int MRDTimes { get; set; }
        static public int OrgSTDConc {get;set;}
        static public int STDParallelCnt { get; set; }
        static public int SampleParallelCnt { get; set; }
        public List<DilutionInfo> Read(string sFilePath,ref List<DilutionInfo> rawDilutionInfo)
        {
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            Application app = new Application();
            try
            {
                app.Visible = false;
                app.DisplayAlerts = false;
                dilutionInfos = ReadImpl(app, sFilePath, ref rawDilutionInfo);
            }
            finally
            {
                app.Quit(); app = null;
            }
            CheckValidity(dilutionInfos);
            return dilutionInfos;
        }

        private void CheckValidity(List<DilutionInfo> dilutionInfos)
        {
            var normalDilutions = dilutionInfos.Where(x=>x.type == SampleType.Norm).ToList();
            
            if(normalDilutions.Exists(x=>x.dilutionTimes <=0))
                 throw new Exception("稀释倍数必须大于0!");
            //if(dilutionInfos.Exists(x=>x.type == SampleType.MatrixBlank && x.dilutionTimes != 0))
            //    throw new Exception("MatrixBlank 稀释倍数必须为0!");
            if(dilutionInfos.Exists(x=>x.dilutionTimes > 6250000))
                throw new Exception("稀释倍数必须不大于6.25M!");

            foreach(var dilutionInfo in dilutionInfos)
            {
                if (dilutionInfo.orgVolume * dilutionInfo.dilutionTimes < Configurations.Instance.DilutionVolume)
                {
                    throw new Exception(string.Format("分析号为：{0}的样品体积太少，无法稀释到{1}", dilutionInfo.analysisNo, Configurations.Instance.DilutionVolume));
                }
            }

            FactorFinder factorFinder = new FactorFinder();
            var validTimes = factorFinder.GetValidDilutionTimes();
            foreach(var dilutionInfo in dilutionInfos)
            {
                if (!validTimes.Exists(x => x == dilutionInfo.dilutionTimes))
                    throw new Exception(string.Format("分析号:{0}样品的稀释倍数：{1}非法！", dilutionInfo.analysisNo, dilutionInfo.dilutionTimes));
            }
        }


        private List<DilutionInfo> ReadImpl(Application app, string sFilePath, ref List<DilutionInfo> rawDilutionInfos)
        {
            Workbook wb = app.Workbooks.Open(sFilePath);
            Worksheet ws = (Worksheet)wb.Worksheets.get_Item(1);
            int rowsint = ws.UsedRange.Cells.Rows.Count; //total rows
            Range rngNormalSampleInfo = ws.Cells.get_Range("A2", "E" + rowsint);   //item
            Range rngStartConc = ws.Cells.get_Range("H1");
            Range rngSTDParallel = ws.Cells.get_Range("H2");
            Range rngSampleParallel = ws.Cells.get_Range("H3");
            Range rngMRDTimes = ws.Cells.get_Range("H4");
            Range rngAssayName = ws.Cells.get_Range("H5");

            if (rngSTDParallel.Value2 == null)
                throw new Exception("STD复孔数未设置！");

            if( rngSampleParallel.Value2 == null)
                throw new Exception("样本复孔数未设置！");

            if(rngStartConc.Value2 == null)
                throw new Exception("STD起始浓度未设置！");


            if (rngMRDTimes.Value2 == null)
                throw new Exception("MRD倍数未设置！");

            if (rngAssayName.Value2 == null)
                throw new Exception("方法名未设置！");


            int stdParallelCnt = int.Parse(rngSTDParallel.Value2.ToString());
            int sampleParallelCnt = int.Parse(rngSampleParallel.Value2.ToString());
            OrgSTDConc = int.Parse(rngStartConc.Value2.ToString());
            STDParallelCnt = stdParallelCnt;
            SampleParallelCnt = sampleParallelCnt;
            MRDTimes = int.Parse(rngMRDTimes.Value2.ToString());
            string assayName = rngAssayName.Value2.ToString();
            if (Configurations.Instance.AssayName != assayName)
                throw new Exception(string.Format("设置方法名为：{0}，文件中方法名为:{1}，不一致！",
                    Configurations.Instance.AssayName,
                    assayName));


            if (STDParallelCnt < 2 || STDParallelCnt > 12)
                throw new Exception("STD复孔数必须介于2~12之间！");
            if(SampleParallelCnt < 2 || STDParallelCnt > 12)
                throw new Exception("Sample复孔数必须介于2~12之间！");

            Dictionary<int, NormalSampleInfo> sampleID_Info = new Dictionary<int, NormalSampleInfo>();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            List<int> remainingWellIDs = new List<int>();
            for(int i = 0; i<96; i++)
            {
                remainingWellIDs.Add(i + 1);
            }

            object[,] arryItem = (object[,])rngNormalSampleInfo.Value2;
            int lastWellIDOccupied = 1;
            int maxParallelCnt = 0;
            rawDilutionInfos = new List<DilutionInfo>();
            int expectedColumnCnt = Configurations.Instance.IsGradualPipetting ? 3 : 5;
            for (int i = 1; i <= rowsint - 1; i++)
            {
                
                if (arryItem[i, 1] == null)
                    break;
                string animalNo = arryItem[i, 1].ToString();
                if (arryItem.GetLength(1) < expectedColumnCnt)
                    throw new Exception(string.Format("动物号为：{0}的样本信息不全！", animalNo));

                int sampleID = int.Parse(arryItem[i, 2].ToString());
                int dilutionTimes = int.Parse(arryItem[i, 3].ToString());
                string sVolume = ((int)Configurations.Instance.DilutionVolume).ToString();
                
                if(!Configurations.Instance.IsGradualPipetting)
                {
                    sVolume = arryItem[i, 4].ToString();
                }
                uint volume = uint.Parse(sVolume);
                rawDilutionInfos.Add(new DilutionInfo(ParseSampleType(animalNo),volume,
                    dilutionTimes, sampleID, 0, 1, animalNo));
            }
            CheckDuplicated(rawDilutionInfos);
            List<DilutionInfo> normalSamples = new List<DilutionInfo>();
            if(Configurations.Instance.IsGradualPipetting) //put normal samples to the end
            {
                normalSamples = rawDilutionInfos.Where(x => x.type == SampleType.Norm).ToList();
                if (normalSamples.Count > 2)
                    throw new Exception("普通样本数不得大于2!");
                rawDilutionInfos = rawDilutionInfos.Except(normalSamples).ToList();
            }


            for (int i = 0; i < rawDilutionInfos.Count; i++)
            {
                int parallelCnt = 0;
                int firstWellID4Parallel = 0;
                DilutionInfo rawDilutionInfo = rawDilutionInfos[i];
                List<DilutionInfo> tmpDilutionInfos = GetDilutionInfos(rawDilutionInfo,
                    stdParallelCnt, sampleParallelCnt, remainingWellIDs, ref parallelCnt, ref firstWellID4Parallel);
                if (parallelCnt > maxParallelCnt)
                    maxParallelCnt = parallelCnt;
                //lastWellIDOccupied = 
                bool isLastWellOfColumn = IsLastWellOfColumn(firstWellID4Parallel);
                if (isLastWellOfColumn)
                {
                    lastWellIDOccupied = firstWellID4Parallel + 8 * (maxParallelCnt - 1);
                    remainingWellIDs.RemoveAll(x => x <= lastWellIDOccupied);
                    maxParallelCnt = 0;
                }

                if (Configurations.Instance.IsGradualPipetting && (i+1) == rawDilutionInfos.Count)//jump the whole columns
                {
                    int regionID = (firstWellID4Parallel + 7) / 8;
                    int lastIDOfRegion = regionID * 8;
                    lastWellIDOccupied = lastIDOfRegion + 8 * (maxParallelCnt - 1);
                    remainingWellIDs.RemoveAll(x => x <= lastWellIDOccupied);
                    maxParallelCnt = 0;
                }
                dilutionInfos.AddRange(tmpDilutionInfos);
            }

            if(Configurations.Instance.IsGradualPipetting) //append normal samples
            {
                rawDilutionInfos.AddRange(normalSamples);
                if (remainingWellIDs.Count / 24 < normalSamples.Count)
                    throw new Exception(string.Format("只剩{0} 个孔位,不够稀释{1}个样品。", remainingWellIDs.Count, normalSamples.Count));

                int normSartWellID = remainingWellIDs.Min();
                foreach(var normalSample in normalSamples)
                {
                    int gradualWellsNeeded = Utility.GetNeededGradualWellsCount(normalSample.dilutionTimes);
                    for (int i = 0; i < gradualWellsNeeded; i++)
                    {
                        for (int parallelOffSet = 0; parallelOffSet < sampleParallelCnt; parallelOffSet++ )
                        {
                            int wellID = normSartWellID + i * sampleParallelCnt + parallelOffSet;
                            dilutionInfos.Add(new DilutionInfo(normalSample.type,
                                normalSample.orgVolume,
                                normalSample.dilutionTimes, 
                                normalSample.seqIDinThisType, 
                                wellID, 
                                i + 1));
                        }
                       
                    }
                    normSartWellID += gradualWellsNeeded * sampleParallelCnt;
                }
            }
            dilutionInfos = dilutionInfos.OrderBy(x => x.destWellID).ToList();
            return dilutionInfos;
        }

        private void CheckDuplicated(List<DilutionInfo> rawDilutionInfos)
        {
            List<string> analysisNos = rawDilutionInfos.Select(x => x.analysisNo).ToList();
            CheckDuplicated(analysisNos,"分析号");
            var stdSeqIDs = rawDilutionInfos.Where(x => x.type == SampleType.STD).Select(x => x.seqIDinThisType.ToString()).ToList();
            var sampleSeqIDs = rawDilutionInfos.Where(x => x.type == SampleType.Norm).Select(x => x.seqIDinThisType.ToString()).ToList();
            CheckDuplicated(stdSeqIDs, "STD序号");
            CheckDuplicated(sampleSeqIDs, "Sample序号");
        }

        private void CheckDuplicated(List<string> strs,string strName)
        {
            for (int i = 0; i < strs.Count; i++)
            {
                var str = strs[i];
                for (int j = i + 1; j < strs.Count; j++)
                {
                    if (strs[j] == str)
                    {
                        throw new Exception(string.Format("{0}:{1}重复！", strName,str));
                    }
                }
            }
        }

        //private DilutionInfo GetDilutionInfos(string animalNo,int sampleID,int dilutionTimes)
        //{
        //    SampleType sampleType = ParseSampleType(animalNo);
        //    return new DilutionInfo(sampleType, dilutionTimes, sampleID, 0);
        //}

        private bool IsLastWellOfColumn(int i)
        {
            return i % 8 == 0;
        }

        private List<DilutionInfo> GetDilutionInfos(DilutionInfo rawDilutionInfo,
            int stdParallelCnt,
            int sampleParallelCnt,
            List<int> remainingWellIDs,
            ref int parallelCnt,
            ref int firstWellID4Parallel)
        {
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            parallelCnt = stdParallelCnt;
            int dilutionTimes = rawDilutionInfo.dilutionTimes;
            int mrdDilutionTimes = ExcelReader.MRDTimes;

            if (mrdDilutionTimes != 1 && dilutionTimes % mrdDilutionTimes != 0 && !Configurations.Instance.IsGradualPipetting)
            {
                throw new Exception(string.Format("分析号{0}的稀释倍数{1}不能整除mrd倍数{2}",
                    rawDilutionInfo.analysisNo, dilutionTimes, mrdDilutionTimes));
            }

            string animalNo = rawDilutionInfo.analysisNo;
            SampleType sampleType = rawDilutionInfo.type;
            int seqNo = rawDilutionInfo.seqIDinThisType;
            uint orgVolume = rawDilutionInfo.orgVolume;
            if (remainingWellIDs.Count == 0)
                throw new Exception(string.Format("在进行到分析号为：{0}号样本时已经没有足够孔位。",
                    rawDilutionInfo.analysisNo));
            firstWellID4Parallel = remainingWellIDs.Min();
            if (sampleType == SampleType.Norm)
                parallelCnt = sampleParallelCnt;

            for(int i = 0; i< parallelCnt; i++)
            {
                int wellID = firstWellID4Parallel + i * 8;
                if (!remainingWellIDs.Contains(wellID))
                    throw new Exception(string.Format("样品：{0}需要孔位：{1}",animalNo,wellID));
                remainingWellIDs.Remove(wellID);
                DilutionInfo dilutionInfo = new DilutionInfo(sampleType,orgVolume,
                    dilutionTimes, 
                    seqNo, wellID, 1, animalNo);
                dilutionInfos.Add(dilutionInfo);
            }
            return dilutionInfos;
        }

        private int ParseSeqNo(string wellInfo)
        {
            if(wellInfo.Contains("Matrix"))
                return 0;
            List<string> keywords = new List<string>() { "STD", "MQC", "LQC","HQC","Test" };
            foreach (string s in keywords)
                wellInfo = wellInfo.Replace(s, "");
            return int.Parse(wellInfo);
        }

        private SampleType ParseSampleType(string sampleDescription)
        {
            if (sampleDescription.Contains("STD"))
                return SampleType.STD;
            else if (sampleDescription.Contains("HQC"))
                return SampleType.HQC;
            else if (sampleDescription.Contains("MQC"))
                return SampleType.MQC;
            else if (sampleDescription.Contains("LQC"))
                return SampleType.LQC;
            else if( sampleDescription.Contains("Matrix"))
            {
                return SampleType.MatrixBlank;
            }
            else
            {
                return SampleType.Norm;
            }
        }

        private List<string> GetPlateLineInfo(string line)
        {
            List<string> strs = line.Split(';').ToList();
            strs =  strs.Skip(startColNo - 1).ToList();
            return strs.Take(colCnt).ToList();
        }

        private NormalSampleInfo GetSampleInfo(string line)
        {
 	        string[] strs = line.Split(';');
            int animalNo = int.Parse(strs[0]);
            int sampleID = int.Parse(strs[1]);
            int dilutionTimes = int.Parse(strs[2]);
            return new NormalSampleInfo(animalNo, sampleID, dilutionTimes);
        }

        private string SaveAsCSV(string sheetPath)
        {
            Application app = new Application();
            app.Visible = false;
            app.DisplayAlerts = false;
            
            string sWithoutSuffix = "";
            int pos = sheetPath.IndexOf(".xls");
            if (pos == -1)
                throw new Exception("Cannot find xls in file name!");
            sWithoutSuffix = sheetPath.Substring(0, pos);
            string sCSVFile = sWithoutSuffix + ".csv";
            if (File.Exists(sCSVFile))
                File.Delete(sCSVFile);
            sCSVFile = sCSVFile.Replace("\\\\", "\\");
            Workbook wbWorkbook = app.Workbooks.Open(sheetPath);
            wbWorkbook.SaveAs(sCSVFile, XlFileFormat.xlCSV);
            wbWorkbook.Close();
            app.Quit();
            return sCSVFile;
        }
    }

    public struct NormalSampleInfo
    {
        public int animalNo;
        public int ID;
        public int dilutionTimes;
        public NormalSampleInfo(int animalNo, int ID, int dilutionTimes)
        {
            this.animalNo = animalNo;
            this.ID = ID;
            this.dilutionTimes = dilutionTimes;
        }
    }

    public struct DilutionInfo
    {
        public SampleType type;
        public int dilutionTimes;
        
        public int seqIDinThisType;
        public int destWellID;
        public string analysisNo;
        public int gradualStep;
        public uint orgVolume;
        public DilutionInfo(SampleType type,uint orgVolume, int dilutionTimes, int seqNo,
            int destWellID, int gradualStep = 1,string analysisNo = "")
        {
            this.type = type;
            this.dilutionTimes = dilutionTimes;
            this.destWellID = destWellID;
            seqIDinThisType = seqNo;
            this.analysisNo = analysisNo;
            this.gradualStep = gradualStep;
            this.orgVolume = orgVolume;
        }
    }

    public enum SampleType
    {
        STD,
        MatrixBlank,
        HQC,
        MQC,
        LQC,
        Norm,
        Empty
        
    }

}
