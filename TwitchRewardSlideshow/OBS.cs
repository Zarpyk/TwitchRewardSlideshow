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
using WebSocketSharp;

namespace TwitchRewardSlideshow {
    public class OBS {
        private OBSWebsocket obs;
        private Timer obsTimer;
        private ImageBuffer intertalBuffer;

        private SourceSettings sourceSettings;
        private SlideshowSettings settings;

        public void Init() {
            obs = new OBSWebsocket();
            AppConfig appConfig = App.config.Get<AppConfig>();
            try {
                obs.Connect(appConfig.obsInfo.obsIP, appConfig.obsInfo.obsPass);
                SetupTimer();
                intertalBuffer = App.config.Get<ImageBuffer>();
                sourceSettings = obs.GetSourceSettings(appConfig.obsInfo.gallerySourceName);
            } catch {
                App.ShowError("OBS", false);
                return;
            }
            if (sourceSettings == null) MessageBox.Show("No se ha encontrado la fuente de la galeria");
            else settings = JsonConvert.DeserializeObject<SlideshowSettings>(sourceSettings.Settings.ToString());
        }

        public void Disconnect(object sender, ExitEventArgs exitEventArgs) {
            ChangeImageSource(null);
            obs.Disconnect();
        }

        public void UpdateImageBuffer(ImageBuffer imageBuffer) {
            UpdateCarouselImages(imageBuffer, x => x.activeImages, x => x.displayedImages);
            intertalBuffer.activeExclusiveImage = imageBuffer.activeExclusiveImage;
            UpdateCarouselImages(imageBuffer, x => x.defaultImages, x => x.displayedDefaultImages);

            obsTimer.Stop();
            NextImage(null, null);
            obsTimer.Start();
        }

        private void UpdateCarouselImages(ImageBuffer buffer,
            Expression<Func<ImageBuffer, List<ImageInfo>>> active,
            Expression<Func<ImageBuffer, List<ImageInfo>>> used) {
            Func<ImageBuffer, List<ImageInfo>> activeF = active.Compile();
            Func<ImageBuffer, List<ImageInfo>> usedF = used.Compile();

            List<ImageInfo> allOldImage = activeF(intertalBuffer).Union(usedF(intertalBuffer)).ToList();

            List<ImageInfo> newImage = activeF(buffer).Except(allOldImage).ToList();
            List<ImageInfo> oldImage = allOldImage.Except(activeF(buffer)).ToList();

            List<ImageInfo> activeV = activeF(intertalBuffer).Union(newImage).Except(oldImage).ToList();
            List<ImageInfo> usedV = usedF(intertalBuffer).Except(oldImage).ToList();

            OtherUtilities.AssignValue(active, intertalBuffer, activeV);
            OtherUtilities.AssignValue(used, intertalBuffer, usedV);
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
            /*string sourceName = appConfig.obsInfo.gallerySourceName;
            SourceSettings sourceSettings = obs.GetSourceSettings(sourceName);
            SlideshowSettings settings =
                JsonConvert.DeserializeObject<SlideshowSettings>(sourceSettings.Settings.ToString());
            obsTimer.Interval = settings!.slide_time;*/
            obsTimer.Interval = appConfig.obsInfo.slideTimeInMilliseconds;
        }

        private void NextImage(object sender, ElapsedEventArgs elapsedEventArgs) {
            if (intertalBuffer.activeExclusiveImage == null) {
                if (intertalBuffer.activeImages.Count == 0) {
                    if (intertalBuffer.displayedImages.Count == 0) {
                        NextDefaultImage();
                        return;
                    }
                    intertalBuffer.activeImages = new List<ImageInfo>(intertalBuffer.displayedImages);
                    intertalBuffer.displayedImages.Clear();
                }
                ImageInfo info = intertalBuffer.activeImages.First();
                intertalBuffer.activeImages.Remove(info);
                intertalBuffer.displayedImages.Add(info);
                ChangeImageSource(info.path);
            } else {
                ChangeImageSource(intertalBuffer.activeExclusiveImage.path);
            }
            //RefreshSlideTime();
        }

        private void NextDefaultImage() {
            if (intertalBuffer.defaultImages.Count == 0) {
                if (intertalBuffer.displayedDefaultImages.Count == 0) {
                    return;
                }
                intertalBuffer.defaultImages = new List<ImageInfo>(intertalBuffer.displayedDefaultImages);
                intertalBuffer.displayedDefaultImages.Clear();
            }
            ImageInfo info = intertalBuffer.defaultImages.First();
            intertalBuffer.defaultImages.Remove(info);
            intertalBuffer.displayedDefaultImages.Add(info);
            ChangeImageSource(info.path);
        }

        private void ChangeImageSource(string path) {
            string sourceName = App.config.Get<AppConfig>().obsInfo.gallerySourceName;
            settings.files = path.IsNullOrEmpty() ? null : new List<FileSettings> { new(false, true, path) };
            obs.SetSourceSettings(sourceName, JObject.Parse(JsonConvert.SerializeObject(settings)));
        }
    }
}