using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AxiomProfiler
{
    [TestClass]
    public class DAGViewTest
    {
        static SmallGraphs graphs = new SmallGraphs();
        static AxiomProfiler axiomprofiler = new AxiomProfiler();

        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual("a", graphs.graph1.Id);
        }
    }
}
