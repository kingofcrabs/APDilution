using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InputBarcodes
{
    public partial class Form1 : Form
    {
        int batchCnt = 0;
        List<string> barcodes = new List<string>();
        public Form1()
        {
            InitializeComponent();
            FolderHelper.WriteResult(false);
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            string sBatchCnt = txtBatchCnt.Text;
            if(sBatchCnt == "")
            {
                SetErrorInfo("请设置批次数！");
                return;
            }
            
            bool bInt = int.TryParse(sBatchCnt, out batchCnt);
            if (!bInt)
            {
                SetErrorInfo("批次数必须为数字！");
                return;
            }
            if(batchCnt <1 || batchCnt > 16)
            {
                SetErrorInfo("批次数必须在1~16之间！");
                return;
            }
            btnOk.Enabled = true;
            InitDatagridView();
        }

        private void InitDatagridView()
        {
            dataGridView.AllowUserToAddRows = false;
            dataGridView.EnableHeadersVisualStyles = false;
            dataGridView.Columns.Clear();

            //for (int i = 0; i < 2; i++)
            {
                DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
                column.HeaderText = "反应板条码";
                column.HeaderCell.Style.BackColor = Color.LightSeaGreen;
                dataGridView.Columns.Add(column);
                dataGridView.Columns[0].SortMode = DataGridViewColumnSortMode.Programmatic;
            }
            dataGridView.RowHeadersWidth = 80;
            dataGridView.Rows.Add(batchCnt);
            for (int i = 0; i < batchCnt; i++)
            {
                dataGridView.Rows[i].HeaderCell.Value = string.Format("批次{0}", i + 1);
            }
        }


        private void SetErrorInfo(string info)
        {
            txtInfo.Text = info;
            txtInfo.ForeColor = Color.Red;
            txtInfo.BackColor = Color.White;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                CheckBarcodes();
                Save2File();
            } 
            catch(Exception ex)
            {
                SetErrorInfo(ex.Message);
                return;
            }
            FolderHelper.WriteResult(true);
            this.Close();
        }

        private void Save2File()
        {
            string sFolder = FolderHelper.GetOutputFolder();
            if (Directory.Exists(sFolder))
                Directory.CreateDirectory(sFolder);
            string sFile = sFolder + "barcodes.txt";
            File.WriteAllLines(sFile, barcodes);
            FolderHelper.AddBarcodes2ExistBarcodeFile(barcodes);
        }

        private void CheckBarcodes()
        {
            barcodes.Clear();

            var existingBarcodes = FolderHelper.GetExistBarcodes();


            for (int i = 0; i < batchCnt; i++)
            {
                for (int indexInBatch = 0; indexInBatch < 1; indexInBatch++)
                {
                    if(dataGridView.Rows[i].Cells[indexInBatch].Value == null)
                        throw new Exception(string.Format("批次：{0}的第{1}个条码为空！", i + 1, 1+indexInBatch));
                    var tmpStr = dataGridView.Rows[i].Cells[indexInBatch].Value.ToString();
                    if(tmpStr == "")
                        throw new Exception(string.Format("批次：{0}的第{1}个条码为空！",i+1,1+indexInBatch));
                    if(existingBarcodes.Contains(tmpStr))
                    {
                        throw new Exception(string.Format("条码{0}在历史条码中已经存在！", tmpStr));
                    }
                    if (barcodes.Contains(tmpStr))//already exist
                    {
                        int index = barcodes.IndexOf(tmpStr);
                        throw new Exception(string.Format("批次{0}和批次{1}的条码重复！", i+1, index + 1));
                    }
                    barcodes.Add(tmpStr);
                }
            }
        }
    }
}
