using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace APDilution
{
    class TPLFile
    {
        static public List<string> Generate(List<DilutionInfo> parallelDilutionInfos)
        {
            List<string> strs = new List<string>();
            //H;31-03-16;15:20:50
            strs.Add( "H;"+DateTime.Now.ToString("ddMMyy;HHmmss"));
            //D;;T01;A1;100000;
            foreach(var diluteInfo in parallelDilutionInfos)
            {
                strs.Add(string.Format("D;;{0};{1};{2}",diluteInfo.analysisNo,diluteInfo.destWellID,diluteInfo.dilutionTimes));
            }
            strs.Add("L;");
            return strs;
        }
    }
}
