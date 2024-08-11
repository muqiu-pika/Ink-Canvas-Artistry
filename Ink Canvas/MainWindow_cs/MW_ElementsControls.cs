using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;
using Microsoft.Win32;

namespace Ink_Canvas
{
    public class MW_ElementsControls
    {
        private InkCanvas inkCanvas;
        private TimeMachine timeMachine;
        private ListBox thumbnailList;
        private Settings settings;  // 假设 Settings 是一个实例类

        public MW_ElementsControls(InkCanvas inkCanvas, TimeMachine timeMachine, ListBox thumbnailList)
        {
            this.inkCanvas = inkCanvas;
            this.timeMachine = timeMachine;
            this.thumbnailList = thumbnailList;
            this.settings = new Settings(); // 实例化 Settings 对象
        }

        #region Image
        public async void BtnImageInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filePath in openFileDialog.FileNames)
                {
                    BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                    AddThumbnailToList(bitmapImage);
                }
            }
        }

        private void AddThumbnailToList(BitmapImage bitmapImage)
        {
            double maxWidth = 130;
            double maxHeight = 231;
            double scale = Math.Min(maxWidth / bitmapImage.PixelWidth, maxHeight / bitmapImage.PixelHeight);

            Image image = new Image
            {
                Source = bitmapImage,
                Width = bitmapImage.PixelWidth * scale,
                Height = bitmapImage.PixelHeight * scale
            };

            ListBoxItem item = new ListBoxItem
            {
                Content = image,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = bitmapImage
            };

            item.Selected += Thumbnail_Selected;
            thumbnailList.Items.Add(item);
        }

        private async void Thumbnail_Selected(object sender, RoutedEventArgs e)
        {
            ListBoxItem selectedItem = sender as ListBoxItem;
            if (selectedItem != null && selectedItem.Tag is BitmapImage bitmapImage)
            {
                Image image = await CreateAndCompressImageAsync(bitmapImage);

                if (image != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    image.Name = timestamp;

                    CenterAndScaleElement(image);

                    InkCanvas.SetLeft(image, 0);
                    InkCanvas.SetTop(image, 0);
                    inkCanvas.Children.Add(image);

                    timeMachine.CommitElementInsertHistory(image);
                }
            }
        }

        public void ThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (thumbnailList.SelectedItem != null)
            {
                ListBoxItem selectedItem = thumbnailList.SelectedItem as ListBoxItem;
                selectedItem?.RaiseEvent(new RoutedEventArgs(ListBoxItem.SelectedEvent, selectedItem));
            }
        }

        private async Task<Image> CreateAndCompressImageAsync(BitmapImage bitmapImage)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                int width = bitmapImage.PixelWidth;
                int height = bitmapImage.PixelHeight;

                if (settings.Canvas.IsCompressPicturesUploaded && (width > 1920 || height > 1080))
                {
                    double scaleX = 1920.0 / width;
                    double scaleY = 1080.0 / height;
                    double scale = Math.Min(scaleX, scaleY);

                    TransformedBitmap transformedBitmap = new TransformedBitmap(bitmapImage, new ScaleTransform(scale, scale));

                    Image image = new Image
                    {
                        Source = transformedBitmap,
                        Width = transformedBitmap.PixelWidth,
                        Height = transformedBitmap.PixelHeight
                    };

                    return image;
                }
                else
                {
                    Image image = new Image
                    {
                        Source = bitmapImage,
                        Width = width,
                        Height = height
                    };

                    return image;
                }
            });
        }
        #endregion

        #region Media
        public async void BtnMediaInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Media files (*.mp4; *.avi; *.wmv)|*.mp4;*.avi;*.wmv",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                await Task.Run(async () =>
                {
                    byte[] mediaBytes = File.ReadAllBytes(filePath);
                    MediaElement mediaElement = await CreateMediaElementAsync(filePath);

                    if (mediaElement != null)
                    {
                        CenterAndScaleElement(mediaElement);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            InkCanvas.SetLeft(mediaElement, 0);
                            InkCanvas.SetTop(mediaElement, 0);
                            inkCanvas.Children.Add(mediaElement);

                            mediaElement.LoadedBehavior = MediaState.Manual;
                            mediaElement.UnloadedBehavior = MediaState.Manual;
                            mediaElement.Play();

                            timeMachine.CommitElementInsertHistory(mediaElement);
                        });
                    }
                });
            }
        }

        private async Task<MediaElement> CreateMediaElementAsync(string filePath)
        {
            string savePath = Path.Combine(settings.Automation.AutoSavedStrokesLocation, "File Dependency");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MediaElement mediaElement = new MediaElement
                {
                    Source = new Uri(filePath),
                    LoadedBehavior = MediaState.Manual,
                    UnloadedBehavior = MediaState.Manual,
                    Width = 256,
                    Height = 256
                };

                string fileExtension = Path.GetExtension(filePath);
                string newFilePath = Path.Combine(savePath, "media_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff") + fileExtension);

                File.Copy(filePath, newFilePath, true);

                mediaElement.Source = new Uri(newFilePath);

                return mediaElement;
            });
        }
        #endregion

        private void CenterAndScaleElement(FrameworkElement element)
        {
            double maxWidth = SystemParameters.PrimaryScreenWidth / 2;
            double maxHeight = SystemParameters.PrimaryScreenHeight / 2;

            double scaleX = maxWidth / element.Width;
            double scaleY = maxHeight / element.Height;
            double scale = Math.Min(scaleX, scaleY);

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(scale, scale));

            double canvasWidth = inkCanvas.ActualWidth;
            double canvasHeight = inkCanvas.ActualHeight;
            double centerX = (canvasWidth - element.Width * scale) / 2;
            double centerY = (canvasHeight - element.Height * scale) / 2;

            transformGroup.Children.Add(new TranslateTransform(centerX, centerY));

            element.RenderTransform = transformGroup;
        }
    }
}
