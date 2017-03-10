using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace InputBarcodes
{
    class FolderHelper
    {

        static public string GetExeFolder()
        {
            return System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        }

        static public string GetExistBarcodeFile()
        {
            return FolderHelper.GetExeFolder() + "existbarcodes.txt";
        }
        static public List<string> GetExistBarcodes()
        {
            string file = GetExistBarcodeFile();
            if (!File.Exists(file))
                return new List<string>();
            return File.ReadAllLines(file).ToList();
        }


        static public void AddBarcodes2ExistBarcodeFile(List<string> barcodes)
        {
            var file = GetExistBarcodeFile();
            File.AppendAllLines(file, barcodes);
        }
        
        static public string GetExeParentFolder()
        {
            string s = GetExeFolder();
            int index = s.LastIndexOf("\\");
            return s.Substring(0, index) + "\\";
        }

        public static string GetOutputFolder()
        {
            string sOutputFolder = GetExeParentFolder() + "Output\\";

            if (!Directory.Exists(sOutputFolder))
            {
                Directory.CreateDirectory(sOutputFolder);
            }
            return sOutputFolder;
        }

        public static void WriteResult(bool bok)
        {
            string sFile = GetOutputFolder() + "result.txt";
            File.WriteAllText(sFile, bok.ToString());
        }
    }
}
