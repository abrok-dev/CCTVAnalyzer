using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;
using System.Drawing;
using Accord.Statistics;
using static System.GC;
using System.Threading;

namespace CCTVAnalyzer
{
    public class Camera : IDisposable
    {
        public string url = "";
        public int? cameraNumber = null;
        public double blurTrashHoldValue = 0;
        public double lightThresholdValue = 0;
        public string cameraName;
        VideoCapture capture;
        Point displayBlurPoint = new Point(10, 25);
        Point displaySabotage = new Point(10, 85);
        MCvScalar blurColor = new MCvScalar(255, 0, 0);
        MCvScalar greenColor = new MCvScalar(0, 255, 0);
        MCvScalar redColor = new MCvScalar(0, 0, 255);
        int frameCounter = 0;
        int frameCounterBlur = 0;
        double accumVariance = 0;
        double[] accumFrameParams = new double[4];
        double? linearDistanceAccum = null;
        bool[] lightStatusHistory = { false, false };
        bool[] blurStatusHistory = { false, false };

        public Camera(string url, float blurTrashHoldValue, float lightThresholdValue, string cameraName, int? cameraNumber = null)
        {
            this.url = url;
            this.blurTrashHoldValue = blurTrashHoldValue;
            this.lightThresholdValue = lightThresholdValue;
            this.cameraName = cameraName;
            this.cameraNumber = cameraNumber;
        }

        public delegate void displayImage(Mat mat);
        public event displayImage calculateImage;
        public delegate void LogHandler(byte[] message);
        public event LogHandler Log;

        public void startCompute()
        {
            if (cameraNumber != null)
            {
                capture = new VideoCapture((int)cameraNumber);
            }
            else
            {
                capture = new VideoCapture(url);
            }
            capture.ImageGrabbed += Capture_ImageGrabbed;
            capture.Start();

        }

        public void Restart()
        {
            capture.ImageGrabbed +=  Capture_ImageGrabbed;
            capture.Start();
        }

        public void Hold()
        {
            capture.Pause();
        }

        public void Stop()
        {
            capture.Stop();
            frameCounter = 0;
            accumVariance = 0;
            accumFrameParams = new double[4];
            linearDistanceAccum = null;
            this.lightStatusHistory[0] = false;
            this.lightStatusHistory[1] = false;
            this.blurStatusHistory[0] = false;
            this.blurStatusHistory[1] = false;
        }

        public void changeParam(double? blurTrashHoldValue, double? lightThresholdValue, string cameraName)
        {
            capture.Pause();
            if (blurTrashHoldValue != null)
            {
                this.blurTrashHoldValue = (double)blurTrashHoldValue;
            }
            if (lightThresholdValue != null)
            {
                this.lightThresholdValue = (double)lightThresholdValue;
            }
            if (cameraName != "")
            {
                this.cameraName = cameraName;
            }
            capture.Start();
        }

