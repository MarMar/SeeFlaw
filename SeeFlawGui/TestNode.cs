using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.ComponentModel;
using System.Threading;
using SeeFlawRunner;

namespace SeeFlawGui
{
    public static class ProgressEvent { public static int LogUpdated = 99; }

    public static class NodeColors
    {
        public static int Default = 0;
        public static int Passed = 2;
        public static int Failed = 4;
        //public static int Unknown = 3;

        public static System.Windows.Forms.ImageList GetColorList()
        {
            System.Windows.Forms.ImageList myImageList = new System.Windows.Forms.ImageList();
            myImageList.Images.Add(Properties.Resources.Default.ToBitmap());
            myImageList.Images.Add(Properties.Resources.DefaultSelect.ToBitmap());
            myImageList.Images.Add(Properties.Resources.Passed.ToBitmap());
            myImageList.Images.Add(Properties.Resources.PassedSelect.ToBitmap());
            myImageList.Images.Add(Properties.Resources.Failed.ToBitmap());
            myImageList.Images.Add(Properties.Resources.FailedSelect.ToBitmap());
            return myImageList;
        }
    }

    public class RootNode : System.Windows.Forms.TreeNode
    {
        private string dirPath = "";

        public RootNode(string name) : base(name)
        {
            this.Name = name;
            ShowDefault();
        }

        public RootNode(string name, string dirPath) : base(name)
        {
            this.dirPath = dirPath;
            this.Name = name;
        }

        public virtual int RunTestSuite(Dictionary<string, string> argDic, BackgroundRunner bw)
        {
            int nodeColor = NodeColors.Passed;
            foreach (RootNode childNode in this.Nodes)
            {
                int childColor = childNode.RunTestSuite(argDic, bw);
                if (childColor != NodeColors.Passed)
                {
                    nodeColor = childColor;
                }
                if (bw.CancellationPending)
                {
                    if (Nodes.IndexOf(childNode) == Nodes.Count - 1)
                    {
                        bw.ReportProgress(nodeColor, this);
                        return nodeColor;
                    }
                    return NodeColors.Default;
                }
            }
            bw.ReportProgress(nodeColor, this);
            return nodeColor;
        }

        public virtual void ClearResults()
        {
            ShowDefault();
            foreach (RootNode childNode in this.Nodes)
            {
                childNode.ClearResults();
            }
        }

        public virtual int Count()
        {
            int nrOfTests = 0;
            foreach (RootNode childNode in this.Nodes)
            {
                nrOfTests += childNode.Count();
            }
            return nrOfTests;
        }

        public virtual string GetLog()
        {
            return this.Name + " is not a test node.";
        }
        
        public virtual string GetResult()
        {
            return this.Name + " no test node.";
        }

        public virtual string GetInput()
        {
            return this.Name;
        }

        public virtual string GetTestFile()
        {
            return "";
        }

        public virtual bool CanAddGroup() { return true; }
        public virtual bool CanAddTest() { return false; }
        /*public void ShowOk()
        {
            ShowColor(NodeColors.Ok);
        }
        public void ShowError()
        {
            ShowColor(NodeColors.Error);
        }*/
        public void ShowDefault()
        {
            ShowColor(NodeColors.Default);
        }
        public void ShowColor(int colorIndex)
        {
            this.ImageIndex = colorIndex;
            this.SelectedImageIndex = colorIndex+1;
        }

        public virtual XmlNode ToXml(XmlDocument doc)
        {
            XmlElement element = doc.CreateElement(GetElementName());
            if (dirPath != "")
            {
                element.SetAttribute("dir", dirPath);
                return element;
            }
            element.SetAttribute("name", Name);
            foreach (RootNode childNode in this.Nodes)
            {
                element.AppendChild(childNode.ToXml(doc));
            }
            return element;
        }
        public virtual string GetElementName()
        {
            return "tree";
        }
    }
    
    public class GroupNode : RootNode
    {
        public GroupNode(string name) : base(name)
        {
        }

        public override bool CanAddTest()
        {
            return true;
        }

        public override string GetElementName()
        {
            return "group";
        }
    }

    public class TestNode : RootNode
    {
        public string testFile;
        private string xmlInput = "";
        public string log = "";
        public string htmlOutput = "";

        public TestNode(string name, string file) : base(name)
        {
            this.testFile = file;
            try
            {
                System.IO.TextReader fileReader = new System.IO.StreamReader(file);
                this.xmlInput = fileReader.ReadToEnd();
                fileReader.Close();
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("Could not read from file " + name + " " + e.ToString());
            }
        }

