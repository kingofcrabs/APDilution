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

        public List<DilutionInfo> Read(string sFilePath)
        {
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            Application app = new Application();
            try
            {
                
                app.Visible = false;
                app.DisplayAlerts = false;
                dilutionInfos = ReadImpl(app,sFilePath);
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

        private List<DilutionInfo> ReadImpl(Application app, string sFilePath)
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
            for (int i = 1; i <= rowsint - 1; i++)
            {
                string animalNo = arryItem[i, 1].ToString();
                int sampleID = int.Parse(arryItem[i, 2].ToString());
                int dilutionTimes = int.Parse(arryItem[i, 3].ToString());
                int parallelCnt = 0;
                List<DilutionInfo> tmpDilutionInfos = GetDilutionInfos(animalNo, sampleID, dilutionTimes,
                    stdParallelCnt, sampleParallelCnt, remainingWellIDs, ref parallelCnt);
                if (parallelCnt > maxParallelCnt)
                    maxParallelCnt = parallelCnt;
                //lastWellIDOccupied = 
                bool isLastWellOfColumn = IsLastWellOfColumn(i);
                if (isLastWellOfColumn)
                {
                    lastWellIDOccupied = i + 8 * (maxParallelCnt - 1);
                    remainingWellIDs.RemoveAll(x => x <= lastWellIDOccupied);
                    maxParallelCnt = 0;
                }
                //
                dilutionInfos.AddRange(tmpDilutionInfos);
                //sampleID_Info.Add(sampleID, new NormalSampleInfo(animalNo, sampleID, dilutionTimes));
            }


            //Range rngPlateInfo = ws.Cells.get_Range("I4", "T18");
            //object[,] val = (object[,])rngPlateInfo.Value2;
            //for (int col = 1; col <= colCnt; col++)
            //{
            //    for (int row = 0; row < rowCnt; row++)
            //    {
            //        int actualRow = row * 2 + 1;
            //        if (val[actualRow, col] == null)
            //        {
            //            dilutionInfos.Add(new DilutionInfo(SampleType.Empty, 0, 0));
            //            continue;
            //        }
            //        string wellInfo = val[actualRow, col].ToString();
            //        DilutionInfo newInfo = new DilutionInfo(
            //            ParseSampleType(wellInfo),
            //            ParseDilutionTimes(wellInfo, sampleID_Info),
            //            ParseSeqNo(wellInfo, sampleID_Info));
            //        if (newInfo.type == SampleType.Empty)
            //            break;
            //        dilutionInfos.Add(newInfo);
            //    }
            //}
            dilutionInfos = dilutionInfos.OrderBy(x => x.destWellID).ToList();
            return dilutionInfos;
        }

        private bool IsLastWellOfColumn(int i)
        {
            return i % 8 == 0;
        }

        private List<DilutionInfo> GetDilutionInfos(string animalNo, 
            int sampleID,
            int dilutionTimes,
            int stdParallelCnt,
            int sampleParallelCnt,
            List<int> remainingWellIDs,
            ref int parallelCnt)
        {
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            parallelCnt = sampleParallelCnt;
            if(animalNo.Contains("STD"))
            {
                parallelCnt = stdParallelCnt;
            }
            SampleType sampleType = ParseSampleType(animalNo);
            int seqNo = sampleID;
            int firstWellID = remainingWellIDs.Min();
            for(int i = 0; i< parallelCnt; i++)
            {
                int wellID = firstWellID + i * 8;
                if (!remainingWellIDs.Contains(wellID))
                    throw new Exception(string.Format("There is no well: {0} for sample {1}!",wellID,animalNo));
                remainingWellIDs.Remove(wellID);
                DilutionInfo dilutionInfo = new DilutionInfo(sampleType, dilutionTimes, seqNo,wellID);
                dilutionInfos.Add(dilutionInfo);
                //if( isLastWellOfColumn && i == parallelCnt-1)
                //    remainingWellIDs.RemoveAll(x => x <= wellID);
                //if( )
                //{
                //    
                //}
            }
            return dilutionInfos;
        }

       

        private int ParseSeqNo(string wellInfo)
        {
            if(wellInfo.Contains("Matrix"))
                return 0;
            List<string> keywords = new List<string>() { "STD", "MQC", "LQC","HQC" };
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
        public DilutionInfo(SampleType type, double dilutionTimes, int seqNo, int destWellID)
        {
            this.type = type;
            this.dilutionTimes = dilutionTimes;
            this.destWellID = destWellID;
            seqIDinThisType = seqNo;
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
