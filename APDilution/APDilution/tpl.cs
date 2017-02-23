using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace APDilution
{
    class TPLFile
    {
        static public void Generate(string sFile, List<DilutionInfo> dilutionInfos)
        {
            List<string> strs = new List<string>();
            //H;31-03-16;15:20:50
            string sDate = DateTime.Now.ToString("dd-MM-yy");
            string sTime = DateTime.Now.ToString("hh:mm:ss");
            string sHeader = string.Format("H;{0};{1};", sDate, sTime);
            strs.Add(sHeader);
            //D; ; T01; A1; 100000;  
            foreach(var dilutionInfo in dilutionInfos)
            {
                string sType = dilutionInfo.type.ToString();
                string desc = string.Format("{0}{1:D2}", sType, dilutionInfo.seqIDinThisType);
                strs.Add(string.Format("D;;{0};{1};{2};", desc, dilutionInfo.destWellID, dilutionInfo.dilutionTimes));
            }
            strs.Add("L;");
            File.WriteAllLines(sFile, strs);
        }
    }
}
