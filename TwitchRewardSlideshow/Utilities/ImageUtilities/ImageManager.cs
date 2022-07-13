using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchRewardSlideshow.Configuration;

namespace TwitchRewardSlideshow.Utilities.ImageUtilities {
    public static class ImageManager {
        /*private static ImageManager _instance;
        public static ImageManager instance => _instance ??= new ImageManager();

        private ImageManager() { }*/

        public static event Action OnNewImageRedempted;
        public static event Action OnNewImageAccepted;
        public static event Action OnNewImageRejected;

        public static void AddImage(string title, string arg, string userName, string redemptionId = null) {
            foreach (RewardConfig reward in App.config.Get<TwitchConfig>().rewards.Where(x =>
                         string.Equals(x.title, title, StringComparison.InvariantCultureIgnoreCase))) {
                ImageInfo info = new(reward.exclusiveImage, reward.timeInMilliseconds, ImageUtils.GetUrl(arg)) {
                    user = userName,
                    rewardId = reward.id,
                    redemptionId = redemptionId
                };
                AppConfig appConfig = App.config.Get<AppConfig>();

                try {
                    info.downloadLink = ImageUtils.FixImageUri(new Uri(info.downloadLink));
                    SaveImage(info);
                    OnNewImageRedempted?.Invoke();
                } catch (Exception ex) {
                    switch (ex) {
                        case InvalidHostNameException:
                            App.twitch.SendMesage(appConfig.messages.invalidHost);
                            break;
                        case InvalidImageFormatException:
                            App.twitch.SendMesage(appConfig.messages.invalidImageFormat);
                            break;
                        case InvalidSizeException:
                            if (appConfig.obsInfo.maxImageSize == appConfig.obsInfo.maxGifSize) {
                                App.twitch.SendMesage(appConfig.messages.invalidSize +
                                                      $" (Tamaño máximo: {appConfig.obsInfo.maxImageSize})");
                            } else {
                                App.twitch.SendMesage(appConfig.messages.invalidSize +
                                                      $" (Tamaño máximo de Imagen: {appConfig.obsInfo.maxImageSize}MB, " +
                                                      $"de GIF: {appConfig.obsInfo.maxGifSize}MB)");
                            }
                            break;
                    }
                    ChangePointStatus(info, false);
                }
            }
        }

        public static void AcceptImage() {
            if (!ImageUtils.HaveMoreImage()) {
                return;
            }
            ImageInfo info = DequeueImage();
            OnNewImageAccepted?.Invoke();
            Task.Run(() => {
                info = AcceptUpdateBuffer(info).Result;
                if (info == null) return;
                ChangePointStatus(info, true);
                App.twitch.SendMesage($"Se ha aceptado la imagen de {info.user}");
            });
        }

        private static ImageInfo DequeueImage() {
            if (App.buffer.toCheckImages.Count == 0) return null;
            ImageInfo info = App.buffer.toCheckImages.Dequeue();
            return info;
        }

        private static async Task<ImageInfo> AcceptUpdateBuffer(ImageInfo info) {
            ImageInfo downloadedInfo = await ImageDownloader.StartDownloadImage(info);
            if (downloadedInfo != null) {
                if (downloadedInfo.exclusive) App.buffer.exclusiveImagesQueue.Enqueue(downloadedInfo);
                else App.buffer.activeImages.Enqueue(downloadedInfo);
            } else {
                ChangePointStatus(info, false);
                return null;
            }
            return info;
        }

        public static void RejectImage() {
            ImageInfo info = DeleteImage();
            if (info == null) return;
            ChangePointStatus(info, false);
            App.twitch.SendMesage($"Se ha rechazado la imagen de {info.user}");
            OnNewImageRejected?.Invoke();
        }

        private static ImageInfo DeleteImage() {
            if (App.buffer.toCheckImages.Count == 0) return null;
            ImageInfo imageInfo = App.buffer.toCheckImages.Dequeue();
            if (imageInfo.path != null) {
                try {
                    File.Delete(imageInfo.path);
                } catch (IOException) { }
            }
            return imageInfo;
        }

        private static void ChangePointStatus(ImageInfo info, bool accept) {
            if (info.redemptionId != null) {
                TwitchConfig config = App.config.Get<TwitchConfig>();
                UpdateCustomRewardRedemptionStatusRequest request = new() {
                    Status = accept ? CustomRewardRedemptionStatus.FULFILLED : CustomRewardRedemptionStatus.CANCELED
                };
                App.twitch.helix.ChannelPoints.UpdateRedemptionStatusAsync(config.channelId, info.rewardId,
                                                                           new List<string> { info.redemptionId },
                                                                           request);
            }
        }

        private static void SaveImage(ImageInfo imageInfo) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            ImageUtils.SaveImageToBuffer(imageInfo);
            App.twitch.SendMesage(appConfig.messages.addSuccess);
        }
    }
}