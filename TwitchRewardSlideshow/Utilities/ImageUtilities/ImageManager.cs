using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
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

        public static Queue<ImageInfo> addImageQueue = new();

        private static Timer _imageTimer = new();
        private const int timerInterval = 1000;
        private static bool adding;
        private static int addingLenght;

        public static void InitAddImageQueue() {
            _imageTimer = new Timer();
            _imageTimer.AutoReset = true;
            _imageTimer.Elapsed += ApplyBuffer;
            _imageTimer.Interval = timerInterval;
            _imageTimer.Start();
        }

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
            addingLenght += 1;
            Task.Run(() => {
                info = AcceptUpdateBuffer(info).Result;
                if (info == null) return;
                ChangePointStatus(info, true);
                App.twitch.SendMesage($"Se ha aceptado la imagen de {info.user}");
                addingLenght -= 1;
            });
        }

        private static ImageInfo DequeueImage() {
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            if (buffer.toCheckImages.Count == 0) return null;
            ImageInfo info = buffer.toCheckImages.Dequeue();
            App.config.Set(buffer);
            return info;
        }

        private static async Task<ImageInfo> AcceptUpdateBuffer(ImageInfo info) {
            ImageInfo downloadedInfo = await ImageDownloader.StartDownloadImage(info);
            if (downloadedInfo != null) {
                addImageQueue.Enqueue(downloadedInfo);
            } else {
                ChangePointStatus(info, false);
                return null;
            }
            return info;
        }

        private static void ApplyBuffer(object sender, ElapsedEventArgs elapsedEventArgs) {
            if (addImageQueue.Count == 0 || adding || addingLenght != 0) return;
            adding = true;
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            int lenght = addImageQueue.Count;
            for (int i = 0; i < lenght; i++) {
                ImageInfo info = addImageQueue.Dequeue();
                if (info.exclusive) buffer.exclusiveImagesQueue.Enqueue(info);
                else buffer.activeImages.Add(info);
            }
            Console.WriteLine("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"); //TODO En alguna otra parte salta error de set
            App.config.Set(buffer);
            adding = false;
        }

        public static void RejectImage() {
            ImageInfo info = DeleteImage();
            if (info == null) return;
            ChangePointStatus(info, false);
            App.twitch.SendMesage($"Se ha rechazado la imagen de {info.user}");
            OnNewImageRejected?.Invoke();
        }

        private static ImageInfo DeleteImage() {
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            if (buffer.toCheckImages.Count == 0) return null;
            ImageInfo imageInfo = buffer.toCheckImages.Dequeue();
            if (imageInfo.path != null) {
                try {
                    File.Delete(imageInfo.path);
                } catch (IOException) { }
            }
            App.config.Set(buffer);
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
            App.twitch.SendMesage(appConfig.messages.downloadSuccess);
        }
    }
}