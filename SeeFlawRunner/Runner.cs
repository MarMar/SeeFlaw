using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;

namespace SeeFlawRunner
{
    public class Runner
    {
        public static class InputType
        {
            public const string NONE = "none";
            public const string DIC = "dic";
            public const string XML = "xml";
        }

        public class InputArguments
        {
            public Dictionary<string, string> visibleArgDic;
            public Object attribute;
        }

        private List<ResultHolder> asyncResultDataList = new List<ResultHolder>();
        private ResultHolder resultData;
        private XmlNode runNode;
        private XmlNode asyncFixtureNode;
        private Object asyncFixture;
        private RunnerDetails runDetails;
        public ResultHolder activeDataHolder = null;
        private bool onlyTest = false;

        public Runner()
        { }

        public Runner(bool onlyTest, RunnerDetails runDetails)
        {
            this.onlyTest = onlyTest;
            this.runDetails = runDetails;
        }

        public Runner(XmlNode callNode, RunnerDetails runDetails)
        {
            this.runNode = callNode;
            this.runDetails = runDetails;
        }
        
        public Runner(XmlNode asyncFixtureNode, Object asyncFixture, RunnerDetails runDetails)
        {
            this.asyncFixtureNode = asyncFixtureNode;
            this.asyncFixture = asyncFixture;
            this.runDetails = runDetails;
        }

        public void ThreadRun()
        {
            resultData = RunFixtureMethodCall(runNode);
        }

        public void ThreadAsyncRun()
        {
            XmlNode fixtureIdAttr = asyncFixtureNode.Attributes.GetNamedItem("id");
            foreach (XmlNode callNode in asyncFixtureNode.ChildNodes)
            {
                ResultHolder resultData = RunFixtureMethodCall(callNode, asyncFixture, fixtureIdAttr.Value);
                asyncResultDataList.Add(resultData);
            }
        }

        public ResultHolder GetThreadRunResult()
        {
            return resultData;
        }

        public List<ResultHolder> GetThreadAsyncRunResult()
        {
            return asyncResultDataList;
        }

        public ResultHolder RunFixtureParam(XmlNodeList paramNodes)
        {
            List<XmlNode> nodeList = new List<XmlNode>();
            foreach (XmlNode node in paramNodes)
            {
                nodeList.Add(node);
            }
            return RunFixtureParam(nodeList);
        }

        public ResultHolder RunFixtureParam(List<XmlNode> paramNodes)
        {
            ResultHolder dataHolder = new ResultHolder("param", true);
            foreach (XmlNode paramNode in paramNodes)
            {
                try
                {
                    Exception fixEx = null;
                    XmlAttribute name = SeeFlaw.GetAttributeByName(paramNode, "name");
                    Dictionary<string, string> inputDic = new Dictionary<string, string>();
                    Dictionary<string, string> outputDic = new Dictionary<string, string>();
                    Dictionary<string, object> resultDic = new Dictionary<string, object>();
                    inputDic.Add("name", name.Value);
                    XmlAttribute valAttr = SeeFlaw.GetAttributeByName(paramNode, "value", true);
                    XmlAttribute argAttr = SeeFlaw.GetAttributeByName(paramNode, "argument", true);
                    if (valAttr == null && argAttr == null)
                    {
                        fixEx = new Exception("argument and value attributes missing in param node");
                    }
                    string paramValue = "";
                    if (valAttr != null)
                    {
                        paramValue = valAttr.Value;
                        inputDic.Add("value", valAttr.Value);
                    }
                    if (argAttr != null)
                    {
                        string val = runDetails.GetArgument(argAttr.Value);
                        if (val != "")
                        {
                            paramValue = val;
                            inputDic.Add("argument", argAttr.Value);
                            outputDic.Add("read_value", "");
                            resultDic.Add("read_value", val);
                        }
                        else if (valAttr == null)
                        {
                            throw new Exception("Argument " + argAttr.Value + " in param node is missing in argument list");
                        }
                    }
                    if (!resultDic.ContainsKey("read_value"))
                    {
                        resultDic.Add("read_value", "");
                    }
                    runDetails.AddParameter(name.Value, paramValue);
                    dataHolder.AddSingleRow(inputDic, outputDic, resultDic, fixEx);

                }
                catch (Exception ex)
                {
                    dataHolder.AddSingleRow(null, null, null, ex);
                }
            }

            return dataHolder;
        }

