using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SeeFlawGui
{
    public partial class Form1 : Form
    {
        private ConfigArgumentControl cfgArgCtrl;
        private SeeFlawRunner.BackgroundRunner bgWorker;
        private string startUpDir = System.IO.Directory.GetCurrentDirectory();
        private string lastLoadDir = System.IO.Directory.GetCurrentDirectory();

        public Form1()
        {
            InitializeComponent();
            InitializeBackGroundWorker();
            cfgArgCtrl = new ConfigArgumentControl(cfgLabelPanel, cfgSplitContainer.Panel2, cbPreCase, tbPreCase, cbPostCase, tbPostCase);
            
            this.Icon = Properties.Resources.SeeFlaw;
            this.Text = "SeeFlaw";
            //Cursor.Current = new Cursor("MyWait.cur");
            string startFile = "";
            if (Environment.GetCommandLineArgs().Count() > 1)
            {
                startFile = Environment.GetCommandLineArgs()[Environment.GetCommandLineArgs().Count()-1];
                if (!System.IO.File.Exists(startFile))
                {
                    MessageBox.Show("No such file " + startFile);
                    startFile = "";
                }
            }
            if (startFile != "")
            {
                OpenFile(startFile);
            }
            else
            {
                CreateTreeNode("first", false);
            }
            runTreeView.ImageList = NodeColors.GetColorList();
            
            //Cursor.Current = Cursors.Default;
        }

        private void InitializeBackGroundWorker()
        {
            bgWorker = new SeeFlawRunner.BackgroundRunner();

            bgWorker.WorkerReportsProgress = true;
            bgWorker.WorkerSupportsCancellation = true;
            bgWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgWorker_DoWork);
            bgWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.bgWorker_RunWorkerCompleted);
            bgWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.bgWorker_ProgressChanged);
        }

        private void CreateTreeNode(string root, bool add)
        {
            RootNode rootNode = new RootNode(root);
            AddCreatedTreeRootNode(rootNode, add);
        }

        private void AddCreatedTreeRootNode(RootNode rootNode, bool add)
        {
            runTreeView.BeginUpdate();
            if (!add)
            {
                runTreeView.Nodes.Clear();
            }
            rootNode.Expand();
            runTreeView.Nodes.Add(rootNode);
            runTreeView.EndUpdate();
        }

        private RootNode GetSelectedNode()
        {
            if (runTreeView.SelectedNode != null)
            {
                return (RootNode) runTreeView.SelectedNode;
            }
            else 
            {
                return (RootNode) runTreeView.Nodes[0];
            }
        }

        private void CreateTreeNodeFromDir(bool add)
        {
            RootNode rootNode = GuiHelper.CreateRootDirectory();
            if (rootNode == null)
            {
                string msg = "No xml testfiles in this directory";
                MessageBox.Show(msg);
            }
            else
            {
                AddCreatedTreeRootNode(rootNode, add);
            }
        }

        private void CreateTreeNodeFromName(bool add)
        {
            string rootVal = "";
            DialogResult result = ConfigArgumentControl.InputTextBox("New tree", "Tree name:", ref rootVal);
            if (result == DialogResult.OK && rootVal != "")
            {
                CreateTreeNode(rootVal, add);
            }
        }

        private void AddDirectory(bool includeSubDirs)
        {
            this.treeNodeCTMS.Close();
            RootNode node = GetSelectedNode();
            GuiHelper.AddDirectory(node, includeSubDirs);
        }

        private void AddGroup()
        {
            RootNode node = GetSelectedNode();
            if (node.CanAddGroup())
            {
                string argVal = "";
                DialogResult result = ConfigArgumentControl.InputTextBox("Add group", "Group name:", ref argVal);
                if (result == DialogResult.OK)
                {
                    node.Nodes.Add(new GroupNode(argVal));
                    node.Expand();
                }
            }
        }

        private void AddTest()
        {
            RootNode node = GetSelectedNode();
            if (node.CanAddTest())
            {

                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "xml files (*.xml)|*.xml";
                dialog.InitialDirectory = lastLoadDir;
                dialog.Title = "Select a test file";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string testName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                    TestNode testNode = new TestNode(testName, dialog.FileName);
                    node.Nodes.Add(testNode);
                    node.Expand();
                }
                lastLoadDir = System.IO.Directory.GetCurrentDirectory();
            }
        }

        private RootNode GetTreeRootNode(RootNode childNode)
        {
            RootNode parentNode = childNode;
            while(parentNode.Parent != null)
            {
                parentNode = (RootNode) parentNode.Parent;
            }
            return parentNode;
        }

        private void RunAllTreeTest()
        {
            System.IO.Directory.SetCurrentDirectory(startUpDir);
            this.DisableControls();
            int nrOfTests = 0;
            foreach (RootNode rootNode in runTreeView.Nodes)
            {
                rootNode.ClearResults();
                nrOfTests += rootNode.Count();
            }
            this.progressBar1.Value = 0;
            this.Refresh();
            this.progressBar1.Maximum = nrOfTests + 1;
            this.progressBar1.Step = 1;
            this.progressBar1.PerformStep();

            bgWorker.RunWorkerAsync(runTreeView.Nodes);
        }

        private void RunTest(bool refresh)
        {
            System.IO.Directory.SetCurrentDirectory(startUpDir);
            this.treeNodeCTMS.Close();
            this.DisableControls();
            RootNode node = GetSelectedNode();
            if (refresh)
            {
                GetTreeRootNode(node).ClearResults();
            }
            
            node.ClearResults();
            this.progressBar1.Value = 0;
            this.Refresh();
            int nrOfTests = node.Count();
            this.progressBar1.Maximum = nrOfTests + 1;
            this.progressBar1.Step = 1;
            this.progressBar1.PerformStep();
            
            bgWorker.RunWorkerAsync(node);
        }

        private void RemoveNode()
        {
            RootNode node = GetSelectedNode();
            if (node.GetType() != typeof(TestNode) && node.GetType() != typeof(GroupNode))
            {
                string msg = "The top node " + node.Text + " can not be removed";
                MessageBox.Show(msg);
            }
            else
            {
                string msg = "Do you really want to remove the " + node.Text + " node?";
                DialogResult result = MessageBox.Show(msg, "Remove node", MessageBoxButtons.YesNo);
                if (result == DialogResult.Yes)
                {
                    node.Parent.Nodes.Remove(node);
                }
            }
        }

        private void showNode()
        {
            RootNode node = GetSelectedNode();
            string testFile = node.GetTestFile();
            this.testcaseWebBrowser.DocumentText = "";
            if (testFile != "")
            {
                this.testcaseWebBrowser.Navigate(testFile);
            }
            this.rtbLog.Text = node.GetLog();
            this.resultWebBrowser.DocumentText = node.GetResult();
        }

        private void DisableControls()
        {
            btnRun.Enabled = false;
            btnRun.Visible = false;
            btnStop.Enabled = true;
            btnStop.Visible = true;
            treeNodeCTMS.Enabled = false;
            menuStrip1.Enabled = false;
            btnCfgAdd.Enabled = false;
        }

        public void EnableControls()
        {
            btnRun.Enabled = true;
            btnRun.Visible = true;
            btnStop.Enabled = false;
            btnStop.Visible = false;
            btnKill.Enabled = false;
            btnKill.Visible = false;
            treeNodeCTMS.Enabled = true;
            menuStrip1.Enabled = true;
            btnCfgAdd.Enabled = true;
        }

        private void btnCfgAdd_Click(object sender, EventArgs e)
        {
            cfgArgCtrl.AddArgument();
        }

        private void btnCfgRemove_Click(object sender, EventArgs e)
        {
            cfgArgCtrl.RemoveArgument();
        }

        private void fileOpenTlMI_Click(object sender, EventArgs e)
        {
            OpenFile(null);
        }

        private void OpenFile(string fileName)
        {
            List<RootNode> rootNodes = GuiHelper.OpenFile(cfgArgCtrl, fileName);
            if (rootNodes.Count == 0)
            {
                MessageBox.Show("No tree was build");
            }
            else
            {
                runTreeView.BeginUpdate();
                runTreeView.Nodes.Clear();
                runTreeView.Nodes.AddRange(rootNodes.ToArray());
                runTreeView.EndUpdate();
            }
        }

        private void fileSaveTSMI_Click(object sender, EventArgs e)
        {
            GuiHelper.SaveFile(cfgArgCtrl, runTreeView);
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void nodeAddGroupToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AddGroup();
        }

        private void nodeAddTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddTest();
        }

        private void runTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            showNode();
        }

        private void runTreeView_MouseUp(object sender, MouseEventArgs e)
        {
            // Point where the mouse is clicked.
            Point p = new Point(e.X, e.Y);

            // Get the node that the user has clicked.
            RootNode node = (RootNode) runTreeView.GetNodeAt(p);
            if (node != null)
            {
                runTreeView.SelectedNode = node;

                treeNodeAddGroupTSMI.Enabled = node.CanAddGroup();
                nodeAddGroupToolStripMenuItem1.Enabled = node.CanAddGroup();
                treeNodeAddTesTSMI.Enabled = node.CanAddTest();
                nodeAddTestToolStripMenuItem.Enabled = node.CanAddTest();
                // Show menu only if the right mouse button is clicked.
                if (e.Button == MouseButtons.Right)
                {
                    treeNodeCTMS.Show(runTreeView, p);
                }
            }
        }

        private void treeNodeAddGroupTSMI_Click(object sender, EventArgs e)
        {
            AddGroup();
        }

        private void treeNodeAddTesTSMI_Click(object sender, EventArgs e)
        {
            AddTest();
        }

        private void nodeRunToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunTest(true);
        }

        private void treeNodeRunTSMI_Click(object sender, EventArgs e)
        {
            RunTest(true);
        }

        private void treeNodeAddDirectoryTSMI_Click(object sender, EventArgs e)
        {
            AddDirectory(false);
        }

        private void treeNodeRunNoRefreshTSMI_Click(object sender, EventArgs e)
        {
            RunTest(false);
        }

        private void noRefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunTest(false);
        }
        
        private void withRefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunTest(true);
        }

        private void andSubdirectoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddDirectory(true);
        }

        private void treeNodeAddDirAndSubDirsTSMI_Click(object sender, EventArgs e)
        {
            AddDirectory(true);
        }

        private void treeNodeAddOnlyDirTSMI_Click(object sender, EventArgs e)
        {
            AddDirectory(false);
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveNode();
        }

        private void treeNodeRemoveTSMI_Click(object sender, EventArgs e)
        {
            RemoveNode();
        }

        private void treeNewTlSMI_Click(object sender, EventArgs e)
        {
            CreateTreeNodeFromName(false);
        }

        private void treeAddTSMI_Click(object sender, EventArgs e)
        {
            CreateTreeNodeFromName(true);
        }

        private void treeNewFromDirTSMI_Click(object sender, EventArgs e)
        {
            CreateTreeNodeFromDir(false);
        }

        private void treeAddFromDirTSMI_Click(object sender, EventArgs e)
        {
            CreateTreeNodeFromDir(true);
        }

        private void treeRunAllTSMI_Click(object sender, EventArgs e)
        {
            RunAllTreeTest();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            RunTest(true);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            bgWorker.CancelAsync();
            btnStop.Enabled = false;
            btnStop.Visible = false;
            btnKill.Enabled = true;
            btnKill.Visible = true;
        }

        private void btnKill_Click(object sender, EventArgs e)
        {
            string msg = "By killing a testprocess you might need to restart the application.";
            msg += "\nAre you sure you want to kill?";
            DialogResult result = MessageBox.Show(msg, "Kill test process", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                if (btnKill.Enabled)// Perhaps stop has end the run already
                {
                    bgWorker.KillAsync();
                    btnKill.Enabled = false;
                }
            }
        }

        private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Dictionary<string, string> argDic = cfgArgCtrl.GetTestArguments();
            if (e.Argument.GetType() == typeof(TreeNodeCollection))
            {
                TreeNodeCollection nodes = (TreeNodeCollection)e.Argument;
                foreach (RootNode rootNode in nodes)
                {
                    rootNode.RunTestSuite(argDic, bgWorker);
                    if (bgWorker.CancellationPending)
                    {
                        break;
                    }
                }
            }
            else
            {
                RootNode node = (RootNode)e.Argument;
                //Delegate callback = logCallback;
                node.RunTestSuite(argDic, bgWorker);
            }
        }

        private void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            RootNode progressNode = (RootNode)e.UserState;
            if (e.ProgressPercentage == ProgressEvent.LogUpdated)
            {
                if (progressNode.GetType() == typeof(TestNode))
                {
                    if (progressNode == GetSelectedNode())
                    {
                        string nodeLog = progressNode.GetLog();
                        if (nodeLog.StartsWith(this.rtbLog.Text))
                        {
                            this.rtbLog.Text += nodeLog.Substring(this.rtbLog.Text.Length);
                        }
                        else
                        {
                            this.rtbLog.Text = progressNode.GetLog();
                        }
                    }
                }
            }
            else
            {
                progressNode.ShowColor(e.ProgressPercentage);

                if (progressNode.GetType() == typeof(TestNode))
                {
                    progressBar1.PerformStep();
                    if (progressNode == GetSelectedNode())
                    {
                        showNode();
                    }
                }
            }
        }

        private void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.EnableControls();
            bgWorker.Reset();
        }

        private void cbPreCase_CheckedChanged(object sender, EventArgs e)
        {
            tbPreCase.Enabled = cbPreCase.Checked;
        }

        private void cbPostCase_CheckedChanged(object sender, EventArgs e)
        {
            tbPostCase.Enabled = cbPostCase.Checked;
        }
    }
}
