using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SeeFlawRunner
{
    public class ExampleFixture
    {
        public static string GetParamExampleMethod()
        {
            return "param test";
        }
        public static string GetParamWithInputExampleMethod(Dictionary<string, string> input)
        {
            return "param " + input["first"];;
        }

        public void NoOutputExampleMethod(Dictionary<string, string> input)
        {
            System.Console.WriteLine("NoOutputExampleMethod called");
            string name;
            input.TryGetValue("name", out name);
            if (name == null)
            {
                throw new Exception("Mandatory input 'name' missing.");
            }
        }

        public Dictionary<string, object> SingleOutputExampleMethod(Dictionary<string, string> input, List<string> outputKeys)
        {
            System.Console.WriteLine("SingleOutputExampleMethod called");

            string name;
            input.TryGetValue("name", out name);
            if (name == null)
            {
                throw new Exception("Mandatory input 'name' missing.");
            }
            Dictionary<string, object> output = new Dictionary<string, object>();
            output.Add("message", "Hello " + name);
            input["name"] = "not in result";
            return output;
        }

        public List<Dictionary<string, object>> MultiOutputExampleMethod(Dictionary<string, string> input, List<string> outputKeys)
        {
            System.Console.WriteLine("MultiOutputExampleMethod called");
            
            string lines;
            input.TryGetValue("lines", out lines);
            if (lines == null)
            {
                throw new Exception("Mandatory input 'lines' missing.");
            }
            int nrOfLines = System.Int32.Parse(lines);

            List<Dictionary<string, object>> outList = new List<Dictionary<string, object>>();
            for (int line = 1; line <= nrOfLines; line++)
            {
                Dictionary<string, object> output = new Dictionary<string, object>();
                output.Add("message", "line" + line);
                outList.Add(output);
            }
            return outList;
        }

        public Dictionary<string, object> SingleOutputDifferentObjectTypesExampleMethod(Dictionary<string, string> input, List<string> outputKeys)
        {
            System.Console.WriteLine("SingleOutputDifferentObjectTypesExampleMethod called");

            Dictionary<string, object> output = new Dictionary<string, object>();
            int i = 12345;
            long l = 12345;
            double d = 12345.12345;
            output.Add("int", i);
            output.Add("long", l);
            output.Add("double", d);
            return output;
        }

        public Dictionary<string, object> SingleOutputXmlInputExampleMethod(XmlNode inputNode, List<string> outputKeys)
        {
            System.Console.WriteLine("SingleOutputXmlInputExampleMethod called");

            Dictionary<string, object> output = new Dictionary<string, object>();
            foreach (XmlNode childNode in inputNode)
            {
                System.Console.WriteLine(childNode.Name);
                if (output.ContainsKey(childNode.Name))
                {
                    object nr = null;
                    output.TryGetValue(childNode.Name, out nr);
                    if (nr != null && nr.GetType() == typeof(int))
                    {
                        int sum = int.Parse(nr.ToString()) + 1;
                        output.Remove(childNode.Name);
                        output.Add(childNode.Name, sum);
                    }
                }
                else
                {
                    output.Add(childNode.Name, 1);
                }
            }
            return output;
        }

        public List<Dictionary<string, object>> MultiOutputXmlInputExampleMethod(XmlNode inputNode, List<string> outputKeys)
        {
            System.Console.WriteLine("MultiOutputXmlInputExampleMethod called");

            List<Dictionary<string, object>> outList = new List<Dictionary<string, object>>();
            foreach (XmlNode childNode in inputNode)
            {
                Dictionary<string, object> output = new Dictionary<string, object>();
                foreach(XmlAttribute attr in childNode.Attributes)
                {
                    output.Add(attr.Name, attr.Value);
                }
                outList.Add(output);
            }
            return outList;
        }

        public void Sleep(Dictionary<string, string> input)
        {
            System.Console.WriteLine("Sleep called");
            
            string seconds;
            input.TryGetValue("seconds", out seconds);
            if (seconds == null)
            {
                throw new Exception("Mandatory input 'seconds' missing.");
            }

            System.Threading.Thread.Sleep(1000 * Int32.Parse(seconds));
        }

    }
}
