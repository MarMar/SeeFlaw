using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace SeeFlawRunner
{
    public class FixtureDetails
    {
        private Object fixture;
        private MethodInfo method;

        public FixtureDetails(Object f, MethodInfo m)
        {
            fixture = f;
            method = m;
        }
        public Object GetFixture()
        {
            return fixture;
        }
        public MethodInfo GetMethod()
        {
            return method;
        }
    }

    [Serializable]
    public class RunnerDetails
    {
        Dictionary<string, object> fixIdDic = new Dictionary<string, object>();
        Dictionary<string, string> paramDic = new Dictionary<string, string>();
        Dictionary<string, string> argumentDic = new Dictionary<string, string>();
        List<string> appDomainList = new List<string>();
        Dictionary<string, AppDomain> appDomainDic = new Dictionary<string, AppDomain>();
        private string preCaseFile = "";
        private string postCaseFile = "";
        private string pluginPath = "";
        private string testFilePath = "";

        public RunnerDetails(Dictionary<string, string> argDic)
        {
            foreach (string key in argDic.Keys)
            {
                argumentDic[key.ToLower()] = argDic[key];
            }
            SetPaths();
        }

        private void SetPaths()
        {
            if (argumentDic.ContainsKey("testfile"))
            {
                testFilePath = argumentDic["testfile"];
            }
            if (argumentDic.ContainsKey("precase"))
            {
                preCaseFile = argumentDic["precase"];
            }
            if (argumentDic.ContainsKey("postcase"))
            {
                postCaseFile = argumentDic["postcase"];
            }
            if (argumentDic.ContainsKey("pluginpath"))
            {
                pluginPath = argumentDic["pluginpath"];
            }
            if (argumentDic.ContainsKey("pluginappdomain"))
            {
                foreach (string pPath in argumentDic["pluginappdomain"].Split(new char[] { ';' }))
                {
                    appDomainList.Add(pPath);
                }
            }
        }

        public RunnerDetails()
        {}

        public RunnerDetails Copy()
        {
            RunnerDetails rd = new RunnerDetails();
            rd.argumentDic = argumentDic;
            rd.SetPaths();
            return rd;
        }

        public RunnerDetails LoadCopy(string loadFile)
        {
            Dictionary<string, string> loadArgDic = new Dictionary<string, string>();
            foreach (string key in argumentDic.Keys)
            {
                loadArgDic[key] = argumentDic[key];
            }
            string dirPath = System.IO.Directory.GetParent(testFilePath).FullName;
            string loadFilePath = System.IO.Path.Combine(dirPath, loadFile);
            loadArgDic["testfile"] = loadFilePath;
            return new RunnerDetails(loadArgDic);
        }

        public string GetArgument(string key)
        {
            string val = "";
            if (argumentDic.ContainsKey(key.ToLower()))
            {
                val = argumentDic[key.ToLower()];
            }
            return val;
        }

        public void AddParameter(string paramName, string paramValue)
        {
            if (HasParameter(paramName))
            {
                string val = GetParameter(paramName);
                if (paramValue != val)
                {
                    throw new Exception("Tried do add existing parameter " + paramName + ":" + val + " with new value " + paramValue);
                }
            }
            else
            {
                paramDic.Add(paramName, paramValue);
            }
        }

        public bool HasParameters()
        {
            if (paramDic != null && paramDic.Count > 0)
            {
                return true;
            }
            return false;
        }

        public bool HasParameter(string val)
        {
            if (HasParameters() && paramDic.ContainsKey(val))
            {
                return true;
            }
            return false;
        }

        public string GetParameter(string val)
        {
            if (HasParameter(val))
            {
                return paramDic[val];
            }
            return "";
        }

        public bool HasPreCase()
        {
            return preCaseFile != "";
        }
        public string GetPreCase()
        {
            return preCaseFile;
        }
        public bool HasPostCase()
        {
            return postCaseFile != "";
        }
        public string GetPostCase()
        {
            return postCaseFile;
        }

        public Object InitFixture(string fixtureName, string fixtureId)
        {
            object fixtureObj = LoadFixture(fixtureName);
            fixIdDic.Add(fixtureId, fixtureObj);
            return fixtureObj;
        }

        public Object GetFixture(string fixtureName)
        {
            if (fixIdDic.ContainsKey(fixtureName))
            {
                return fixIdDic[fixtureName];
            }
            return LoadFixture(fixtureName);
        }

        public Object GetInitiatedFixture(string fixtureId)
        {
            if (fixIdDic.ContainsKey(fixtureId))
            {
                return fixIdDic[fixtureId];
            }
            return null;        
        }

        private Object LoadFixture(string name)
        {
            if (!name.Contains('.'))
            {
                throw new Exception("fixture '" + name + "' is given in bad format, should be Assembly.Class");
            }
            int delimIndex = name.LastIndexOf('.');
            string assemblyName = name.Substring(0, delimIndex);
            string instanceName = name.Substring(delimIndex + 1);

            if (appDomainList.Contains(assemblyName))
            {
                if (!appDomainDic.ContainsKey(assemblyName))
                {
                    string assemblyFile = GetAssemblyFile(assemblyName);
                    System.IO.FileInfo assFileInfo = new System.IO.FileInfo(assemblyFile);
                    System.IO.FileInfo exeFileInfo = new System.IO.FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (assFileInfo.Directory.FullName != exeFileInfo.Directory.FullName)
                    {
                        string message = "The plugin " + assemblyName + " must be placed in the same directory as the SeeFlaw executing binary.";
                        message += " \nOtherwise the plugin class can not be deserialized.";
                        message += " \nPluginFile: " + assFileInfo.FullName;
                        message += " \nExeDir: " + exeFileInfo.Directory.FullName;
                        throw new Exception(message);
                    }

                    AppDomainSetup domaininfo = new AppDomainSetup();
                    domaininfo.ApplicationName = "SeeFlaw" + "_" + Environment.TickCount;
                    domaininfo.ApplicationBase = assFileInfo.Directory.FullName;
                    string configFile = assemblyFile + ".config";
                    if (System.IO.File.Exists(configFile))
                    {
                        domaininfo.ConfigurationFile = configFile;
                    }

                    AppDomain pluginAppDomain = System.AppDomain.CreateDomain(assemblyName, null, domaininfo);
                    appDomainDic[assemblyName] = pluginAppDomain;
                }
                System.Runtime.Remoting.ObjectHandle handle = appDomainDic[assemblyName].CreateInstance(assemblyName, name);
                Object obj = handle.Unwrap();
                return obj;
            }

            Type type = Type.GetType(name);
            if (type == null)
            {
                try
                {
                    string assemblyFile = GetAssemblyFile(assemblyName);
                    Assembly u = Assembly.LoadFrom(assemblyFile);
                    type = u.GetType(name);
                }
                catch
                {
                    System.Console.WriteLine("Could not find/load Fixture: " + name);
                }
                if (type == null)
                {
                    return null;
                }
            }
            return System.Activator.CreateInstance(type);
        }
        
        private string GetAssemblyFile(string assemblyName)
        {
            string[] fileNames = new string[]{assemblyName + ".dll", assemblyName + ".exe"};
            List<string> dirList = new List<string>();
            if (pluginPath != "")
            {
                foreach (string pPath in pluginPath.Split(new char[] { ';' }))
                {
                    dirList.Add(pPath);
                }
            }
            dirList.Add(".");
            dirList.Add(System.IO.Directory.GetCurrentDirectory());
            dirList.Add(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));

            foreach (string dir in dirList)
            {
                foreach (string fileName in fileNames)
                {
                    string filePath = System.IO.Path.Combine(dir, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        return filePath;
                    }
                }
            }

            string envPath = Environment.GetEnvironmentVariable("Path");
            foreach (string p in envPath.Split(new char[] { ';' }))
            {
                foreach (string fileName in fileNames)
                {
                    string envFile = System.IO.Path.Combine(p, fileName);
                    if (System.IO.File.Exists(envFile))
                    {
                        return envFile;
                    }
                }
            }

            throw new Exception("Could not find any of the files " + fileNames[0] + " or " + fileNames[1]);
        }
    }
}
