using APDilution.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        int transferVolume = 0;
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
            //int batchID = 1;
            string assaName = "";
            try
            {
                ParseCmdLine(ref assaName, ref transferVolume);
                CheckAssayName(assaName);
                Configurations.Instance.AssayName = assaName;
            }
            catch (Exception ex)
            {
                string errMsg = "在解析命令行发生错误: " + ex.Message;
                SetErrorInfo(errMsg);
                Configurations.Instance.WriteResult(false, errMsg);
                return;
            }
        }

        private void CheckAssayName(string assayName)
        {
            string sFolder = Helper.GetConfigFolder();
            List<string> files = Directory.EnumerateFiles(sFolder).ToList();
            if (files.Count == 0)
                throw new Exception("没有试剂定义！");
            if (!files.Exists(x=>x.Contains(assayName)))
                throw new Exception(string.Format("没有找到方法名为:{0}的试剂定义文件！", assayName));
        }

        private void ParseCmdLine(ref string assayName, ref int transferVolume)
        {
            this.Title = string.Format("Dilution {0}", strings.version);

            String[] args = System.Environment.GetCommandLineArgs();
            if (args.Count() < 3)
                throw new Exception("未指定方法名！");

            assayName = args[1];
            transferVolume = int.Parse(args[2]);
            
        }

        private void GenerateWorklist(string fileName, string assayName, string barcode)
        {
#if DEBUG
            GenerateWorklistImpl(fileName, assayName, barcode, transferVolume);
#else
            try
            {
                GenerateWorklistImpl(fileName, assayName, barcode,transferVolume);
            }

            catch (Exception ex)
            {
                string errMsg = "在生成worklsit时发生错误: " + ex.Message;
                SetErrorInfo(errMsg);
                btnOk.IsEnabled = false;
                Configurations.Instance.WriteResult(false, errMsg);
                return;
            }
#endif
            Configurations.Instance.WriteResult(true, "");
        }
     
        private void btnSetBarcode_Click(object sender, RoutedEventArgs e)
        {
            btnOk.IsEnabled = false;
            try
            {
                if(txtBarcode.Text == "")
                {
                    throw new Exception("反应板条码不得为空！");
                    
                }

                string sBarcode = txtBarcode.Text;
                Configurations.Instance.ReactionBarcode = sBarcode;
                var existBarcodes = Helper.GetExistBarcodes();
                if(existBarcodes.Contains(sBarcode))
                {
                    throw new Exception("条码已经存在！");
                }

                string sFile = Configurations.Instance.WorkingFolder + sBarcode + ".xlsx";
                if(!File.Exists(sFile))
                {
                    throw new Exception("无法找到条码对应的文件！");
                    
                }

                GenerateWorklistImpl(sFile, Configurations.Instance.AssayName, sBarcode, transferVolume);
                SetInfo(string.Format("加载文件：{0}成功！\r\n方法名:{1}\r\n反应板条码:{2}",
           sFile, Configurations.Instance.AssayName, sBarcode));
            
            }
            catch(Exception ex)
            {
                SetErrorInfo(ex.Message);
                return;
            }
            btnOk.IsEnabled = true;

        }

        private void GenerateWorklistImpl(string fileName, string assayName, string reactionBarcode, int transferVolume)
        {
            ExcelReader excelReader = new ExcelReader();
            List<DilutionInfo> rawDilutionInfos = new List<DilutionInfo>();
            var folder = Configurations.Instance.WorkingFolder;
            String commandLineString = System.Environment.CommandLine;

            if (!File.Exists(fileName))
                throw new Exception(string.Format("指定的excel文件：{0}不存在！", fileName));

            dilutionInfos = excelReader.Read(fileName, ref rawDilutionInfos);
            SetInfo(string.Format("加载文件：{0}成功！\r\n方法名:{1}\r\n反应板条码:{2}",
                fileName, assayName, reactionBarcode));
            
            plateViewer = new PlateViewer(new Size(900, 600), new Size(30, 40));
            plateViewer.SetDilutionInfos(dilutionInfos);
            canvas.Children.Add(plateViewer);
            worklist wklist = new worklist();
            wklist.DoJob(assayName, reactionBarcode,
                dilutionInfos, rawDilutionInfos, 
                out firstPlateBuffer,
                out secondPlateBuffer, transferVolume);

         
            plateViewer.SetBuffer(firstPlateBuffer, secondPlateBuffer);


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
            historyFolder += DateTime.Now.ToString("hhmmss");
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
                plateViewer.SetCurrentPlate(Plate2Show.reaction);
                Save2Image(plateViewer, sz, Utility.GetSubOutputFolder() + "reaction.png");
                plateViewer.SetCurrentPlate(Plate2Show.dilution1);
                Save2Image(plateViewer, sz, Utility.GetSubOutputFolder() + "dilution1.png");
                plateViewer.SetCurrentPlate(Plate2Show.dilution2);
                Save2Image(plateViewer, sz, Utility.GetSubOutputFolder() + "dilution2.png");
                Helper.AddBarcodes2ExistBarcodeFile(txtBarcode.Text);
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
            index_Name.Add(0, Plate2Show.reaction);
            index_Name.Add(1, Plate2Show.dilution1);
            index_Name.Add(2, Plate2Show.dilution2);
            plateViewer.SetCurrentPlate(index_Name[index]);
        }

    

   
       
    }
}
