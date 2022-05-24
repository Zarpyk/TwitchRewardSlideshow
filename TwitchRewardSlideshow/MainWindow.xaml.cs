using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using TwitchLib.Client.Events;
using TwitchLib.Communication.Events;
using TwitchLib.PubSub.Events;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Utilities;
using XamlAnimatedGif;

namespace TwitchRewardSlideshow {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public static event Action OnNewImageAccepted;

        private static Timer timer;
        private static int time = 500;

        private bool alreadyTryGetToken;

        public MainWindow() {
            InitializeComponent();
            AppConfig config = App.config.Get<AppConfig>();
            BufferPathText.Text = config.imageFolder;
            DefaultImageText.Text = config.defaultPosterFolder;

            CheckDirectories(config);

            App.OnNewImageDownloaded += RefreshImage;

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
            if (PreviewImage.Source == null) {
                ShowNextImage();
                HaveMoreImageText.Visibility = HaveMoreImage() ? Visibility.Hidden : Visibility.Visible;
            }
        }

        private void OnAcceptImage(object sender, RoutedEventArgs e) {
            AcceptImage(false);
        }

        internal void AcceptImage(bool ignoreTimer) {
            if ((time < 500 && !ignoreTimer) || !HaveMoreImage()) {
                return;
            }
            string user = null;
            Dispatcher.Invoke(() => {
                time = 0;
                ClearImagePreview();
                user = SendImageToFolder();
                CheckNextImage();
                OnNewImageAccepted?.Invoke();
            });
            if (user == null) return;
            App.twitch.SendMesage($"Se ha aceptado la imagen de {user}");
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
            RejectImage();
        }

        internal void RejectImage() {
            string user = null;
            Dispatcher.Invoke(() => {
                ClearImagePreview();
                user = DeleteImage();
                CheckNextImage();
                //ChangeLastImageInfo();
            });
            if (user == null) return;
            App.twitch.SendMesage($"Se ha rechazado la imagen de {user}");
        }
        #endregion

        #region ImagePreview
        private string DeleteImage() {
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            if (buffer.toCheckImages.Count == 0) return null;
            ImageInfo imageInfo = buffer.toCheckImages.Dequeue();
            try {
                File.Delete(imageInfo.path);
                App.config.Set(buffer);
            } catch (IOException) { }
            return imageInfo.user;
        }

        private void CheckNextImage() {
            if (HaveMoreImage()) {
                HaveMoreImageText.Visibility = Visibility.Hidden;
                ShowNextImage();
            } else {
                HaveMoreImageText.Visibility = Visibility.Visible;
                User.Content = "Del usuario: ...";
            }
        }

        private void ShowNextImage() {
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            if (buffer.toCheckImages.Count == 0) return;
            ImageInfo imageInfo = App.config.Get<ImageBuffer>().toCheckImages.Dequeue();
            if (ImageUtilities.GetFileExtension(new Uri(imageInfo.downloadLink)) == ".gif") {
                AnimationBehavior.SetSourceUri(PreviewImage, new Uri(imageInfo.path));
            } else {
                PreviewImage.Source = BitmapFromUri(new Uri(imageInfo.path));
            }
            User.Content = $"Del usuario: {imageInfo.user}";
        }

        public static ImageSource BitmapFromUri(Uri source) {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = source;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }

        private string SendImageToFolder() {
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            if (buffer.toCheckImages.Count == 0) return null;
            ImageInfo imageInfo = buffer.toCheckImages.Dequeue();
            AppConfig config = App.config.Get<AppConfig>();
            imageInfo.MovePath(Path.Combine(config.imageFolder, config.acceptedImageFolder));
            if (imageInfo.exclusive) buffer.exclusiveImagesQueue.Enqueue(imageInfo);
            else buffer.activeImages.Add(imageInfo);
            App.config.Set(buffer);
            return imageInfo.user;
        }

        private bool HaveMoreImage() {
            return App.config.Get<ImageBuffer>().toCheckImages.Count > 0;
        }