        private void Capture_ImageGrabbed(object sender, EventArgs e)
        {
            Mat sourceImage = new Mat();
            Mat grayImage = new Mat();
            Mat hsvImage = new Mat();
            capture.Read(sourceImage);
            if (sourceImage.IsEmpty)
            {
                return;
            }
            //Thread.Sleep(200);
            this.frameCounter++;
            this.frameCounterBlur++;
            CvInvoke.CvtColor(sourceImage, grayImage, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
            grayImage.ConvertTo(grayImage, Emgu.CV.CvEnum.DepthType.Cv8U);
            sourceImage = CalculateBlur(sourceImage, grayImage);

            CvInvoke.CvtColor(sourceImage, hsvImage, Emgu.CV.CvEnum.ColorConversion.Rgb2Hsv);
            sourceImage = CalculateSabotage(sourceImage, hsvImage);

            if (Form1.activeCameraName == this.cameraName)
            {
                calculateImage(sourceImage);
            }
            hsvImage.Dispose();
            sourceImage.Dispose();
            grayImage.Dispose();
        }

        private Mat CalculateBlur(Mat sourceImage, Mat grayImage)
        {
            double variance;
            if (frameCounterBlur > 5)
            {
                Mat outlineImage = new Mat();
                CvInvoke.Laplacian(grayImage, outlineImage, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
                CvInvoke.Normalize(outlineImage, outlineImage, 255, 0, Emgu.CV.CvEnum.NormType.MinMax, Emgu.CV.CvEnum.DepthType.Cv8U);
                var mass = outlineImage.GetData();
                byte[] data = new byte[mass.Length];
                double[] data2 = new double[mass.Length];
                Buffer.BlockCopy(mass, 0, data, 0, mass.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    data2[i] = (double)data[i];
                }
                variance = Measures.Variance(data2);
                outlineImage.Dispose();
                accumVariance = variance;
                frameCounterBlur= 0;
            }
            else
            {
                variance = accumVariance;
            }

            if (variance > blurTrashHoldValue)
            {
                CvInvoke.PutText(sourceImage, string.Format("Not Blurry, varianse= {0:f3}", variance), displayBlurPoint, Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, blurColor, 2);
                blurStatusHistory[0] = blurStatusHistory[1];
                blurStatusHistory[1] = false;
            }
            else
            {
                CvInvoke.PutText(sourceImage, string.Format("Blurry, varianse= {0:f3}", variance), displayBlurPoint, Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, redColor, 2);
                blurStatusHistory[0] = blurStatusHistory[1];
                blurStatusHistory[1] = true;
            }
            grayImage.Dispose();
            LogBlurStatusFile(variance);
            return sourceImage;
        }

        public void LogLightStatusFile(double value)
        {
            if (Form1.logFilePath != "")
            {
                if (!lightStatusHistory[0] && lightStatusHistory[1])
                {
                    byte[] message = Encoding.UTF8.GetBytes(DateTime.Now.ToString() + "  Обнаружено отклонение у камеры:" + cameraName + "  световой гаммы:" + value.ToString() + "\n");
                    Log(message);
                }
                else if (lightStatusHistory[0] && !lightStatusHistory[1])
                {
                    byte[] message = Encoding.UTF8.GetBytes(DateTime.Now.ToString() + "  Световая гамма камеры:" + cameraName +"  вернулась в норму:" + value.ToString() + "\n");
                    Log(message);
                }
            }
        }
        public void LogBlurStatusFile(double value)
        {
            if (Form1.logFilePath != "")
            {
                if (!blurStatusHistory[0] && blurStatusHistory[1])
                {
                    byte[] message = Encoding.UTF8.GetBytes(DateTime.Now.ToString() + "  Обнаружено размытие у камеры " + cameraName +  " : " + value.ToString() + "\n");
                    Log(message);
                }
                else if (blurStatusHistory[0] && !blurStatusHistory[1])
                {
                    byte[] message = Encoding.UTF8.GetBytes(DateTime.Now.ToString() + "  Чёткость изображения  камеры:" + cameraName + "  вернулось в норму:" + value.ToString() + "\n");
                    Log(message);
                }
            }
        }

        public void Dispose()
        {
            capture.Stop();
            capture.Dispose();
        }

        private Mat CalculateSabotage(Mat sourceImage, Mat hsvImage)
        {
            var mass = hsvImage.GetData();
            byte[] flatImage = new byte[mass.Length];
            double[] componentHData = new double[(mass.Length) / 3];
            double linearDistance = 0;

            Buffer.BlockCopy(mass, 0, flatImage, 0, mass.Length);
            int j = 0;
        
            for (int i = 2; i < mass.Length; i = i+3)
            {
                componentHData[j++] = (double)flatImage[i];
            }

            Array.Sort(componentHData);
            var mean = Task.Run(() => Measures.Mean(componentHData));
            var quantile90 = Task.Run(() => Measures.Quantile(componentHData, 0.75, true, QuantileMethod.Type3));
            var median = Task.Run(() => Measures.Median(componentHData, true, QuantileMethod.Type3));
            mean.Wait();
            var std = Task.Run(() => Measures.StandardDeviation(componentHData, mean.Result));
            quantile90.Wait();
            std.Wait();
            median.Wait();
            if (this.frameCounter == 1)
            {
                this.accumFrameParams[0] = quantile90.Result;
                this.accumFrameParams[1] = std.Result;
                this.accumFrameParams[2] = mean.Result;
                this.accumFrameParams[3] = median.Result;
            }
            else if (this.frameCounter > 7)
            {
                double[] currentParams = { quantile90.Result, std.Result, mean.Result, median.Result };
                linearDistance = Accord.Math.Distance.Euclidean(accumFrameParams, currentParams);
                this.frameCounter = 1;

                if (linearDistance > lightThresholdValue)
                {
                    CvInvoke.PutText(sourceImage, string.Format("Light sabotage detected :  {0:f3}", linearDistance), displaySabotage, Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, redColor, 2);
                    lightStatusHistory[0] = lightStatusHistory[1];
                    lightStatusHistory[1] = true;
                }
                else
                {
                    this.accumFrameParams[0] = quantile90.Result;
                    this.accumFrameParams[1] = std.Result;
                    this.accumFrameParams[2] = mean.Result;
                    this.accumFrameParams[3] = median.Result;
                    CvInvoke.PutText(sourceImage, string.Format("Light sabotage no detected :  {0:f3}", linearDistance), displaySabotage, Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, greenColor, 2);
                    lightStatusHistory[0] = lightStatusHistory[1];
                    lightStatusHistory[1] = false;
                }
                linearDistanceAccum = linearDistance;

            }
            else
            {
                if (linearDistanceAccum > lightThresholdValue)
                {
                    CvInvoke.PutText(sourceImage, string.Format("Light sabotage detected :  {0:f3}", linearDistanceAccum), displaySabotage, Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, redColor, 2);
                    lightStatusHistory[0] = lightStatusHistory[1];
                    lightStatusHistory[1] = true;
                }
                else if (linearDistanceAccum >= 0)
                {
                    CvInvoke.PutText(sourceImage, string.Format("Light sabotage no detected :  {0:f3}", linearDistanceAccum), displaySabotage, Emgu.CV.CvEnum.FontFace.HersheySimplex, 1, greenColor, 2);
                    lightStatusHistory[0] = lightStatusHistory[1];
                    lightStatusHistory[1] = false;
                }
            }
            LogLightStatusFile(Convert.ToDouble(linearDistanceAccum));
            quantile90.Dispose();
            std.Dispose();
            mean.Dispose();
            median.Dispose();
            return sourceImage;
        }
    }
}