        public ResultHolder RunFixtureParam(XmlNode callNode, Object callFixture, string fixtureName)
        {
            activeDataHolder = null;
            string fixtureMethod = "";
            try
            {
                XmlAttribute name = SeeFlaw.GetAttributeByName(callNode, "name");
                XmlAttribute methodAttr = SeeFlaw.GetAttributeByName(callNode, "method");
                fixtureMethod = methodAttr.Value;
                activeDataHolder = new ResultHolder("param", fixtureName, fixtureMethod, name.Value, false);
                MethodInfo methodInfo = callFixture.GetType().GetMethod(methodAttr.Value);
                string inputType = CheckMethodInfo(methodInfo);

                ParameterInfo[] infos = methodInfo.GetParameters();
                if (infos.Length > 1)
                {
                    throw new Exception("To many arguments in method");
                }

                Dictionary<string, string> inputDic = new Dictionary<string, string>();
                Dictionary<string, string> outputDic = new Dictionary<string, string>();
                Dictionary<string, object> resultDic = new Dictionary<string, object>();
                Object[] attributes = new Object[] { };
                string paramValue = "";
                if (infos.Length == 1)
                {
                    XmlNode paramFixtureInputNode = HtmlTransformer.GetChildNodeByName(callNode, "input");
                    InputArguments inputArgs = ReadArgumentsFromNode(paramFixtureInputNode, inputType);
                    inputDic = inputArgs.visibleArgDic;
                    attributes = new Object[] { inputArgs.attribute };
                }
                paramValue = (string)methodInfo.Invoke(callFixture, attributes);
                outputDic.Add("read_value", "");
                resultDic.Add("read_value", paramValue);
                runDetails.AddParameter(name.Value, paramValue);
                activeDataHolder.AddSingleRow(inputDic, outputDic, resultDic, null);
            }
            catch (Exception ex)
            {
                activeDataHolder = new ResultHolder(fixtureName, fixtureMethod, null, true);
                activeDataHolder.AddSingleRow(null, null, null, ex);
            }
            return activeDataHolder;
        }

        public ResultHolder RunFixtureInit(XmlNodeList initNodes)
        {
            List<XmlNode> nodeList = new List<XmlNode>();
            foreach (XmlNode node in initNodes)
            {
                nodeList.Add(node);
            }
            return RunFixtureInit(nodeList);
        }

        public ResultHolder RunFixtureInit(List<XmlNode> initNodes)
        {
            ResultHolder dataHolder = new ResultHolder("init", true);
            foreach (XmlNode initNode in initNodes)
            {
                try
                {
                    Exception fixEx = null;

                    XmlAttribute fixture = SeeFlaw.GetAttributeByName(initNode, "fixture");
                    XmlAttribute idNode = SeeFlaw.GetAttributeByName(initNode, "id");
                    Object fixtureObj = runDetails.InitFixture(fixture.Value, idNode.Value);

                    Dictionary<string, string> initDic = new Dictionary<string, string>();
                    initDic.Add("fixture", fixture.Value);
                    initDic.Add("id", idNode.Value);
                    dataHolder.AddSingleRow(initDic, null, null, fixEx);
                }
                catch (Exception ex)
                {
                    dataHolder.AddSingleRow(null, null, null, ex);
                }
            }

            return dataHolder;
        }

        public ResultHolder RunFixtureMethodCall(XmlNode callNode)
        {
            XmlAttribute fixtureAttr = SeeFlaw.GetAttributeByName(callNode, "fixture");
            Object callFixture = runDetails.GetFixture(fixtureAttr.Value);
            if (callNode.LocalName == SeeFlaw.ActionType.PARAM)
            {
                return RunFixtureParam(callNode, callFixture, fixtureAttr.Value);
            }
            else
            {
                return RunFixtureMethodCall(callNode, callFixture, fixtureAttr.Value);
            }
        }

