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

namespace FanControl
{
    public partial class Form1 : Form
    {
        public bool overridingFan = false;
        public byte fanOffset;
        public int trackBarValue;

        public Form1()
        {
            InitializeComponent();
            try
            {
                fanOffset = (byte)int.Parse(textBox1.Text);
            }
            catch (Exception ex) { }
            try
            {
                trackBar1.Maximum = int.Parse(textBox2.Text);
            }
            catch (Exception ex) { }
            Thread t = new Thread(FanSetter);
            t.IsBackground = true;
            t.Start();
        }

        private void FanSetter()
        {
            while (true)
            {
                if (overridingFan)
                {
                    FanController.ECWrite(fanOffset, (byte)trackBarValue, false);
                }

                Thread.Sleep(1000);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            trackBar1.Maximum = 100;
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            trackBarValue = trackBar1.Value;
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                trackBar1.Maximum = int.Parse(textBox2.Text);
            }
            catch (Exception ex) { }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            overridingFan = checkBox1.Checked;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                fanOffset = (byte)int.Parse(textBox1.Text);
            }
            catch (Exception ex) { }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
        }
    }
}
