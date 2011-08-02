using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SeeFlawRunner
{
    public class HtmlTransformer
    {
        private static string passedColor = "lightgreen";//#99EE99
        private static string failedColor = "#FF5555";
        private static string errorColor = "#FF0000";
        private static string inputTitleColor = "#AAAAFF";
        private static string inputColor = "#CCCCFF";
        private static string outputTitleColor = "#EEAAFF";
        private static string outputFailColor = "#FF9999";
        private static string outputExpectedColor = "#CCFFCC";
        private static string outputUnMatchColor = "#FF5555";
        private static string outputMissingColor = "#FFCCCC";
        private static string unprocessedColor = "#999999";
        private static string outputUnprocessedColor = "#BBBBBB";
        private static string loadTableColor = "#DDCC88";
        private static string asyncTableColor = "#FFBB00";
        private static string textColor = "yellow";

        protected static string GetTitleColor(string type)
        {
            switch (type)
            {
                case ResultParser.ResultType.PASS: return passedColor;
                default: return failedColor;
            }
        }

        protected static string GetMatchColor(string type)
        {
            switch (type)
            {
                case ResultParser.ResultType.PASS: return passedColor;
                case ResultParser.ResultType.UNPROCESSED: return unprocessedColor;
                default: return errorColor;
            }
        }

        protected static string GetOutputColor(string type)
        {
            switch (type)
            {
                case ResultParser.ResultType.PASS: return passedColor;
                case ResultParser.ResultType.UNPROCESSED: return outputUnprocessedColor;
                case ResultParser.ResultType.SURPLUS: return passedColor;
                case ResultParser.ResultType.UNMATCH: return outputUnMatchColor;
                case ResultParser.ResultType.MISSING: return outputMissingColor;
                default: return outputFailColor;
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
                if (attr.Name == name)
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

        public static XmlNode CreateOuterTable(XmlDocument doc, string name, string color)
        {
            XmlNode outerTable = BuildMainTable(doc, "1", "");
            XmlNode titleTable = BuildTitleTable(doc, name, color);
            AppendInnerTable(doc, outerTable, titleTable);
            return outerTable;
        }
        
        public static XmlNode CreateAsyncFixtureTable(XmlDocument doc, string id)
        {
            XmlNode asyncTable = BuildMainTable(doc, "0", "");
            XmlNode titleTable = BuildTitleTable(doc, "fixture id: " + id, "");
            AppendInnerTable(doc, asyncTable, titleTable);
            return asyncTable;
        }

        public static XmlNode BuildMainTable(XmlDocument doc, string border, string style)
        {
            XmlElement table = doc.CreateElement("table");
            table.SetAttribute("border", border);
            table.SetAttribute("cellpadding", "0");
            if (style != "")
            {
                table.SetAttribute("class", style);
            }
            return table;
        }

        public static XmlNode BuildTitleTable(XmlDocument doc, string title, string bgcolor)
        {
            XmlElement titleTable = doc.CreateElement("table");
            titleTable.SetAttribute("width", "100%");
            XmlElement titleElement = doc.CreateElement("th");
            titleElement.SetAttribute("align", "left");
            titleElement.InnerText = title;
            XmlElement titleRow = doc.CreateElement("tr");
            titleRow.AppendChild(titleElement);
            titleTable.AppendChild(titleRow);
            if (bgcolor != "")
            {
                titleElement.SetAttribute("bgcolor", bgcolor);
            }
            titleElement.SetAttribute("class", "title");
            return titleTable;
        }

        public static void AppendInnerTable(XmlDocument doc, XmlNode parentTable, XmlNode childTable)
        {
            AppendInnerTable(doc, parentTable, childTable, false);
        }

        public static void AppendInnerTable(XmlDocument doc, XmlNode parentTable, XmlNode childTable, bool addBr)
        {
            if (childTable == null)
            {
                return;
            }
            XmlElement tableRow = doc.CreateElement("tr");
            XmlElement tableData = doc.CreateElement("td");
            tableData.AppendChild(childTable);
            if (addBr)
            {
                tableData.AppendChild(doc.CreateElement("br"));
            }
            tableRow.AppendChild(tableData);
            parentTable.AppendChild(tableRow);
        }

        public static XmlElement BuildResultTable(XmlDocument doc)
        {
            return BuildResultTable(doc, "1");
        }

        public static XmlElement BuildResultTable(XmlDocument doc, string border)
        {
            XmlElement resultTable = doc.CreateElement("table");
            resultTable.SetAttribute("border", border);
            resultTable.SetAttribute("cellpadding", "3");
            resultTable.SetAttribute("cellspacing", "0");
            resultTable.SetAttribute("width", "100%");
            return resultTable;
        }

        public static XmlNode ConvertToHtml(XmlDocument doc, XmlNode resultNode)
        {
            if (resultNode.LocalName.EndsWith("result"))
            {
                return HtmlTransformer.ConvertCallNodeToHtml(doc, resultNode);
            }
            if (resultNode.LocalName == "text")
            {
                return HtmlTransformer.ConvertTextNodeToHtml(doc, resultNode);
            }
            if (resultNode.LocalName == "load")
            {
                XmlAttribute loadFileAttr = (XmlAttribute) resultNode.Attributes.GetNamedItem("file");
                string loadName = "load : ";
                if (loadFileAttr != null)
                {
                    loadName += loadFileAttr.Value;
                }
                XmlNode loadTable = CreateOuterTable(doc, loadName, loadTableColor);
                foreach (XmlNode loadResultNode in resultNode.ChildNodes)
                {
                    AppendInnerTable(doc, loadTable, ConvertToHtml(doc, loadResultNode), true);
                }
                return loadTable;
            }
            if (resultNode.LocalName == "async")
            {
                XmlNode asyncTable = CreateOuterTable(doc, "async", asyncTableColor);
                foreach (XmlNode fixtureNode in resultNode.ChildNodes)
                {
                    if (fixtureNode.Name == "fixture")
                    {
                        XmlAttribute idAttr = GetAttributeByName(fixtureNode, "id");
                        XmlNode asyncFixtureTable = CreateAsyncFixtureTable(doc, idAttr.Value);
                        foreach(XmlNode callNode in fixtureNode.ChildNodes)
                        {
                            if (callNode.Name == "callresult")
                            {
                                AppendInnerTable(doc, asyncFixtureTable, ConvertCallNodeToHtml(doc, callNode), true);
                            }
                        }
                        AppendInnerTable(doc, asyncTable, asyncFixtureTable);
                    }
                }
                return asyncTable;
            }
            return null;
        }

        public static XmlNode ConvertTextNodeToHtml(XmlDocument doc, XmlNode textNode)
        {
            string style = "step_text";
            XmlNode mainTable = BuildMainTable(doc, "1", style);

            XmlElement titleTable = doc.CreateElement("table");
            titleTable.SetAttribute("width", "100%");
            titleTable.SetAttribute("bgcolor", textColor);
            
            foreach (XmlNode rowNode in textNode.ChildNodes)
            {
                XmlElement titleElement = doc.CreateElement("th");
                titleElement.SetAttribute("align", "left");
                titleElement.InnerText = rowNode.InnerText;
                titleElement.SetAttribute("class", "text");
                XmlElement titleRow = doc.CreateElement("tr");
                titleRow.AppendChild(titleElement);
                titleTable.AppendChild(titleRow);
            }

            AppendInnerTable(doc, mainTable, titleTable);
            return mainTable;
        }

        public static XmlNode ConvertCallNodeToHtml(XmlDocument doc, XmlNode callResultNode)
        {
            XmlAttribute statusAttr = GetAttributeByName(callResultNode, "status");
            XmlAttribute fixtureAttr = GetAttributeByName(callResultNode, "fixture");
            XmlAttribute methodAttr = GetAttributeByName(callResultNode, "method");
            XmlAttribute typeAttr = GetAttributeByName(callResultNode, "type");
            XmlAttribute paramNameAttr = GetAttributeByName(callResultNode, "name", true);

            string style = "step_" + statusAttr.Value;
            XmlNode mainTable = BuildMainTable(doc, "0", style);

            string bgcolor = GetTitleColor(statusAttr.Value);
            string title = fixtureAttr.Value;
            if (methodAttr.Value != "")
            {
                title += " : " + methodAttr.Value;
            }
            if (paramNameAttr != null && paramNameAttr.Value != "")
            {
                title += " : " + paramNameAttr.Value;
            }
            AppendInnerTable(doc, mainTable, BuildTitleTable(doc, title, bgcolor));

            if (typeAttr.Value == "single")
            {
                XmlNode singleResultTable = BuildRowTable(doc, callResultNode, statusAttr.Value, true);
                AppendInnerTable(doc, mainTable, singleResultTable);
            }
            if (typeAttr.Value == "multi")
            {
                XmlNode multiInputTable = BuildRowTable(doc, callResultNode, statusAttr.Value, false);
                XmlNode multiResultTable = BuildMultiRowTable(doc, callResultNode, statusAttr.Value);
                AppendInnerTable(doc, mainTable, multiInputTable);
                AppendInnerTable(doc, mainTable, multiResultTable);
            }

            return mainTable;
        }

        public static XmlElement BuildRowTable(XmlDocument doc, XmlNode callResultNode, string resultType, bool useOutput)
        {
            List<string> inputKeys = GetInputKeys(callResultNode);
            Dictionary<string, string> toleranceDic = new Dictionary<string, string>();
            List<string> outputKeys = GetOutputKeys(callResultNode, toleranceDic);
            List<XmlNode> resultNodes = GetChildNodesByName(callResultNode, "result");
            
            XmlElement rowResultTable = doc.CreateElement("table");
            rowResultTable.SetAttribute("border", "1");
            rowResultTable.SetAttribute("cellpadding", "3");
            rowResultTable.SetAttribute("cellspacing", "0");
            rowResultTable.SetAttribute("width", "100%");

            XmlElement titleRow = doc.CreateElement("tr");
            string titleStatus = "status_" + resultType;
            XmlElement typeData = CreateDataElement(doc, "Type", "", titleStatus);//or status_passed
            typeData.SetAttribute("class", titleStatus);
            titleRow.AppendChild(typeData);

            XmlAttribute timeAttr = GetAttributeByName(callResultNode, "time", true);
            if (timeAttr != null && timeAttr.Value != "")
            {
                string time = "Time";
                if (resultNodes.Count > 1)
                {
                    time = "Time " + timeAttr.Value;
                }
                XmlElement timeData = CreateDataElement(doc, time, "", "time_value");
                titleRow.AppendChild(timeData);
            }
            foreach (string inputKey in inputKeys)
            {
                XmlElement inputDataKey = CreateDataElement(doc, inputKey, inputTitleColor, "input_title");
                titleRow.AppendChild(inputDataKey);
            }
            if (useOutput)
            {
                foreach (string outputKey in outputKeys)
                {
                    string outputTitle = AddWithinTolerance(outputKey, toleranceDic);
                    XmlElement outputDataKey = CreateDataElement(doc, outputTitle, outputTitleColor, "output_title");
                    outputDataKey.SetAttribute("colspan", "2");
                    titleRow.AppendChild(outputDataKey);
                }
            }
            rowResultTable.AppendChild(titleRow);

            string matchStr = "Match";
            if (callResultNode.Name == "initresult")
            {
                matchStr = "Init";
            }
            if (callResultNode.Name == "paramresult")
            {
                matchStr = "Param";
            }
            foreach (XmlNode rowResultNode in resultNodes)
            {
                Dictionary<string, string> inputDic = GetInputAsDic(rowResultNode);
                XmlAttribute statusAttr = GetAttributeByName(rowResultNode, "status");
                string status = "statusvalue_" + statusAttr.Value;
                string matchColor = GetMatchColor(statusAttr.Value);
                XmlElement resultRow = doc.CreateElement("tr");
                XmlElement matchData = CreateDataElement(doc, matchStr, matchColor, status);
                resultRow.AppendChild(matchData);

                XmlAttribute rowTimeAttr = GetAttributeByName(rowResultNode, "time", true);
                if (rowTimeAttr != null && rowTimeAttr.Value != "")
                {
                    XmlElement timeValueData = CreateDataElement(doc, rowTimeAttr.Value, "", "time_value");
                    resultRow.AppendChild(timeValueData);
                }

                foreach (string inputKey in inputKeys)
                {
                    string input = GetInputData(inputKey, inputDic);
                    XmlElement inputData = CreateDataElement(doc, input, inputColor, "input_value");
                    resultRow.AppendChild(inputData);
                }
                if (statusAttr.Value == ResultParser.ResultType.ERROR)
                {
                    matchData.InnerXml = "";
                    XmlNode rowErrorNode = GetChildNodeByName(rowResultNode, "error");
                    XmlAttribute errAttr = GetAttributeByName(rowErrorNode, "name");
                    
                    XmlElement arefNode = doc.CreateElement("a");
                    arefNode.SetAttribute("href", "#" + errAttr.Value);
                    arefNode.InnerXml = errAttr.Value;
                    matchData.AppendChild(arefNode);

                    XmlNode rowOutputNode = GetChildNodeByName(rowResultNode, "output");
                    if (useOutput && rowOutputNode != null)
                    {
                        foreach (XmlNode outputNode in rowOutputNode.ChildNodes)
                        {
                            XmlAttribute outAttr = GetAttributeByName(outputNode, "status");
                            string outErrColor = GetOutputColor(outAttr.Value);
                            string outErrStatus = "outputvalue_" + outAttr.Value;
                            XmlAttribute outExpectedAttr = GetAttributeByName(outputNode, "expected");
                            
                            XmlElement outputData = CreateDataElement(doc, outExpectedAttr.Value, outErrColor, outErrStatus);
                            outputData.SetAttribute("colspan", "2");
                            resultRow.AppendChild(outputData);
                        }
                    }
                }
                else
                {
                    XmlNode rowOutputNode = GetChildNodeByName(rowResultNode, "output");
                    if (useOutput && rowOutputNode != null)
                    {
                        AddOutputColumns(doc, rowOutputNode, resultRow);
                    }
                }
                rowResultTable.AppendChild(resultRow);
            }
            return rowResultTable;
        }

        public static XmlElement BuildMultiRowTable(XmlDocument doc, XmlNode callResultNode, string resultType)
        {
            Dictionary<string, string> toleranceDic = new Dictionary<string, string>();
            List<string> outputKeys = GetOutputKeys(callResultNode, toleranceDic);
            
            XmlElement resultTable = BuildResultTable(doc);
            XmlElement resultTitleRow = doc.CreateElement("tr");
            XmlElement typeTitleData = CreateDataElement(doc, "Type", "", "status_passed");
            string titleStatus = "status_" + resultType;
            typeTitleData.SetAttribute("class", titleStatus);
            resultTitleRow.AppendChild(typeTitleData);
            foreach (string outputKey in outputKeys)
            {
                string outputTitle = AddWithinTolerance(outputKey, toleranceDic);
                XmlElement outputDataKey = CreateDataElement(doc, outputTitle, outputTitleColor, "output_title");
                outputDataKey.SetAttribute("colspan", "2");
                resultTitleRow.AppendChild(outputDataKey);
            }
            resultTable.AppendChild(resultTitleRow);
            XmlNode multiResultNode = GetChildNodeByName(callResultNode, "result");
            if (multiResultNode != null)
            {
                foreach (XmlNode outputNode in multiResultNode.ChildNodes)
                {
                    if (outputNode.Name == "output")
                    {
                        XmlAttribute outRowStatusAttr = GetAttributeByName(outputNode, "status");
                        XmlElement resultRow = doc.CreateElement("tr");
                        string matchColor = GetMatchColor(outRowStatusAttr.Value);
                        string status = "statusvalue_" + outRowStatusAttr.Value;
                        string match = "Match";
                        if (outRowStatusAttr.Value == ResultParser.ResultType.MISSING)
                        {
                            match = "Missing";
                        }
                        if (outRowStatusAttr.Value == ResultParser.ResultType.SURPLUS)
                        {
                            match = "Surplus";
                            if (resultType == ResultParser.ResultType.PASS)
                            {
                                matchColor = passedColor;
                                status = "statusvalue_passed";
                            }
                        }
                        if (outRowStatusAttr.Value == ResultParser.ResultType.UNPROCESSED)
                        {
                            match = "Unprocessed";
                        }
                        XmlElement typeData = CreateDataElement(doc, match, matchColor, status);
                        resultRow.AppendChild(typeData);
                        AddOutputColumns(doc, outputNode, resultRow);
                        resultTable.AppendChild(resultRow);
                    }
                }
            }
            return resultTable;
        }

        public static void AddOutputColumns(XmlDocument doc, XmlNode outputNode, XmlNode resultRow)
        {
            foreach (XmlNode childNode in outputNode.ChildNodes)
            {
                XmlAttribute outStatusAttr = GetAttributeByName(childNode, "status");
                XmlAttribute outExpectedAttr = GetAttributeByName(childNode, "expected");
                XmlAttribute outActualAttr = GetAttributeByName(childNode, "actual");

                string outStatus = "outputvalue_" + outStatusAttr.Value;
                if (outStatusAttr.Value == ResultParser.ResultType.UNMATCH)
                {
                    XmlElement outputData = CreateDataElement(doc, outExpectedAttr.Value, outputExpectedColor, outStatus);
                    XmlElement resultData = CreateDataElement(doc, outActualAttr.Value, outputUnMatchColor, outStatus);
                    resultRow.AppendChild(outputData);
                    resultRow.AppendChild(resultData);
                    if (outActualAttr.Value == "")
                    {
                        resultData.SetAttribute("width", "3px");
                    }
                }
                else
                {
                    string outBgcolor = GetOutputColor(outStatusAttr.Value);
                    string outValue = outActualAttr.Value;
                    if (outExpectedAttr.Value != "")
                    {
                        outValue = outExpectedAttr.Value;
                    }
                    XmlElement outputData = CreateDataElement(doc, outValue, outBgcolor, outStatus);
                    outputData.SetAttribute("colspan", "2");
                    resultRow.AppendChild(outputData);
                }
            }
        }
        public static XmlElement CreateDataElement(XmlDocument doc, string val, string bgcolor, string classType)
        {
            XmlElement tableData = doc.CreateElement("td");
            if (val != null && val != "")
            {
                tableData.InnerXml = System.Web.HttpUtility.HtmlEncode(val);
            }
            if (bgcolor != null && bgcolor != "")
            {
                tableData.SetAttribute("bgcolor", bgcolor);
            }
            if (classType != null && classType != "")
            {
                tableData.SetAttribute("class", classType);
            }
            return tableData;
        }

        public static List<string> GetInputKeys(XmlNode resultNode)
        {
            List<string> inputKeys = new List<string>();
            XmlNode inputNode = GetChildNodeByName(resultNode, "inputkeys");
            if (inputNode != null)
            {
                foreach (XmlNode childNode in inputNode.ChildNodes)
                {
                    inputKeys.Add(childNode.Name);
                }
            }
            return inputKeys;
        }

        public static List<string> GetOutputKeys(XmlNode resultNode, Dictionary<string, string> toleranceDic)
        {
            List<string> outputKeys = new List<string>();
            XmlNode outputNode = GetChildNodeByName(resultNode, "outputkeys");
            if (outputNode != null)
            {
                foreach (XmlNode childNode in outputNode.ChildNodes)
                {
                    outputKeys.Add(childNode.Name);
                    XmlAttribute toleranceAttr = GetAttributeByName(childNode, "tolerance", true);
                    if (toleranceAttr != null && toleranceAttr.Value != "")
                    {
                        toleranceDic.Add(childNode.Name, toleranceAttr.Value);
                    }
                }
            }
            return outputKeys;
        }

        public static XmlNode GetChildNodeByName(XmlNode node, string name)
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == name)
                {
                    return childNode;
                }
            }
            return null;
        }

        public static List<XmlNode> GetChildNodesByName(XmlNode node, string name)
        {
            List<XmlNode> children = new List<XmlNode>();
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == name)
                {
                    children.Add(childNode);
                }
            } 
            return children;
        }

        public static string AddWithinTolerance(string key, Dictionary<string, string> toleranceDic)
        {
            string within = "";
            if (toleranceDic != null)
            {
                if (toleranceDic.TryGetValue(key, out within))
                {
                    return key + " within(" + within + ")";
                }
            }
            return key;
        }

        public static Dictionary<string, string> GetInputAsDic(XmlNode resultNode)
        {
            Dictionary<string, string> inputDic = new Dictionary<string, string>();
            XmlNode inputNode = GetChildNodeByName(resultNode, "input");
            if (inputNode != null)
            {
                foreach (XmlAttribute inputAttr in inputNode.Attributes)
                {
                    inputDic.Add(inputAttr.Name, inputAttr.Value);
                }
            }
            return inputDic;
        }

        public static string GetInputData(string key, Dictionary<string, string> inputDic)
        {
            string value = "";
            if (inputDic != null && inputDic.ContainsKey(key))
            {
                inputDic.TryGetValue(key, out value);
            }
            return value;
        }

        public static void AppendHtmlErrors(XmlElement bodyElement, XmlNode resultNode)
        {
            List<XmlNode> errorNodes = GetAllErrorNodes(resultNode);
            if (errorNodes.Count > 0)
            {
                XmlDocument doc = bodyElement.OwnerDocument;
                XmlElement header = doc.CreateElement("h2");
                header.InnerXml = "Exceptions Listing";
                XmlElement errorTable = BuildResultTable(doc, "0");

                foreach (XmlNode errorNode in errorNodes)
                {
                    errorTable.AppendChild(ConvertError(doc, errorNode));
                }
                bodyElement.AppendChild(header);
                bodyElement.AppendChild(errorTable);
            }
        }

        public static XmlNode ConvertError(XmlDocument doc, XmlNode errorNode)
        {
            XmlAttribute nameAttr = GetAttributeByName(errorNode, "name");

            XmlElement tableRow = doc.CreateElement("tr");
            XmlElement tableRefData = CreateTopDataElement(doc, "td");
            XmlElement refNode = doc.CreateElement("a");
            refNode.SetAttribute("name", nameAttr.Value);
            refNode.InnerXml = nameAttr.Value;
            tableRefData.AppendChild(refNode);
            XmlElement preDataNode = CreateTopDataElement(doc, "td");
            XmlElement preNode = CreateTopDataElement(doc, "pre");
            preNode.InnerXml = errorNode.InnerXml;
            preDataNode.AppendChild(preNode);
            tableRow.AppendChild(tableRefData);
            tableRow.AppendChild(preDataNode);

            return tableRow;
        }

        public static XmlElement CreateTopDataElement(XmlDocument doc, string data)
        {
            XmlElement tableData = doc.CreateElement(data);
            tableData.SetAttribute("valign", "top");
            return tableData;
        }

        public static List<XmlNode> GetAllErrorNodes(XmlNode node)
        {
            List<XmlNode> errorNodes = new List<XmlNode>();
            GetErrorNodesRecursive(node, errorNodes);
            return errorNodes;
        }

        public static void GetErrorNodesRecursive(XmlNode node, List<XmlNode> errorNodes)
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == "error" && node.Name == "result")
                {
                    errorNodes.Add(childNode);
                }
                else
                {
                    GetErrorNodesRecursive(childNode, errorNodes);
                }
            }
        }

    }
}
