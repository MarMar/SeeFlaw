using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;

namespace SeeFlawRunner
{
    public class ResultParser
    {
        
        public static class ResultType
        {
            public const string PASS = "passed";
            public const string FAIL = "failed";
            public const string ERROR = "error";
            public const string MISSING = "missing";
            public const string UNMATCH = "unmatch";
            public const string SURPLUS = "surplus";
            public const string UNPROCESSED = "unprocessed";
        }

        public class Result
        {
            public bool passed = true;
            public string type = ResultType.PASS;
            public void Fail()
            {
                passed = false;
                type = ResultType.FAIL;
            }
            public void Error()
            {
                passed = false;
                type = ResultType.ERROR;
            }
        }

        public class ResultData
        {
            public bool match = true;
            public bool missing = false;
            public string output = "";
            public string result = "";
        }

        private XmlDocument doc;
        private ErrorParser errorParser;
        private bool showNoTime = false;

        public ResultParser(XmlDocument doc, ErrorParser errParser, bool onlyTest)
        {
            this.doc = doc;
            this.errorParser = errParser;
            this.showNoTime = onlyTest;
        }

        public XmlNode GetResultAsXml(ResultHolder dataHolder)
        {
            if (dataHolder == null)
            {
                return null;
            }
            Result result = new Result();
            List<XmlNode> rowNodes = null;
            XmlElement multiRowTable = null;
            string methodType = "";
            if (dataHolder.multiRows != null)
            {
                methodType = "multi";
                {
                    multiRowTable = BuildMultiRowNode(dataHolder, result);
                }
            }
            else
            {
                methodType = "single";
                rowNodes = BuildRowNode(dataHolder, result);
            }
            XmlElement callNode = BuildHeadNode(dataHolder, result, methodType);
            if (rowNodes != null)
            {
                foreach (XmlNode rowNode in rowNodes)
                {
                    callNode.AppendChild(rowNode);
                }
            }
            if (multiRowTable != null)
            {
                callNode.AppendChild(multiRowTable);
            }
            if (!result.passed)
            {
                dataHolder.TestFailed();
            }
            return callNode;
        }

        public XmlElement BuildHeadNode(ResultHolder dataHolder, Result result, string methodType)
        {
            string callType = "call";
            if (dataHolder.callType != "")
            {
                callType = dataHolder.callType;
            }
            callType += "result";
            XmlElement headNode = doc.CreateElement(callType);
            headNode.SetAttribute("fixture", dataHolder.fixtureName);
            headNode.SetAttribute("method", dataHolder.fixtureMethod);
            headNode.SetAttribute("status", result.type);
            headNode.SetAttribute("type", methodType);
            if (!dataHolder.skipTime)
            {
                headNode.SetAttribute("time", GetTimeString(dataHolder.GetRunTime()));
            }
            if (dataHolder.paramName != "")
            {
                headNode.SetAttribute("name", dataHolder.paramName);
            }
            XmlElement inputNode = doc.CreateElement("inputkeys");
            foreach (string inputKey in dataHolder.inputKeys)
            {
                XmlElement inputKeyNode = doc.CreateElement(inputKey);
                inputNode.AppendChild(inputKeyNode);
            }
            XmlElement outputNode = doc.CreateElement("outputkeys");
            foreach (string outputKey in dataHolder.outputKeys)
            {
                XmlElement outputKeyNode = doc.CreateElement(outputKey);
                outputKeyNode.SetAttribute("tolerance", GetTolerance(outputKey, dataHolder));
                outputNode.AppendChild(outputKeyNode);
            }
            foreach (string outputKey in dataHolder.multiRowKeys)
            {
                XmlElement outputKeyNode = doc.CreateElement(outputKey);
                outputKeyNode.SetAttribute("tolerance", GetTolerance(outputKey, dataHolder));
                outputNode.AppendChild(outputKeyNode);
            }
            headNode.AppendChild(inputNode);
            headNode.AppendChild(outputNode);
            return headNode;
        }

