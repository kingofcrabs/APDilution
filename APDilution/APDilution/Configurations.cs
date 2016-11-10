using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace APDilution
{
    class Configurations
    {
        static Configurations instance;
        static public Configurations Instance
        {
            get
            {
                if (instance == null)
                    instance = new Configurations();
                return instance;
            }
        }

        private Configurations()
        {
            DilutionVolume = int.Parse(ConfigurationManager.AppSettings["DilutionVolume"]);
            ReactionVolume = int.Parse(ConfigurationManager.AppSettings["ReactionVolume"]);
            BufferLiquidClass = ConfigurationManager.AppSettings["BufferLiquidClass"];
            SampleLiquidClass = ConfigurationManager.AppSettings["SampleLiquidClass"];
            TransferLiquidClass = ConfigurationManager.AppSettings["TransferLiquidClass"];
            GradualTimes = int.Parse(ConfigurationManager.AppSettings["GradualTimes"]);
        }

        public string GetExeFolder()
        {
            string s = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return s + "\\";
        }

        public string GetExeParentFolder()
        {
            string s = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            int index = s.LastIndexOf("\\");
            return s.Substring(0, index) + "\\";
        }

        public  string GetOutputFolder()
        {
            string sOutputFolder = GetExeParentFolder() + "Output\\";
            if (!Directory.Exists(sOutputFolder))
            {
                Directory.CreateDirectory(sOutputFolder);
            }
            return sOutputFolder;
        }

        public void WriteResult(bool bSuccess, string errMsg)
        {

            string[] strs = new string[1];
            strs[0] = bSuccess.ToString();
            System.IO.File.WriteAllLines(GetOutputFolder() + "result.txt", strs);
            strs[0] = errMsg;
            System.IO.File.WriteAllLines(GetOutputFolder() + "errMsg.txt", strs);
        }


        public int GradualTimes { get; set; }
        
        public string SampleLabware
        {
            get
            {
                return "Sample";
            }
        }
        public string WorkingFolder { get; set; }

        public string ReactionPlateName
        {
            get
            {
                return "Reaction";
            }
        }
        public int DilutionVolume { get; set; }
        public int ReactionVolume { get; set; }

        public string BufferLiquidClass { get; set; }

        public string SampleLiquidClass { get; set; }

        public string TransferLiquidClass { get; set; }
    }
}
