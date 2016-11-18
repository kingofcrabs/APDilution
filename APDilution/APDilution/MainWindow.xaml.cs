using System;
using System.Collections.Generic;
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
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                GenerateWorklist();
            }
            catch(Exception ex)
            {
                txtInfo.Text = "Error happened in generating worklist." + ex.Message;
            }
            //worklist wklist = new worklist();
            //wklist.DoJob(dilutionInfos);
        }

        private void GenerateWorklist()
        {
            ExcelReader excelReader = new ExcelReader();
            var dilutionInfos = excelReader.Read(@"D:\Projects\APDilution\test.xlsx");
            plateViewer = new PlateViewer(new Size(900, 600), new Size(30, 40));
            plateViewer.SetDilutionInfos(dilutionInfos);
            canvas.Children.Add(plateViewer);
        }
    }
}
