using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace APDilution
{
    class Helper
    {
        static public string GetExeParentFolder()
        {
            string s = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            int index = s.LastIndexOf("\\");
            return s.Substring(0, index) + "\\";
        }

        static public string GetExistBarcodeFile()
        {
            return GetExeFolder() + "existbarcodes.txt";
        }

        static public string GetExeFolder()
        {
            return System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
        }

        static public List<string> GetExistBarcodes()
        {
            string file = GetExistBarcodeFile();
            if (!File.Exists(file))
                return new List<string>();
            return File.ReadAllLines(file).ToList();
        }


        static public void AddBarcodes2ExistBarcodeFile(string barcode)
        {
            var file = GetExistBarcodeFile();
            File.AppendAllLines(file, new List<string>(){barcode});
        }

        public static string GetOutputFolder()
        {
            string sOutputFolder = GetExeParentFolder() + "output\\";

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

        internal static string GetConfigFolder()
        {
            return GetExeParentFolder() + "config\\";
        }
    }
}
