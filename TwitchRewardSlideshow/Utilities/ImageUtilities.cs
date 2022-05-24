using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchRewardSlideshow.Configuration;

namespace TwitchRewardSlideshow.Utilities {
    public class ImageUtilities {
        internal static string GetUrl(string redemptionUserInput) {
            Match match = Regex.Match(redemptionUserInput,
                @"((https|http)(://))?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");
            if (!match.Value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !match.Value.StartsWith(
                    "http://", StringComparison.OrdinalIgnoreCase)) return "https://" + match.Value;
            return match.Value;
        }

        internal static async Task<ImageInfo> DownloadImage(ImageInfo imageInfo) {
            Uri uri = new(imageInfo.downloadLink);
            if (CheckHostName(uri)) return imageInfo;
            string fileExtension = GetFileExtension(uri);
            ImageInfo result;
            //Si no tiene una extension, prueba con el .png al final del link
            bool extensionIsEmpty = fileExtension.Equals(string.Empty);
            if (!extensionIsEmpty) {
                result = await TryDownloadWithExtension(imageInfo, fileExtension, uri);
            } else {
                //Normalmente es la 1 o la 2, no he visto ningun link con la 3 y 4, pero por si a caso
                result = await TryDownloadNoExtension(imageInfo, true, "") ??
                         await TryDownloadNoExtension(imageInfo, true, "/a") ??
                         await TryDownloadNoExtension(imageInfo, false, "") ??
                         await TryDownloadNoExtension(imageInfo, false, "/a");
            }
            return result;
        }

        private static bool CheckHostName(Uri uri) {
            HashSet<string> set = new() {
                "media.discordapp.net", "cdn.discordapp.com", "gyazo.com", "i.gyazo.com",
                "imgur.com", "i.imgur.com", ""
            };
            return !set.Contains(uri.Host);
        }

        internal static string GetFileExtension(Uri uri) {
            string uriWithoutQuery = uri.GetLeftPart(UriPartial.Path);
            string fileExtension = Path.GetExtension(uriWithoutQuery);
            return fileExtension;
        }

        private static async Task<ImageInfo> TryDownloadWithExtension(ImageInfo imageInfo, string fileExtension, Uri uri) {
            ImageInfo result;
            if (!fileExtension.Equals(".png") && !fileExtension.Equals(".jpg") && !fileExtension.Equals(".gif")) {
                result = imageInfo;
            } else {
                result = await TryDownloadImage(imageInfo, fileExtension, uri);
            }
            return result;
        }

        private static async Task<ImageInfo> TryDownloadNoExtension(ImageInfo imageInfo, bool withInitialI,
            string extra) {
            string initial = withInitialI ? AddInitialI(imageInfo) : imageInfo.downloadLink;
            ImageInfo result = await TryDownloadAddingExtension(extra, initial, imageInfo, ".png");
            if (result.path != null) return result;
            result = await TryDownloadAddingExtension(extra, initial, imageInfo, ".jpg");
            if (result.path != null) return result;
            result = await TryDownloadAddingExtension(extra, initial, imageInfo, ".gif");
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

        private static async Task<ImageInfo> TryDownloadImage(ImageInfo imageInfo, string fileExtension, Uri uri) {
            using var httpClient = new HttpClient();
            AppConfig config = App.config.Get<AppConfig>();
            string directoryPath = Path.Combine(config.imageFolder, config.tempImageFolder);
            string id = Guid.NewGuid().ToString("N");
            string path = Path.Combine(directoryPath,
                $"{id}{fileExtension}");
            Directory.CreateDirectory(directoryPath);

            try {
                Console.WriteLine($"Try downloading image with {fileExtension} extension with source {uri}...");
                byte[] imageBytes = await httpClient.GetByteArrayAsync(uri);
                await File.WriteAllBytesAsync(path, imageBytes);
                Console.WriteLine("Image donwloaded correctly.");
                imageInfo.path = path;
            } catch {
                Console.WriteLine("Image download not complete");
            }
            return imageInfo;
        }
    }
}