        public ResultHolder RunFixtureMethodCall(XmlNode callNode, Object callFixture, string fixtureName)
        {
            activeDataHolder = null;
            Dictionary<string, string> toleranceDic = ReadToleranceFromNode(callNode);
            string fixtureMethod = "";
            try
            {
                XmlAttribute methodAttr = SeeFlaw.GetAttributeByName(callNode, "method");
                fixtureMethod = methodAttr.Value;
                activeDataHolder = new ResultHolder(fixtureName, fixtureMethod, toleranceDic, false);
                MethodInfo methodInfo = callFixture.GetType().GetMethod(methodAttr.Value);
                string inputType = CheckMethodInfo(methodInfo);
                if (methodInfo.ReturnType == typeof(void))
                {
                    bool calledWithInput = false;
                    foreach (XmlNode childNode in callNode)
                    {
                        if (childNode.LocalName == "input")
                        {
                            calledWithInput = true;
                            InputArguments inputArgs = ReadArgumentsFromNode(childNode, inputType);
                            activeDataHolder.CreateSingleRow(inputArgs.visibleArgDic, null);
                            Object[] attributes = PopulateAttributes(methodInfo, inputArgs, null);
                            try
                            {
                                methodInfo.Invoke(callFixture, attributes);
                                activeDataHolder.SetSingleRowResult(null, null);
                            }
                            catch (Exception ex)
                            {
                                activeDataHolder.SetSingleRowResult(null, ex);
                            }
                        }
                    }
                    if (!calledWithInput)
                    {
                        activeDataHolder.CreateSingleRow(null, null);
                        Object[] attributes = new Object[]{};
                        try
                        {
                            methodInfo.Invoke(callFixture, attributes);
                            activeDataHolder.SetSingleRowResult(null, null);
                        }
                        catch (Exception ex)
                        {
                            activeDataHolder.SetSingleRowResult(null, ex);
                        }
                    }
                }
                else if (methodInfo.ReturnType == typeof(Dictionary<string, object>))
                {
                    InputArguments inputArgs = null;
                    Dictionary<string, string> outputDic = null;
                    foreach (XmlNode childNode in callNode)
                    {
                        if (childNode.LocalName == "input")
                        {
                            inputArgs = ReadArgumentsFromNode(childNode, inputType);
                        }
                        else if (childNode.LocalName == "output")
                        {
                            outputDic = ReadDicFromNode(childNode, InputType.DIC);
                            activeDataHolder.CreateSingleRow(inputArgs.visibleArgDic, outputDic);
                            Object[] attributes = PopulateAttributes(methodInfo, inputArgs, outputDic.Keys.ToList());
                            try
                            {
                                Dictionary<string, object> result = (Dictionary<string, object>)methodInfo.Invoke(callFixture, attributes);
                                activeDataHolder.SetSingleRowResult(result, null);
                            }
                            catch (Exception ex)
                            {
                                if (ex.InnerException != null)
                                {
                                    System.Console.WriteLine(ex.InnerException.Message);
                                }
                                activeDataHolder.SetSingleRowResult(null, ex);
                            }
                        }
                    }
                }
                else if (methodInfo.ReturnType == typeof(List<Dictionary<string, object>>))
                {
                    InputArguments inputArgs = ReadArguments(callNode, inputType);
                    List<Dictionary<string, string>> outputList = ReadOutputList(callNode, runDetails);
                    if (outputList.Count == 0)
                    {
                        List<string> outputKeys = ReadOutputKeys(callNode);
                        bool allowSurplus = false;
                        if (SeeFlaw.GetAttributeByName(callNode, "allowsurplus", true) != null)
                        {
                            allowSurplus = true;
                        }
                        activeDataHolder.CreateMultiRows(inputArgs.visibleArgDic, outputKeys, allowSurplus);
                    }
                    else
                    {
                        activeDataHolder.CreateMultiRows(inputArgs.visibleArgDic, outputList);
                    }
                    Object[] attributes = PopulateAttributes(methodInfo, inputArgs, activeDataHolder.multiRowKeys);
                    try
                    {
                        List<Dictionary<string, object>> result = (List<Dictionary<string, object>>)methodInfo.Invoke(callFixture, attributes);
                        activeDataHolder.SetMultiRowsResult(result, null);
                    }
                    catch (Exception ex)
                    {
                        activeDataHolder.SetMultiRowsResult(null, ex);
                    }
                }
                else
                {
                    throw new Exception("fixture method return type not supported");
                }
            }
            catch (Exception ex)
            {
                activeDataHolder = new ResultHolder(fixtureName, fixtureMethod, null, true);
                activeDataHolder.AddSingleRow(null, null, null, ex);
            }
            return activeDataHolder;
        }