        public List<XmlNode> BuildRowNode(ResultHolder dataHolder, Result result)
        {
            List<XmlNode> resultNodes = new List<XmlNode>();
            
            foreach (ResultHolder.RowResult rowResult in dataHolder.singleRows)
            {
                string partPassed = ResultType.PASS;
                XmlElement resultNode = doc.CreateElement("result");
                resultNode.SetAttribute("status", ResultType.PASS);
                if (!dataHolder.skipTime)
                {
                    resultNode.SetAttribute("time", GetTimeString(rowResult.partTime));
                }

                XmlElement rowInputNode = doc.CreateElement("input");
                resultNode.AppendChild(rowInputNode);
                foreach (string inputKey in dataHolder.inputKeys)
                {
                    string input = GetInputData(inputKey, rowResult.inputDic);
                    rowInputNode.SetAttribute(inputKey, input);
                }
                if (rowResult.fixException != null)
                {
                    resultNode.SetAttribute("status", ResultType.ERROR);
                    resultNode.AppendChild(errorParser.AddXmlError(doc, rowResult.fixException));
                    result.Error();

                    XmlElement rowOutputNode = doc.CreateElement("output");
                    resultNode.AppendChild(rowOutputNode);

                    foreach (string outputKey in dataHolder.outputKeys)
                    {
                        XmlElement matchOutputNode = doc.CreateElement(outputKey);
                        rowOutputNode.AppendChild(matchOutputNode);
                        string outExpected = "";
                        if (rowResult.outputDic != null)
                        {
                            rowResult.outputDic.TryGetValue(outputKey, out outExpected);
                        }
                        matchOutputNode.SetAttribute("expected", outExpected);
                        matchOutputNode.SetAttribute("actual", "");
                        matchOutputNode.SetAttribute("status", ResultType.UNPROCESSED);
                    }
                }
                else
                {
                    XmlElement rowOutputNode = doc.CreateElement("output");
                    resultNode.AppendChild(rowOutputNode);

                    partPassed = BuildMatchingData(rowOutputNode, rowResult.outputDic, rowResult.resultDic, dataHolder.outputKeys, dataHolder.toleranceDic);
                    if (partPassed != ResultType.PASS)
                    {
                        resultNode.SetAttribute("status", ResultType.FAIL);
                        result.Fail();
                    }
                }
                resultNodes.Add(resultNode);
            }
            return resultNodes;
        }

        public XmlElement BuildMultiRowNode(ResultHolder dataHolder, Result result)
        {
            XmlElement resultNode = doc.CreateElement("result");
            
            ResultHolder.RowResult rowResult = dataHolder.singleRows[0];
            if (rowResult != null)
            {
                if (!dataHolder.skipTime)
                {
                    resultNode.SetAttribute("time", GetTimeString(rowResult.partTime));
                }

                XmlElement rowInputNode = doc.CreateElement("input");
                resultNode.AppendChild(rowInputNode);
                foreach (string inputKey in dataHolder.inputKeys)
                {
                    string input = GetInputData(inputKey, rowResult.inputDic);
                    rowInputNode.SetAttribute(inputKey, input);
                }
                if (rowResult.fixException != null)
                {
                    resultNode.SetAttribute("status", ResultType.UNPROCESSED);
                    resultNode.AppendChild(errorParser.AddXmlError(doc, rowResult.fixException));
                    result.Error();

                    foreach (Dictionary<string, string> outputDic in dataHolder.multiRows.multiOutput)
                    {
                        XmlElement resultOutputNode = doc.CreateElement("output");
                        resultOutputNode.SetAttribute("status", ResultType.UNPROCESSED);
                        resultNode.AppendChild(resultOutputNode);
                        
                        foreach (string outputKey in dataHolder.multiRowKeys)
                        {
                            XmlElement matchOutputNode = doc.CreateElement(outputKey);
                            resultOutputNode.AppendChild(matchOutputNode);
                            string outExpected = "";
                            outputDic.TryGetValue(outputKey, out outExpected);
                            matchOutputNode.SetAttribute("expected", outExpected);
                            matchOutputNode.SetAttribute("actual", "");
                            matchOutputNode.SetAttribute("status", ResultType.UNPROCESSED);
                        }
                    }
                }
            }

            if (dataHolder.multiRows.multiResult != null)
            {
                List<Dictionary<string, object>> matchedResults = MatchOutputToResult(dataHolder);
                int matchIndex = 0;
                foreach (Dictionary<string, string> outputDic in dataHolder.multiRows.multiOutput)
                {
                    Dictionary<string, object> resultDic = matchedResults[matchIndex];
                    XmlElement resultOutputNode = doc.CreateElement("output");

                    string partPassed = BuildMatchingData(resultOutputNode, outputDic, resultDic, dataHolder.multiRowKeys, dataHolder.toleranceDic);
                    if (partPassed != ResultType.PASS)
                    {
                        result.Fail();
                    }
                    resultOutputNode.SetAttribute("status", partPassed);
                    resultNode.AppendChild(resultOutputNode);
                    matchIndex++;
                }
                if (matchIndex < matchedResults.Count)
                {
                    if (!dataHolder.allowSurplus)
                    {
                        result.Fail();
                    }
                    while (matchIndex < matchedResults.Count)
                    {
                        Dictionary<string, object> surplusDic = matchedResults[matchIndex];
                        XmlElement surplusNode = doc.CreateElement("output");
                        surplusNode.SetAttribute("status", ResultType.SURPLUS);

                        foreach (string outputKey in dataHolder.multiRowKeys)
                        {
                            object value = null;
                            if (surplusDic.ContainsKey(outputKey))
                            {
                                surplusDic.TryGetValue(outputKey, out value);
                            }
                            XmlElement outputNode = doc.CreateElement(outputKey);
                            outputNode.SetAttribute("expected", "");
                            outputNode.SetAttribute("actual", GetObjStr(value));
                            outputNode.SetAttribute("status", ResultType.SURPLUS);
                            surplusNode.AppendChild(outputNode);
                        }
                        resultNode.AppendChild(surplusNode);
                        matchIndex++;
                    }
                }
            }
            resultNode.SetAttribute("status", result.type);
            return resultNode;
        }

