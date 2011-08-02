using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SeeFlawRunner
{
    [Serializable]
    public class ErrorParser
    {
        private int errorCounter = 0;

        public ErrorParser()
        {}

        public XmlElement AddXmlError(XmlDocument doc, Exception ex)
        {
            errorCounter++;
            string error = "Exception" + errorCounter;

            XmlElement errorNode = doc.CreateElement("error");
            errorNode.SetAttribute("name", error);
            errorNode.InnerText = GetError(ex);

            return errorNode;
        }

        public string GetError(Exception ex)
        {
            string message = "";
            if (ex.InnerException != null)
            {
                message = ex.InnerException.Message;
                message += "\n";
                message += ex.InnerException.StackTrace;
                //message = ex.InnerException.ToString();
            }
            else
            {
                message = ex.ToString();
            }
            return message;
        }
    }
}
