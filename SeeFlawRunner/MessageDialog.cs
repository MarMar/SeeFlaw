using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SeeFlawRunner
{
    public class MessageDialog
    {
        public void Show(XmlNode inputNode)
        {
            string message = "";
            foreach(XmlNode rowNode in inputNode.ChildNodes)
            {
                if(rowNode.Name == "row")
                {
                    message += rowNode.InnerText + "\r\n";
                }
            }
        }
    }
}
