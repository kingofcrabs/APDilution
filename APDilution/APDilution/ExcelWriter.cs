using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Office.Interop.Excel;
using System;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;


namespace APDilution
{
    class ExcelWriter
    {
        List<string> names = new List<string>();
        List<List<PipettingInfo>> pipettingInfosList = new List<List<PipettingInfo>>();
        public void PrepareSave2File(string name,List<PipettingInfo> pipettingInfos)
        {
            names.Add(name);
            pipettingInfosList.Add(pipettingInfos);
        }

        public static short Excel2Pdf(string orgPath, string pdfPath)
        {
            short convertExcel2PdfResult = -1;

            // Create COM Objects
            Microsoft.Office.Interop.Excel.Application excelApplication = null;
            Microsoft.Office.Interop.Excel.Workbook excelWorkbook = null;
            object unknownType = Type.Missing;
            // Create new instance of Excel
            try
            {
                //open excel application
                excelApplication = new Microsoft.Office.Interop.Excel.Application
                {
                    ScreenUpdating = false,
                    DisplayAlerts = false
                };

                //open excel sheet
                if (excelApplication != null)
                    excelWorkbook = excelApplication.Workbooks.Open(orgPath, unknownType, unknownType,
                                                                    unknownType, unknownType, unknownType,
                                                                    unknownType, unknownType, unknownType,
                                                                    unknownType, unknownType, unknownType,
                                                                    unknownType, unknownType, unknownType);
                if (excelWorkbook != null)
                {


                    // Call Excel's native export function (valid in Office 2007 and Office 2010, AFAIK)
                    excelWorkbook.ExportAsFixedFormat(Microsoft.Office.Interop.Excel.XlFixedFormatType.xlTypePDF,
                                                      pdfPath,
                                                      unknownType, unknownType, unknownType, unknownType, unknownType,
                                                      unknownType, unknownType);

                    convertExcel2PdfResult = 0;

                }
                else
                {
                    Console.WriteLine("Error occured for conversion of office excel to PDF ");
                    convertExcel2PdfResult = 504;
                }

            }
            catch (Exception exExcel2Pdf)
            {
                Console.WriteLine("Error occured for conversion of office excel to PDF, Exception: ", exExcel2Pdf);
                convertExcel2PdfResult = 504;
            }
            finally
            {
                // Close the workbook, quit the Excel, and clean up regardless of the results...

                if (excelWorkbook != null)
                    excelWorkbook.Close(unknownType, unknownType, unknownType);
                if (excelApplication != null) 
                    excelApplication.Quit();
            }
            return convertExcel2PdfResult;
        }

        public void Save()
        {
            Application app = new Application();
            app.Visible = false;
            app.DisplayAlerts = false;
            Workbook wb = app.Workbooks.Add();
            try
            {
                for (int i = 0; i < names.Count; i++ )
                {
                    Worksheet ws = null;
                    if(i >= wb.Worksheets.Count)
                        ws = (Worksheet)wb.Worksheets.Add();
                    else
                        ws = (Worksheet)wb.Worksheets[i+1];
                    ws.get_Range("A1").Value2 = names[i];
                    FillHeader(ws);
                    foreach (var pipettingInfo in pipettingInfosList[i])
                    {
                        double times = pipettingInfo.dilutionTimes;
                        Range rng = GetRange(ws, pipettingInfo.dstWellID);
                        rng.Interior.Color = GetColor(pipettingInfo.type);
                        rng.Value2 = string.Format("{0}", pipettingInfo.dilutionTimes);
                    }
                }
                wb.SaveAs(Utility.GetOutputFolder() +"DilutionInfo.xlsx");
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
