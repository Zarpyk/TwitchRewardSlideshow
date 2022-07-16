using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client.Events;
using TwitchLib.Communication.Events;
using TwitchLib.PubSub.Events;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Json;
using TwitchRewardSlideshow.Utilities;
using TwitchRewardSlideshow.Utilities.ImageUtilities;
using TwitchRewardSlideshow.Utilities.TwitchUtilities;

namespace TwitchRewardSlideshow.Windows {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private static Timer timer;
        private static int time = 500;

        private bool alreadyTryGetToken;

        public MainWindow() {
            InitializeComponent();
            AppConfig config = App.config.Get<AppConfig>();
            BufferPathText.Text = config.imageFolder;
            SetImagePreviewSource(null);

            CheckDirectories(config);

            ImageManager.OnNewImageRedempted += RefreshImage;
            ImageManager.OnNewImageAccepted += AcceptImage;
            ImageManager.OnNewImageRejected += RejectImage;

            RefreshImage();
            RefreshDefaultImages();
            SetupTimer();
            SetupTwitchStatus();
        }

        #region AcceptDenyImage
        private void SetupTimer() {
            timer = new Timer();
            timer.AutoReset = true;
            timer.Elapsed += DelayTimer;
            timer.Interval = 100;
            timer.Start();
        }

        private void RefreshImage() {
            Dispatcher.Invoke(() => {
                if (IsImagePreviewNull()) {
                    ShowNextImage();
                    HaveMoreImageText.Visibility = ImageUtils.HaveMoreImage() ? Visibility.Hidden : Visibility.Visible;
                }
            });
        }

        private void OnAcceptImage(object sender, RoutedEventArgs e) {
            ImageManager.AcceptImage();
        }

        internal void AcceptImage() {
            /*if ((time < 500 && !ignoreTimer) || !HaveMoreImage()) {
                return;
            }*/
            Dispatcher.Invoke(() => {
                //time = 0;
                ClearImagePreview();
                CheckNextImage();
                //OnNewImageAccepted?.Invoke();
            });
        }

        private void DelayTimer(object sender, ElapsedEventArgs e) {
            if (time < 500) {
                Dispatcher.BeginInvoke(new Action(() => AcceptButton.IsEnabled = false));
                time += 100;
            } else {
                Dispatcher.BeginInvoke(new Action(() => AcceptButton.IsEnabled = true));
            }
        }

        private void OnRejectImage(object sender, RoutedEventArgs e) {
            ImageManager.RejectImage();
        }

        internal void RejectImage() {
            Dispatcher.Invoke(() => {
                ClearImagePreview();
                CheckNextImage();
            });
        }
        #endregion

        #region ImagePreview
        private void CheckNextImage() {
            if (ImageUtils.HaveMoreImage()) {
                HaveMoreImageText.Visibility = Visibility.Hidden;
                ShowNextImage();
            } else {
                HaveMoreImageText.Visibility = Visibility.Visible;
                User.Content = "Del usuario: ...";
                Exclusive.Content = "Exclusivo: ...";
            }
        }

        private void ShowNextImage() {
            if (App.buffer.toCheckImages.Count == 0) return;
            ImageInfo imageInfo = App.buffer.toCheckImages.Peek();
            try {
                SetImagePreviewSource(imageInfo.downloadLink);
            } catch (Exception) {
                SetImagePreviewSource(null);
                App.buffer.toCheckImages.Dequeue();
                CheckNextImage();
            }
            User.Content = $"Del usuario: {imageInfo.user}";
            Exclusive.Content = $"Exclusivo: {(imageInfo.exclusive ? "Sí" : "No")}";
        }

        private void ClearImagePreview() {
            SetImagePreviewSource(null);
        }

        private void SetImagePreviewSource(string url) {
            if (url == null) {
                ImagePreview.Source = new Uri("about:blank", UriKind.Absolute);
                ImagePreview.Visibility = Visibility.Hidden;
            } else {
                ImagePreview.Source = new Uri(url);
                ImagePreview.Visibility = Visibility.Visible;
            }
        }

