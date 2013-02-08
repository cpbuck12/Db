using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
namespace Concierge_Manager
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            webBrowser.Navigated += new WebBrowserNavigatedEventHandler(webBrowser_Navigated);
            webBrowser.ObjectForScripting = this;
        }

        public void webBrowser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            string s = Properties.Settings.Default.ProjectDir;
            s = s + s;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            string s = Properties.Settings.Default.ProjectDir;
            //webBrowser.Navigate(@"c:\users\pacmny_local\documents\visual~1\Projects\Db\concie~1\web\public\work.htm");
            webBrowser.Navigate("http://localhost:50505");
            s = Properties.Settings.Default.ProjectDir;
            webBrowser.WebBrowserShortcutsEnabled = false; // prevents F5 from refreshing.  probably does nasty side effects
            webBrowser.ObjectForScripting = new ObjectForScripting();

        }
    }
}
