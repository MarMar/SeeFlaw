using System;
using System.Reflection;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace SeeFlawRunner
{
    public delegate void TestFinishedDelegate(bool successful, string errorMessage);

    [Serializable]
    public class SeeFlaw
    {
        private bool runSuccessful = true;
        private bool onlyTest = false;
        private Dictionary<string, XmlNode> loadDic = new Dictionary<string, XmlNode>();
        private XmlDocument testDocument = null;
        private XmlDocument resultDocument = null;
        private XmlDocument htmlResultDocument = null;
        private string testFile;
        private TestFinishedDelegate testFinished = null;
        private BackgroundRunner bgRunner = null;
        private XmlNode testResultSaveInput = null;
        private RunnerDetails sfRunDetails = new RunnerDetails(new Dictionary<string,string>());
        private ErrorParser errParser = new ErrorParser();
            
        public SeeFlaw()
        { }

        public SeeFlaw(bool nunitTest)
        {
            onlyTest = nunitTest;
        }

        public void AddDetails(XmlDocument testDoc,
                        XmlDocument resultDoc,
                        XmlDocument htmlResultDoc,
                        Dictionary<string, string> argsDic,
                        TestFinishedDelegate testFinishedCallback,
                        BackgroundRunner bgWorker)
        {
            testDocument = testDoc;
            resultDocument = resultDoc;
            htmlResultDocument = htmlResultDoc;
            testFile = argsDic["testfile"];
            testFinished = testFinishedCallback;
            bgRunner = bgWorker;
            sfRunDetails = new RunnerDetails(argsDic);
        }

        // For Nunit testing
        public void SetTestTime()
        {
            onlyTest = true;
        }

        // For Nunit testing
        public void AddTestLoadNode(string name, XmlNode loadNode)
        {
            loadDic.Add(name, loadNode);
        }

        public void RunTestCase()
        {
            RunTestCaseSuccessful();
        }

        public bool RunTestCaseSuccessful()
        {
            if (sfRunDetails.HasPreCase())
            {
                runSuccessful = true;
                RunPreTestCase(sfRunDetails.GetPreCase());
                System.Console.WriteLine("");
            }
            XmlNode seeFlawNode = null;
            try
            {
                ValidatedTest validTest = TestSyntax.ValidateDocument(testDocument, testFile, sfRunDetails, testResultSaveInput);
                seeFlawNode = validTest.seeFlawNode;
                loadDic = validTest.loadDic;
            }
            catch (Exception ex)
            {
                if (testFinished != null)
                {
                    testFinished(false, ex.Message);
                    return false;
                }
                throw new Exception(ex.Message);
            }
            if (resultDocument == null)
            {
                resultDocument = new XmlDocument();
            }
            XmlElement resultElement = resultDocument.CreateElement("seeflawresult");
            resultDocument.AppendChild(resultElement);
            AppendHead(resultElement, testFile);
            runSuccessful = true;
            bool successful = RunTest(seeFlawNode, resultElement);
            TransformToHtml(resultElement);

            if (sfRunDetails.HasPostCase())
            {
                runSuccessful = true;
                testResultSaveInput = resultElement;
                System.Console.WriteLine("");
                RunPostTestCase(sfRunDetails.GetPostCase());
            }
            if (testFinished != null)
            {
                testFinished(successful, "");
            }
            return successful;
        }

        public void RunPreTestCase(string preCaseFile)
        {
            RunPrePostTestCase(preCaseFile, "PreCase");
        }

        public void RunPostTestCase(string postCaseFile)
        {
            RunPrePostTestCase(postCaseFile, "PostCase");
        }

        public void RunPrePostTestCase(string filePath, string testCase)
        {
            filePath = filePath.Trim(new char[]{'\"'});
            string errorMessage = "";
            System.Console.WriteLine("----- Start of " + testCase + " -----");
            if (!System.IO.File.Exists(filePath))
            {
                errorMessage = "No such " + testCase + " file " + filePath;
            }
            else
            {
                XmlDocument document = new XmlDocument();
                document.Load(filePath);
                try
                {
                    ValidatedTest validTest = TestSyntax.ValidateDocument(document, filePath,sfRunDetails, testResultSaveInput);
                    XmlNode caseNode = validTest.seeFlawNode;
                    loadDic = validTest.loadDic;
                    XmlDocument resultDoc = new XmlDocument();
                    XmlElement resultElement = resultDoc.CreateElement("caseresult");
                    if (!RunTest(caseNode, resultElement))
                    {
                        errorMessage = testCase + " failed";
                        WritePrePostCaseError(resultElement);
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine(ex.Message);
                }
            }
            if (errorMessage != "")
            {
                System.Console.WriteLine(errorMessage);
            }
            System.Console.WriteLine("----- End of " + testCase + " -----");
        }

        private void WritePrePostCaseError(XmlNode resultNode)
        {
            foreach (XmlNode childNode in resultNode.ChildNodes)
            {
                if (childNode.LocalName.EndsWith("result"))
                {
                    XmlNode statusAttr = childNode.Attributes.GetNamedItem("status");
                    if (statusAttr != null && statusAttr.Value == "error")
                    {
                        foreach (XmlNode innerChildNode in childNode.ChildNodes)
                        {
                            if (innerChildNode.LocalName == "result")
                            {
                                XmlNode innerStatusAttr = innerChildNode.Attributes.GetNamedItem("status");
                                if (innerStatusAttr != null && innerStatusAttr.Value == "error")
                                {
                                    foreach (XmlNode errorNode in innerChildNode.ChildNodes)
                                    {
                                        if (errorNode.LocalName == "error")
                                        {
                                            System.Console.WriteLine(errorNode.InnerText);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void TransformToHtml(XmlNode resultRootNode)
        {
            XmlDocument doc = htmlResultDocument;
            if (doc == null)
            {
                return;
            }
            doc.AppendChild(TransformToHtml(resultRootNode, doc));
        }

        private XmlElement TransformToHtml(XmlNode resultRootNode, XmlDocument doc)
        {
            //"<!DOCTYPE html PUBLIC ' -//W3C//DTD XHTML 1.0 Transitional//EN' 'http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd'>"
            XmlElement htmlElement = doc.CreateElement("html");
            htmlElement.SetAttribute("xmlns", "http://www.w3.org/1999/xhtml");
            
            AppendHeadStyle(htmlElement);
            XmlElement bodyElement = doc.CreateElement("body");
            htmlElement.AppendChild(bodyElement);
            foreach (XmlNode resultNode in resultRootNode.ChildNodes)
            {
                if (resultNode.Name == "head")
                {
                    AppendHeadings(bodyElement, resultNode);
                }
                else
                {
                    XmlNode resultTable = HtmlTransformer.ConvertToHtml(doc, resultNode);
                    bodyElement.AppendChild(resultTable);
                    bodyElement.AppendChild(doc.CreateElement("br"));
                }
            }
            HtmlTransformer.AppendHtmlErrors(bodyElement, resultRootNode);
            return htmlElement;
        }

        public void AppendHeadStyle(XmlElement htmlElement)
        {
            XmlElement head = htmlElement.OwnerDocument.CreateElement("head");
            XmlElement style = htmlElement.OwnerDocument.CreateElement("style");
            style.SetAttribute("media", "all");
            style.SetAttribute("type", "text/css");
            style.InnerText = "@import url(seeflaw.css);";
            head.AppendChild(style);
            htmlElement.AppendChild(head);
        }

        public static class ActionType
        {
            public const string TEXT = "text";
            public const string PARAM = "param";
            public const string INIT = "init";
            public const string LOAD = "load";
            public const string CALL = "call";
            public const string ASYNC = "async";
            public const string SAVE = "save";
        }

        protected class ThreadRunnerInfo
        {
            public Thread thread;
            public Runner runner;
            public string fixtureId;

            public ThreadRunnerInfo(Thread t, Runner r, string id)
            {
                thread = t;
                runner = r;
                fixtureId = id;
            }
        }

        public static XmlAttribute GetAttributeByName(XmlNode node, string name)
        {
            return GetAttributeByName(node, name, false);
        }

        public static XmlAttribute GetAttributeByName(XmlNode node, string name, bool allowNull)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                if (attr.LocalName == name)
                {
                    return attr;
                }
            }
            if (allowNull)
            {
                return null;
            }
            throw new Exception(name + " attribute missing in " + node.Name + " node.");
        }

        public XmlNode RunTest(System.Xml.XmlNode seeflawNode)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement bodyElement = doc.CreateElement("body");
            RunTest(seeflawNode, bodyElement);
            return bodyElement;
        }

        public bool RunTest(System.Xml.XmlNode seeflawNode, XmlElement bodyElement)
        {
            RunnerDetails runDetails = sfRunDetails.Copy();
            return RunTest(seeflawNode, bodyElement, runDetails);
        }

        public bool RunTest(System.Xml.XmlNode seeflawNode, XmlElement bodyElement, RunnerDetails runDetails)
        {
            XmlDocument doc = bodyElement.OwnerDocument;
            ResultParser parser = new ResultParser(doc, errParser, onlyTest);
            Runner defaultRunner = new Runner(onlyTest, runDetails);

            for (int i = 0; i < seeflawNode.ChildNodes.Count; i++)
            {
                XmlNode childNode = seeflawNode.ChildNodes[i];
                switch (childNode.LocalName)
                {
                    case ActionType.TEXT:
                        // Gather all text nodes that follows each other
                        List<XmlNode> textNodes = GetSelectedNodes(seeflawNode.ChildNodes, ActionType.TEXT, i);
                        XmlElement textElement = bodyElement.OwnerDocument.CreateElement("text");
                        bodyElement.AppendChild(textElement);
                        foreach (XmlNode rowNode in textNodes)
                        {
                            XmlElement rowElement = bodyElement.OwnerDocument.CreateElement("row");
                            rowElement.InnerText = rowNode.InnerText;
                            textElement.AppendChild(rowElement);
                        }
                        i += textNodes.Count - 1;
                        break;
                    case ActionType.PARAM:
                        // Gather all param nodes that follows each other
                        List<XmlNode> paramNodes = GetParamNodes(seeflawNode.ChildNodes, i);
                        if (paramNodes.Count > 0)
                        {
                            ResultHolder paramData = defaultRunner.RunFixtureParam(paramNodes);
                            AppendResultTable(bodyElement, paramData, parser);
                            i += paramNodes.Count - 1;
                        }
                        else
                        {
                            // This is a fixture param node
                            Runner paramRunner = new Runner(childNode, runDetails);
                            Thread paramRunnerThread = new Thread(new ThreadStart(paramRunner.ThreadRun));
                            paramRunnerThread.Start();
                            ResultHolder paramAbortData = HandleRunnerThread(paramRunnerThread, paramRunner);
                            ResultHolder paramCallData = paramRunner.GetThreadRunResult();
                            AppendResultTable(bodyElement, paramCallData, parser);
                            AppendResultTable(bodyElement, paramAbortData, parser);
                        }
                        break;
                    case ActionType.INIT:
                        // Gather all init nodes that follows each other
                        List<XmlNode> initNodes = GetSelectedNodes(seeflawNode.ChildNodes, ActionType.INIT, i);
                        ResultHolder initData = defaultRunner.RunFixtureInit(initNodes);
                        AppendResultTable(bodyElement, initData, parser);
                        i += initNodes.Count - 1;
                        break;
                    case ActionType.CALL:
                        Runner runner = new Runner(childNode, runDetails);
                        Thread runnerThread = new Thread(new ThreadStart(runner.ThreadRun));
                        runnerThread.Start();
                        ResultHolder abortData = HandleRunnerThread(runnerThread, runner);
                        ResultHolder callData = runner.GetThreadRunResult();
                        AppendResultTable(bodyElement, callData, parser);
                        AppendResultTable(bodyElement, abortData, parser);
                        break;
                    case ActionType.SAVE:
                        XmlNode saveInputNode = childNode.OwnerDocument.ImportNode(testResultSaveInput, true);
                        XmlElement htmlResultNode = TransformToHtml(testResultSaveInput, childNode.OwnerDocument);
                        bool inputNodesExist = false;
                        if (childNode.ChildNodes.Count > 0)
                        {
                            foreach (XmlNode inputChildNode in childNode.ChildNodes)
                            {
                                if (inputChildNode.LocalName == "input")
                                {
                                    if (inputChildNode.ChildNodes.Count > 0)
                                    {
                                        inputChildNode.InsertBefore(htmlResultNode.Clone(), inputChildNode.FirstChild);
                                        inputChildNode.InsertBefore(saveInputNode.Clone(), inputChildNode.FirstChild);
                                    }
                                    else
                                    {
                                        inputChildNode.AppendChild(saveInputNode.Clone());
                                        inputChildNode.AppendChild(htmlResultNode.Clone());
                                    }
                                    inputNodesExist = true;
                                }
                            }
                        }
                        if (!inputNodesExist)
                        {
                            XmlNode inputNode = childNode.OwnerDocument.CreateElement("input");
                            inputNode.AppendChild(saveInputNode);
                            inputNode.AppendChild(htmlResultNode);
                            childNode.AppendChild(inputNode);
                        }
                        goto case ActionType.CALL;
                    case ActionType.LOAD:
                        XmlAttribute fileLoadAttr = GetAttributeByName(childNode, "file");
                        XmlNode loadTestNode = null;
                        if(loadDic.TryGetValue(fileLoadAttr.Value, out loadTestNode))
                        {
                            XmlElement loadBodyNode = doc.CreateElement("load");
                            loadBodyNode.SetAttribute("file", fileLoadAttr.Value);
                            RunnerDetails loadRunDetails = runDetails.LoadCopy(fileLoadAttr.Value);
                            RunTest(loadTestNode, loadBodyNode, loadRunDetails);
                            bodyElement.AppendChild(loadBodyNode);
                        }
                        break;
                    case ActionType.ASYNC:
                        List<ThreadRunnerInfo> asyncPairList = new List<ThreadRunnerInfo>();
                        foreach (XmlNode fixtureNode in childNode.ChildNodes)
                        {
                            if (fixtureNode.LocalName == "fixture")
                            {
                                XmlAttribute fixId = GetAttributeByName(fixtureNode, "id");
                                Object asyncFixture = runDetails.GetInitiatedFixture(fixId.Value);
                                if (asyncFixture != null)
                                {
                                    Runner asyncRunner = new Runner(fixtureNode, asyncFixture, runDetails);
                                    Thread asyncThread = new Thread(new ThreadStart(asyncRunner.ThreadAsyncRun));
                                    asyncThread.Start();
                                    asyncPairList.Add(new ThreadRunnerInfo(asyncThread, asyncRunner, fixId.Value));
                                }
                            }
                        }
                        XmlElement asyncNode = doc.CreateElement("async");
                        foreach (ThreadRunnerInfo asyncInfo in asyncPairList)
                        {
                            XmlElement asyncFixtureNode = doc.CreateElement("fixture");
                            asyncFixtureNode.SetAttribute("id", asyncInfo.fixtureId);
                            ResultHolder abortAsyncData = HandleRunnerThread(asyncInfo.thread, asyncInfo.runner);
                            foreach (ResultHolder asyncData in asyncInfo.runner.GetThreadAsyncRunResult())
                            {
                                AppendResultTable(asyncFixtureNode, asyncData, parser);
                            }
                            AppendResultTable(asyncFixtureNode, abortAsyncData, parser);
                            asyncNode.AppendChild(asyncFixtureNode);
                        }
                        bodyElement.AppendChild(asyncNode);
                        break;
                    default:
                        // TODO error message
                        break;
                }
                if (bgRunner != null && bgRunner.KillPending)
                {
                    break;
                }
            }
            return runSuccessful;
        }

        private List<XmlNode> GetParamNodes(XmlNodeList nodeList, int index)
        {
            List<XmlNode> typeNodeList = new List<XmlNode>();
            for (int k = index; k < nodeList.Count; k++)
            {
                if (nodeList[k].LocalName == ActionType.PARAM)
                {
                    if (nodeList[k].Attributes.GetNamedItem("fixture") != null)
                    {
                        break;
                    }
                    typeNodeList.Add(nodeList[k]);
                }
                else
                {
                    break;
                }
            }
            return typeNodeList;
        }

        private List<XmlNode> GetSelectedNodes(XmlNodeList nodeList, string aType, int index)
        {
            List<XmlNode> typeNodeList = new List<XmlNode>();
            for(int k = index; k<nodeList.Count; k++)
            {
                if (nodeList[k].LocalName == aType)
                {
                    typeNodeList.Add(nodeList[k]);
                }
                else
                {
                    break;
                }
            }
            return typeNodeList;
        }

        private ResultHolder HandleRunnerThread(Thread runnerThread, Runner runner)
        {
            if (bgRunner != null)
            {
                while (!bgRunner.KillPending && runnerThread.IsAlive)
                {
                    runnerThread.Join(20);
                }
                if (runnerThread.IsAlive)
                {
                    try
                    {
                        runnerThread.Abort();
                        ResultHolder abortData = runner.activeDataHolder;
                        if (abortData != null)
                        {
                            abortData.SetRowError(new Exception("Process killed by user."));
                            return abortData;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine(ex.ToString());
                    }
                }
            }
            else
            {
                runnerThread.Join();
            }      
            return null;
        }

        public void AppendHead(XmlNode resultRootNode, string file)
        {
            XmlDocument doc = resultRootNode.OwnerDocument;
            XmlElement headNode = doc.CreateElement("head");
            headNode.SetAttribute("test", file);
            headNode.SetAttribute("version", "SeeFlaw v1.0");
            headNode.SetAttribute("runtime", System.DateTime.Now.ToString());
            resultRootNode.AppendChild(headNode);
        }

        public void AppendHeadings(XmlElement bodyElement, XmlNode headNode)
        {
            XmlDocument doc = bodyElement.OwnerDocument;
            XmlAttribute testAttr = GetAttributeByName(headNode, "test");
            XmlAttribute versionAttr = GetAttributeByName(headNode, "version");
            XmlAttribute runtimeAttr = GetAttributeByName(headNode, "runtime");
            XmlElement heading = doc.CreateElement("h1");
            XmlElement center = doc.CreateElement("center");
            center.InnerText = versionAttr.Value + " run";
            if (!onlyTest)
            {
                center.InnerText += " " + runtimeAttr.Value;
            }
            heading.AppendChild(center);
            bodyElement.AppendChild(heading);
            bodyElement.AppendChild(doc.CreateElement("br"));
            XmlElement heading2 = doc.CreateElement("h2");
            heading2.InnerText = testAttr.Value;
            bodyElement.AppendChild(heading2);
            bodyElement.AppendChild(doc.CreateElement("br"));
        }

        public void AppendResultTable(XmlElement bodyElement, ResultHolder dataHolder, ResultParser parser)
        {
            XmlNode table = parser.GetResultAsXml(dataHolder);
            if (table == null)
            {
                return;
            }
            bodyElement.AppendChild(table);
            //bodyElement.AppendChild(bodyElement.OwnerDocument.CreateElement("br"));
            CheckResultStatus(dataHolder);
        }

        public void CheckResultStatus(ResultHolder dataHolder)
        {
            if (dataHolder == null)
            {
                return;
            }
            if (!dataHolder.IsSuccessful())
            {
                runSuccessful = false;
            }
        }
    }


    [TestFixture]
    public class SeeFlawRunnerTest
    {
        private String xml = @"<seeflaw>
            <init fixture='SeeFlawRunner.ExampleFixture' id='tp1'/>
            <init fixture='SeeFlawRunner.ExampleFixture' id='tp2'/>
            <call fixture='tp1' method='SingleOutputExampleMethod'>
                <input name='Hello Kalle Kula'/>
                <output message='Hello Kalle Kula'/>
            </call>
            <call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'>
                <input name='Arne Anka' />
                <output message='Hello Arne Anka'/>
            </call>
            <async>
                <fixture id='tp1'>
                    <call method='SingleOutputExampleMethod'>
                        <input name='Kajsa'/>
                        <output message='Hello Kajsa'/>
                    </call>
                    <call method='MultiOutputExampleMethod'>
                        <input lines='2'/>
                        <output message='line1'/>
                        <output message='line2'/>
                    </call>
                </fixture>
                <fixture id='tp2'>
                    <call method='MultiOutputExampleMethod'>
                        <input lines='3'/>
                        <output message='line1'/>
                        <output message='line3'/>
                        <output message='line2'/>
                    </call>
                </fixture>
            </async>
        </seeflaw>";
        private String xml2 = @"<seeflaw>
        <param name='param1' value='Kalle'/>
        <param name='param2' argument='user'/>
        <init fixture='SeeFlawRunner.ExampleFixture' id='tp1'/>
        <init fixture='SeeFlawRunner.ExampleFixture' id='tp2'/>
        <call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'>
            <input name='param1' />
            <output message='Hello Kalle'/>
        </call>
        <async>
            <fixture id='tp1'>
                <call method='SingleOutputExampleMethod'>
                    <input name='param1'/>
                    <output message='Hello Kalle'/>
                </call>
                <call method='SingleOutputExampleMethod'>
                    <input name='param2'/>
                    <output message='Hello Arne'/>
                </call>
            </fixture>
            <fixture id='tp2'>
                <call method='SingleOutputExampleMethod'>
                    <input name='param1'/>
                    <output message='Hello Kalle'/>
                </call>
            </fixture>
        </async>
        </seeflaw>";
        public SeeFlaw seeFlaw;
        public System.Xml.XmlDocument doc;
        public System.Xml.XmlDocument resultDoc;
        public System.Xml.XmlDocument htmlDoc;
        public Dictionary<string, string> argDic;

        [SetUp]
        public void SetUpTest()
        {
            seeFlaw = new SeeFlaw(true);
            doc = new System.Xml.XmlDocument();
            resultDoc = new System.Xml.XmlDocument();
            htmlDoc = new XmlDocument();
            argDic = new Dictionary<string, string>();
            argDic["testfile"] = "testfile";
        }

        [Test]
        public void RunTest()
        {
            doc.LoadXml(xml);
            argDic["testfile"] = "xml";
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, resultDoc, htmlDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            System.Console.WriteLine(resultDoc.FirstChild.OuterXml);
            System.Console.WriteLine(htmlDoc.FirstChild.OuterXml);
        }
        [Test]
        public void RunTest2()
        {
            doc.LoadXml(xml2);
            argDic["testfile"] = "xml2";
            argDic.Add("user", "Arne");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.RunTestCase();
            System.Console.WriteLine(resultDoc.FirstChild.OuterXml);
        }
        [Test]
        public void RunTwoParams()
        {
            doc.LoadXml("<seeflaw><param name='param1' value='Arne'/><param name='param2' argument='user'/></seeflaw>");
            argDic.Add("user", "Kalle");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">param</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">value</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">argument</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td><td bgcolor=\"#CCCCFF\" class=\"input_value\" /><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\" /></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param2</td><td bgcolor=\"#CCCCFF\" class=\"input_value\" /><td bgcolor=\"#CCCCFF\" class=\"input_value\">user</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Kalle</td></tr>";
            expected += "</table></td></tr></table><br />";
            System.Console.WriteLine(resultDoc.FirstChild.InnerXml);
            NUnit.Framework.Assert.AreEqual(expected, resultDoc.FirstChild.LastChild.InnerXml);
        }
        [Test]
        public void RunSingleTestWithParamValue()
        {
            doc.LoadXml("<seeflaw><param name='param1' value='Arne'/><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='param1'/><output message='Hello Arne'/></call></seeflaw>");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            XmlNode resultNode = resultDoc.FirstChild.LastChild;
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">param</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td></tr>";
            expected += "</table></td></tr></table><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne</td></tr>";
            expected += "</table></td></tr></table><br />";
            System.Console.WriteLine(resultNode.InnerXml);
            NUnit.Framework.Assert.AreEqual(expected, resultNode.InnerXml);
        }
        [Test]
        public void RunSingleTestWithDefaultParamValue()
        {
            string xmlInput = "<seeflaw><param name='param1' value='Arne' argument='param1val'/><param name='param2' value='default' argument='param2val'/>";
            xmlInput += "<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='param1'/><output message='Hello Arne'/><input name='param2'/><output message='Hello Kalle'/></call></seeflaw>";
            doc.LoadXml(xmlInput);
            argDic["param2val"] = "Kalle";
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            XmlNode resultNode = resultDoc.FirstChild.LastChild;
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">param</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">value</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">argument</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td><td bgcolor=\"#CCCCFF\" class=\"input_value\" /><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\" /></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param2</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">default</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param2val</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Kalle</td></tr>";
            expected += "</table></td></tr></table><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time (00:00:00)</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Kalle</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Kalle</td></tr>";
            expected += "</table></td></tr></table><br />";
            System.Console.WriteLine(resultNode.InnerXml);
            NUnit.Framework.Assert.AreEqual(expected, resultNode.InnerXml);
        }
        [Test]
        public void RunSingleTestWithParamFixture()
        {
            doc.LoadXml("<seeflaw><param name='param1' fixture='SeeFlawRunner.ExampleFixture' method='GetParamExampleMethod'/><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='param1'/><output message='Hello param test'/></call></seeflaw>");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            XmlNode resultNode = resultDoc.FirstChild.LastChild;
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : GetParamExampleMethod : param1</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">param test</td></tr>";
            expected += "</table></td></tr></table><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param test</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello param test</td></tr>";
            expected += "</table></td></tr></table><br />";
            System.Console.WriteLine(resultNode.InnerXml);
            NUnit.Framework.Assert.AreEqual(expected, resultNode.InnerXml);
        }
        [Test]
        public void RunNoOutputTestFixtureException()
        {
            doc.LoadXml("<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='NoOutputExampleMethod'><input test='error'/></call></seeflaw>");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, resultDoc, htmlDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            System.Console.WriteLine(resultDoc.FirstChild.OuterXml);
            XmlNode bodyNode = htmlDoc.FirstChild.LastChild;
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_error\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : NoOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_error\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">test</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_error\"><a href=\"#Exception1\">Exception1</a></td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">error</td></tr>";
            expected += "</table></td></tr></table>";
            expected += "<br /><h2>Exceptions Listing</h2>";
            expected += "<table border=\"0\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td valign=\"top\"><a name=\"Exception1\">Exception1</a></td>";
            expected += "<td valign=\"top\"><pre valign=\"top\">Mandatory input 'name' missing";//</pre></td></table>";
            System.Console.WriteLine(bodyNode.OuterXml);
            int length = System.Math.Min(expected.Length, bodyNode.InnerXml.Length - 1);
            string resultStr = bodyNode.InnerXml.Substring(0, length);
            NUnit.Framework.Assert.AreEqual(expected, resultStr);
        }
        [Test]
        public void RunSingleTestFixtureException()
        {
            doc.LoadXml("<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input test='error'/><output message='some'/></call></seeflaw>");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, resultDoc, htmlDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            System.Console.WriteLine(resultDoc.FirstChild.OuterXml);
            XmlNode bodyNode = htmlDoc.FirstChild.LastChild;
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_error\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_error\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">test</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_error\"><a href=\"#Exception1\">Exception1</a></td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">error</td><td bgcolor=\"#BBBBBB\" class=\"outputvalue_unprocessed\" colspan=\"2\">some</td></tr>";
            expected += "</table></td></tr></table>";
            expected += "<br /><h2>Exceptions Listing</h2>";
            expected += "<table border=\"0\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td valign=\"top\"><a name=\"Exception1\">Exception1</a></td>";
            expected += "<td valign=\"top\"><pre valign=\"top\">Mandatory input 'name' missing.";
            System.Console.WriteLine(bodyNode.OuterXml);
            int length = System.Math.Min(expected.Length, bodyNode.InnerXml.Length - 1);
            string resultStr = bodyNode.InnerXml.Substring(0, length);
            NUnit.Framework.Assert.AreEqual(expected, resultStr);
        }
        [Test]
        public void RunMultiTestFixtureException()
        {
            doc.LoadXml("<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod'><input test='error'/><output message='some'/></call></seeflaw>");
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, resultDoc, htmlDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            seeFlaw.RunTestCase();
            System.Console.WriteLine(resultDoc.FirstChild.OuterXml);
            XmlNode bodyNode = htmlDoc.FirstChild.LastChild;
            string expected = "<h1><center>SeeFlaw v1.0 run</center></h1><br />";
            expected += "<h2>testfile</h2><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_error\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_error\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">test</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_error\"><a href=\"#Exception1\">Exception1</a></td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">error</td></tr>";
            expected += "</table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_error\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"#999999\" class=\"statusvalue_unprocessed\">Unprocessed</td><td bgcolor=\"#BBBBBB\" class=\"outputvalue_unprocessed\" colspan=\"2\">some</td></tr>";
            expected += "</table></td></tr></table>";
            expected += "<br /><h2>Exceptions Listing</h2>";
            expected += "<table border=\"0\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td valign=\"top\"><a name=\"Exception1\">Exception1</a></td>";
            expected += "<td valign=\"top\"><pre valign=\"top\">Mandatory input 'lines' missing.";//</pre></td></table>";
            System.Console.WriteLine(bodyNode.OuterXml);
            int length = System.Math.Min(expected.Length, bodyNode.InnerXml.Length - 1);
            string resultStr = bodyNode.InnerXml.Substring(0, length);
            NUnit.Framework.Assert.AreEqual(expected, resultStr);
        }
        [Test]
        public void RunLoadTest()
        {
            string testXml = "<seeflaw><load file='load1.xml'/><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Arne'/><output message='Hello Arne'/></call></seeflaw>";
            string loadXml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Kalle'/><output message='Hello Kalle'/></call></seeflaw>";
            doc.LoadXml(testXml);
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            XmlDocument loadDoc = new XmlDocument();
            loadDoc.LoadXml(loadXml);
            seeFlaw.AddTestLoadNode("load1.xml", loadDoc.GetElementsByTagName("seeflaw")[0]);
            XmlElement resultElement = resultDoc.CreateElement("seeflawresult");
            bool successful = seeFlaw.RunTest(doc.GetElementsByTagName("seeflaw")[0], resultElement);
            System.Console.WriteLine(resultElement.OuterXml);
            seeFlaw.TransformToHtml(resultElement);
            XmlNode resultNode = resultDoc.FirstChild.LastChild;
            string expected = "<table border=\"1\" cellpadding=\"0\">";
            expected += "<tr><td><table width=\"100%\"><tr><th align=\"left\" bgcolor=\"#DDCC88\" class=\"title\">load : load1.xml</th></tr></table></td></tr>";
            expected += "<tr><td>";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Kalle</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Kalle</td></tr>";
            expected += "</table></td></tr></table><br />";
            expected += "</td></tr></table><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne</td></tr>";
            expected += "</table></td></tr></table><br />";
            System.Console.WriteLine(resultNode.InnerXml);
            NUnit.Framework.Assert.AreEqual(expected, resultNode.InnerXml);
        }
        [Test]
        public void RunTextTest()
        {
            string testXml = "<seeflaw><text>Comment1 line1</text><text>Comment1 line2</text><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Arne'/><output message='Hello Arne'/></call><text>Comment2</text></seeflaw>";
            doc.LoadXml(testXml);
            seeFlaw = new SeeFlaw();
            seeFlaw.AddDetails(doc, null, resultDoc, argDic, null, null);
            seeFlaw.SetTestTime();
            XmlDocument loadDoc = new XmlDocument();
            XmlElement resultElement = resultDoc.CreateElement("seeflawresult");
            bool successful = seeFlaw.RunTest(doc.GetElementsByTagName("seeflaw")[0], resultElement);
            System.Console.WriteLine(resultElement.OuterXml);
            seeFlaw.TransformToHtml(resultElement);
            XmlNode resultNode = resultDoc.FirstChild.LastChild;
            string expected = "<table border=\"1\" cellpadding=\"0\" class=\"step_text\"><tr><td><table width=\"100%\" bgcolor=\"yellow\">";
            expected += "<tr><th align=\"left\" class=\"text\">Comment1 line1</th></tr>";
            expected += "<tr><th align=\"left\" class=\"text\">Comment1 line2</th></tr>";
            expected += "</table></td></tr></table><br />";
            expected += "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne</td></tr>";
            expected += "</table></td></tr></table><br />";
            expected += "<table border=\"1\" cellpadding=\"0\" class=\"step_text\"><tr><td><table width=\"100%\" bgcolor=\"yellow\">";
            expected += "<tr><th align=\"left\" class=\"text\">Comment2</th></tr>";
            expected += "</table></td></tr></table><br />";
            System.Console.WriteLine(resultNode.InnerXml);
            NUnit.Framework.Assert.AreEqual(expected, resultNode.InnerXml);
        }
    }
}
