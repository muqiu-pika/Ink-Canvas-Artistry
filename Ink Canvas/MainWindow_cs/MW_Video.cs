using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public class MW_Video
    {
        private ListBox cameraDeviceList;
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap currentFrame;
        private readonly Border saveResultBorder;
        private readonly TextBlock saveResultIcon;
        private readonly TextBlock saveResultText;
        private readonly Button saveButton;
        private readonly Button captureButton;
        private readonly Button confirmButton;
        private readonly Button cancelButton;
        private readonly Button screenshotButton;

        public MW_Video(ListBox cameraDeviceList, Border saveResultBorder, TextBlock saveResultIcon, TextBlock saveResultText,
                        Button saveButton, Button captureButton, Button confirmButton, Button cancelButton, Button screenshotButton)
        {
            this.cameraDeviceList = cameraDeviceList;
            this.saveResultBorder = saveResultBorder;
            this.saveResultIcon = saveResultIcon;
            this.saveResultText = saveResultText;
            this.saveButton = saveButton;
            this.captureButton = captureButton;
            this.confirmButton = confirmButton;
            this.cancelButton = cancelButton;
            this.screenshotButton = screenshotButton;
        }

        public void LoadCameraDevices()
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            cameraDeviceList.Items.Clear();
            foreach (FilterInfo device in videoDevices)
            {
                cameraDeviceList.Items.Add(new ListBoxItem { Content = device.Name });
            }
        }

        public void StartCamera(string deviceName)
        {
            if (videoDevices == null) return;

            foreach (FilterInfo device in videoDevices)
            {
                if (device.Name == deviceName)
                {
                    videoSource = new VideoCaptureDevice(device.MonikerString);
                    videoSource.NewFrame += new NewFrameEventHandler(VideoSource_NewFrame);
                    videoSource.Start();
                    screenshotButton.Visibility = Visibility.Visible; // 显示截屏按钮
                    break;
                }
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            currentFrame?.Dispose();
            currentFrame = (Bitmap)eventArgs.Frame.Clone();
        }

        public void CaptureScreenshot()
        {
            if (currentFrame == null) return;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string screenshotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"Screenshot_{timestamp}.png");

            using (MemoryStream memory = new MemoryStream())
            {
                currentFrame.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                using (FileStream fileStream = new FileStream(screenshotPath, FileMode.Create, FileAccess.Write))
                {
                    memory.CopyTo(fileStream);
                }
            }

            ShowSaveResult("截屏已保存", "\uE74E", System.Windows.Media.Brushes.Green);
        }

        public void StopCamera()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.NewFrame -= new NewFrameEventHandler(VideoSource_NewFrame);
                videoSource = null;
                screenshotButton.Visibility = Visibility.Collapsed; // 隐藏截屏按钮
            }

            currentFrame?.Dispose();
            currentFrame = null;
        }

        public void HandleSaveButtonClick()
        {
            // 具体的保存逻辑已经被移动到MW_ElementsControls中，故在此略过
        }

        public void HandleCancelButtonClick()
        {
            ResetButtons();
        }

        public void ResetButtons()
        {
            saveButton.Visibility = Visibility.Visible;
            captureButton.Visibility = Visibility.Visible;
            confirmButton.Visibility = Visibility.Collapsed;
            cancelButton.Visibility = Visibility.Collapsed;
        }

        private void ShowSaveResult(string message, string icon, System.Windows.Media.Brush color)
        {
            saveResultText.Text = message;
            saveResultIcon.Text = icon;
            saveResultText.Foreground = color;
            saveResultIcon.Foreground = color;
            saveResultBorder.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                saveResultBorder.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        public void CameraDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cameraDeviceList.SelectedItem != null)
            {
                var selectedDevice = cameraDeviceList.SelectedItem as ListBoxItem;
                string deviceName = selectedDevice.Content.ToString();
                StartCamera(deviceName);
            }
        }
    }
}
