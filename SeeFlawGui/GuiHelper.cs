using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Xml;

namespace SeeFlawGui
{
    class GuiHelper
    {
        public static void SaveFile(ConfigArgumentControl cfgArgCtrl, TreeView tree)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "xml files (*.xml)|*.xml";
            dialog.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            dialog.Title = "Name for config file";
            if (dialog.ShowDialog() == DialogResult.OK && dialog.FileName != "")
            {
                XmlDocument doc = new XmlDocument();
                XmlElement rootElement = doc.CreateElement("seeflawgui");
                doc.AppendChild(rootElement);
                XmlElement configElement = doc.CreateElement("config");
                rootElement.AppendChild(configElement);
                Dictionary<string, string> argDic = cfgArgCtrl.GetConfigArguments();
                foreach (string key in argDic.Keys)
                {
                    string value = "";
                    argDic.TryGetValue(key, out value);
                    configElement.SetAttribute(key, value);
                }
                foreach (RootNode rootNode in tree.Nodes)
                {
                    rootElement.AppendChild(rootNode.ToXml(doc));
                }
                doc.Save(dialog.FileName);
            }
        }

        public static List<RootNode> OpenFile(ConfigArgumentControl cfgArgCtrl, string fileName)
        {
            if (fileName != null)
            {
                if (!fileName.EndsWith(".xml"))
                {
                    MessageBox.Show("File must be of type xml!");
                    return OpenFileInternal(cfgArgCtrl);
                }
                return OpenFileInternal(cfgArgCtrl, fileName);
            }
            else
            {
                return OpenFileInternal(cfgArgCtrl);
            }
        }

