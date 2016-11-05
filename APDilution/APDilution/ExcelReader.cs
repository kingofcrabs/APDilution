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
            return dilutionInfos;
        }

        private List<DilutionInfo> ReadImpl(Application app, string sFilePath)
        {
            Workbook wb = app.Workbooks.Open(sFilePath);
            Worksheet ws = (Worksheet)wb.Worksheets.get_Item(1);
            int rowsint = ws.UsedRange.Cells.Rows.Count; //total rows
            Range rngNormalSampleInfo = ws.Cells.get_Range("A2", "C" + rowsint);   //item
            Dictionary<int, NormalSampleInfo> sampleID_Info = new Dictionary<int, NormalSampleInfo>();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();

            object[,] arryItem = (object[,])rngNormalSampleInfo.Value2;
            for (int i = 1; i <= rowsint - 1; i++)
            {
                int animalNo = int.Parse(arryItem[i, 1].ToString());
                int sampleID = int.Parse(arryItem[i, 2].ToString());
                int dilutionTimes = int.Parse(arryItem[i, 3].ToString());
                sampleID_Info.Add(sampleID, new NormalSampleInfo(animalNo, sampleID, dilutionTimes));
            }

            Range rngPlateInfo = ws.Cells.get_Range("I4", "T18");
            object[,] val = (object[,])rngPlateInfo.Value2;
            for (int col = 1; col <= colCnt; col++)
            {
                for (int row = 0; row < rowCnt; row++)
                {
                    int actualRow = row * 2 + 1;
                    if (val[actualRow, col] == null)
                    {
                        dilutionInfos.Add(new DilutionInfo(SampleType.Empty, 0, 0));
                        continue;
                    }
                    string wellInfo = val[actualRow, col].ToString();
                    DilutionInfo newInfo = new DilutionInfo(
                        ParseSampleType(wellInfo),
                        ParseDilutionTimes(wellInfo, sampleID_Info),
                        ParseSeqNo(wellInfo, sampleID_Info));
                    if (newInfo.type == SampleType.Empty)
                        break;
                    dilutionInfos.Add(newInfo);

                }
            }
           
            return dilutionInfos;
        }

        private int ParseSeqNo(string wellInfo, Dictionary<int, NormalSampleInfo> sampleID_Info)
        {
            if(wellInfo.Contains("Matrix"))
                return 0;

            if(wellInfo.Contains("\n"))
            {
                string[] strs = wellInfo.Split('\n');
                List<string> keywords = new List<string>() { "STD", "MQC", "LQC","HQC" };
                string content = strs[0];
                foreach (string s in keywords)
                    content = content.Replace(s,"");
                return int.Parse(content);
            }
            else
            {
                return int.Parse(wellInfo);
            }
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
                return SampleType.Normal;
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
        public DilutionInfo(SampleType type, double dilutionTimes, int seqNo)
        {
            this.type = type;
            this.dilutionTimes = dilutionTimes;
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
        Normal,
        Empty
    }
}