        public override int RunTestSuite(Dictionary<string, string> argDic, BackgroundRunner bw) 
        {
            bool testResult = false;
            this.log = "";
            System.IO.TextWriter standardWr = System.Console.Out;
            System.IO.StringWriter strWr = new System.IO.StringWriter();
            System.Console.SetOut(strWr);
            try
            {
                TestCaller caller = new TestCaller(bw);
                testResult = caller.RunTest(this, argDic, strWr);
            }
            catch (Exception ex)
            {
                this.log += ex.ToString();
                this.htmlOutput = "<html><body>";
                this.htmlOutput += ex.Message;
                this.htmlOutput += "</body></html>";
            }
            strWr.Close();
            System.Console.SetOut(standardWr);
            int testColor = NodeColors.Passed;
            if (!testResult)
            {
                testColor = NodeColors.Failed;
            }
            bw.ReportProgress(testColor, this);
            return testColor;
        }

        public override void ClearResults()
        {
            base.ClearResults();
            this.log = "";
            this.htmlOutput = "";
        }

        public override int Count()
        {
            return 1;
        }

        public override string GetLog()
        {
            return this.log;
        }

        public override string GetResult()
        {
            return this.htmlOutput;
        }

        public override string  GetTestFile()
        {
            return this.testFile;
        } 
        
        public override string GetInput()
        {
            return this.xmlInput;
        }

        public override bool CanAddGroup()
        {
            return false;
        }

        public override XmlNode ToXml(XmlDocument doc)
        {
            XmlElement testElement = doc.CreateElement("test");
            testElement.SetAttribute("name", Name);
            testElement.SetAttribute("file", this.testFile);
            return testElement;
        }
    }

    public class TestCaller
    {
        private SeeFlawRunner.SeeFlaw seeFlaw;
        private bool isFinished = false;
        private bool testSuccessful = false;
        private string error = "";
        private BackgroundRunner bgRunner;

        public TestCaller(BackgroundRunner bw)
        {
            bgRunner = bw;
        }

        public bool RunTest(TestNode testNode, Dictionary<string, string> argDic, System.IO.StringWriter strWr)
        {
            XmlDocument testDoc = new XmlDocument();
            XmlDocument resultDoc = new XmlDocument();
            testDoc.Load(testNode.testFile);
            argDic["testfile"] = testNode.testFile;

            SeeFlawRunner.TestFinishedDelegate callBack = new SeeFlawRunner.TestFinishedDelegate(this.TestFinished);

            System.AppDomain SeeFlawRunnerDomain = System.AppDomain.CreateDomain("SeeFlawRunnerDomain");
            System.Runtime.Remoting.ObjectHandle handle = SeeFlawRunnerDomain.CreateInstance("SeeFlawRunner", "SeeFlawRunner.SeeFlaw");
            seeFlaw = (SeeFlawRunner.SeeFlaw) handle.Unwrap();
            seeFlaw.AddDetails(testDoc, null, resultDoc, argDic, callBack, bgRunner);

            Thread testThread = new Thread(new ThreadStart(seeFlaw.RunTestCase));
            testThread.Start();
            while (!isFinished)
            {
                testThread.Join(200);
                if (AddLog(testNode, strWr))
                {
                    bgRunner.ReportProgress(ProgressEvent.LogUpdated, testNode);
                }
            }
            System.AppDomain.Unload(SeeFlawRunnerDomain);
            if (!testSuccessful && error != "")
            {
                XmlElement htmlElement = resultDoc.CreateElement("html");
                XmlElement bodyElement = resultDoc.CreateElement("body");
                string[] errorLines = error.Split(new char[] { '\n' });
                for (int line = 0; line < errorLines.Length; line++)
                {
                    bodyElement.AppendChild(resultDoc.CreateTextNode(errorLines[line]));
                    bodyElement.AppendChild(resultDoc.CreateElement("br"));
                }
                htmlElement.AppendChild(bodyElement);
                resultDoc.AppendChild(htmlElement);
            }
            testNode.htmlOutput = resultDoc.OuterXml;
            AddLog(testNode, strWr);
            return testSuccessful;
        }

        public bool AddLog(TestNode testNode, System.IO.StringWriter strWr)
        {
            string log = strWr.GetStringBuilder().ToString();
            if (log.Length > 0)
            {
                testNode.log += log;
                strWr.GetStringBuilder().Remove(0, log.Length);
                return true;
            }
            return false;
        }

        public void TestFinished(bool successful, string errorMessage)
        {
            testSuccessful = successful;
            error = errorMessage;
            isFinished = true;
        }
    }
}
