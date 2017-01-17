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
                string errMsg = "Error happened in generating worklist." + ex.Message;
                txtInfo.Text = errMsg;
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
            string sFile = folder + args[1] + ".xlsx";
            if (!File.Exists(sFile))
                throw new Exception(string.Format("指定的excel文件：{0}不存在！", sFile));
            dilutionInfos = excelReader.Read(sFile, ref rawDilutionInfos);
            txtInfo.Text = "Load excel file successfully.";
            plateViewer = new PlateViewer(new Size(900, 600), new Size(30, 40));
            plateViewer.SetDilutionInfos(dilutionInfos);
            canvas.Children.Add(plateViewer);
            worklist wklist = new worklist();
            var strs = wklist.DoJob(dilutionInfos, rawDilutionInfos,out firstPlateBuffer,out secondPlateBuffer);
            sFile = Utility.GetOutputFolder() + "dilution.gwl";
            File.WriteAllLines(sFile, strs);
            plateViewer.SetBuffer(firstPlateBuffer, secondPlateBuffer);
        }

    
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        void lstPlateName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstPlateName.SelectedIndex == -1)
                return;
            Dictionary<int, Plate2Show> index_Name = new Dictionary<int, Plate2Show>();
            index_Name.Add(0, Plate2Show.dilution);
            index_Name.Add(1, Plate2Show.reaction1);
            index_Name.Add(2, Plate2Show.reaction2);
            plateViewer.SetCurrentPlate(index_Name[lstPlateName.SelectedIndex]);
        }
       
    }
}
