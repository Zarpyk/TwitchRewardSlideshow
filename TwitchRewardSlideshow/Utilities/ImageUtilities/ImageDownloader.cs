using System;
using System.Collections.Generic;
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

        internal static string GetUrl(string redemptionUserInput) {
            Match match = Regex.Match(redemptionUserInput,
                                      @"((https|http)(://))?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");
            if (!match.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !match.Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "https://" + match.Value;
            return match.Value;
        }

        internal static bool StartDownloadImage(ImageInfo imageInfo) {
            ImageInfo info = imageInfo;
            AppConfig appConfig = App.config.Get<AppConfig>();

            try {
                imageInfo = Task.Run(async () => await DownloadImage(info)).Result;
            } catch (AggregateException ex) {
                Exception e = ex.InnerException;
                switch (e) {
                    case HttpRequestException or InvalidHostNameException:
                        App.twitch.SendMesage(appConfig.messages.downloadFail);
                        break;
                    case InvalidSizeException when appConfig.obsInfo.maxImageSize == appConfig.obsInfo.maxGifSize:
                        App.twitch.SendMesage(appConfig.messages.invalidSize +
                                              $" (Tamaño máximo: {appConfig.obsInfo.maxImageSize})");
                        break;
                    case InvalidSizeException:
                        App.twitch.SendMesage(appConfig.messages.invalidSize +
                                              $" (Tamaño máximo de Imagen: {appConfig.obsInfo.maxImageSize}MB, " +
                                              $"de GIF: {appConfig.obsInfo.maxGifSize}MB)");
                        break;
                    case InvalidImageFormatException:
                        App.twitch.SendMesage(appConfig.messages.invalidImageFormat);
                        break;
                }
                return false;
            }

            SaveBuffer(imageInfo);
            App.twitch.SendMesage(appConfig.messages.downloadSuccess);
            return true;
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
        /// <exception cref="InvalidSizeException"></exception>
        /// <exception cref="InvalidImageFormatException"></exception>
        /// <returns></returns>
        internal static async Task<ImageInfo> DownloadImage(ImageInfo imageInfo) {
            Uri uri = new(imageInfo.downloadLink);
            CheckHostName(uri);
            string fileExtension = GetFileExtension(uri);
            ImageInfo result = null;
            bool extensionIsEmpty = fileExtension.Equals(string.Empty);
            if (!extensionIsEmpty) {
                result = await TryDownloadWithExtension(imageInfo, fileExtension, uri);
            } else {
                string[] extras = { "", "/a" };
                for (int i = 0; i < 2; i++) {
                    foreach (string extra in extras) {
                        try {
                            result = await TryDownloadNoExtension(imageInfo, i == 0, extra);
                            goto LoopEnd;
                        } catch (HttpRequestException) {
                            if (i == 1 && extras.Last().Equals(extra)) {
                                throw;
                            }
                        }
                    }
                }
            }
            LoopEnd:
            return result;
        }

        private static void CheckHostName(Uri uri) {
            HashSet<string> set = new() {
                "media.discordapp.net", "cdn.discordapp.com", "gyazo.com", "i.gyazo.com",
                "imgur.com", "i.imgur.com", ""
            };
            if (!set.Contains(uri.Host)) {
                throw new InvalidHostNameException();
            }
        }

        internal static string GetFileExtension(Uri uri) {
            string uriWithoutQuery = uri.GetLeftPart(UriPartial.Path);
            string fileExtension = Path.GetExtension(uriWithoutQuery);
            return fileExtension;
        }

        private static async Task<ImageInfo> TryDownloadWithExtension(ImageInfo imageInfo, string fileExtension,
                                                                      Uri uri) {
            if (!fileExtension.Equals(".png") && !fileExtension.Equals(".jpg") && !fileExtension.Equals(".gif")) {
                throw new InvalidImageFormatException();
            }
            return await TryDownloadImage(imageInfo, fileExtension, uri);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imageInfo"></param>
        /// <param name="withInitialI"></param>
        /// <param name="extra"></param>
        /// <exception cref="HttpRequestException"></exception>
        /// <returns></returns>
        private static async Task<ImageInfo> TryDownloadNoExtension(ImageInfo imageInfo, bool withInitialI,
                                                                    string extra) {
            string initial = withInitialI ? AddInitialI(imageInfo) : imageInfo.downloadLink;
            ImageInfo result;
            try {
                result = await TryDownloadAddingExtension(extra, initial, imageInfo, ".png");
            } catch {
                try {
                    result = await TryDownloadAddingExtension(extra, initial, imageInfo, ".jpg");
                } catch {
                    result = await TryDownloadAddingExtension(extra, initial, imageInfo, ".gif");
                }
            }
            return result;
        }

        private static async Task<ImageInfo> TryDownloadAddingExtension(string extra, string initial,
                                                                        ImageInfo imageInfo, string fileExtension) {
            if (imageInfo.path == null) {
                Uri uri = new(initial + extra + fileExtension);
                imageInfo = await TryDownloadImage(imageInfo, fileExtension, uri);
            }
            return imageInfo;
        }

        private static string AddInitialI(ImageInfo imageInfo) {
            return imageInfo.downloadLink.Replace("https://", "https://i.").Replace("http://", "http://i.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imageInfo"></param>
        /// <param name="fileExtension"></param>
        /// <param name="uri"></param>
        /// <exception cref="HttpRequestException">Can't download the image</exception>
        /// <exception cref="InvalidSizeException">The image is too big</exception>
        /// <exception cref="InvalidImageFormatException">The image have wrong format</exception>
        /// <returns></returns>
        private static async Task<ImageInfo> TryDownloadImage(ImageInfo imageInfo, string fileExtension, Uri uri) {
            using var httpClient = new HttpClient();
            AppConfig config = App.config.Get<AppConfig>();
            //string directoryPath = Path.Combine(config.imageFolder, config.tempImageFolder);
            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(config.imageFolder, $"{id}{fileExtension}");
            Directory.CreateDirectory(config.imageFolder);
            try {
                CheckValidSize(uri, fileExtension);
                Console.WriteLine($"Try downloading image with {fileExtension} extension with source {uri}...");
                byte[] imageBytes = await httpClient.GetByteArrayAsync(uri);
                CheckValidImage(imageBytes);
                await File.WriteAllBytesAsync(path, imageBytes);
                Console.WriteLine("Image donwloaded correctly.");
                imageInfo.path = path;
            } catch (HttpRequestException) {
                Console.WriteLine("Image download not complete");
                throw;
            }
            return imageInfo;
        }

        private static void CheckValidSize(Uri uri, string fileExtension) {
            OBSInfo info = App.config.Get<AppConfig>().obsInfo;
            double imageSize = GetImageSize(uri.AbsoluteUri);
            switch (fileExtension) {
                case ".gif":
                    if (imageSize > info.maxGifSize + info.securitySize) throw new InvalidSizeException();
                    break;
                case ".png":
                case ".jpg":
                    if (imageSize > info.maxImageSize + info.securitySize) throw new InvalidSizeException();
                    break;
            }
        }

        private static double GetImageSize(string url) {
            HttpClient client = new();
            Task<HttpResponseMessage> getAsync = client.GetAsync(url);
            HttpContentHeaders httpResponseHeaders = getAsync.Result.Content.Headers;
            string contentLenght;
            try {
                contentLenght = httpResponseHeaders.GetValues("Content-Length").First();
            } catch (InvalidOperationException) {
                throw new InvalidHostNameException();
            }
            return int.Parse(contentLenght) / Math.Pow(1024, 2);
        }

        private static void CheckValidImage(byte[] bytes) {
            try {
                using (MemoryStream ms = new(bytes)) Image.FromStream(ms);
            } catch (ArgumentException) {
                throw new InvalidImageFormatException();
            }
        }
    }
}