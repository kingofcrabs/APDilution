using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Office.Interop.Excel;
using System;
using System.Drawing;


namespace APDilution
{
    class ExcelWriter
    {
        List<string> files = new List<string>();
        List<List<PipettingInfo>> pipettingInfosList = new List<List<PipettingInfo>>();
        public void PrepareSave2File(string file,List<PipettingInfo> pipettingInfos)
        {
            files.Add(file);
            pipettingInfosList.Add(pipettingInfos);
        }

        public void Save()
        {
            Application app = new Application();
            app.Visible = false;
            app.DisplayAlerts = false;
            Workbook wb = app.Workbooks.Add();
            try
            {
                Worksheet ws = (Worksheet)wb.Worksheets[1];
                ws.get_Range("A1").Value2 = "Dilution Information";
                FillHeader(ws);
                for (int i = 0; i < files.Count; i++ )
                {
                    foreach (var pipettingInfo in pipettingInfosList[i])
                    {
                        double times = pipettingInfo.dilutionTimes;
                        Range rng = GetRange(ws, pipettingInfo.dstWellID);
                        rng.Interior.Color = GetColor(pipettingInfo.type);
                        rng.Value2 = string.Format("{0}", pipettingInfo.dilutionTimes);
                    }
                    wb.SaveAs(files[i]);
                }
            }
            finally
            {
                wb.Close(false);
                app.Quit();
                app = null;
            }
        }


        private Color GetColor(SampleType sampleType)
        {
            Dictionary<SampleType, Color> type_Color = new Dictionary<SampleType, Color>();
            type_Color.Add(SampleType.Norm, Color.Green);
            type_Color.Add(SampleType.STD, Color.LightBlue);
            type_Color.Add(SampleType.MQC, Color.LightPink);
            type_Color.Add(SampleType.LQC, Color.LightPink);
            type_Color.Add(SampleType.HQC, Color.LightPink);
            return type_Color[sampleType];            
        }

        private Range GetRange(Worksheet ws, int wellID)
        {
            int colID = (wellID+7) / 8;
            int rowID = wellID - (colID-1) * 8;
            return ws.get_Range(string.Format("{0}{1}", (char)(colID + 'A'), rowID + 1));
        }

        private void FillHeader(Worksheet ws)
        {
            for (int i = 0; i < 8; i++)
            {
                ws.get_Range(string.Format("A{0}", i + 2)).Value2 = ((char)('A' + i)).ToString();
            }
            for (int i = 0; i < 12; i++)
            {
                ws.get_Range(string.Format("{0}1", (char)(i+1 + 'A'))).Value2 = i+1;
            }
        }
    }
}