        public static List<RootNode> OpenFileInternal(ConfigArgumentControl cfgArgCtrl)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "xml files (*.xml)|*.xml";
            dialog.InitialDirectory = System.IO.Directory.GetCurrentDirectory();
            dialog.Title = "Name of config file";
            if (dialog.ShowDialog() == DialogResult.OK && dialog.FileName != "")
            {
                return OpenFile(cfgArgCtrl, dialog.FileName);
            }
            return new List<RootNode>();
        }

        public static List<RootNode> OpenFileInternal(ConfigArgumentControl cfgArgCtrl, string fileName)
        {
            List<RootNode> resultList = new List<RootNode>();
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            XmlNode rootElement = doc.FirstChild;
            if (rootElement == null || rootElement.Name != "seeflawgui")
            {
                MessageBox.Show("Xmlfile does not start with a seeflawgui node");
                return resultList;
            }
            cfgArgCtrl.RemoveAllArguments();
            XmlNode configElement = GetElementByName(rootElement, "config");
            if (configElement != null)
            {
                foreach (XmlAttribute attr in configElement.Attributes)
                {
                    cfgArgCtrl.AddArgument(attr.Name, attr.Value);
                }
            }

            foreach (XmlNode treeNode in rootElement.ChildNodes)
            {
                if (treeNode.Name == "tree")
                {
                    XmlAttribute dirAttr = GetAttributeByName(treeNode, "dir");
                    if (dirAttr != null)
                    {
                        if (System.IO.Directory.Exists(dirAttr.Value))
                        {
                            RootNode rootNode = CreateRootDirectory(dirAttr.Value);
                            if (rootNode != null)
                            {
                                resultList.Add(rootNode);
                            }
                        }
                        else
                        {
                            System.Console.WriteLine("No such directory exist " + dirAttr.Value);
                        }
                    }
                    else
                    {
                        XmlAttribute nameAttr = GetAttributeByName(treeNode, "name");
                        {
                            if (nameAttr != null)
                            {
                                RootNode rootNode = new RootNode(nameAttr.Value);
                                foreach (XmlNode childNode in treeNode.ChildNodes)
                                {
                                    RootNode child = BuildTree(childNode);
                                    if (child != null)
                                    {
                                        rootNode.Nodes.Add(child);
                                    }
                                }
                                resultList.Add(rootNode);
                            }
                        }
                    }
                }
            }
            return resultList;
        }

        private static RootNode BuildTree(XmlNode treeNode)
        {
            XmlAttribute nameAttr = GetAttributeByName(treeNode, "name");
            if (treeNode.Name == "test")
            {
                XmlAttribute fileAttr = GetAttributeByName(treeNode, "file");
                if (nameAttr != null && fileAttr != null)
                {
                    return new TestNode(nameAttr.Value, fileAttr.Value);
                }
            }
            else
            {
                if (nameAttr != null)
                {
                    GroupNode groupNode = new GroupNode(nameAttr.Value);
                    foreach (XmlNode childNode in treeNode.ChildNodes)
                    {
                        RootNode child = BuildTree(childNode);
                        if (child != null)
                        {
                            groupNode.Nodes.Add(child);
                        }
                    }
                    return groupNode;
                }
            }
            return null;
        }

        private static XmlNode GetElementByName(XmlNode node, string name)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static XmlAttribute GetAttributeByName(XmlNode node, string name)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                if (attr.Name == name)
                {
                    return attr;
                }
            }
            return null;
        }

        public static RootNode CreateRootDirectory()
        {
            RootNode rootNode = null;
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.SelectedPath = System.IO.Directory.GetCurrentDirectory();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                rootNode = CreateRootDirectory(dialog.SelectedPath);
            }
            return rootNode;
        }

        private static RootNode CreateRootDirectory(string rootPath)
        {
            RootNode rootNode = null;
            string rootName = System.IO.Path.GetFileNameWithoutExtension(rootPath);
            string[] subDirs = System.IO.Directory.GetDirectories(rootPath);
            if (subDirs.Count() > 0)
            {
                foreach (string subDir in subDirs)
                {
                    RootNode subDirNode = AddSubDirs(subDir, true);
                    if (subDirNode != null)
                    {
                        if (rootNode == null)
                        {
                            rootNode = new RootNode(rootName, rootPath);
                        }
                        rootNode.Nodes.Add(subDirNode);
                    }
                }
            }
            return rootNode;
        }

        public static void AddDirectory(RootNode rootNode, bool includeSubDirs)
        {
            if (rootNode.CanAddGroup())
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.SelectedPath = System.IO.Directory.GetCurrentDirectory();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    RootNode dirNode = AddSubDirs(dialog.SelectedPath, includeSubDirs);
                    if (dirNode == null)
                    {
                        MessageBox.Show("No xml testfiles in directory");
                    }
                    else
                    {
                        rootNode.Nodes.Add(dirNode);
                        rootNode.Expand();
                    }
                }
            }
        }

        private static RootNode AddSubDirs(string dirPath, bool includeSubDirs)
        {
            bool hasTests = false;
            string dirName = System.IO.Path.GetFileNameWithoutExtension(dirPath);
            RootNode dirNode = new GroupNode(dirName);
            if (includeSubDirs)
            {
                string[] subDirs = System.IO.Directory.GetDirectories(dirPath);
                if (subDirs.Count() > 0)
                {
                    foreach (string subDir in subDirs)
                    {
                        RootNode subDirNode = AddSubDirs(subDir, includeSubDirs);
                        if (subDirNode != null)
                        {
                            dirNode.Nodes.Add(subDirNode);
                            hasTests = true;
                        }
                    }
                }
            }
            string[] files = System.IO.Directory.GetFiles(dirPath, "*.xml");
            if (files.Count() > 0)
            {
                foreach (string file in files)
                {
                    string testName = System.IO.Path.GetFileNameWithoutExtension(file);
                    TestNode testNode = new TestNode(testName, file);
                    dirNode.Nodes.Add(testNode);
                }
                hasTests = true;
            }
            if (hasTests)
            {
                return dirNode;
            }
            return null;
        }
    }
}