        private void ClearImagePreview() {
            PreviewImage.Source = null;
            AnimationBehavior.SetSourceUri(PreviewImage, null);
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
                App.config.Set<AppConfig>(x => x.imageFolder, dlg.resultPath);
                BufferPathText.Text = App.config.Get<AppConfig>().imageFolder;
            }
        }

        private void ClickDefineDefaultImagesFolder(object sender, RoutedEventArgs e) {
            AppConfig appConfig = App.config.Get<AppConfig>();
            string defaultImageFolder = appConfig.defaultPosterFolder;
            FolderPicker dlg = new() {
                inputPath = Directory.Exists(defaultImageFolder) ? defaultImageFolder : @"C:\"
            };
            if (dlg.ShowDialog() == true) {
                appConfig = App.config.Get<AppConfig>();
                appConfig.defaultPosterFolder = dlg.resultPath;
                App.config.Set(appConfig);
                RefreshDefaultImages();
                DefaultImageText.Text = dlg.resultPath;
            }
        }

        private void RefreshDefaultImages() {
            AppConfig config = App.config.Get<AppConfig>();
            if (config.defaultPosterFolder.Equals(string.Empty)) return;
            ImageBuffer buffer = App.config.Get<ImageBuffer>();
            buffer.defaultImages = new List<ImageInfo>();
            foreach (string path in Directory.GetFiles(config.defaultPosterFolder).Where(CheckExtension)) {
                buffer.defaultImages.Add(new ImageInfo(false, 999999999999, null) {
                    path = path
                });
            }
            App.config.Set(buffer);
            App.obs.UpdateImageBuffer(buffer);
        }

        private bool CheckExtension(string path) {
            string extension = Path.GetExtension(path);
            return extension.Equals(".png") || extension.Equals(".jpg") || extension.Equals(".gif");
        }

        private void CheckDirectories(AppConfig config) {
            if (!Directory.Exists(config.imageFolder)) {
                MessageBox.Show("La carpeta de imagenes principal no existe");
                ClickDefineMainFolder(null, null);
            }
            if (!Directory.Exists(config.defaultPosterFolder)) {
                MessageBox.Show("La carpeta de imagenes por defecto no existe");
                ClickDefineDefaultImagesFolder(null, null);
            }
        }
        #endregion

        #region Twitch
        private void SetupTwitchStatus() {
            App.twitch.pubSubClient.OnListenResponse += OnPubSubListenResponse;
            App.twitch.pubSubClient.OnPubSubServiceClosed += OnPubSubClosed;

            App.twitch.client.OnConnected += OnClientConnected;
            App.twitch.client.OnDisconnected += OnClientDisconnected;
        }

        private void ClickReloadReward(object sender, RoutedEventArgs e) {
            App.twitch.PubSubConnect();
        }

        private void OnPubSubClosed(object sender, EventArgs e) {
            Dispatcher.BeginInvoke(new Action(() => {
                RewardStatusText.Text = "Reward no disponible";
                RewardStatusText.Foreground = new SolidColorBrush(Colors.Red);
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
#if DEBUG
                GoToTwitchPanel();
#endif
#if RELEASE
                if (e.Response.Error == "ERR_BADAUTH" && !alreadyTryGetToken) {
                    GetToken();
                } else {
                    GoToTwitchPanel();
                }
#endif
            }
        }

        private void ClickReloadChat(object sender, RoutedEventArgs e) {
            App.twitch.client.Connect();
        }

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

        private void GetToken() {
            Dispatcher.BeginInvoke(new Action(() => {
                string link = "https://id.twitch.tv/oauth2/authorize?response_type=token&";
                link += "client_id=eg9uc3o0ngoo7ohl3n1a3fjtpoi1j8&";
                link += "redirect_uri=http://localhost:3000&";
                link += HttpUtility.UrlEncode("scope=channel:read:redemptions");
                WebView.Source = new Uri(link);
                WebView.SourceChanged += WebViewOnSourceChanged;
                //https://id.twitch.tv/oauth2/authorize?response_type=token&client_id=eg9uc3o0ngoo7ohl3n1a3fjtpoi1j8&redirect_uri=http://localhost:3000&scope=channel:read:redemptions
            }));
        }

        private void WebViewOnSourceChanged(object sender, CoreWebView2SourceChangedEventArgs e) {
            if (WebView.Source.Host.Equals("localhost")) {
                //http://localhost:3000/#access_token=rsdg9u3pbgn4yo3tlwa4eruwfs4ifp&scope=&token_type=bearer
                string token = WebView.Source.AbsoluteUri.Split('/').Last().Split('&').First()
                    .Replace("#access_token=", "");
                App.config.Set<TwitchConfig>(x => x.token, token);
                App.twitch.pubSubClient.Disconnect();
                App.twitch.PubSubConnect();
                alreadyTryGetToken = true;
            }
        }

        private void GoToTwitchPanel() {
            Dispatcher.BeginInvoke(new Action(() => {
                TwitchConfig twitchConfig = App.config.Get<TwitchConfig>();
                WebView.Source = new Uri("https://www.twitch.tv/login");
                WebView.SourceChanged += (_, _) => {
                    if (WebView.Source.Equals(new Uri("https://www.twitch.tv/?no-reload=true"))) {
#if DEBUG
                        WebView.Source =
                            new Uri($"https://www.twitch.tv/popout/{twitchConfig.destinationChannel}/chat");
#endif
#if RELEASE
                    WebView.Source =
                        new Uri($"https://www.twitch.tv/popout/{twitchConfig.destinationChannel}/reward-queue");
#endif
                    }
                };
            }));
        }
        #endregion
    }
}