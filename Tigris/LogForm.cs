using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Tigris
{
    public partial class LogForm : Form
    {
        public LogForm()
        {
            InitializeComponent();
        }

        public void Log(string log)
        {
            textLog.DeselectAll();
            textLog.SelectionFont = new Font(textLog.SelectionFont, FontStyle.Regular);
            textLog.Text = "";
            textLog.AppendText(log);
            textLog.ScrollToCaret();
            textLog.Refresh();
        }

        public void AddBoldedText(string text)
        {
            string[] str = text.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);

            if (str.Length == 2)
            {
                textLog.DeselectAll();
                textLog.SelectionFont = new Font(textLog.SelectionFont, FontStyle.Bold);
                textLog.AppendText(str[0] + ";");
                textLog.SelectionFont = new Font(textLog.SelectionFont, FontStyle.Regular);
                textLog.AppendText(str[1] + Environment.NewLine);
                textLog.ScrollToCaret();
                textLog.Refresh();
            }
        }

        private void LogForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
