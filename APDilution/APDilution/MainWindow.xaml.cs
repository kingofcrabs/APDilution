using APDilution.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace APDilution
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        PlateViewer plateViewer;
        List<DilutionInfo> dilutionInfos;
        List<PipettingInfo> firstPlateBuffer, secondPlateBuffer;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            Configurations.Instance.WriteResult(false, "Init");
            lstPlateName.SelectedIndex = 0;
            lstPlateName.SelectionChanged += lstPlateName_SelectionChanged;
            
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = string.Format("Dilution {0}",strings.version);
#if DEBUG
            GenerateWorklist();
#else
            try
            {
                GenerateWorklist();
            }

            catch(Exception ex)
            {
                string errMsg = "在生成worklsit时发生错误: " + ex.Message;
                SetErrorInfo(errMsg);
                Configurations.Instance.WriteResult(false, errMsg);
                return;
            }
#endif
            Configurations.Instance.WriteResult(true, "");
        }

        private void GenerateWorklist()
        {
            ExcelReader excelReader = new ExcelReader();
            List<DilutionInfo> rawDilutionInfos = new List<DilutionInfo>();
            var folder = Configurations.Instance.WorkingFolder;
            String commandLineString = System.Environment.CommandLine;
            String[] args = System.Environment.GetCommandLineArgs();
            if (args.Count() == 1)
                throw new Exception("未指定excel文件！");
            if(args.Count() == 3 && args[2] != "G")
            {
                throw new Exception("命令行第二个参数必须为'G'！");
            }
      
            string assayName = "";
            string reactionBarcode = "";
            string fileName = args[1];
            int batchID = 0;
            GetBarcodesAndAssayName(fileName, ref assayName, ref reactionBarcode, ref batchID);
            
            string sFile = folder + args[1] + ".xlsx";
            if (!File.Exists(sFile))
                throw new Exception(string.Format("指定的excel文件：{0}不存在！", sFile));

            dilutionInfos = excelReader.Read(sFile, ref rawDilutionInfos);
            SetInfo(string.Format("加载文件：{0}成功！\r\n专题号：{1}\r\n批次号:{2}\r\n反应板条码{3}", 
                args[1], assayName, batchID,reactionBarcode));
            
            plateViewer = new PlateViewer(new Size(900, 600), new Size(30, 40));
            plateViewer.SetDilutionInfos(dilutionInfos);
            canvas.Children.Add(plateViewer);
            worklist wklist = new worklist();
            List<string> readableWklists;
            var strs = wklist.DoJob(assayName, reactionBarcode,
                dilutionInfos, rawDilutionInfos, 
                out firstPlateBuffer, 
                out secondPlateBuffer, 
                out readableWklists);
            sFile = Utility.GetOutputFolder() + "dilution.gwl";
            string assayFolder = Utility.GetAssayFolder(assayName);
            string tplFile = assayFolder + string.Format("{0}.tpl", batchID);
            TPLFile.Generate(tplFile, dilutionInfos);

            var sReadableFile = assayFolder + string.Format("readable{0}.csv",batchID);
            File.WriteAllLines(sFile, strs);
            File.WriteAllLines(sReadableFile, readableWklists);
            
            plateViewer.SetBuffer(firstPlateBuffer, secondPlateBuffer);
        }

        private void GetBarcodesAndAssayName(string fileName, ref string assayName, ref string reactionBarcode,ref int batchID)
        {
            string sBatchNum = "";
            for (int i = fileName.Length - 1; i >= 0; i--)
            {
                char ch = fileName[i];
                if (Char.IsDigit(ch))
                    sBatchNum += ch;
                else
                    break;
            }
            if (sBatchNum == "")
                throw new Exception("未指定批次号！");

            bool bok = int.TryParse(sBatchNum, out batchID);
            if (!bok)
                throw new Exception("未指定批次号！");

            assayName = fileName.Substring(0, fileName.Length - sBatchNum.Length);
            reactionBarcode = GetBatchBarcode(batchID);
        }

        private string GetBatchBarcode(int batchNum)
        {
            string barcodeFile = Helper.GetOutputFolder() + "barcodes.txt";
            if (!File.Exists(barcodeFile))
                throw new Exception(string.Format("找不到位于：{0}条码文件：", barcodeFile));
            var strs = File.ReadAllLines(barcodeFile);
            if(batchNum > strs.Length)
            {
                throw new Exception("找不到批次号对应的反应板条码！");
            }
            int startIndex = batchNum - 1;
            return strs[startIndex];

        }

       

        private void Save2Image(FrameworkElement element,Size sz,string sFile)
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)sz.Width, (int)sz.Height,
                                                                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);

            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (Stream s = File.OpenWrite(sFile))
            {
                encoder.Save(s);
            }
        }
        
        private void Backup()
        {
            string historyFolder = Utility.GetHistoryFolder() + DateTime.Now.ToString("yyyyMMdd") + "\\";
            if (!Directory.Exists(historyFolder))
                Directory.CreateDirectory(historyFolder);
            string currentFolder = Utility.GetOutputFolder();
            Utility.BackupFolder(currentFolder, historyFolder);
        }
     

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Size sz = new Size(canvas.ActualWidth, canvas.ActualHeight);
                plateViewer.SetCurrentPlate(Plate2Show.dilution);
                Save2Image(plateViewer, sz, Utility.GetOutputFolder() + "dilution.png");
                plateViewer.SetCurrentPlate(Plate2Show.reaction1);
                Save2Image(plateViewer, sz, Utility.GetOutputFolder() + "reaction1.png");
                plateViewer.SetCurrentPlate(Plate2Show.reaction2);
                Save2Image(plateViewer, sz, Utility.GetOutputFolder() + "reaction2.png");
                Backup(); 
            }
            catch(Exception ex)
            {
                SetErrorInfo(ex.Message);
            }
            
            this.Close();
        }

        private void SetErrorInfo(string errMsg)
        {
            txtInfo.Text = errMsg;
            txtInfo.Foreground = Brushes.Red;
        }

        private void SetInfo(string msg)
        {
            txtInfo.Text = msg;
            txtInfo.Foreground = Brushes.Black;
        }

        void lstPlateName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstPlateName.SelectedIndex == -1)
                return;
            SwitchView(lstPlateName.SelectedIndex);
        }

        private void SwitchView(int index)
        {
            Dictionary<int, Plate2Show> index_Name = new Dictionary<int, Plate2Show>();
            index_Name.Add(0, Plate2Show.dilution);
            index_Name.Add(1, Plate2Show.reaction1);
            index_Name.Add(2, Plate2Show.reaction2);
            plateViewer.SetCurrentPlate(index_Name[index]);
        }
       
    }
}