        private bool IsImagePreviewNull() {
            return ImagePreview.Visibility is Visibility.Hidden or Visibility.Collapsed;
        }
        #endregion

        #region Folders
        private void ClickDefineMainFolder(object sender, RoutedEventArgs e) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            string imageFolder = appConfig.imageFolder;
            FolderPicker dlg = new() {
                inputPath = Directory.Exists(imageFolder) ? imageFolder : @"C:\"
            };
            if (dlg.ShowDialog() == true) {
                appConfig = App.config.Get<AppConfig>();
                appConfig.imageFolder = dlg.resultPath;
                appConfig.defaultPosterFolder = Path.Combine(dlg.resultPath, "Default");
                Directory.CreateDirectory(appConfig.defaultPosterFolder);
                App.config.Set(appConfig);
                BufferPathText.Text = App.config.Get<AppConfig>().imageFolder;
                RefreshDefaultImages();
            }
        }

        private void ClickAddDefaultImage(object sender, RoutedEventArgs e) {
            AddRemoveDefaultImage(true);
        }

        private void ClickRemoveDefaultImage(object sender, RoutedEventArgs e) {
            AddRemoveDefaultImage(false);
        }

        private void AddRemoveDefaultImage(bool add) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            string folder;
            if (add) {
                folder = Directory.Exists(appConfig.lastAddedImageFolder) ?
                             appConfig.lastAddedImageFolder :
                             appConfig.defaultPosterFolder;
            } else {
                folder = appConfig.defaultPosterFolder;
            }

            if (!Directory.Exists(appConfig.defaultPosterFolder)) {
                Directory.CreateDirectory(appConfig.defaultPosterFolder);
            }

            OpenFileDialog dlg = new() {
                InitialDirectory = folder,
                Filter = "Imagenes (*.PNG;*.JPG;*.GIF)|*.PNG;*.JPG;*.GIF",
                Multiselect = true,
                Title = add ? "Añadir imagen por defecto" : "Quitar imagen por defecto"
            };

            if (dlg.ShowDialog() == true) {
                appConfig = App.config.Get<AppConfig>();
                if (add) appConfig.lastAddedImageFolder = Path.GetDirectoryName(dlg.FileNames.First());
                App.config.Set(appConfig);
                foreach (string file in dlg.FileNames) {
                    if (add) {
                        try {
                            File.Copy(file, Path.Combine(appConfig.defaultPosterFolder,
                                                         Guid.NewGuid().ToString("N") + Path.GetExtension(file)));
                        } catch {
                            MessageBox.Show("No se ha podido copiar la imagen");
                        }
                    } else {
                        try {
                            File.Delete(file);
                        } catch {
                            MessageBox.Show("No se ha podido borrar la imagen");
                        }
                    }
                }
                RefreshDefaultImages();
            }
        }

        private void RefreshDefaultImages() {
            AppConfig config = App.config.Get<AppConfig>();
            if (config.defaultPosterFolder.Equals(string.Empty)) return;
            App.buffer.defaultImages = new Queue<ImageInfo>();
            foreach (string path in Directory.GetFiles(config.defaultPosterFolder).Where(CheckExtension)) {
                App.buffer.defaultImages.Enqueue(new ImageInfo(false, 999999999999, null) {
                    path = path
                });
            }
            App.obs.UpdateImageBuffer(false);
        }

        private bool CheckExtension(string path) {
            string extension = Path.GetExtension(path);
            return extension.Equals(".png") || extension.Equals(".jpg") || extension.Equals(".gif");
        }

        private void CheckDirectories(AppConfig config) {
            if (!Directory.Exists(config.imageFolder)) {
                MessageBox.Show("La carpeta de imagenes no existe");
                ClickDefineMainFolder(null, null);
            }
        }
        #endregion

        #region Twitch
        private void SetupTwitchStatus() {
            App.twitch.pubSubClient.OnListenResponse -= OnPubSubListenResponse;
            App.twitch.pubSubClient.OnPubSubServiceClosed -= OnPubSubClosed;
            App.twitch.pubSubClient.OnListenResponse += OnPubSubListenResponse;
            App.twitch.pubSubClient.OnPubSubServiceClosed += OnPubSubClosed;

            App.twitch.client.OnConnected -= OnClientConnected;
            App.twitch.client.OnDisconnected -= OnClientDisconnected;
            App.twitch.client.OnConnected += OnClientConnected;
            App.twitch.client.OnDisconnected += OnClientDisconnected;
        }

        private void ClickReloadReward(object sender, RoutedEventArgs e) {
            App.twitch.PubSubConnect();
        }

        private void ClickReloadChat(object sender, RoutedEventArgs e) {
            App.twitch.client.Connect();
        }

        #region Connect/Disconnect
        private void OnClientConnected(object sender, OnConnectedArgs e) {
            Dispatcher.BeginInvoke(new Action(() => {
                ChatStatusText.Text = "Chat disponible";
                ChatStatusText.Foreground = new SolidColorBrush(Colors.White);
            }));
        }

        private void OnClientDisconnected(object sender, OnDisconnectedEventArgs e) {
            Dispatcher.BeginInvoke(new Action(() => {
                ChatStatusText.Text = "Chat no disponible";
                ChatStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }));
        }

        private void OnPubSubListenResponse(object sender, OnListenResponseArgs e) {
            if (e.Successful) {
                GoToTwitchPanel();
                Dispatcher.BeginInvoke(new Action(() => {
                    RewardStatusText.Text = "Reward disponible";
                    RewardStatusText.Foreground = new SolidColorBrush(Colors.White);
                }));
            } else {
                OnPubSubClosed(null, null);
                if (e.Response.Error == "ERR_BADAUTH" && !alreadyTryGetToken) GetAuthToken();
                else GoToTwitchPanel();
            }
        }

        private void OnPubSubClosed(object sender, EventArgs e) {
            Dispatcher.BeginInvoke(new Action(() => {
                RewardStatusText.Text = "Reward no disponible";
                RewardStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }));
        }
        #endregion

        private void GetAuthToken() {
            Dispatcher.BeginInvoke(new Action(() => {
                string link = TwitchUtilities.GetTokenLink();
                TwitchWeb.Source = new Uri(link);
                TwitchWeb.SourceChanged += TwitchWebOnSourceChanged;
            }));
        }

        private void TwitchWebOnSourceChanged(object sender, CoreWebView2SourceChangedEventArgs e) {
            if (TwitchWeb.Source.Host.Equals("localhost")) {
                string oauth = TwitchUtilities.GetOAuth(TwitchWeb.Source.AbsoluteUri);
                ValidateAccessTokenResponse info = TwitchUtilities.GetUserOAuthInfo(oauth);
                if (!TwitchUtilities.SaveData(oauth, info)) return;
                App.twitch.RestartAll();
                alreadyTryGetToken = true;
            }
        }

        private void GoToTwitchPanel() {
            Dispatcher.BeginInvoke(new Action(() => {
                TwitchConfig twitchConfig = App.config.Get<TwitchConfig>();
                TwitchWeb.Source = new Uri("https://www.twitch.tv/login");
                TwitchWeb.SourceChanged += (_, _) => {
                    if (TwitchWeb.Source.Equals(new Uri("https://www.twitch.tv/?no-reload=true"))) {
#if DEBUG
                        TwitchWeb.Source =
                            new Uri($"https://www.twitch.tv/popout/{twitchConfig.channelName}/chat");
#endif
#if RELEASE
                        TwitchWeb.Source =
                            new Uri($"https://www.twitch.tv/popout/{twitchConfig.channelName}/reward-queue");
#endif
                    }
                };
            }));
        }

        private void ClickManageRewards(object sender, RoutedEventArgs e) {
            ManageRewardWindow dlg = new();
            try {
                dlg.Show();
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }
        #endregion

        private void ClickResetToken(object sender, RoutedEventArgs e) {
            TwitchUtilities.ResetTwitchConfig(App.config.Get<TwitchConfig>());
        }
    }
}