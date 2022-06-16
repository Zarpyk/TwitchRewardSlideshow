using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchRewardSlideshow.Configuration;

namespace TwitchRewardSlideshow.Utilities.ImageUtilities {
    public static class ImageUtils {
        public static string GetUrl(string redemptionUserInput) {
            Match match = Regex.Match(redemptionUserInput,
                                      @"((https|http)(://))?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");
            if (!match.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !match.Value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "https://" + match.Value;
            return match.Value;
        }

        public static void SaveImageToBuffer(ImageInfo imageInfo) {
            ImageBuffer imageBuffer = App.config.Get<ImageBuffer>();
            Queue<ImageInfo> images = imageBuffer.toCheckImages;
            images.Enqueue(imageInfo);
            imageBuffer.toCheckImages = images;
            App.config.Set(imageBuffer);
        }

        public static string FixImageUri(Uri uri) {
            CheckHostName(uri);
            CheckValidConection(uri);
            CheckUriExtension(ref uri);
            CheckValidSize(uri);
            return uri.AbsoluteUri;
        }

        public static bool HaveMoreImage() {
            return App.config.Get<ImageBuffer>().toCheckImages.Count > 0;
        }

        public static ImageExtension GetImageExtensionWithUri(Uri uri) {
            string uriWithoutQuery = uri.GetLeftPart(UriPartial.Path);
            string fileExtension = Path.GetExtension(uriWithoutQuery).TrimStart('.');
            return Enum.Parse<ImageExtension>(fileExtension);
        }

        private static void CheckHostName(Uri uri) {
            HashSet<string> set = new() {
                "media.discordapp.net", "cdn.discordapp.com", "gyazo.com", "i.gyazo.com"
            };
            if (!set.Contains(uri.Host)) {
                throw new InvalidHostNameException();
            }
        }

        private static void CheckValidConection(Uri uri) {
            HttpClient client = new();
            HttpResponseMessage httpResponseMessage = client.GetAsync(uri).Result;
            if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Conection to {uri} fail");
        }

        private static void CheckUriExtension(ref Uri uri) {
            HashSet<string> fixSet = new() {
                "gyazo.com"
            };
            if (fixSet.Contains(uri.Host)) {
                string finalUri = uri.AbsoluteUri;
                finalUri = finalUri.Replace("http://", "https://").Replace("https://", "https://i.");
                HttpClient client = new();
                bool isValidUri = false;
                finalUri = TryAddExtensionToUri(client, finalUri, ref isValidUri);
                if (!isValidUri) throw new InvalidImageFormatException();
                uri = new Uri(finalUri);
                return;
            }
            CheckImageExtension(uri);
        }

        private static string TryAddExtensionToUri(HttpClient client, string finalUri, ref bool isValidUri) {
            foreach (ImageExtension imageExtension in Enum.GetValues(typeof(ImageExtension))) {
                HttpResponseMessage httpResponseMessage = client.GetAsync(finalUri + $".{imageExtension}").Result;
                if (httpResponseMessage.StatusCode == HttpStatusCode.OK) {
                    finalUri += $".{imageExtension}";
                    isValidUri = true;
                    break;
                }
            }
            return finalUri;
        }

        private static void CheckImageExtension(Uri uri) {
            try {
                GetImageExtensionWithUri(uri);
            } catch
                (Exception) {
                throw new InvalidHostNameException();
            }
        }

        private static void CheckValidSize(Uri uri) {
            OBSInfo info = App.config.Get<AppConfig>().obsInfo;
            ImageExtension imageExtension = GetImageExtensionWithUri(uri);
            double imageSize = GetImageSize(uri.AbsoluteUri);
            switch (imageExtension) {
                case ImageExtension.gif:
                    if (imageSize > info.maxGifSize + info.securitySize) throw new InvalidSizeException();
                    break;
                case ImageExtension.png:
                case ImageExtension.jpg:
                    if (imageSize > info.maxImageSize + info.securitySize) throw new InvalidSizeException();
                    break;
                default: throw new ArgumentOutOfRangeException();
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
    }
}