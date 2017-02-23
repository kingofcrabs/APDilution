﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace APDilution
{

    class Utility
    {
        public static string GetExeFolder()
        {
            string s = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return s + "\\";
        }

        public static string GetExeParentFolder()
        {
            string s = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            int index = s.LastIndexOf("\\");
            return s.Substring(0, index) + "\\";
        }


        public static void BackupFolder(string sourceDirectory, string destDirectory)
        {
            //获取所有文件名称
            string[] fileName = Directory.GetFiles(sourceDirectory);

            foreach (string filePath in fileName)
            {
                //根据每个文件名称生成对应的目标文件名称
                string filePathTemp = destDirectory + "\\" + filePath.Substring(sourceDirectory.Length + 1);

                //若不存在，直接复制文件；若存在，覆盖复制
                if (File.Exists(filePathTemp))
                {
                    File.Copy(filePath, filePathTemp, true);
                }
                else
                {
                    File.Copy(filePath, filePathTemp);
                }
            }
        }    

        public static string GetHistoryFolder()
        {
            string sOutputFolder = GetExeParentFolder() + "History\\";
            if (!Directory.Exists(sOutputFolder))
            {
                Directory.CreateDirectory(sOutputFolder);
            }
            return sOutputFolder;
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
        public static int GetNeededGradualWellsCount(double times)
        {
            int gradualTimes = Configurations.Instance.GradualTimes;
            int wellsNeeded = (int)Math.Ceiling(Math.Log(times, gradualTimes));
            return wellsNeeded;
        }
    }
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

        internal bool isTesting = false;
        internal bool isGradual = false;
        public bool IsGradualPipetting
        {
            get
            {
                if (isTesting)
                    return isGradual;
                var cmdLines = Environment.GetCommandLineArgs();
                return cmdLines.Count() > 2 && cmdLines[2] == "G"; // gradual pipetting
            }
        }

        private Configurations()
        {
            DilutionVolume = int.Parse(ConfigurationManager.AppSettings["DilutionVolume"]);
            BufferLiquidClass = ConfigurationManager.AppSettings["BufferLiquidClass"];
            SampleLiquidClass = ConfigurationManager.AppSettings["SampleLiquidClass"];
            TransferLiquidClass = ConfigurationManager.AppSettings["TransferLiquidClass"];
            GradualTimes = int.Parse(ConfigurationManager.AppSettings["GradualTimes"]);
            WorkingFolder = ConfigurationManager.AppSettings["WorkingFolder"];
            Buffer1LabwareName = "Buffer1";
            Buffer2LabwareName = "Buffer2";
        }

        public void WriteResult(bool bSuccess, string errMsg)
        {
            string[] strs = new string[1];
            strs[0] = bSuccess.ToString();
            System.IO.File.WriteAllLines(Utility.GetOutputFolder() + "result.txt", strs);
            strs[0] = errMsg;
            System.IO.File.WriteAllLines(Utility.GetOutputFolder() + "errMsg.txt", strs);
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

        //public string GradualPlateName { get; set; }

        public string Buffer1LabwareName { get; set; }
        public string Buffer2LabwareName { get; set; }

        //public bool StandardQCSameTrough { get; set; } //whether standard & qc comes from same trough.
    }
}
