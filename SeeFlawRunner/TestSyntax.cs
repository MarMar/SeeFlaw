using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Reflection;
using NUnit.Framework;

namespace SeeFlawRunner
{
    public class ValidatedTest
    {
        public XmlNode seeFlawNode;
        public Dictionary<string, XmlNode> loadDic = new Dictionary<string, XmlNode>();
        public ValidatedTest(XmlNode testNode)
        {
            seeFlawNode = testNode;
        }
    }

    public class TestSyntax
    {
        private RunnerDetails sfRunDetails = new RunnerDetails(new Dictionary<string, string>());
        private System.IO.DirectoryInfo testDirInfo;
        private XmlNode testResultSaveInput = null;

        public TestSyntax(string testFile, RunnerDetails runDetails, XmlNode saveInput)
        {
            sfRunDetails = runDetails;
            testResultSaveInput = saveInput;
            testDirInfo = System.IO.Directory.GetParent(testFile);
        }
        
        public static ValidatedTest ValidateDocument(XmlDocument testDoc, string testFile, RunnerDetails runDetails, XmlNode saveInput)
        {
            TestSyntax validator = new TestSyntax(testFile, runDetails, saveInput);
            ValidatedTest validTest = new ValidatedTest(GetDocumentNode(testDoc));
            
            validator.CheckXML(validTest);
            return validTest;
        }

        public static XmlNode GetDocumentNode(XmlDocument testDoc)
        {
            XmlNodeList nodeList = testDoc.GetElementsByTagName("seeflaw");
            if (nodeList.Count == 0)
            {
                throw new Exception("No seeflaw root node in xml file");
            }
            if (nodeList.Count > 1)
            {
                throw new Exception("More than one seeflaw node in xml file");
            }
            return nodeList.Item(0);
        }

        public void RaiseIfNull(System.Object obj, string message)
        {
            if (obj == null)
            {
                throw new Exception(message);
            }
        }

        public void CheckXML(ValidatedTest testInValidation)
        {
            List<string> loadFileNames = new List<string>();
            CheckXML(testInValidation.seeFlawNode, loadFileNames);
            foreach (string loadName in loadFileNames)
            {
                string loadPath = loadName;
                if (!System.IO.File.Exists(loadPath))
                {
                    if (testDirInfo.Exists)
                    {
                        loadPath = System.IO.Path.Combine(testDirInfo.FullName, loadName);
                    }
                    else
                    {
                        throw new Exception("Can not find load file " + loadName);
                    }
                }
                XmlDocument loadDoc = new XmlDocument();
                loadDoc.Load(loadPath);
                XmlNode loadNode = GetDocumentNode(loadDoc);
                CheckXML(loadNode, null);
                testInValidation.loadDic.Add(loadName, loadNode);
            }
        }

