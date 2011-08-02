using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using SeeFlawRunner;

namespace SeeFlawConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length <= 1)
                {
                    ShowUsage();
                }
                Dictionary<string, string> argsDic = ReadArguments(args);
                if (!argsDic.ContainsKey("testfile"))
                {
                    throw new Exception("TestFile argument missing");
                }
                string testFile = argsDic["testfile"];
                if (!System.IO.File.Exists(testFile))
                {
                    throw new Exception("No such TestFile: " + testFile);
                }
                string outFile = "";
                string outXmlFile = "";
                argsDic.TryGetValue("outfile", out outFile);
                argsDic.TryGetValue("outxmlfile", out outXmlFile);

                XmlDocument testDoc = new XmlDocument();
                testDoc.Load(testFile);
                XmlDocument outDoc = new XmlDocument();
                XmlDocument outXmlDoc = null;
                if (!string.IsNullOrEmpty(outXmlFile))
                {
                    outXmlDoc = new XmlDocument();
                }
                SeeFlaw runner = new SeeFlaw();
                runner.AddDetails(testDoc, outXmlDoc, outDoc, argsDic, null, null);
                bool successful = runner.RunTestCaseSuccessful();
                if (!string.IsNullOrEmpty(outFile))
                {
                    outDoc.Save(outFile);
                }
                if (!string.IsNullOrEmpty(outXmlFile))
                {
                    outXmlDoc.Save(outXmlFile);
                }
                if (!successful)
                {
                    Environment.Exit(2);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                Environment.Exit(1);
            }
        }

        public static void ShowUsage()
        {
            string usage = "\nSeeFlawConsole -TestFile [-OutFile] [-OutXmlFile] [-ConfigFile]";
            usage += "\n";
            usage += "\n-TestFile   The xml testfile";
            usage += "\n-OutFile    The resulting out file in html format";
            usage += "\n-OutXmlFile The resulting out file in xml format";
            usage += "\n-ConfigFile The path to a xml config file containing arguments";
            usage += "\n-AnyOther   All other arguments will be passed to SeeFlaw and possible to use as a param";
            System.Console.WriteLine(usage);
            Environment.Exit(1);
        }

        public static Dictionary<string, string> ReadArguments(string[] args)
        {
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            string argName = "";
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    argName = arg.Substring(1, arg.Length - 1).ToLower();
                }
                else
                {
                    argDic.Add(argName, arg);
                }
            }
            if (argDic.ContainsKey("configfile"))
            {
                ReadConfigFile(argDic);
            }
            return argDic;
        }

        public static void ReadConfigFile(Dictionary<string, string> argDic)
        {
            string configFile = argDic["configfile"];
            if (!System.IO.File.Exists(configFile))
            {
                throw new Exception("No such ConfigFile: " + configFile);
            }
            XmlDocument doc = new XmlDocument();
            doc.Load(configFile);
            foreach (XmlNode node in doc.ChildNodes)
            {
                if (node.Name == "seeflawconfig")
                {
                    foreach (XmlAttribute attr in node.Attributes)
                    {
                        argDic.Add(attr.Name.ToLower(), attr.Value);
                    }
                    foreach (XmlNode childNode in node.ChildNodes)
                    {
                        argDic.Add(childNode.Name.ToLower(), childNode.InnerText);
                    }
                }
            }
        }

        [Test]
        public void ReadArguments()
        {
            string[] args = new string[] { "-infile", "test.xml", "-user", "Kalle", "-pwd", "some" };
            Dictionary<string, string> result = ReadArguments(args);
            NUnit.Framework.Assert.AreEqual(3, result.Count);
            string infile;
            result.TryGetValue("infile", out infile);
            NUnit.Framework.Assert.AreEqual("test.xml", infile);
            string user;
            result.TryGetValue("user", out user);
            NUnit.Framework.Assert.AreEqual("Kalle", user);
            string pwd;
            result.TryGetValue("pwd", out pwd);
            NUnit.Framework.Assert.AreEqual("some", pwd);
        }
    }
}
