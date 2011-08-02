using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Xml;

namespace SeeFlawGui
{
    class ConfigArgumentControl
    {
        private CheckBox preCaseCB;
        private CheckBox postCaseCB;
        private TextBox preCaseTB;
        private TextBox postCaseTB;
        private Panel labelPanel;
        private Panel textboxPanel;
        private int delta = 30;
        private int Y = 0;

        public ConfigArgumentControl(Panel cfgLabelPanel, Panel cfgTextboxPanel,
                                    CheckBox preCB, TextBox preTB,
                                    CheckBox postCB, TextBox postTB)
        {
            this.labelPanel = cfgLabelPanel;
            this.textboxPanel = cfgTextboxPanel;
            this.preCaseCB = preCB;
            this.preCaseTB = preTB;
            this.postCaseCB = postCB;
            this.postCaseTB = postTB;
        }

        public Dictionary<string, string> GetTestArguments()
        {
            Dictionary<string, string> argDic = GetArguments();
            if (preCaseCB.Checked && preCaseTB.Text != "")
            {
                argDic.Add("PreCase", preCaseTB.Text);
            }
            if (postCaseCB.Checked && postCaseTB.Text != "")
            {
                argDic.Add("PostCase", postCaseTB.Text);
            }
            return argDic;
        }

        public Dictionary<string, string> GetConfigArguments()
        {
            Dictionary<string, string> argDic = GetArguments();
            argDic.Add("PreCaseChecked", preCaseCB.Checked.ToString());
            argDic.Add("PreCase", preCaseTB.Text);
            argDic.Add("PostCaseChecked", postCaseCB.Checked.ToString());
            argDic.Add("PostCase", postCaseTB.Text);
            return argDic;
        }

        private Dictionary<string, string> GetArguments()
        {
            Dictionary<string, string> argDic = new Dictionary<string, string>();
            for (int i = 0; i < labelPanel.Controls.Count; i++)
            {
                Label lbl = (Label)labelPanel.Controls[i];
                TextBox tb = (TextBox)textboxPanel.Controls[i];
                if (lbl != null && tb != null)
                {
                    argDic.Add(lbl.Text, tb.Text);
                }
            }
            return argDic;
        }

        public void AddArgument(string argName, string argValue)
        {
            if(argName == "PreCaseChecked")
            {
                preCaseCB.Checked = argValue.ToUpper() == "TRUE";
                return;
            }
            if(argName == "PreCase")
            {
                preCaseTB.Text = argValue;
                return;
            }
            if (argName == "PostCaseChecked")
            {
                postCaseCB.Checked = argValue.ToUpper() == "TRUE";
                return;
            }
            if (argName == "PostCase")
            {
                postCaseTB.Text = argValue;
                return;
            }

            Label lbl = new Label();
            lbl.Location = new System.Drawing.Point(0, Y);
            lbl.AutoSize = true;
            lbl.Text = argName;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            labelPanel.Controls.Add(lbl);


            TextBox tb = new TextBox();
            tb.Size = new Size(300, 10);
            tb.Text = argValue;
            tb.Location = new System.Drawing.Point(0, Y);
            textboxPanel.Controls.Add(tb);
            Y += delta;
        }

        public void AddArgument()
        {
            string argVal = "";
            DialogResult result = InputTextBox("Add argument", "Argument name:", ref argVal);
            if (result == DialogResult.OK)
            {
                System.Console.WriteLine("OK " + argVal);
                if (GetArguments().ContainsKey(argVal))
                {
                    string msg = "An argument with that name already exists!";
                    MessageBox.Show(msg);
                    return;
                }
                AddArgument(argVal, "");
            }
            else
            {
                System.Console.WriteLine("Not OK");
            }
        }

        public void RemoveAllArguments()
        {
            preCaseCB.Checked = false;
            preCaseTB.Text = "";
            postCaseCB.Checked = false;
            postCaseTB.Text = "";
            labelPanel.Controls.Clear();
            textboxPanel.Controls.Clear();
            Y = 0;
        }

        private void RemoveArgument(string arg)
        {
            bool found = false;
            Label lblRemove = null;
            TextBox tbRemove = null;
            for (int i = 0; i < labelPanel.Controls.Count; i++)
            {
                Label lbl = (Label)labelPanel.Controls[i];
                TextBox tb = (TextBox)textboxPanel.Controls[i];
                if (lbl != null && tb != null)
                {
                    if (!found)
                    {
                        if (lbl.Text == arg)
                        {
                            found = true;
                            lblRemove = lbl;
                            tbRemove = tb;
                            Y -= delta;
                        }
                    }
                    else
                    {
                        lbl.Location = new Point(0, lbl.Location.Y - delta);
                        tb.Location = new Point(0, tb.Location.Y - delta);
                    }
                }
            }
            if (lblRemove != null && tbRemove != null)
            {
                labelPanel.Controls.Remove(lblRemove);
                textboxPanel.Controls.Remove(tbRemove);
            }
        }

        public void RemoveArgument()
        {
            string[] args = GetArguments().Keys.ToArray<string>();
            string removeArg = "";
            DialogResult result = InputComboBox("Remove argument", "Argument name:", args, ref removeArg);
            if (result == DialogResult.OK)
            {
                RemoveArgument(removeArg);
            }
        }

        public static DialogResult InputComboBox(string title, string promptText, object[] items, ref string value)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(items);
            if (items.Count() > 0)
            {
                comboBox.SelectedItem = items[0];
            }
            return InputBox(title, promptText, comboBox, ref value);
        }
        
        public static DialogResult InputTextBox(string title, string promptText, ref string value)
        {
            TextBox textBox = new TextBox();
            return InputBox(title, promptText, textBox, ref value);
        }

        private static DialogResult InputBox(string title, string promptText, Control textBox, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }

    }
}