        public string BuildMatchingData(XmlElement outputNode, Dictionary<string, string> outputDic, 
                                        Dictionary<string, object> resultDic,
                                        List<string> outputKeys, 
                                        Dictionary<string, string> toleranceDic)
        {
            string partPassed = ResultType.PASS;
            if (outputKeys.Count == 0)
            {
                return partPassed;
            }
            if (resultDic == null)
            {
                partPassed = ResultType.MISSING;
            }
            foreach (string outputKey in outputKeys)
            {
                XmlElement matchOutputNode = doc.CreateElement(outputKey);
                outputNode.AppendChild(matchOutputNode);
                ResultData data = GetOutputData(outputKey, outputDic, resultDic, toleranceDic);
                matchOutputNode.SetAttribute("expected", data.output);
                matchOutputNode.SetAttribute("actual", data.result);
                if (partPassed == ResultType.MISSING)
                {
                    matchOutputNode.SetAttribute("status", ResultType.MISSING);
                }
                else
                {
                    if (data.match)
                    {
                        matchOutputNode.SetAttribute("status", ResultType.PASS);
                    }
                    else
                    {
                        if (data.missing)
                        {

                            matchOutputNode.SetAttribute("status", ResultType.MISSING);
                            partPassed = ResultType.FAIL;
                        }
                        else
                        {
                            matchOutputNode.SetAttribute("status", ResultType.UNMATCH);
                            partPassed = ResultType.FAIL;
                        }
                    }
                }
            }
            return partPassed;
        }

        public string GetTolerance(string key, ResultHolder dataHolder)
        {
            string within = "";
            if (dataHolder.toleranceDic != null)
            {
                dataHolder.toleranceDic.TryGetValue(key, out within);
            }
            return within;
        }
        
        public string GetObjStr(object obj)
        {
            if (obj == null)
            {
                return "";
            }
            return obj.ToString();
        }

        public string GetInputData(string key, Dictionary<string, string> inputDic)
        {
            string value = "";
            if (inputDic != null && inputDic.ContainsKey(key))
            {
                inputDic.TryGetValue(key, out value);
            }
            return value;
        }

        public ResultData GetOutputData(string key, Dictionary<string, string> outputDic,
                                    Dictionary<string, object> resultDic, Dictionary<string, string> tolDic)
        {
            ResultData data = new ResultData();
            string tolVal = "";
            if (outputDic.ContainsKey(key))
            {
                outputDic.TryGetValue(key, out data.output);
            }
            if (resultDic == null)
            {
                data.missing = true;
                data.match = false;
                return data;
            }
            object resultObj = null;
            if (resultDic.ContainsKey(key))
            {
                resultDic.TryGetValue(key, out resultObj);
            }
            else
            {
                data.missing = true;
                data.match = false;
                return data;
            }
            if (data.output == "")
            {
                if (resultObj != null)
                {
                    data.result = resultObj.ToString();
                }
            }
            else if (data.output.ToUpper() == "NULL")
            {
                if (resultObj == null || resultObj.ToString() == "")
                {
                    data.match = true;
                }
                else
                {
                    data.match = false;
                }
            }
            else
            {
                if (resultObj != null)
                {
                    if (tolDic != null)
                    {
                        tolDic.TryGetValue(key, out tolVal);
                    }
                    data.match = IsDataMatching(data.output, resultObj, tolVal);
                    data.result = resultObj.ToString();
                }
                else
                {
                    data.match = false;
                }
            }
            return data;
        }

        public bool IsDataMatching(string key, Dictionary<string, string> outputDic,
                                    Dictionary<string, object> resultDic, Dictionary<string, string> tolDic)
        {
            string outVal = "";
            string tolVal = "";
            if (outputDic.ContainsKey(key))
            {
                outputDic.TryGetValue(key, out outVal);
            }
            if (resultDic == null)
            {
                return false;
            }
            object resultObj = null;
            if (resultDic.ContainsKey(key))
            {
                resultDic.TryGetValue(key, out resultObj);
            }
            if (outVal != "")
            {
                if (resultObj != null)
                {
                    if (tolDic != null)
                    {
                        tolDic.TryGetValue(key, out tolVal);
                    }
                    return IsDataMatching(outVal, resultObj, tolVal);
                }
                return false;
            }
            return true;
        }

