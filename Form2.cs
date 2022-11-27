using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CCTVAnalyzer
{
    public partial class Form2 : Form
    {
        public delegate void inputData(string cameraName, int thresholdLight, int thresholdBlur, string cameraIP);
        public event inputData getNewCameraData;
        public Form2()
        {
            InitializeComponent();
        }

        private void buttonEnter_Click(object sender, EventArgs e)
        {
            string cameraName = textBoxName.Text;
            int thresholdLight = Convert.ToInt32(textBoxLight.Text);
            int thresholdBlur = Convert.ToInt32(textBoxBlur.Text);
            getNewCameraData.Invoke(textBoxName.Text, Convert.ToInt32(textBoxLight.Text), Convert.ToInt32(textBoxBlur.Text), TextBoxIP.Text);
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

