using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using Ink_Canvas.Helpers;
using Microsoft.Win32;

namespace Ink_Canvas
{
    public class MW_Video
    {
        private ListBox thumbnailList;
        private ListBox cameraDeviceList;
        private Border saveResultBorder;
        private TextBlock saveResultIcon;
        private TextBlock saveResultText;
        private Button saveButton;
        private Button captureButton;
        private Button confirmButton;
        private Button cancelButton;
        private Button screenshotButton;
        private InkCanvas inkCanvas;
        private MW_ElementsControls elementsControls;
        private TimeMachine timeMachine;
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private System.Drawing.Bitmap currentFrame;
        private bool isSaveButtonClicked = false;

        public MW_Video(
            ListBox thumbnailList,
            ListBox cameraDeviceList,
            Border saveResultBorder,
            TextBlock saveResultIcon,
            TextBlock saveResultText,
            Button saveButton,
            Button captureButton,
            Button confirmButton,
            Button cancelButton,
            Button screenshotButton,  // 截屏按钮传递
            InkCanvas inkCanvas,
            TimeMachine timeMachine)
        {
            this.thumbnailList = thumbnailList;
            this.cameraDeviceList = cameraDeviceList;
            this.saveResultBorder = saveResultBorder;
            this.saveResultIcon = saveResultIcon;
            this.saveResultText = saveResultText;
            this.saveButton = saveButton;
            this.captureButton = captureButton;
            this.confirmButton = confirmButton;
            this.cancelButton = cancelButton;
            this.screenshotButton = screenshotButton; // 初始化按钮
            this.inkCanvas = inkCanvas;
            this.elementsControls = new MW_ElementsControls(inkCanvas, timeMachine);
            this.timeMachine = timeMachine;
        }

        // 加载摄像头设备列表
        public void LoadCameraDevices()
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            cameraDeviceList.Items.Clear();
            int count = 1;
            foreach (FilterInfo device in videoDevices)
            {
                cameraDeviceList.Items.Add(new { DeviceName = device.Name, Count = count++ });
            }
        }

