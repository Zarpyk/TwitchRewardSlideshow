using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Timers;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Json;
using TwitchRewardSlideshow.Utilities;
using TwitchRewardSlideshow.Utilities.ImageUtilities;
using WebSocketSharp;

namespace TwitchRewardSlideshow {
    public partial class OBS {
        private OBSWebsocket obs;
        private Timer obsTimer;

        private ImageBuffer intertalBuffer;

        private SlideshowSettings settings;

        private const string errorMsg = "No se ha podido conectar al WebSocket o " +
                                        "no se ha encontrado la galería de imágenes. " +
                                        "Revisa la configuración y tu OBS.";

        private static ImageMode _imageMode = ImageMode.Default;
        public static ImageMode imageMode {
            set {
                ImageMode oldMode = _imageMode;
                _imageMode = value;
                if (oldMode != _imageMode) App.obs.UpdateImageBuffer(true);
            }
        }

        public void Init() {
            obs = new OBSWebsocket();
            AppConfig appConfig = App.config.Get<AppConfig>();
            SourceSettings sourceSettings;
            try {
                obs.Connect(appConfig.obsInfo.obsIP, appConfig.obsInfo.obsPass);
                SetupTimer();
                intertalBuffer = App.buffer.Clone();
                sourceSettings = obs.GetSourceSettings(appConfig.obsInfo.gallerySourceName);
            } catch {
                App.ShowError(errorMsg);
                return;
            }
            settings = JsonConvert.DeserializeObject<SlideshowSettings>(sourceSettings.Settings.ToString());
            CheckImageMode();
        }

        public void Disconnect(object sender, ExitEventArgs exitEventArgs) {
            ChangeImageSource(null);
            obs.Disconnect();
        }

        public void UpdateImageBuffer(bool forceUpdateTimer) {
            UpdateCarouselImages(App.buffer, x => x.activeImages, x => x.displayedImages);
            intertalBuffer.activeExclusiveImage = App.buffer.activeExclusiveImage;
            UpdateCarouselImages(App.buffer, x => x.defaultImages, x => x.displayedDefaultImages);
            
            if (forceUpdateTimer) {
                obsTimer.Stop();
                NextImage(null, null);
                obsTimer.Start();
            } else {
                CheckImageMode();
            }
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool UpdateCarouselImages(ImageBuffer buffer,
                                          Expression<Func<ImageBuffer, Queue<ImageInfo>>> active,
                                          Expression<Func<ImageBuffer, Queue<ImageInfo>>> used) {
            Func<ImageBuffer, Queue<ImageInfo>> activeF = active.Compile();
            Func<ImageBuffer, Queue<ImageInfo>> usedF = used.Compile();

            Queue<ImageInfo> allOldImage = new(activeF(intertalBuffer).Union(usedF(intertalBuffer)));

            Queue<ImageInfo> newImage = new(activeF(buffer).Except(allOldImage));
            Queue<ImageInfo> oldImage = new(allOldImage.Except(activeF(buffer)));

            if (newImage.Count == 0 && oldImage.Count == 0) return false;

            Queue<ImageInfo> activeV = new(activeF(intertalBuffer).Union(newImage).Except(oldImage));
            Queue<ImageInfo> usedV = new(usedF(intertalBuffer).Except(oldImage));

            OtherUtilities.AssignValue(active, intertalBuffer, activeV);
            OtherUtilities.AssignValue(used, intertalBuffer, usedV);
            return true;
        }

        private void SetupTimer() {
            obsTimer = new Timer();
            obsTimer.AutoReset = true;
            obsTimer.Elapsed += NextImage;
            RefreshSlideTime();
            obsTimer.Start();
        }

        private void RefreshSlideTime() {
            AppConfig appConfig = App.config.Get<AppConfig>();
            obsTimer.Interval = appConfig.obsInfo.slideTimeInMilliseconds;
        }

        private void NextImage(object sender, ElapsedEventArgs elapsedEventArgs) {
            if (intertalBuffer.activeExclusiveImage == null) {
                if (intertalBuffer.activeImages.Count == 0) {
                    if (intertalBuffer.displayedImages.Count == 0) {
                        imageMode = ImageMode.Default;
                        NextDefaultImage();
                        return;
                    }
                    intertalBuffer.activeImages = new Queue<ImageInfo>(intertalBuffer.displayedImages);
                    intertalBuffer.displayedImages.Clear();
                }
                imageMode = ImageMode.Normal;
                ImageInfo info = intertalBuffer.activeImages.Dequeue();
                intertalBuffer.displayedImages.Enqueue(info);
                ChangeImageSource(info.path);
            } else {
                imageMode = ImageMode.Exclusive;
                ChangeImageSource(intertalBuffer.activeExclusiveImage.path);
            }
        }

        private void CheckImageMode() {
            if (intertalBuffer.activeExclusiveImage == null) {
                if (intertalBuffer.activeImages.Count == 0) {
                    if (intertalBuffer.displayedImages.Count == 0) {
                        imageMode = ImageMode.Default;
                        return;
                    }
                }
                imageMode = ImageMode.Normal;
            } else {
                imageMode = ImageMode.Exclusive;
            }
        }

        private void NextDefaultImage() {
            if (intertalBuffer.defaultImages.Count == 0) {
                if (intertalBuffer.displayedDefaultImages.Count == 0) {
                    ChangeImageSource(null);
                    return;
                }
                intertalBuffer.defaultImages = new Queue<ImageInfo>(intertalBuffer.displayedDefaultImages);
                intertalBuffer.displayedDefaultImages.Clear();
            }
            ImageInfo info = intertalBuffer.defaultImages.Dequeue();
            intertalBuffer.displayedDefaultImages.Enqueue(info);
            ChangeImageSource(info.path);
        }

        private void ChangeImageSource(string path) {
            if (settings == null) return;
            string sourceName = App.config.Get<AppConfig>().obsInfo.gallerySourceName;
            settings.files = path.IsNullOrEmpty() ?
                                 new List<FileSettings>() :
                                 new List<FileSettings> { new(false, true, path) };
            obs.SetSourceSettings(sourceName, JObject.Parse(JsonConvert.SerializeObject(settings)));
        }
    }
}