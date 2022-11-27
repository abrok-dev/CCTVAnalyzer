using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using System.Threading;
using System.IO;


namespace CCTVAnalyzer
{
    public partial class Form1 : Form
    {
        public delegate void InvokeDrawImage();

        Dictionary<string, Camera> cameraList = new Dictionary<string, Camera>();

        protected Queue<byte[]> queueLog = new Queue<byte[]>();
        protected bool logging = true;
        public static string activeCameraName = "custom";
        public static string logFilePath = "";
        Bitmap displayImage = new Bitmap(1000, 1000);
        public Form1()
        {

            InitializeComponent();
            // Camera someCamera = new Camera("rtsp://admin:12345@71.70.194.6", 20, 20, 20, "not_blur");
            Camera someCamera = new Camera("C:\\Users\\world\\Desktop\\дипломчик_прога\\zaslon_4.mp4", 150, 40, "custom", 0);
            Task someTask = new Task(() => someCamera.startCompute());
            someTask.Start();
            someCamera.calculateImage += displayImageToBox;
            someCamera.Log += addMessage;
            Task logger = new Task(() => writeLog());
            logger.Start();
        }

        public void addMessage(byte[] message)
        {
            this.queueLog.Enqueue(message);
        }

        protected void writeLog()
        {
            while (logging)
            {
                Thread.Sleep(10000);
                byte[] buffer;
                if (logFilePath != "")
                {
                    try
                    {
                        FileStream fs = new FileStream(logFilePath, FileMode.Append);

                        while (queueLog.Count > 0)
                        {
                            buffer = queueLog.Dequeue();
                            fs.Write(buffer, 0, buffer.Length);
                        }
                        fs.Close();
                    } catch
                    {
                      
                    }
                }
            }
        }

        public void displayImageToBox(Mat image)
        {
            displayImage = image.ToBitmap();
            IAsyncResult res = pictureBox1.BeginInvoke(new InvokeDrawImage(DrawImage));
           
            image.Dispose();

            pictureBox1.EndInvoke(res);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        public void DrawImage()
        {
            if (pictureBox1.Image != null)
            {
                pictureBox1.Image.Dispose();
            }

            pictureBox1.Image = displayImage;

        }

        private void CreateNewCamera(string cameraName, int thresholdLight, int thresholdBlur, string cameraIP)
        {
            Camera someCamera = new Camera(cameraIP, thresholdBlur, thresholdLight , cameraName);
            cameraList.Add(cameraName, someCamera);
            Task someTask = new Task(() => someCamera.startCompute());
            comboBox1.Items.Add(cameraName);
            someTask.Start();
            someCamera.calculateImage += displayImageToBox;
            someCamera.Log += addMessage;
        }

        private void AddCameraButton_Click(object sender, EventArgs e)
        {
            Form2 inputBox = new Form2();
            inputBox.getNewCameraData += CreateNewCamera;
            inputBox.ShowDialog();
        }

        private void comboBox1_TextUpdate(object sender, EventArgs e)
        {
            
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (cameraList.ContainsKey(comboBox1.Text))
            {
                Task someTask = new Task(() => cameraList[activeCameraName].startCompute());
                someTask.Start();
            }
        }

        private void buttonHold_Click(object sender, EventArgs e)
        {
            cameraList[activeCameraName].Hold();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            cameraList[activeCameraName].Stop();
        }

        private void buttonChange_Click(object sender, EventArgs e)
        {
            cameraList[activeCameraName].changeParam(Convert.ToDouble(textBoxBlur.Text), Convert.ToDouble(textBoxLight.Text), "");
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            activeCameraName = comboBox1.Text;
            textBoxBlur.Text = cameraList[activeCameraName].blurTrashHoldValue.ToString();
            textBoxLight.Text = cameraList[activeCameraName].lightThresholdValue.ToString();
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            cameraList[activeCameraName].Dispose();
            cameraList.Remove(activeCameraName);
            comboBox1.Items.Remove(activeCameraName);
            textBoxBlur.Text = "";
            textBoxLight.Text = "";
        }

        private void buttonSetLogFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    logFilePath = openFileDialog.FileName;
                    textBoxLogPath.Text = openFileDialog.FileName;
                }
            }
        }
    }
}
