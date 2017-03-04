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
        public void TestSplitVolume()
        {

            List<PipettingInfo> pipettingInfos = new List<PipettingInfo>();
            pipettingInfos.Add(new PipettingInfo("src", 1, "dst", 1, 250, 1, SampleType.Norm, "1"));
            pipettingInfos.Add(new PipettingInfo("src", 2, "dst", 2, 360, 1, SampleType.Norm, "2"));
            pipettingInfos.Add(new PipettingInfo("src", 3, "dst", 3, 190, 1, SampleType.Norm, "3"));
            pipettingInfos.Add(new PipettingInfo("src", 4, "dst", 6, 120, 1, SampleType.Norm, "4"));
            pipettingInfos.Add(new PipettingInfo("src", 5, "dst", 7, 380, 1, SampleType.Norm, "5"));
            worklist wklist = new worklist();
            pipettingInfos = wklist.SplitByVolume(pipettingInfos);
            Assert.AreEqual(pipettingInfos[1].vol, 70);

        }

      
    }
}