        public void CheckXML(XmlNode seeFlawNode, List<string> loadNames)
        {
            Dictionary<string, Object> fixtureDic = new Dictionary<string, Object>();
            List<string> paramList = new List<string>();
            foreach (XmlNode childNode in seeFlawNode.ChildNodes)
            {
                switch (childNode.LocalName)
                {
                    case SeeFlaw.ActionType.TEXT:
                        break;
                    case SeeFlaw.ActionType.PARAM:
                        XmlAttribute name = SeeFlaw.GetAttributeByName(childNode, "name");
                        if (paramList.Contains(name.Value))
                        {
                            throw new Exception("More than one param node with same name attribute " + name.Value + ".");
                        }
                        else
                        {
                            CheckParameter(childNode, fixtureDic);
                            paramList.Add(name.Value);
                        }
                        break;
                    case SeeFlaw.ActionType.INIT:
                        XmlAttribute fixture = SeeFlaw.GetAttributeByName(childNode, "fixture");
                        XmlAttribute id = SeeFlaw.GetAttributeByName(childNode, "id");
                        Object test = sfRunDetails.GetFixture(fixture.Value);
                        RaiseIfNull(test, "Invalid fixture " + fixture.Value + " in init node.");
                        if (fixtureDic.ContainsKey(id.Value))
                        {
                            throw new Exception("More than one init fixture with id " + id.Value);
                        }
                        fixtureDic.Add(id.Value, test);
                        break;
                    case SeeFlaw.ActionType.LOAD:
                        XmlAttribute loadFile = SeeFlaw.GetAttributeByName(childNode, "file");
                        if (loadNames == null)
                        {
                            throw new Exception("Load file from within a load file not allowed. load file: " + loadFile.Value);
                        }
                        if (loadNames.Contains(loadFile.Value))
                        {
                            throw new Exception("More than one load node with same file attribute " + loadFile.Value + ".");
                        }
                        loadNames.Add(loadFile.Value);
                        break;
                    case SeeFlaw.ActionType.CALL:
                        CheckCallNode(childNode, fixtureDic, null);
                        break;
                    case SeeFlaw.ActionType.SAVE:
                        CheckSaveNode(childNode, fixtureDic, null);
                        break;
                    case SeeFlaw.ActionType.ASYNC:
                        foreach (XmlNode fixtureNode in childNode.ChildNodes)
                        {
                            if (fixtureNode.LocalName == "fixture")
                            {
                                XmlAttribute fixId = SeeFlaw.GetAttributeByName(fixtureNode, "id");
                                Object asyncFixture = null;
                                if (fixtureDic.ContainsKey(fixId.Value))
                                {
                                    fixtureDic.TryGetValue(fixId.Value, out asyncFixture);
                                }
                                RaiseIfNull(asyncFixture, "Invalid id '" + fixId.Value + "' in async fixture node.");
                                foreach (XmlNode callNode in fixtureNode.ChildNodes)
                                {
                                    if (fixtureNode.LocalName == "fixture")
                                    {
                                        CheckCallNode(callNode, null, asyncFixture);
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        // TODO error message
                        break;
                }
            }
        }

        public void CheckParameter(XmlNode argNode, Dictionary<string, Object> dic)
        {
            string val = "";
            XmlAttribute name = SeeFlaw.GetAttributeByName(argNode, "name", false);
            XmlAttribute value = SeeFlaw.GetAttributeByName(argNode, "value", true);
            XmlAttribute argument = SeeFlaw.GetAttributeByName(argNode, "argument", true);
            XmlAttribute fixture = SeeFlaw.GetAttributeByName(argNode, "fixture", true);
            XmlAttribute method = SeeFlaw.GetAttributeByName(argNode, "method", true);
            if (value == null && argument == null && fixture == null)
            {
                throw new Exception("No argument or value attribute in param node.");
            }
            if (argument != null)
            {
                val = sfRunDetails.GetArgument(argument.Value);
                if (val == "")
                {
                    if (value == null)
                    {
                        throw new Exception("Argument " + argument.Value + " in param node was not found in argument list.");
                    }
                }
            }
            if (fixture != null)
            {
                Object paramFixture = GetCheckedFixture(argNode, dic);
                string errorStart = "Fixture " + fixture.Value + " in param node";
                if (paramFixture == null)
                {
                    throw new Exception(errorStart + " could not be found");
                }
                if (method == null)
                {
                    throw new Exception("Missing method attribute for fixture param " + name);
                }
                MethodInfo methodInfo = paramFixture.GetType().GetMethod(method.Value);
                if (methodInfo == null)
                {
                    throw new Exception("No such method '" + method.Value + "' found for fixture " + fixture.Value);
                }
                if (methodInfo.ReturnType != typeof(string))
                {
                    throw new Exception(errorStart + " has wrong return type, must be string");
                }
                ParameterInfo[] infos = methodInfo.GetParameters();
                if (infos.Length > 2)
                {
                    throw new Exception(errorStart + " takes to many inparameters");
                }
                if (infos.Length == 1 &&
                    infos[0].ParameterType != typeof(XmlNode) &&
                    infos[0].ParameterType != typeof(Dictionary<string, string>))
                {
                    throw new Exception(errorStart + " has wrong type of parameter");
                }
            }
        }

        public void CheckCallNode(XmlNode callNode, Dictionary<string, Object> dic, Object callFixture)
        {
            XmlAttribute methodAttr = SeeFlaw.GetAttributeByName(callNode, "method");
            if (callFixture == null)
            {
                callFixture = GetCheckedFixture(callNode, dic);
            }
            MethodInfo methodInfo = callFixture.GetType().GetMethod(methodAttr.Value);
            if (methodInfo == null)
            {
                throw new Exception("Invalid method " + methodAttr.Value + " in call node.");
            }
            ParameterInfo[] paramInfos = methodInfo.GetParameters();
            if (paramInfos.Length > 2)
            {
                throw new Exception("Method " + methodAttr.Value + " takes more than two input parameter.");
            }
            if (paramInfos.Length > 0)
            {
                if (paramInfos[0].ParameterType != typeof(Dictionary<string, string>) &&
                    paramInfos[0].ParameterType != typeof(XmlNode))
                {
                    throw new Exception("Method " + methodAttr.Value + " first parameter is of wrong type.");
                }
            }
            if (paramInfos.Length == 2 && paramInfos[1].ParameterType != typeof(List<string>))
            {
                throw new Exception("Method " + methodAttr.Value + " second parameter is of wrong type.");
            }

            CheckCallReturnType(callNode, methodInfo, callFixture, methodAttr.Value, paramInfos.Length > 0);
        }

        private void CheckCallReturnType(XmlNode callNode, MethodInfo methodInfo, Object callFixture, string methodName, bool voidRequireInput)
        {
            if (methodInfo.ReturnType == typeof(void))
            {
                bool inputFound = false;
                foreach (XmlNode callChildNode in callNode.ChildNodes)
                {
                    if (callChildNode.Name == "output")
                    {
                        throw new Exception("No output node allowed in " + callFixture.ToString() + " : " + methodInfo.Name);
                    } 
                    if (callChildNode.LocalName == "input")
                    {
                        inputFound = true;
                    }
                }
                if (voidRequireInput && !inputFound)
                {
                    throw new Exception("Missing input node in " + callFixture.ToString() + " : " + methodInfo.Name);
                }
            }
            else if (methodInfo.ReturnType == typeof(Dictionary<string, object>))
            {
                bool inputFound = false;
                bool nextOutput = false;
                foreach (XmlNode callChildNode in callNode.ChildNodes)
                {
                    if (callChildNode.LocalName == "output")
                    {
                        if (!nextOutput)
                        {
                            throw new Exception("output node without input node in " + callFixture.ToString() + " : " + methodInfo.Name);
                        }
                        nextOutput = false;
                    }
                    if (callChildNode.LocalName == "input")
                    {
                        inputFound = true;
                        if (nextOutput)
                        {
                            throw new Exception("Multiple input nodes not allowed in " + callFixture.ToString() + " : " + methodInfo.Name);
                        }
                        nextOutput = true;
                    }
                }
                if (!inputFound)
                {
                    throw new Exception("No input node in " + callFixture.ToString() + " : " + methodInfo.Name);
                }
                if (nextOutput)
                {
                    throw new Exception("No output node after last input node in " + callFixture.ToString() + " : " + methodInfo.Name);
                }
            }
            else if (methodInfo.ReturnType == typeof(List<Dictionary<string, object>>))
            {
                bool inputFound = false;
                bool outputFound = false;
                foreach (XmlNode callChildNode in callNode.ChildNodes)
                {
                    if (callChildNode.LocalName == "output")
                    {
                        if (!inputFound)
                        {
                            throw new Exception("No input node before output nodes in " + callFixture.ToString() + " : " + methodInfo.Name);
                        }
                    }
                    if (callChildNode.LocalName == "input")
                    {
                        if (inputFound)
                        {
                            throw new Exception("Multiple input nodes not allowed in " + callFixture.ToString() + " : " + methodInfo.Name);
                        }
                        inputFound = true;
                    }
                }
                if (!inputFound)
                {
                    throw new Exception("No input node in " + callFixture.ToString() + " : " + methodInfo.Name);
                }
                if (outputFound)
                {
                    throw new Exception("No output node in " + callFixture.ToString() + " : " + methodInfo.Name);
                }
            }
            else
            {
                throw new Exception("Wrong return type of method " + methodName + " in " + callFixture.ToString() + ".");
            }
        }

        public Object GetCheckedFixture(XmlNode node, Dictionary<string, object> fixDic)
        {
            Object callFixture = null;
            XmlAttribute fixtureAttr = SeeFlaw.GetAttributeByName(node, "fixture");
            if (fixDic.ContainsKey(fixtureAttr.Value))
            {
                fixDic.TryGetValue(fixtureAttr.Value, out callFixture);
            }
            else
            {
                callFixture = sfRunDetails.GetFixture(fixtureAttr.Value);
            }
            RaiseIfNull(callFixture, "Invalid fixture " + fixtureAttr.Value + " in " + node.Name + " node.");
            return callFixture;
        }

        public void CheckSaveNode(XmlNode saveNode, Dictionary<string, Object> dic, Object callFixture)
        {
            if (testResultSaveInput == null)
            {
                throw new Exception("No result data to save");
            }
            XmlAttribute methodAttr = SeeFlaw.GetAttributeByName(saveNode, "method");
            if (callFixture == null)
            {
                callFixture = GetCheckedFixture(saveNode, dic);
            }
            MethodInfo methodInfo = callFixture.GetType().GetMethod(methodAttr.Value);
            if (methodInfo == null)
            {
                throw new Exception("Invalid method " + methodAttr.Value + " in save node.");
            }
            ParameterInfo[] paramInfos = methodInfo.GetParameters();
            if (paramInfos.Length == 0)
            {
                throw new Exception("Method " + methodAttr.Value + " takes no input parameter.");
            }
            if (paramInfos.Length > 2)
            {
                throw new Exception("Method " + methodAttr.Value + " takes more than two input parameter.");
            }
            if (paramInfos[0].ParameterType != typeof(XmlNode))
            {
                throw new Exception("Method " + methodAttr.Value + " parameter is of wrong type. Must be XmlNode");
            }
            if (paramInfos.Length == 2 && paramInfos[1].ParameterType != typeof(List<string>))
            {
                throw new Exception("Method " + methodAttr.Value + " second parameter is of wrong type.");
            }

            CheckCallReturnType(saveNode, methodInfo, callFixture, methodAttr.Value, false);
        }
    }

    [TestFixture]
    public class TestSyntaxTest
    {
        public TestSyntax validator;
        public System.Xml.XmlDocument doc;

        [SetUp]
        public void SetUpTest()
        {
            doc = new System.Xml.XmlDocument();
        }

        private void CheckXmlHelper(string xml, string facit)
        {
            doc.LoadXml(xml);
            try
            {
                TestSyntax.ValidateDocument(doc, "testfile", new RunnerDetails(new Dictionary<string, string>()), null);
            }
            catch(Exception ex)
            {
                NUnit.Framework.Assert.AreEqual(facit, ex.Message);
                return;
            }
            NUnit.Framework.Assert.Fail("No exception");
        }
        [Test]
        public void CheckXmlNoNameInParam()
        {
            string xml = "<seeflaw><param value='tp1'/></seeflaw>";
            CheckXmlHelper(xml, "name attribute missing in param node.");
        }
        [Test]
        public void CheckXmlNoValueOrArgumentInParam()
        {
            string xml = "<seeflaw><param name='tp1'/></seeflaw>";
            CheckXmlHelper(xml, "No argument or value attribute in param node.");
        }
        [Test]
        public void CheckXmlNoNoArgToArgumentInParam()
        {
            string xml = "<seeflaw><param name='tp1' argument='user'/></seeflaw>";
            CheckXmlHelper(xml, "Argument user in param node was not found in argument list.");
        }
        [Test]
        public void CheckXmlNoFixtureInInit()
        {
            string xml = "<seeflaw><init id='tp1'/></seeflaw>";
            CheckXmlHelper(xml, "fixture attribute missing in init node.");
        }
        [Test]
        public void CheckXmlNoIdInInit()
        {
            string xml = "<seeflaw><init fixture='printFixture'/></seeflaw>";
            CheckXmlHelper(xml, "id attribute missing in init node.");
        }
        [Test]
        public void CheckXmlInvalidFixtureInInit()
        {
            string xml = "<seeflaw><init fixture='invalidFixture' id='tp1'/></seeflaw>";
            CheckXmlHelper(xml, "fixture 'invalidFixture' is given in bad format, should be Assembly.Class");
        }
        [Test]
        public void CheckXmlNoFixtureInCall()
        {
            string xml = "<seeflaw><call method='some'/></seeflaw>";
            CheckXmlHelper(xml, "fixture attribute missing in call node.");
        }
        [Test]
        public void CheckXmlNoMethodInCall()
        {
            string xml = "<seeflaw><call fixture='test'/></seeflaw>";
            CheckXmlHelper(xml, "method attribute missing in call node.");
        }
        [Test]
        public void CheckXmlInvalidFixtureInCall()
        {
            string xml = "<seeflaw><call fixture='invalidFixture' method='some'/></seeflaw>";
            CheckXmlHelper(xml, "fixture 'invalidFixture' is given in bad format, should be Assembly.Class");
        }
        [Test]
        public void CheckXmlInvalidMethodInCall()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='some'/></seeflaw>";
            CheckXmlHelper(xml, "Invalid method some in call node.");
        }
        [Test]
        public void CheckXmlNoInputInSingleRowCall()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'/></seeflaw>";
            CheckXmlHelper(xml, "No input node in SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod");
        }
        [Test]
        public void CheckXmlNoOutputInSingleRowCall()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input/></call></seeflaw>";
            CheckXmlHelper(xml, "No output node after last input node in SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod");
        }
        [Test]
        public void CheckXmlWrongInputInSingleRowCall()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input/><output/><input/><input/><output/></call></seeflaw>";
            CheckXmlHelper(xml, "Multiple input nodes not allowed in SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod");
        }
        [Test]
        public void CheckXmlWrongOutputInSingleRowCall()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input/><output/><output/><input/><output/></call></seeflaw>";
            CheckXmlHelper(xml, "output node without input node in SeeFlawRunner.ExampleFixture : SingleOutputExampleMethod");
        }
        [Test]
        public void CheckXmlFixtureInAsyncCall()
        {
            string xml = "<seeflaw><async><fixture id='ExampleFixture'><call method='SingleOutputExampleMethod'><input/><output/></call></fixture></async></seeflaw>";
            CheckXmlHelper(xml, "Invalid id 'ExampleFixture' in async fixture node.");
        }
        [Test]
        public void CheckXmlParamNodeWithSameName()
        {
            string xml = "<seeflaw><param name='param1' value='some'/><param name='param1' value='test'/></seeflaw>";
            CheckXmlHelper(xml, "More than one param node with same name attribute param1.");
        }
        [Test]
        public void CheckXmlCorrectSingleRowCall()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.ExampleFixture' method='SingleOutputExampleMethod'><input/><output/><input/><output/></call></seeflaw>";
            doc.LoadXml(xml);
            TestSyntax.ValidateDocument(doc, "testfile", new RunnerDetails(new Dictionary<string, string>()), null);
        }
        [Test]
        public void CheckXmlOnlyFixtureIdInAsyncCall()
        {
            string xml = "<seeflaw><init fixture='SeeFlawRunner.ExampleFixture' id='id1'/><async><fixture id='id1'><call method='SingleOutputExampleMethod'><input name='t'/><output/></call></fixture></async></seeflaw>";
            doc.LoadXml(xml);
            TestSyntax.ValidateDocument(doc, "testfile", new RunnerDetails(new Dictionary<string, string>()), null);
        }
        public Dictionary<string, object> CallThreeParameters(XmlNode node, List<string> list, int third)
        {
            return new Dictionary<string, object>();
        }
        [Test]
        public void CheckXmlCallMethodThreeParameters()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.TestSyntaxTest' method='CallThreeParameters'><input/></call></seeflaw>";
            CheckXmlHelper(xml, "Method CallThreeParameters takes more than two input parameter.");
        }
        public void CallWrongFirstParameter(string test) { }
        [Test]
        public void CheckXmlCallMethodWrongFirstParameter()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.TestSyntaxTest' method='CallWrongFirstParameter'><input/></call></seeflaw>";
            CheckXmlHelper(xml, "Method CallWrongFirstParameter first parameter is of wrong type.");
        }
        public Dictionary<string, object> CallWrongSecondParameter(XmlNode node, int second)
        {
            return new Dictionary<string, object>();
        }
        [Test]
        public void CheckXmlCallMethodWrongSecondParameter()
        {
            string xml = "<seeflaw><call fixture='SeeFlawRunner.TestSyntaxTest' method='CallWrongSecondParameter'><input/></call></seeflaw>";
            CheckXmlHelper(xml, "Method CallWrongSecondParameter second parameter is of wrong type.");
        }
    }
}