        // 启动摄像头
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
                    screenshotButton.Visibility = Visibility.Visible; // 启用截屏按钮
                    break;
                }
            }
        }

        // 摄像头新帧处理
        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            currentFrame?.Dispose();
            currentFrame = (System.Drawing.Bitmap)eventArgs.Frame.Clone();
        }

        // 停止摄像头
        public void StopCamera()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.NewFrame -= new NewFrameEventHandler(VideoSource_NewFrame);
                videoSource = null;
            }

            currentFrame?.Dispose();
            currentFrame = null;
            screenshotButton.Visibility = Visibility.Collapsed; // 隐藏截屏按钮
        }

        // 捕获屏幕截图
        public void CaptureScreenshot()
        {
            if (currentFrame == null) return;

            BitmapImage bitmapImage;
            using (MemoryStream memory = new MemoryStream())
            {
                currentFrame.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }

            AddThumbnailToList(bitmapImage);
        }

        // 上传照片处理
        public async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                byte[] imageBytes = await Task.Run(() => File.ReadAllBytes(filePath));

                var image = await CreateAndCompressImageAsync(imageBytes);  // 使用 await 确保异步方法正确调用

                if (image != null)
                {
                    AddThumbnailToList(image.Source as BitmapImage);
                }
            }
        }

        // 创建并压缩图片
        private async Task<System.Windows.Controls.Image> CreateAndCompressImageAsync(byte[] imageBytes)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                BitmapImage bitmapImage = new BitmapImage();
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                }

                System.Windows.Controls.Image image = new System.Windows.Controls.Image
                {
                    Source = bitmapImage
                };

                return image;
            });
        }

        // 将缩略图添加到列表
        private void AddThumbnailToList(BitmapImage bitmapImage)
        {
            double maxWidth = 130; // 选中框的宽度
            double maxHeight = 231; // 选中框的高度
            double scale = Math.Min(maxWidth / bitmapImage.PixelWidth, maxHeight / bitmapImage.PixelHeight);

            System.Windows.Controls.Image image = new System.Windows.Controls.Image
            {
                Source = bitmapImage,
                Width = bitmapImage.PixelWidth * scale,
                Height = bitmapImage.PixelHeight * scale
            };

            // 创建一个新的ListBoxItem并将图片添加到其中
            ListBoxItem item = new ListBoxItem
            {
                Content = image,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            thumbnailList.Items.Add(item);
        }

        // 处理保存按钮点击事件
        public void HandleSaveButtonClick()
        {
            if (!isSaveButtonClicked)
            {
                saveButton.Visibility = Visibility.Collapsed;
                captureButton.Visibility = Visibility.Collapsed;
                confirmButton.Visibility = Visibility.Visible;
                cancelButton.Visibility = Visibility.Visible;
                thumbnailList.SelectionMode = SelectionMode.Multiple;
                isSaveButtonClicked = true;
            }
            else
            {
                if (thumbnailList.SelectedItems.Count >= 1)
                {
                    var dialog = new System.Windows.Forms.FolderBrowserDialog();
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string folderPath = dialog.SelectedPath;
                        int count = 1;
                        string timeStamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm");
                        foreach (var selectedItem in thumbnailList.SelectedItems)
                        {
                            var listBoxItem = selectedItem as ListBoxItem;
                            var image = listBoxItem?.Content as System.Windows.Controls.Image;
                            var bitmapImage = image?.Source as BitmapImage;
                            if (bitmapImage != null)
                            {
                                string fileName = $"ica-{timeStamp}";
                                if (thumbnailList.SelectedItems.Count > 1)
                                {
                                    fileName += $"-{count}";
                                    count++;
                                }
                                fileName += ".jpg";
                                string filePath = Path.Combine(folderPath, fileName);
                                SaveImageToFile(bitmapImage, filePath);
                            }
                        }
                        ShowSaveResult("保存成功", "\uF89A", System.Windows.Media.Brushes.Green);
                    }
                    else
                    {
                        ShowSaveResult("保存错误", "\uE894", System.Windows.Media.Brushes.Red);
                    }
                    ResetButtons();
                }
                else
                {
                    MessageBox.Show("请选择至少一张照片");
                }
            }
        }

        // 处理取消按钮点击事件
        public void HandleCancelButtonClick()
        {
            ResetButtons();
        }

        // 重置按钮状态
        private void ResetButtons()
        {
            saveButton.Visibility = Visibility.Visible;
            captureButton.Visibility = Visibility.Visible;
            confirmButton.Visibility = Visibility.Collapsed;
            cancelButton.Visibility = Visibility.Collapsed;
            thumbnailList.SelectionMode = SelectionMode.Single;
            isSaveButtonClicked = false;
        }

        // 保存图像到文件
        private void SaveImageToFile(BitmapImage bitmapImage, string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(fileStream);
            }
        }

        // 显示保存结果
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

        // 处理缩略图选择变化事件
        public void HandleThumbnailSelectionChanged()
        {
            if (thumbnailList.SelectedItem != null)
            {
                int selectedIndex = thumbnailList.SelectedIndex;
                var uploadedFilePaths = elementsControls.GetUploadedFilePaths();
                if (selectedIndex >= 0 && selectedIndex < uploadedFilePaths.Count)
                {
                    string filePath = uploadedFilePaths[selectedIndex];
                    var task = elementsControls.OpenAndCompressImage(filePath, inkCanvas);  // 使用 await 确保异步方法正确调用
                }
            }
        }

        // 摄像头列表项选择变化处理
        public void CameraDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cameraDeviceList.SelectedItem != null)
            {
                dynamic selectedDevice = cameraDeviceList.SelectedItem;
                string deviceName = selectedDevice.DeviceName;
                StartCamera(deviceName);
            }
            else
            {
                StopCamera();
            }
        }
    }
}