        public Dictionary<string, string> ReadToleranceFromNode(XmlNode node)
        {
            foreach (XmlNode childNode in node)
            {
                if (childNode.LocalName == "tolerance")
                {
                    return ReadDicFromNode(childNode, InputType.DIC);
                }
            }
            return null;
        }

        public string CheckMethodInfo(MethodInfo methodInfo)
        {
            string inputType = InputType.NONE;
            ParameterInfo[] infos = methodInfo.GetParameters();
            if (infos.Length == 0)
            {
                return inputType;
            }
            if (infos.Length > 2)
            {
                throw new Exception("Method " + methodInfo.Name + " takes to many inparameters");
            }
            if (infos[0].ParameterType != typeof(XmlNode) && infos[0].ParameterType != typeof(Dictionary<string, string>))
            {
                throw new Exception("First parameter is of wrong type in method " + methodInfo.Name);
            }
            if (infos[0].ParameterType == typeof(XmlNode))
            {
                inputType = InputType.XML;
            }
            else
            {
                inputType = InputType.DIC;
            }
            if (infos.Length == 2)
            {
                if (infos[1].ParameterType != typeof(List<string>))
                {
                    throw new Exception("Second parameter is of wrong type in method " + methodInfo.Name);
                }
            }
            return inputType;
        }

        public Object[] PopulateAttributes(MethodInfo methodInfo, InputArguments inputArgs, List<string> outputKeys)
        {
            ParameterInfo[] infos = methodInfo.GetParameters();
            Object[] attributes = null;
            if (infos.Length == 0)
            {
                return new Object[] { };
            }
            if (infos.Length == 2)
            {
                attributes = new Object[2] { inputArgs.attribute, outputKeys };
            }
            else
            {
                attributes = new Object[1] { inputArgs.attribute };
            }
            return attributes;
        }

        public InputArguments ReadArguments(XmlNode callNode, string inputType)
        {
            foreach (XmlNode inputNode in callNode)
            {
                if (inputNode.LocalName == "input")
                {
                    return ReadArgumentsFromNode(inputNode, inputType);
                }
            }
            return null;
        }

        public InputArguments ReadArgumentsFromNode(XmlNode inputNode, string inputType)
        {
            InputArguments inputs = new InputArguments();
            inputs.visibleArgDic = ReadDicFromNode(inputNode, inputType, false);
            if (inputType == InputType.DIC)
            {
                inputs.attribute = ReadDicFromNode(inputNode, inputType);
            }
            else
            {
                inputs.attribute = ReadXmlFromNode(inputNode);
            }
            return inputs;
        }

        public Dictionary<string, string> ReadDicFromNode(XmlNode node, string inputType)
        {
            return ReadDicFromNode(node, inputType, true);
        }
        
        public Dictionary<string, string> ReadDicFromNode(XmlNode node, string inputType, bool recursive)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            AddNodeArgumentsRecursive(node, "", dic, inputType, recursive);
            if (runDetails != null && runDetails.HasParameters())
            {
                foreach (string key in dic.Keys.ToList())
                {
                    string val = dic[key];
                    if (runDetails.HasParameter(val))
                    {
                        dic[key] = runDetails.GetParameter(val);
                    }
                }
            }
            return dic;
        }

        public void AddNodeArgumentsRecursive(XmlNode node, string subName, Dictionary<string, string> inputDic, string inputType, bool recursive)
        {
            if (node == null)
            {
                return;
            }

            foreach (XmlAttribute attr in node.Attributes)
            {
                string key = attr.LocalName;
                if (subName != "")
                {
                    key = subName + "." + attr.LocalName;
                }
                AddToArgDic(inputDic, key, attr.Value, inputType);
            }
            foreach (XmlNode childNode in node.ChildNodes)
            {
                string childName = childNode.LocalName;
                if (childNode.NodeType == XmlNodeType.Text)
                {
                    AddToArgDic(inputDic, subName, childNode.Value, inputType);
                }
                if (childNode.NodeType == XmlNodeType.Element && recursive)
                {
                    if (subName != "")
                    {
                        childName = subName + "." + childName;
                    }
                    AddNodeArgumentsRecursive(childNode, childName, inputDic, inputType, true);
                }
            }
        }
        public void AddToArgDic(Dictionary<string, string> inputDic, string key, string val, string inputType)
        {
            if (key != "" && !inputDic.ContainsKey(key))
            {
                if (inputType != InputType.XML || !key.Contains(':'))
                {
                    inputDic.Add(key, val);
                }
            }
        }

