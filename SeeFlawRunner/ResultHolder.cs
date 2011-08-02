using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SeeFlawRunner
{
    public class ResultHolder
    {
        public class RowResult
        {
            public Dictionary<string, string> inputDic;
            public Dictionary<string, string> outputDic;
            public Dictionary<string, object> resultDic;
            public Exception fixException;
            public System.TimeSpan partTime;

            public RowResult(Dictionary<string, string> input,
                             Dictionary<string, string> output)
            {
                inputDic = CloneDic(input);
                outputDic = output;
                resultDic = null;
                fixException = null;
                partTime = TimeSpan.Zero;
            }

            public void SetResult(Dictionary<string, object> result,
                                  Exception ex,
                                  TimeSpan time)
            {
                resultDic = result;
                fixException = ex;
                partTime = time;
            }
        }

        public class MultiRowResult
        {
            public List<Dictionary<string, string>> multiOutput;
            public List<Dictionary<string, object>> multiResult;

            public MultiRowResult(List<Dictionary<string, string>> output)
            {
                multiOutput = output;
                multiResult = null;
            }

            public void SetResult(List<Dictionary<string, object>> result)
            {
                multiResult = result;
            }
        }

        public static Dictionary<string, string> CloneDic(Dictionary<string, string> dic)
        {
            if (dic == null)
            {
                return null;
            }
            Dictionary<string, string> cloneDic = new Dictionary<string, string>();
            foreach (string key in dic.Keys)
            {
                cloneDic.Add(key, dic[key]);
            }
            return cloneDic;
        }

        public List<string> inputKeys = new List<string>();
        public List<string> outputKeys = new List<string>();
        public List<string> multiRowKeys = new List<string>();
        public List<RowResult> singleRows = new List<RowResult>();
        public MultiRowResult multiRows = null;
        public Dictionary<string, string> toleranceDic;
        public string callType = "";
        public string fixtureName;
        public string fixtureMethod;
        public string paramName = "";
        public bool skipTime = false;
        public bool allowSurplus = false;
        private System.DateTime startTime = System.DateTime.Now;
        private System.DateTime partStartTime = System.DateTime.Now;
        private bool successful = true;

        public ResultHolder(string fixture, Dictionary<string, string> tolDic, bool noTime)
        {
            fixtureName = fixture;
            fixtureMethod = "";
            skipTime = noTime;
            toleranceDic = tolDic;
        }

        public ResultHolder(string fixture, string method, Dictionary<string, string> tolDic, bool noTime)
        {
            fixtureName = fixture;
            fixtureMethod = method;
            skipTime = noTime;
            toleranceDic = tolDic;
        }

        public ResultHolder(string cType, string fixture, string method, string pName, bool noTime)
        {
            callType = cType;
            //fixtureName = fixture + " : " + method + " : " + paramName;
            fixtureName = fixture;
            fixtureMethod = method;
            paramName = pName;
            skipTime = noTime;
            toleranceDic = null;
        }

        public ResultHolder(string cType, bool noTime)
        {
            callType = cType;
            fixtureName = cType;
            fixtureMethod = "";
            skipTime = noTime;
            toleranceDic = null;
        }

        public void AddSingleRow(Dictionary<string, string> inputDic,
                                 Dictionary<string, string> outputDic,
                                 Dictionary<string, object> resultDic,
                                 Exception ex)
        {
            CreateSingleRow(inputDic, outputDic);
            SetSingleRowResult(resultDic, ex);
        }

        public void CreateSingleRow(Dictionary<string, string> inputDic,
                                 Dictionary<string, string> outputDic)
        {
            AddKeys(inputDic, inputKeys);
            AddKeys(outputDic, outputKeys);
            singleRows.Add(new RowResult(inputDic, outputDic));
        }

        public void SetSingleRowResult(Dictionary<string, object> resultDic,
                                       Exception ex)
        {
            System.DateTime partEndTime = System.DateTime.Now;
            RowResult row = singleRows[singleRows.Count - 1];
            row.resultDic = resultDic;
            row.fixException = ex;
            row.partTime = partEndTime - partStartTime;
            partStartTime = partEndTime;
        }

        public void CreateMultiRows(Dictionary<string, string> inputDic,
                                 List<Dictionary<string, string>> outputList)
        {
            AddKeys(inputDic, inputKeys);
            singleRows.Add(new RowResult(inputDic, null));
            foreach (Dictionary<string, string> outputDic in outputList)
            {
                AddKeys(outputDic, multiRowKeys);
            }
            multiRows = new MultiRowResult(outputList);
        }

        public void CreateMultiRows(Dictionary<string, string> inputDic,
                                 List<string> outputKeys,
                                    bool allowSurplus)
        {
            AddKeys(inputDic, inputKeys);
            singleRows.Add(new RowResult(inputDic, null));
            this.allowSurplus = allowSurplus;
            multiRowKeys = outputKeys;
            multiRows = new MultiRowResult(new List<Dictionary<string, string>>());
        }

        public void SetMultiRowsResult(List<Dictionary<string, object>> resultList,
                                       Exception ex)
        {
            System.DateTime endTime = System.DateTime.Now;
            singleRows[0].partTime = endTime - startTime;
            singleRows[0].fixException = ex;
            multiRows.multiResult = resultList;
        }

        public void SetRowError(Exception ex)
        {
            SetSingleRowResult(null, ex);
        }

        public void LoadOutputKeys(Dictionary<string, string> outputDic)
        {
            foreach (string key in outputDic.Keys)
            {
                outputKeys.Add(key);
            }
        }

        public System.TimeSpan GetRunTime()
        {
            System.DateTime endTime = System.DateTime.Now;
            return endTime - startTime;
        }

        public bool IsSuccessful()
        {
            return successful;
        }

        public void TestFailed()
        {
            successful = false;
        }

        private void AddKeys(Dictionary<string, string> dic, List<string> keys)
        {
            if (dic != null)
            {
                foreach (string key in dic.Keys)
                {
                    if (!keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }
            }
        }

    }
}
