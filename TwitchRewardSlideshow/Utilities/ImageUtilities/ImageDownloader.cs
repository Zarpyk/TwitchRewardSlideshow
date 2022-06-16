using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoSauce.MagicScaler;
using TwitchRewardSlideshow.Configuration;

namespace TwitchRewardSlideshow.Utilities.ImageUtilities {
    public class ImageDownloader {
        private const int intSize = 4;
        private static readonly byte[] gif = Encoding.ASCII.GetBytes("GIF");
        private static readonly byte[] png = { 137, 80, 78, 71 };
        private static readonly byte[] jpeg = { 255, 216, 255, 224 };
        private static readonly byte[] jpeg2 = { 255, 216, 255, 225 };

        public static ImageSource BitmapFromUri(Uri source) {
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = source;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }

        public static async Task<ImageInfo> StartDownloadImage(ImageInfo imageInfo) {
            try {
                return await DownloadImage(imageInfo);
            } catch (AggregateException ex) {
                Exception e = ex.InnerException;
                AppConfig appConfig = App.config.Get<AppConfig>();
                switch (e) {
                    case HttpRequestException or InvalidHostNameException:
                        App.twitch.SendMesage(appConfig.messages.downloadFail);
                        break;
                    case InvalidImageFormatException:
                        App.twitch.SendMesage(appConfig.messages.invalidImageFormat);
                        break;
                }
                return null;
            }
        }

        private static void SaveBuffer(ImageInfo imageInfo) {
            ImageBuffer imageBuffer = App.config.Get<ImageBuffer>();
            Queue<ImageInfo> images = imageBuffer.toCheckImages;
            images.Enqueue(imageInfo);
            imageBuffer.toCheckImages = images;
            App.config.Set(imageBuffer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imageInfo"></param>
        /// <exception cref="HttpRequestException"></exception>
        /// <exception cref="InvalidImageFormatException"></exception>
        /// <returns></returns>
        public static async Task<ImageInfo> DownloadImage(ImageInfo imageInfo) {
            Uri uri = new(imageInfo.downloadLink);
            ImageInfo result = await TryDownloadImage(imageInfo, uri);
            return result;
        }

        private static async Task<ImageInfo> TryDownloadImage(ImageInfo imageInfo, Uri uri) {
            using var httpClient = new HttpClient();
            AppConfig config = App.config.Get<AppConfig>();
            string id = Guid.NewGuid().ToString("N");
            ImageExtension extension = ImageUtils.GetImageExtensionWithUri(uri);
            string path = Path.Combine(config.imageFolder, $"{id}.{extension}");
            Directory.CreateDirectory(config.imageFolder);
            try {
                Console.WriteLine($"Try downloading image with \"{extension}\" extension with source {uri}...");
                byte[] imageBytes = await httpClient.GetByteArrayAsync(uri);
                CheckValidImage(imageBytes);

                imageBytes = ResizeImageBytes(imageBytes);

                await File.WriteAllBytesAsync(path, imageBytes);
                Console.WriteLine("Image donwloaded correctly.");
                imageInfo.path = path;
            } catch (HttpRequestException) {
                Console.WriteLine("Image download not complete");
                throw;
            }
            return imageInfo;
        }

        private static byte[] ResizeImageBytes(byte[] imageBytes) {
            AppConfig config = App.config.Get<AppConfig>();
            AspectRatio aspectRatio = config.obsInfo.aspectRatio;
            using (MemoryStream outStream = new()) {
                ProcessImageSettings processImageSettings = new() {
                    Width = aspectRatio.width,
                    Height = aspectRatio.height,
                    ResizeMode = CropScaleMode.Stretch,
                    HybridMode = HybridScaleMode.Turbo
                };
                MagicImageProcessor.ProcessImage(imageBytes, outStream, processImageSettings);
                return outStream.ToArray();
            }
        }

        private static void CheckValidImage(byte[] bytes) {
            using var httpClient = new HttpClient();
            try {
                using (MemoryStream ms = new(bytes)) Image.FromStream(ms);
            } catch (ArgumentException) {
                throw new InvalidImageFormatException();
            }
        }
    }
}