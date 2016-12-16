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
        int orgSTDConc = 100;

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
                 throw new Exception("Normal samples' dilution times must > 0!");
            if(dilutionInfos.Exists(x=>x.type == SampleType.MatrixBlank && x.dilutionTimes != 0))
                throw new Exception("MatrixBlank samples' dilution times must be 0!");
            if(dilutionInfos.Exists(x=>x.dilutionTimes > 6250000))
                throw new Exception("Dilution times must be smaller than 6.25M!");
        }

        private List<DilutionInfo> ReadImpl(Application app, string sFilePath, ref List<DilutionInfo> rawDilutionInfos)
        {
            Workbook wb = app.Workbooks.Open(sFilePath);
            Worksheet ws = (Worksheet)wb.Worksheets.get_Item(1);
            int rowsint = ws.UsedRange.Cells.Rows.Count; //total rows
            Range rngNormalSampleInfo = ws.Cells.get_Range("A2", "C" + rowsint);   //item
            Range rngStartConc = ws.Cells.get_Range("H1");
            Range rngSTDParallel = ws.Cells.get_Range("H2");
            Range rngSampleParallel = ws.Cells.get_Range("H3");
            int stdParallelCnt = int.Parse(rngSTDParallel.Value2.ToString());
            int sampleParallelCnt = int.Parse(rngSampleParallel.Value2.ToString());

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
            for (int i = 1; i <= rowsint - 1; i++)
            {
                if (arryItem[i, 1] == null)
                    break;
                string animalNo = arryItem[i, 1].ToString();
                int sampleID = int.Parse(arryItem[i, 2].ToString());
                int dilutionTimes = int.Parse(arryItem[i, 3].ToString());
                rawDilutionInfos.Add(new DilutionInfo(ParseSampleType(animalNo), dilutionTimes, ParseSeqNo(animalNo), 0,1,animalNo));
            }

            List<DilutionInfo> normalSamples = new List<DilutionInfo>();
            if(Configurations.Instance.IsGradualPipetting) //put normal samples to the end
            {
                normalSamples = rawDilutionInfos.Where(x => x.type == SampleType.Norm).ToList();
                if (normalSamples.Count > 2)
                    throw new Exception("Normal samples' count cannot > 2!");
                rawDilutionInfos = rawDilutionInfos.Except(normalSamples).ToList();
            }


            for (int i = 0; i < rawDilutionInfos.Count; i++)
            {
                int parallelCnt = 0;
                int id = i + 1;
                DilutionInfo rawDilutionInfo = rawDilutionInfos[i];
                List<DilutionInfo> tmpDilutionInfos = GetDilutionInfos(rawDilutionInfo,
                    stdParallelCnt, sampleParallelCnt, remainingWellIDs, ref parallelCnt);
                if (parallelCnt > maxParallelCnt)
                    maxParallelCnt = parallelCnt;
                //lastWellIDOccupied = 
                bool isLastWellOfColumn = IsLastWellOfColumn(id);
                if (isLastWellOfColumn)
                {
                    lastWellIDOccupied = id + 8 * (maxParallelCnt - 1);
                    remainingWellIDs.RemoveAll(x => x <= lastWellIDOccupied);
                    maxParallelCnt = 0;
                }

                if(Configurations.Instance.IsGradualPipetting && id == rawDilutionInfos.Count)//jump the whole columns
                {
                    int regionID = (id + 7) / 8;
                    int lastID = regionID * 8;
                    lastWellIDOccupied = regionID * 8 * maxParallelCnt;
                    remainingWellIDs.RemoveAll(x => x <= lastWellIDOccupied);
                    maxParallelCnt = 0;
                }

                dilutionInfos.AddRange(tmpDilutionInfos);
            }

            if(Configurations.Instance.IsGradualPipetting) //append normal samples
            {
                rawDilutionInfos.AddRange(normalSamples);
                if (remainingWellIDs.Count / 24 < normalSamples.Count)
                    throw new Exception(string.Format("There are {0} wells remaining, not enough for {1} samples.", remainingWellIDs.Count, normalSamples.Count));

                int wellID = remainingWellIDs.Min();
                foreach(var normalSample in normalSamples)
                {
                    int gradualWellsNeeded = Utility.GetNeededGradualWellsCount(normalSample.dilutionTimes);
                    for (int i = 0; i < gradualWellsNeeded; i++)
                    {
                        int firstWellID = wellID + i * 2;
                        int secondWellID = wellID + i * 2 + 1;
                        dilutionInfos.Add(new DilutionInfo(normalSample.type, normalSample.dilutionTimes, normalSample.seqIDinThisType, firstWellID,i+1));
                        dilutionInfos.Add(new DilutionInfo(normalSample.type, normalSample.dilutionTimes, normalSample.seqIDinThisType, secondWellID, i + 1));
                    }
                    wellID += 24;
                }
            }
            dilutionInfos = dilutionInfos.OrderBy(x => x.destWellID).ToList();
            return dilutionInfos;
        }

        private DilutionInfo GetDilutionInfos(string animalNo,int sampleID,int dilutionTimes)
        {
            SampleType sampleType = ParseSampleType(animalNo);
            return new DilutionInfo(sampleType, dilutionTimes, sampleID, 0);
        }

        private bool IsLastWellOfColumn(int i)
        {
            return i % 8 == 0;
        }

        private List<DilutionInfo> GetDilutionInfos(DilutionInfo rawDilutionInfo,
            int stdParallelCnt,
            int sampleParallelCnt,
            List<int> remainingWellIDs,
            ref int parallelCnt)
        {
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            parallelCnt = sampleParallelCnt;
            double dilutionTimes = rawDilutionInfo.dilutionTimes;
            string animalNo = rawDilutionInfo.animalNo;
            SampleType sampleType = rawDilutionInfo.type;
            int seqNo = rawDilutionInfo.seqIDinThisType;
            int firstWellID = remainingWellIDs.Min();
            
            for(int i = 0; i< parallelCnt; i++)
            {
                int wellID = firstWellID + i * 8;
                if (!remainingWellIDs.Contains(wellID))
                    throw new Exception(string.Format("There is no well: {0} for sample {1}!",wellID,animalNo));
                remainingWellIDs.Remove(wellID);
                DilutionInfo dilutionInfo = new DilutionInfo(sampleType, dilutionTimes, seqNo, wellID, 1, animalNo);
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

        private double ParseDilutionTimes(string wellInfo, Dictionary<int, NormalSampleInfo> sampleID_Info)
        {
            if (wellInfo.Contains("Matrix"))
                return 0;

            if (wellInfo.Contains("\n"))
            {
                string[] strs = wellInfo.Split('\n');
                string content = strs[1];
                content = content.ToLower();
                content = content.Replace("ng/ml","");
                return orgSTDConc/double.Parse(content);
            }
            else
            {
                return int.Parse(wellInfo);
            }
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
        public double dilutionTimes;
        public int seqIDinThisType;
        public int destWellID;
        public string animalNo;
        public int gradualStep;
        public DilutionInfo(SampleType type, double dilutionTimes, int seqNo, int destWellID, int gradualStep = 1,string animalNo = "")
        {
            this.type = type;
            this.dilutionTimes = dilutionTimes;
            this.destWellID = destWellID;
            seqIDinThisType = seqNo;
            this.animalNo = animalNo;
            this.gradualStep = gradualStep;
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