        public bool IsDataMatching(string outputVal, object resultObj, string toleranceVal)
        {
            if (resultObj.GetType() == typeof(string) ||
                resultObj.GetType() == typeof(int) ||
                resultObj.GetType() == typeof(long) || 
                resultObj.GetType() == typeof(byte) ||
                resultObj.GetType() == typeof(System.Byte) ||
                resultObj.GetType() == typeof(System.Int16) ||
                resultObj.GetType() == typeof(System.Int32) ||
                resultObj.GetType() == typeof(System.Int64))
            {
                return outputVal == resultObj.ToString();
            }
            else if (resultObj.GetType() == typeof(bool) || resultObj.GetType() == typeof(System.Boolean))
            {
                if (outputVal == "1" || outputVal.ToUpper() == "TRUE")
                {
                    return ((bool)resultObj == true);
                }
                else
                {
                    return ((bool)resultObj == false);
                }
            }
            else if (resultObj.GetType() == typeof(double) || resultObj.GetType() == typeof(System.Decimal))
            {
                try
                {
                    double tolerance = 0.0;
                    double result;
                    if (resultObj.GetType() == typeof(double))
                    {
                        result = (double)resultObj;
                    }
                    else
                    {
                        result = double.Parse(resultObj.ToString());
                    }
                    try
                    {
                        tolerance = ConvertDoubleToString(toleranceVal);
                    }
                    catch (Exception)
                    { }
                    double outObj = ConvertDoubleToString(outputVal);
                    if (System.Math.Abs(outObj - result) <= tolerance)
                    {
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine("Exception " + ex.ToString());
                    return false;
                }
            }
            else if (resultObj.GetType() == typeof(System.DateTime))
            {
                System.DateTime resultTime = (System.DateTime) resultObj;
                System.DateTime outDate;
                if (outputVal.ToUpper() == "TODAY")
                {
                    outDate = System.DateTime.Today;
                }
                else 
                { 
                    if(!System.DateTime.TryParse(outputVal, out outDate))
                    {
                        return false;
                    }
                }
                if (toleranceVal != null && toleranceVal != "")
                {
                    TimeSpan diffTime = resultTime - outDate;
                    TimeSpan toleranceSpan = GetToleranceTime(toleranceVal);
                    if (toleranceSpan != TimeSpan.Zero)
                    {
                        if (diffTime.Duration() <= toleranceSpan)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    if (outputVal.Length < 11)
                    {
                        if (outDate == resultTime.Date)
                        {
                            return true;
                        }
                    }
                    else if (outDate == resultTime)
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (resultObj.GetType() == typeof(System.DBNull))
            {
                if (outputVal == "" || outputVal.ToUpper() == "NULL")
                {
                    return true;
                }
                return false;
            }
            else
            {
                System.Console.WriteLine("Unhandled DataType " + resultObj.GetType() + " in IsDataMatching.");
                return false;
            }
        }

        public double ConvertDoubleToString(string strVal)
        {
            string tempStr = strVal;
            string decimalSep = System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
            if (decimalSep == "," && tempStr.Contains("."))
            {
                tempStr = tempStr.Replace(".", decimalSep);
            }
            if (decimalSep == "." && tempStr.Contains(","))
            {
                tempStr = tempStr.Replace(",", decimalSep);
            }
            return Double.Parse(tempStr);
        }

        public string GetTimeString(System.TimeSpan time)
        {
            if (showNoTime)
            {
                return "(" + new System.TimeSpan().ToString() + ")";
            }
            return "(" + time.ToString() + ")";
        }

        public TimeSpan GetToleranceTime(string tolerance)
        {
            int timePart;
            if (!GetToleranceTimePart(tolerance, out timePart))
            {
                return TimeSpan.Zero;
            }
            string lower = tolerance.ToLower();
            if (lower.EndsWith("millisecond") || lower.EndsWith("ms"))
            {
                return new TimeSpan(0, 0, 0, 0, timePart);
            }
            if (lower.EndsWith("second") || lower.EndsWith("s"))
            {
                return new TimeSpan(0, 0, 0, timePart, 0);
            }
            if (lower.EndsWith("minute") || lower.EndsWith("m"))
            {
                return new TimeSpan(0, 0, timePart, 0, 0);
            }
            if (lower.EndsWith("hour") || lower.EndsWith("h"))
            {
                return new TimeSpan(0, timePart, 0, 0, 0);
            }
            if (lower.EndsWith("day") || lower.EndsWith("d"))
            {
                return new TimeSpan(timePart, 0, 0, 0, 0);
            }
            System.Console.WriteLine("Could not read DateTime tolerance: " + tolerance);
            return TimeSpan.Zero;
        }

        private bool GetToleranceTimePart(string tolerance, out int timePart)
        {
            int index = 0;
            for (; index < tolerance.Length; index++)
            {
                if (!Char.IsNumber(tolerance, index))
                {
                    break;
                }
            }
            if (index > 0)
            {
                if (Int32.TryParse(tolerance.Substring(0, index), out timePart))
                {
                    return true;
                }
            }
            System.Console.WriteLine("Could not parse DateTime tolerance: " + tolerance);
            timePart = 0;
            return false;
        }

        private class OutMatchObject
        {
            public int outIndex;
            public List<ResultMatchObject> resultMatchList = new List<ResultMatchObject>();
            public OutMatchObject(int index)
            {
                outIndex = index;
            }
            public void AddResult(ResultMatchObject resMatch)
            {
                resultMatchList.Add(resMatch);
            }
            public void RemoveResult(int itemIndex)
            {
                resultMatchList.RemoveAt(itemIndex);
            }
            public MatchObject GetHighestMatch()
            {
                ResultMatchObject highestResultMatch = null;
                int itemIndex = 0;
                int highestItemIndex = 0;
                foreach (ResultMatchObject resMatch in resultMatchList)
                {
                    if (highestResultMatch == null || resMatch.match > highestResultMatch.match)
                    {
                        highestResultMatch = resMatch;
                        highestItemIndex = itemIndex;
                    }
                    itemIndex++;
                }
                MatchObject matchObject = new MatchObject();
                matchObject.highestMatch = highestResultMatch.match;
                matchObject.outIndex = this.outIndex;
                matchObject.resultIndex = highestResultMatch.resultIndex;
                matchObject.resultItemIndex = highestItemIndex;
                return matchObject;
            }
        }
        private class ResultMatchObject
        {
            public int resultIndex;
            public int match;
            public ResultMatchObject(int index, int nrOfMatches)
            {
                resultIndex = index;
                match = nrOfMatches;
            }
        }
        private class MatchObject
        {
            public int highestMatch = 0;
            public int outIndex;
            public int resultIndex;
            public int resultItemIndex;
        }

        private class ResultDicObject
        {
            public int dicItemIndex;
            public Dictionary<string, object> resultDic;
            public ResultDicObject(int index, Dictionary<string, object> dic)
            {
                dicItemIndex = index;
                resultDic = dic;
            }
        }

        public List<Dictionary<string, object>> MatchOutputToResult(ResultHolder dataHolder)
        {
            ResultHolder.MultiRowResult multiResult = dataHolder.multiRows;
            List<Dictionary<string, object>> matchList = new List<Dictionary<string, object>>();
            int listSize = Math.Max(multiResult.multiOutput.Count, multiResult.multiResult.Count);
            for (int nrOfResults = 0; nrOfResults < listSize; nrOfResults++)
            {
                matchList.Add(null);
            }
            
            List<Dictionary<string, object>> resultCopy = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> result in multiResult.multiResult)
            {
                resultCopy.Add(result);
            }

            List<ResultDicObject> defaultResultList = new List<ResultDicObject>();
            int defaultIndex = 0;
            foreach (Dictionary<string, object> result in multiResult.multiResult)
            {
                defaultResultList.Add(new ResultDicObject(defaultIndex, result));
                defaultIndex++;
            }

            List<OutMatchObject> rowOutMatch = new List<OutMatchObject>();
            int outDicCount = 0;
            foreach (Dictionary<string, string> output in multiResult.multiOutput)
            {
                OutMatchObject outMatch = new OutMatchObject(outDicCount);
                int resultDicCounter = 0;
                bool fullMatchFound = false;
                int fullMatchResultIndex = 0;
                foreach (ResultDicObject resultDicObj in defaultResultList)
                {
                    int nrOfMatches = 0;
                    foreach (string key in dataHolder.multiRowKeys)
                    {
                        if (IsDataMatching(key, output, resultDicObj.resultDic, dataHolder.toleranceDic))
                        {
                            nrOfMatches++;
                        }
                    }
                    if (nrOfMatches == dataHolder.multiRowKeys.Count)
                    {
                        fullMatchFound = true;
                        fullMatchResultIndex = resultDicObj.dicItemIndex;
                        break;
                    }
                    ResultMatchObject resMatch = new ResultMatchObject(resultDicObj.dicItemIndex, nrOfMatches);
                    outMatch.AddResult(resMatch);
                    resultDicCounter++;
                }
                if (fullMatchFound)
                {
                    matchList[outDicCount] = resultCopy[fullMatchResultIndex];
                    resultCopy[fullMatchResultIndex] = null;

                    defaultResultList.RemoveAt(resultDicCounter);
                    foreach (OutMatchObject outMatch2 in rowOutMatch)
                    {
                        outMatch2.RemoveResult(resultDicCounter);
                    }
                }
                else
                {
                    rowOutMatch.Add(outMatch);
                }
                outDicCount++;
            }
            PrintMatchMatrix(rowOutMatch);
            int rowSize = System.Math.Min(rowOutMatch.Count, defaultResultList.Count);
            for (int nrOfRows = 0; nrOfRows < rowSize; nrOfRows++)
            {
                int outItemIndex = 0;
                int highestOutItemIndex = 0;
                MatchObject highestMatchObject = null;
                foreach (OutMatchObject outMatch in rowOutMatch)
                {
                    MatchObject matchObject = outMatch.GetHighestMatch();
                    if (highestMatchObject == null || matchObject.highestMatch > highestMatchObject.highestMatch)
                    {
                        highestMatchObject = matchObject;
                        highestOutItemIndex = outItemIndex;
                    }
                    outItemIndex++;
                }
                if (highestMatchObject != null)
                {
                    matchList[highestMatchObject.outIndex] = resultCopy[highestMatchObject.resultIndex];
                    resultCopy[highestMatchObject.resultIndex] = null;

                    rowOutMatch.RemoveAt(highestOutItemIndex);
                    foreach (OutMatchObject outMatch in rowOutMatch)
                    {
                        outMatch.RemoveResult(highestMatchObject.resultItemIndex);
                    }
                }
            }
            if (multiResult.multiOutput.Count < listSize)
            {
                int matchIndex = multiResult.multiOutput.Count;
                foreach (Dictionary<string, object> resultDic in resultCopy)
                {
                    if (resultDic != null)
                    {
                        matchList[matchIndex] = resultDic;
                        matchIndex++;
                    }
                }
            }
            return matchList;
        }
        
        private void PrintMatchMatrix(List<OutMatchObject> matchMatrix)
        {
            if (showNoTime)
            {
                if (matchMatrix.Count > 0 && matchMatrix[0].resultMatchList.Count > 0)
                {
                    string head = " res";
                    int lastOutIndex = matchMatrix[0].resultMatchList[matchMatrix[0].resultMatchList.Count - 1].resultIndex;
                    int outIndex = 0;
                    while (outIndex <= lastOutIndex)
                    {
                        head += " " + outIndex.ToString("00");
                        outIndex++;
                    }
                    System.Console.WriteLine(head);
                    System.Console.WriteLine("out|");

                    outIndex = 0;
                    foreach (OutMatchObject outMatch in matchMatrix)
                    {
                        while (outIndex < outMatch.outIndex)
                        {
                            System.Console.WriteLine(outIndex.ToString("00") + " |");
                            outIndex++;
                        }
                        string m = outMatch.outIndex.ToString("00") + " |";
                        int resIndex = 0;
                        foreach (ResultMatchObject resMatch in outMatch.resultMatchList)
                        {
                            while (resIndex < resMatch.resultIndex)
                            {
                                m += "   ";
                                resIndex++;
                            }
                            m += " " + resMatch.match.ToString("00");
                            resIndex++;
                        }
                        System.Console.WriteLine(m);
                        outIndex++;
                    }
                }
            }
        }
        private void PrintList(List<Dictionary<string, string>> list)
        {
            foreach (Dictionary<string, string> dic in list)
            {
                if (dic != null)
                {
                    foreach (KeyValuePair<string, string> val in dic)
                    {
                        System.Console.WriteLine("key " + val.Key + " val " + val.Value);
                    }
                }
                else
                {
                    System.Console.WriteLine("dic null");
                }
            }
        }
    }

    [TestFixture]
    public class ParseResultTest
    {
        private ResultParser parser;
        private ResultHolder holder;

        private Dictionary<string, string> dic1;
        private Dictionary<string, string> dic2;
        private Dictionary<string, string> dic3;
        private Dictionary<string, string> dic4;
        private Dictionary<string, string> dic5;
        private Dictionary<string, object> dic5b;
        private Dictionary<string, object> dic6;
        private Dictionary<string, object> dic7;
        private Dictionary<string, object> dic8;
        private Dictionary<string, object> dic9;

        private Dictionary<string, string> outputDic;
        private Dictionary<string, object> resultDic;

        [SetUp]
        public void SetUpTest()
        {
            XmlDocument doc = new XmlDocument();
            parser = new ResultParser(doc, null, true);
            holder = new ResultHolder("test", null, true);

            outputDic = new Dictionary<string, string>();
            resultDic = new Dictionary<string, object>();

            dic1 = new Dictionary<string, string>();
            dic2 = new Dictionary<string, string>();
            dic3 = new Dictionary<string, string>();
            dic4 = new Dictionary<string, string>();
            dic5 = new Dictionary<string, string>();
            dic5b = new Dictionary<string, object>();
            dic6 = new Dictionary<string, object>();
            dic7 = new Dictionary<string, object>();
            dic8 = new Dictionary<string, object>();
            dic9 = new Dictionary<string, object>();

            dic1.Add("first", "A");
            dic1.Add("second", "B");
            dic1.Add("third", "C");
            dic1.Add("fourth", "D");

            dic2.Add("first", "A");
            dic2.Add("second", "B");
            dic2.Add("third", "B");
            dic2.Add("fourth", "B");

            dic3.Add("first", "A");
            dic3.Add("second", "C");
            dic3.Add("third", "C");
            dic3.Add("fourth", "C");

            dic4.Add("first", "C");
            dic4.Add("second", "B");
            dic4.Add("third", "C");
            dic4.Add("fourth", "D");

            dic5.Add("first", "C");
            dic5.Add("second", "B");
            dic5.Add("third", "D");
            dic5.Add("fourth", "D");

            dic5b.Add("first", "C");
            dic5b.Add("second", "B");
            dic5b.Add("third", "D");
            dic5b.Add("fourth", "D");

            dic6.Add("first", "A");
            dic6.Add("second", "C");
            dic6.Add("third", "C");
            dic6.Add("fourth", "C");

            dic7.Add("first", "C");
            dic7.Add("second", "B");
            dic7.Add("third", "D");
            dic7.Add("fourth", "C");

            dic8.Add("first", "A");
            dic8.Add("second", "B");
            dic8.Add("third", "B");
            dic8.Add("fourth", "C");

            dic9.Add("first", "A");
            dic9.Add("second", "B");
            dic9.Add("third", "D");
            dic9.Add("fourth", "C");

            holder.LoadOutputKeys(dic1);
        }

        [Test]
        public void Match5OutputsTo4Results()
        {
            List<Dictionary<string, string>> outputList = new List<Dictionary<string, string>>();
            List<Dictionary<string, object>> resultList = new List<Dictionary<string, object>>();

            outputList.Add(dic1);
            outputList.Add(dic2);
            outputList.Add(dic3);
            outputList.Add(dic4);
            outputList.Add(dic5);

            resultList.Add(dic6);
            resultList.Add(dic7);
            resultList.Add(dic8);
            resultList.Add(dic9);

            holder.CreateMultiRows(null, outputList);
            holder.SetMultiRowsResult(resultList, null);
            List<Dictionary<string, object>> matchList = parser.MatchOutputToResult(holder);

            NUnit.Framework.Assert.AreEqual(matchList.Count, 5);
            NUnit.Framework.Assert.AreEqual(matchList[0], resultList[3]);
            NUnit.Framework.Assert.AreEqual(matchList[1], resultList[2]);
            NUnit.Framework.Assert.AreEqual(matchList[2], resultList[0]);
            NUnit.Framework.Assert.AreEqual(matchList[3], null);
            NUnit.Framework.Assert.AreEqual(matchList[4], resultList[1]);
        }

        [Test]
        public void Match4OutputsTo5Results()
        {
            List<Dictionary<string, string>> outputList = new List<Dictionary<string, string>>();
            List<Dictionary<string, object>> resultList = new List<Dictionary<string, object>>();

            outputList.Add(dic1);
            outputList.Add(dic2);
            outputList.Add(dic3);
            outputList.Add(dic4);

            resultList.Add(dic5b);
            resultList.Add(dic6);
            resultList.Add(dic7);
            resultList.Add(dic8);
            resultList.Add(dic9);

            holder.CreateMultiRows(null, outputList);
            holder.SetMultiRowsResult(resultList, null);
            List<Dictionary<string, object>> matchList = parser.MatchOutputToResult(holder);

            NUnit.Framework.Assert.AreEqual(matchList.Count, 5);
            NUnit.Framework.Assert.AreEqual(matchList[0], resultList[4]);
            NUnit.Framework.Assert.AreEqual(matchList[1], resultList[3]);
            NUnit.Framework.Assert.AreEqual(matchList[2], resultList[1]);
            NUnit.Framework.Assert.AreEqual(matchList[3], resultList[0]);
            NUnit.Framework.Assert.AreEqual(matchList[4], resultList[2]);
        }

        [Test]
        public void GetResultDataBoolMatch()
        {
            outputDic.Add("key", "1");
            resultDic.Add("key", true);
            outputDic.Add("key2", "True");
            resultDic.Add("key2", true);
            outputDic.Add("key3", "0");
            resultDic.Add("key3", false);
            outputDic.Add("key4", "False");
            resultDic.Add("key4", false);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("True", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("0", data.output);
            data = parser.GetOutputData("key4", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("False", data.output);
        }
        [Test]
        public void GetResultDataBoolUnMatch()
        {
            outputDic.Add("key", "1");
            resultDic.Add("key", false);
            outputDic.Add("key2", "True");
            resultDic.Add("key2", false);
            outputDic.Add("key3", "0");
            resultDic.Add("key3", true);
            outputDic.Add("key4", "False");
            resultDic.Add("key4", true);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("True", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("0", data.output);
            data = parser.GetOutputData("key4", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("False", data.output);
        }
        [Test]
        public void GetResultDataSystemBoolMatch()
        {
            System.Boolean boolTrue = true;
            System.Boolean boolFalse = false;
            outputDic.Add("key", "1");
            resultDic.Add("key", boolTrue);
            outputDic.Add("key2", "True");
            resultDic.Add("key2", boolTrue);
            outputDic.Add("key3", "0");
            resultDic.Add("key3", boolFalse);
            outputDic.Add("key4", "False");
            resultDic.Add("key4", boolFalse);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("True", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("0", data.output);
            data = parser.GetOutputData("key4", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("False", data.output);
        }
        [Test]
        public void GetResultDataSystemBoolUnMatch()
        {
            System.Boolean boolTrue = true;
            System.Boolean boolFalse = false;
            outputDic.Add("key", "1");
            resultDic.Add("key", boolFalse);
            outputDic.Add("key2", "True");
            resultDic.Add("key2", boolFalse);
            outputDic.Add("key3", "0");
            resultDic.Add("key3", boolTrue);
            outputDic.Add("key4", "False");
            resultDic.Add("key4", boolTrue);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("True", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("0", data.output);
            data = parser.GetOutputData("key4", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("False", data.output);
        }
        [Test]
        public void GetResultDataSystemByteMatch()
        {
            System.Byte b0 = 0;
            System.Byte b1 = 1;
            System.Byte b5 = 5;
            outputDic.Add("key", "0");
            resultDic.Add("key", b0);
            outputDic.Add("key2", "1");
            resultDic.Add("key2", b1);
            outputDic.Add("key3", "5");
            resultDic.Add("key3", b5);
            
            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("0", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("5", data.output);
        }
        [Test]
        public void GetResultDataSystemByteUnMatch()
        {
            System.Byte b0 = 0;
            System.Byte b1 = 1;
            outputDic.Add("key", "1");
            resultDic.Add("key", b0);
            outputDic.Add("key2", "0");
            resultDic.Add("key2", b1);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("0", data.output);
        }
        [Test]
        public void GetResultDataSystemDbNull()
        {
            System.DBNull db0 = DBNull.Value;
            outputDic.Add("key", "Null");
            resultDic.Add("key", db0);
            outputDic.Add("key2", "");
            resultDic.Add("key2", db0);
            outputDic.Add("key3", "Some");
            resultDic.Add("key3", db0);
            outputDic.Add("key4", "1");
            resultDic.Add("key4", db0);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("Null", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("Some", data.output);
            data = parser.GetOutputData("key4", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("1", data.output);
        }
        [Test]
        public void GetResultDataSystemDateTimeMatch()
        {
            System.DateTime time0 = System.DateTime.Parse("2010-05-10 12:00:00");
            System.DateTime time1 = System.DateTime.Today;
            outputDic.Add("key", "2010-05-10");
            resultDic.Add("key", time0);
            outputDic.Add("key2", "2010-05-10 12:00:00");
            resultDic.Add("key2", time0);
            outputDic.Add("key3", "today");
            resultDic.Add("key3", time1);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("2010-05-10", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("2010-05-10 12:00:00", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("today", data.output);
        }
        [Test]
        public void GetResultDataSystemDateTimeUnMatch()
        {
            System.DateTime time0 = System.DateTime.Parse("2010-05-10 12:00:00");
            outputDic.Add("key", "2010-05-09");
            resultDic.Add("key", time0);
            outputDic.Add("key2", "2010-05-10 12:00:01");
            resultDic.Add("key2", time0);
            outputDic.Add("key3", "today");
            resultDic.Add("key3", time0);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("2010-05-09", data.output);
            data = parser.GetOutputData("key2", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("2010-05-10 12:00:01", data.output);
            data = parser.GetOutputData("key3", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("today", data.output);
        }
        [Test]
        public void GetResultDataIntegerMatch()
        {
            outputDic.Add("key", "345");
            resultDic.Add("key", 345);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("345", data.output);
        }
        [Test]
        public void GetResultDataIntegerUnMatch()
        {
            outputDic.Add("key", "346");
            resultDic.Add("key", 345);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("346", data.output);
        }
        [Test]
        public void GetResultDataDoubleMatchPointSep()
        {
            double res = 345.123;
            outputDic.Add("key", "345.123");
            resultDic.Add("key", res);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("345.123", data.output);
        }
        [Test]
        public void GetResultDataDoubleMatchCommaSep()
        {
            double res = 345.123;
            outputDic.Add("key", "345,123");
            resultDic.Add("key", res);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("345,123", data.output);
        }
        [Test]
        public void GetResultDataDoubleUnMatch()
        {
            double res = 345.123;
            outputDic.Add("key", "345.12");
            resultDic.Add("key", res);

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, null);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("345.12", data.output);
        }
        [Test]
        public void GetResultDataDoubleToleranceMatch()
        {
            double res = 345.123;
            outputDic.Add("key", "345.121");
            resultDic.Add("key", res);
            Dictionary<string, string> tolerance = new Dictionary<string, string>();
            tolerance.Add("key", "0.01");

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, tolerance);
            NUnit.Framework.Assert.AreEqual(true, data.match);
            NUnit.Framework.Assert.AreEqual("345.121", data.output);
        }
        [Test]
        public void GetResultDataDoubleToleranceUnMatch()
        {
            double res = 345.123;
            outputDic.Add("key", "345.121");
            resultDic.Add("key", res);
            Dictionary<string, string> tolerance = new Dictionary<string, string>();
            tolerance.Add("key", "0.001");

            ResultParser.ResultData data = parser.GetOutputData("key", outputDic, resultDic, tolerance);
            NUnit.Framework.Assert.AreEqual(false, data.match);
            NUnit.Framework.Assert.AreEqual("345.121", data.output);
        }

        [Test]
        public void TestToleranceDateTime()
        {
            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 2), parser.GetToleranceTime("2ms"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 25), parser.GetToleranceTime("25millisecond"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 0, 100), parser.GetToleranceTime("100ms"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 10, 0), parser.GetToleranceTime("10s"));
            Assert.AreEqual(new TimeSpan(0, 0, 0, 1, 0), parser.GetToleranceTime("1second"));
            Assert.AreEqual(new TimeSpan(0, 0, 15, 0, 0), parser.GetToleranceTime("15m"));
            Assert.AreEqual(new TimeSpan(0, 0, 4, 0, 0), parser.GetToleranceTime("4minute"));
            Assert.AreEqual(new TimeSpan(0, 1, 0, 0, 0), parser.GetToleranceTime("1h"));
            Assert.AreEqual(new TimeSpan(0, 24, 0, 0, 0), parser.GetToleranceTime("24hour"));
            Assert.AreEqual(new TimeSpan(3, 0, 0, 0, 0), parser.GetToleranceTime("3d"));
            Assert.AreEqual(new TimeSpan(30, 0, 0, 0, 0), parser.GetToleranceTime("30day"));
        }
    }
}