        public XmlNode ReadXmlFromNode(XmlNode node)
        {
            if (!runDetails.HasParameters() || node == null)
            {
                return node;
            }
            XmlNode inputNode = node.Clone();
            UpdateAttributes(inputNode);
            
            return inputNode;
        }

        public void UpdateAttributes(XmlNode node)
        {
            if (node.NodeType == XmlNodeType.Comment)
            {
                return;
            }
            if (node.NodeType == XmlNodeType.Text)
            {
                if (runDetails.HasParameter(node.Value))
                {
                    node.Value = runDetails.GetParameter(node.Value);
                }
                return;
            }
            foreach (XmlAttribute attr in node.Attributes)
            {
                if (runDetails.HasParameter(attr.Value))
                {
                    attr.Value = runDetails.GetParameter(attr.Value);
                }
            }
            foreach (XmlNode childNode in node.ChildNodes)
            {
                UpdateAttributes(childNode);
            }
        }

        public List<Dictionary<string, string>> ReadOutputList(XmlNode node, RunnerDetails runDetails)
        {
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.LocalName == "output")
                {
                    list.Add(ReadDicFromNode(childNode, InputType.DIC));
                }
            }
            return list;
        }

        public List<string> ReadOutputKeys(XmlNode node)
        {
            List<string> keys = new List<string>();
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.LocalName == "outputkeys")
                {
                    foreach (XmlAttribute attr in childNode.Attributes)
                    {
                        keys.Add(attr.Name);
                    }
                }
            }
            return keys;
        }
    }

    [TestFixture]
    public class RunnerTest
    {
        public Runner runner;
        public RunnerDetails runDetails;
        public System.Xml.XmlDocument doc;
        public ResultParser parser;
        public ErrorParser errParser;

        [SetUp]
        public void SetUpTest()
        {
            runDetails = new RunnerDetails(new Dictionary<string, string>());
            runner = new Runner(true, runDetails);
            doc = new System.Xml.XmlDocument();
            errParser = new ErrorParser();
            parser = new ResultParser(doc, errParser, true);
        }
        [Test]
        public void TestReadDicFromNode()
        {
            string xml = "<input arg1='a1'><sub1 arg1='sa1'>subText</sub1><sub2 arg1='sa2'/></input>";
            doc.LoadXml(xml);
            Dictionary<string, string> inputdic = runner.ReadDicFromNode(doc.FirstChild, Runner.InputType.DIC);
            Assert.AreEqual(4, inputdic.Count);
            Assert.AreEqual("a1", inputdic["arg1"]);
            Assert.AreEqual("subText", inputdic["sub1"]);
            Assert.AreEqual("sa1", inputdic["sub1.arg1"]);
            Assert.AreEqual("sa2", inputdic["sub2.arg1"]);
        }
        [Test]
        public void RunFixtureParamValue()
        {
            doc.LoadXml("<param name='param1' value='some'/>");
            ResultHolder dataHolder = runner.RunFixtureParam(doc.SelectNodes("param"));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">param</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">some</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureParamArgument()
        {
            doc.LoadXml("<param name='param1' argument='user'/>");
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            argDic.Add("user", "Arne");
            runDetails = new RunnerDetails(argDic);
            runner = new Runner(true, runDetails);
            ResultHolder dataHolder = runner.RunFixtureParam(doc.SelectNodes("param"));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">param</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">argument</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">user</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Arne</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureParamFixture()
        {
            doc.LoadXml("<param name='param1' fixture='SeeFlawRunner.ExampleFixture' method='GetParamExampleMethod'/>");
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.SelectNodes("param").Item(0));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : GetParamExampleMethod : param1</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">param test</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureParamFixtureWithInput()
        {
            doc.LoadXml("<param name='param1' fixture='SeeFlawRunner.ExampleFixture' method='GetParamWithInputExampleMethod'><input first='1' second='2'/></param>");
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.SelectNodes("param").Item(0));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : GetParamWithInputExampleMethod : param1</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">first</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">second</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">param 1</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureParamTwoNodes()
        {
            doc.LoadXml("<doc><param name='param1' value='some'/><param name='param2' argument='user'/></doc>");
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            argDic.Add("user", "Arne");
            runDetails = new RunnerDetails(argDic);
            runner = new Runner(true, runDetails);
            ResultHolder dataHolder = runner.RunFixtureParam(doc.FirstChild.SelectNodes("param"));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">param</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">value</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">argument</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">read_value</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param1</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">some</td><td bgcolor=\"#CCCCFF\" class=\"input_value\" /><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\" /></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Param</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">param2</td><td bgcolor=\"#CCCCFF\" class=\"input_value\" /><td bgcolor=\"#CCCCFF\" class=\"input_value\">user</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Arne</td></tr>";
            expected += "</table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureInit()
        {
            doc.LoadXml("<init fixture='SeeFlawRunner.ExampleFixture' id='fixture_id1'/>");
            ResultHolder dataHolder = runner.RunFixtureInit(doc.SelectNodes("init"));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">init</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">fixture</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">id</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Init</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">SeeFlawRunner.ExampleFixture</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">fixture_id1</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureNoOutputExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='NoOutputExampleMethod'><input name='some'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : NoOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">some</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureSingleOutputExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Arne Anka'/><output message='Hello Arne Anka'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne Anka</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne Anka</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureSingleOutputExampleMethodDiffInOutput()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Arne Anka'/><output message='Hello Arne'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne Anka</td>";
            expected += "<td bgcolor=\"#CCFFCC\" class=\"outputvalue_unmatch\">Hello Arne</td><td bgcolor=\"#FF5555\" class=\"outputvalue_unmatch\">Hello Arne Anka</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureSingleOutputTwoExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Arne Anka'/><output message='Hello Arne Anka'/><input name='Kalle'/><output message='Hello Kalle'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time (00:00:00)</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne Anka</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne Anka</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Kalle</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Kalle</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureSingleOutputThreeExampleMethodDiffInOneOutput()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Kalle'/><output message='Hello Kalle'/><input name='Arne Anka'/><output message='Hello Arne'/><input name='Kajsa'/><output message='Hello Kajsa'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            //XmlNode resultNode = parser.GetResultAsHtml(dataHolder);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time (00:00:00)</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Kalle</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Kalle</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne Anka</td><td bgcolor=\"#CCFFCC\" class=\"outputvalue_unmatch\">Hello Arne</td><td bgcolor=\"#FF5555\" class=\"outputvalue_unmatch\">Hello Arne Anka</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Kajsa</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Kajsa</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureSingleOutputMissingKeyExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input name='Arne Anka'/><output message='Hello Arne Anka' from=''/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">from</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Arne Anka</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">Hello Arne Anka</td><td bgcolor=\"#FFCCCC\" class=\"outputvalue_missing\" colspan=\"2\" /></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputTwoLinesExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod'><input lines='2'/><output message='line1'/><output message='line2'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            //XmlNode resultNode = parser.GetResultAsHtml(dataHolder);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line1</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line2</td></tr></table>";
            expected += "</td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputThreeLinesOneMissingExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod'><input lines='2'/><output message='line3'/><output message='line1'/><output message='line2'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_missing\">Missing</td><td bgcolor=\"#FFCCCC\" class=\"outputvalue_missing\" colspan=\"2\">line3</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line1</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line2</td></tr></table>";
            expected += "</td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputThreeLinesOneMissingOutputKeyMissingExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod'><input lines='2'/><output message='line3' missing='no output'/><output message='line1' missing='some'/><output message='line2' missing=''/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">missing</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_missing\">Missing</td><td bgcolor=\"#FFCCCC\" class=\"outputvalue_missing\" colspan=\"2\">line3</td><td bgcolor=\"#FFCCCC\" class=\"outputvalue_missing\" colspan=\"2\">no output</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line1</td><td bgcolor=\"#FFCCCC\" class=\"outputvalue_missing\" colspan=\"2\">some</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line2</td><td bgcolor=\"#FFCCCC\" class=\"outputvalue_missing\" colspan=\"2\" /></tr></table>";
            expected += "</td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputTwoLinesOneSurplusExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod'><input lines='3'/><output message='line1'/><output message='line2'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">3</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line1</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">line2</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_surplus\">Surplus</td><td bgcolor=\"lightgreen\" class=\"outputvalue_surplus\" colspan=\"2\">line3</td></tr></table>";
            expected += "</td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputKeysTwoSurplusRowsExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod'><input lines='2'/><outputkeys message=''/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_failed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_failed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_failed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_surplus\">Surplus</td><td bgcolor=\"lightgreen\" class=\"outputvalue_surplus\" colspan=\"2\">line1</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_surplus\">Surplus</td><td bgcolor=\"lightgreen\" class=\"outputvalue_surplus\" colspan=\"2\">line2</td></tr></table>";
            expected += "</td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputKeysAllowTwoSurplusRowsExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputExampleMethod' allowsurplus='1'><input lines='2'/><outputkeys message=''/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Surplus</td><td bgcolor=\"lightgreen\" class=\"outputvalue_surplus\" colspan=\"2\">line1</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Surplus</td><td bgcolor=\"lightgreen\" class=\"outputvalue_surplus\" colspan=\"2\">line2</td></tr>";
            expected += "</table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureCaughtException()
        {
            ResultHolder dataHolder = new ResultHolder("Testing exception", null, false);
            dataHolder.AddSingleRow(null, null, null, new Exception("Exception message"));
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_error\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"#FF5555\" class=\"title\">Testing exception</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_error\">Type</td><td class=\"time_value\">Time</td></tr>";
            expected += "<tr><td bgcolor=\"#FF0000\" class=\"statusvalue_error\"><a href=\"#Exception1\">Exception1</a></td><td class=\"time_value\">(00:00:00)</td></tr>";
            expected += "</table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureDifferentObjectTypes()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputDifferentObjectTypesExampleMethod'><input/><output int='12345' double='12345.12345'/></call>");
            Dictionary<string, Object> dic = new Dictionary<string, Object>();
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputDifferentObjectTypesExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">int</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">double</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">12345</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">12345.12345</td></tr>";
            expected += "</table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureObjectTypesDoubleWithTolerance()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputDifferentObjectTypesExampleMethod'><tolerance double='0.01'/><input/><output double='12345.12'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputDifferentObjectTypesExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">double within(0.01)</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">12345.12</td></tr>";
            expected += "</table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureSingleOutputXmlInputExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputXmlInputExampleMethod'><input name='Test'><row/><row/><col/></input><output col='1' row='2'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : SingleOutputXmlInputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">name</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">col</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">row</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">Test</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">1</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">2</td></tr></table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
        [Test]
        public void RunFixtureMultiOutputXmlInputExampleMethod()
        {
            doc.LoadXml("<call fixture='SeeFlawRunner.ExampleFixture' method='MultiOutputXmlInputExampleMethod'><input lines='2'><row message='first'/><row message='second'/></input><output message='first'/><output message='second'/></call>");
            ResultHolder dataHolder = runner.RunFixtureMethodCall(doc.FirstChild);
            XmlNode resultNode = parser.GetResultAsXml(dataHolder);
            System.Console.WriteLine(resultNode.OuterXml);
            XmlNode htmlNode = HtmlTransformer.ConvertToHtml(resultNode.OwnerDocument, resultNode);
            string expected = "<table border=\"0\" cellpadding=\"0\" class=\"step_passed\"><tr><td><table width=\"100%\">";
            expected += "<tr><th align=\"left\" bgcolor=\"lightgreen\" class=\"title\">SeeFlawRunner.ExampleFixture : MultiOutputXmlInputExampleMethod</th></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td class=\"time_value\">Time</td><td bgcolor=\"#AAAAFF\" class=\"input_title\">lines</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td class=\"time_value\">(00:00:00)</td><td bgcolor=\"#CCCCFF\" class=\"input_value\">2</td></tr></table></td></tr>";
            expected += "<tr><td><table border=\"1\" cellpadding=\"3\" cellspacing=\"0\" width=\"100%\">";
            expected += "<tr><td class=\"status_passed\">Type</td><td bgcolor=\"#EEAAFF\" class=\"output_title\" colspan=\"2\">message</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">first</td></tr>";
            expected += "<tr><td bgcolor=\"lightgreen\" class=\"statusvalue_passed\">Match</td><td bgcolor=\"lightgreen\" class=\"outputvalue_passed\" colspan=\"2\">second</td></tr>";
            expected += "</table></td></tr></table>";
            System.Console.WriteLine(htmlNode.OuterXml);
            NUnit.Framework.Assert.AreEqual(expected, htmlNode.OuterXml);
        }
    }
}
