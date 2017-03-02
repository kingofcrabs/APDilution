using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APDilution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace APDilution.Tests
{
    [TestClass()]
    public class worklistTests
    {
        [TestMethod()]
        public void GetEachStepVolume()
        {
            worklist worklist = new APDilution.worklist();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            //worklist.GetEachStepVolume(10,20,)
           //private List<int> GetEachStepVolume(int times, uint sampleVol, bool isBuffer,ref  List<int> eachStepTimes)
            //worklist.DoJob(dilutionInfos);
        }

      
    }
}
