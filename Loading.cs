using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace vividstasis_Text_Editor
{
    public partial class Loading : Form
    {
        public Loading()
        {
            InitializeComponent();
        }

        public void EditLoadingMessage(string msg)
        {
            if(label2.InvokeRequired)
            {
                label2.Invoke(new Action(() => { label2.Text = msg; }));
            } else
            {
                label2.Text = msg;
            }
        }

        public void EditLoadingTitle(string msg)
        {
            if(label1.InvokeRequired)
            {
                label2.Invoke(new Action(() => { label1.Text = msg; }));
            } else
            {
                label1.Text = msg;
            }
        }
    }
}
