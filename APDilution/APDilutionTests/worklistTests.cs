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
        public void DoJobTest()
        {
            //Assert.Fail();
        }

        [TestMethod()]
        public void GenerateBufferPipettingInfosTest()
        {
            worklist worklist = new APDilution.worklist();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            dilutionInfos.Add(new DilutionInfo(SampleType.STD,27000,1));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD,400,2));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD,25,3));
            var pipettingInfoLists = worklist.GenerateBufferPipettingInfos(dilutionInfos);
            //Assert.Fail();
        }

        [TestMethod()]
        public void GenerateSamplePipettingInfosTest()
        {
            worklist worklist = new APDilution.worklist();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 100, 1));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 80, 2));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 40, 3));
            worklist.GenerateSamplePipettingInfos(dilutionInfos);
            //Assert.Fail();
        }

        [TestMethod()]
        public void GenerateSamplePipettingInfosTest2()
        {
            worklist worklist = new APDilution.worklist();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 125000, 1));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 65000, 2));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 2000, 3));
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 40, 4));
            var pipettingInfos = worklist.GenerateSamplePipettingInfos(dilutionInfos);
            var srcWellIDs = pipettingInfos.Select(x => x.srcWellID).ToList();
            //Assert.Fail();
            List<int> expectedWellIDs = new List<int> {1,2,3,4,1,9,17,2,10,3 };
            Assert.AreEqual(srcWellIDs.Count,expectedWellIDs.Count);
            for(int i = 0; i<srcWellIDs.Count; i++)
            {
                Assert.AreEqual(srcWellIDs[i], expectedWellIDs[i]);
            }
            //pipetting2 
            Assert.AreEqual(pipettingInfos[1].vol, 8.0);
            Assert.AreEqual(pipettingInfos[1].srcLabware, "Sample");
            Assert.AreEqual(pipettingInfos[1].dstWellID, 2);
            Assert.AreEqual(pipettingInfos[1].srcWellID, 2);

            //pipetting9 
            Assert.AreEqual(pipettingInfos[9].vol, 7.0);
            Assert.AreEqual(pipettingInfos[9].srcLabware, "Dilution1");
            Assert.AreEqual(pipettingInfos[9].dstWellID, 11);
            Assert.AreEqual(pipettingInfos[9].srcWellID, 3);
            
        }

        [TestMethod()]
        public void GenerateSamplePipettingInfosTest3()
        {
            worklist worklist = new APDilution.worklist();
            List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
            dilutionInfos.Add(new DilutionInfo(SampleType.STD, 125000, 1));
            dilutionInfos.Add(new DilutionInfo(SampleType.Normal, 65000, 2));
            var pipettingInfos = worklist.GenerateSamplePipettingInfos(dilutionInfos);
            var srcWellIDs = pipettingInfos.Select(x => x.srcWellID).ToList();
            //Assert.Fail();
            //List<int> expectedWellIDs = new List<int> { 1, 2, 3, 4, 1, 9, 17, 2, 10, 3 };
            //Assert.AreEqual(srcWellIDs.Count, expectedWellIDs.Count);
            //for (int i = 0; i < srcWellIDs.Count; i++)
            //{
            //    Assert.AreEqual(srcWellIDs[i], expectedWellIDs[i]);
            //}
            ////pipetting2 
            //Assert.AreEqual(pipettingInfos[1].vol, 8.0);
            //Assert.AreEqual(pipettingInfos[1].srcLabware, "Sample");
            //Assert.AreEqual(pipettingInfos[1].dstWellID, 2);
            //Assert.AreEqual(pipettingInfos[1].srcWellID, 2);

            ////pipetting9 
            //Assert.AreEqual(pipettingInfos[9].vol, 7.0);
            //Assert.AreEqual(pipettingInfos[9].srcLabware, "Dilution1");
            //Assert.AreEqual(pipettingInfos[9].dstWellID, 11);
            //Assert.AreEqual(pipettingInfos[9].srcWellID, 3);

        }
    }
}
