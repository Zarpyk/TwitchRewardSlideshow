using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using AppConfiguration;
using Octokit;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.PubSub.Events;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Utilities;
using Application = System.Windows.Application;
using OnLogArgs = TwitchLib.Client.Events.OnLogArgs;

namespace TwitchRewardSlideshow {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public static Twitch twitch;
        public static ConfigManager config;
        public static OBS obs;

        public const string devName = "GuerreroBit";
        public const string productName = "TwitchRewardSlideshow";
        public const string version = "1.3";

        public static event Action OnNewImageDownloaded;

        private static ConsoleManager consoleManager;

        private Timer imageTimer;
        private const int timerInterval = 5000;

        private MainWindow window;

        private void AppOnStartup(object sender, StartupEventArgs e) {
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SetupConsole();
            SetupConfig();

            CheckUpdate();

            SetupTwitch();
            SetupOBS();
            SetupImageTimer();

            TwitchRewardSlideshow.MainWindow.OnNewImageAccepted += ChangeImagesFolder;
            ChangeImagesFolder();

            window = new MainWindow();
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Current.MainWindow = window;
            window.Show();

            twitch.Connect();
        }

        private void CheckUpdate() {
            GitHubClient github = new(new ProductHeaderValue(productName, version));
            IReadOnlyList<Release> releases = github.Repository.Release.GetAll(devName, productName).Result;
            Release latest = releases[0];
            if (latest.TagName != version) {
                MessageBoxResult result = MessageBox.Show("Hay una nueva version disponible ¿Quieres descargarlo?",
                    productName, MessageBoxButton.YesNo);
                switch (result) {
                    case MessageBoxResult.Yes:
                        Process.Start(new ProcessStartInfo {
                            FileName = latest.HtmlUrl,
                            UseShellExecute = true
                        });
                        Environment.Exit(0);
                        break;
                    case MessageBoxResult.No: break;
                }
            }
        }

        private void SetupConsole() {
            consoleManager = new ConsoleManager();
            DispatcherUnhandledException += ConsoleManager.App_DispatcherUnhandledException;
            Exit += (_, _) => ConsoleManager.backupFile();
        }

        private void SetupConfig() {
            config = new ConfigManager(devName, productName);
            config.Set(config.Get<AppConfig>());
            config.Set(config.Get<ImageBuffer>());
            config.Set(config.Get<TwitchConfig>());
        }

        private void SetupTwitch() {
            twitch = new Twitch();
            twitch.pubSubClient.OnChannelPointsRewardRedeemed += SortReward;
            twitch.client.OnChatCommandReceived += SortReward;
            twitch.client.OnLog += SortReward;
        }

        private void SetupOBS() {
            obs = new OBS();
            obs.Init();
            Exit += obs.Disconnect;
        }

        private void SetupImageTimer() {
            imageTimer = new Timer();
            imageTimer.AutoReset = true;
            imageTimer.Elapsed += UpdateImageInfo;
            imageTimer.Interval = timerInterval;
            imageTimer.Start();
        }

        private void UpdateImageInfo(object sender, ElapsedEventArgs e) {
            ImageBuffer buffer = config.Get<ImageBuffer>();
            if (buffer.activeExclusiveImage == null) {
                List<ImageInfo> deleteImage = new();
                foreach (ImageInfo info in buffer.activeImages.Union(buffer.displayedImages).ToList()) {
                    info.usedTime += timerInterval;
                    Console.WriteLine(info.usedTime);
                    if (info.usedTime >= info.totalActiveTime) {
                        File.Delete(info.path);
                        deleteImage.Add(info);
                    }
                }
                buffer.activeImages = buffer.activeImages.Except(deleteImage).ToList();
                buffer.displayedImages = buffer.displayedImages.Except(deleteImage).ToList();
            } else {
                buffer.activeExclusiveImage.usedTime += timerInterval;
                if (buffer.activeExclusiveImage.usedTime >= buffer.activeExclusiveImage.totalActiveTime) {
                    File.Delete(buffer.activeExclusiveImage.path);
                    buffer.activeExclusiveImage = null;
                }
            }
            config.Set(buffer);
            ChangeImagesFolder();
        }

        private void ChangeImagesFolder() {
            ImageBuffer buffer = config.Get<ImageBuffer>();
            AppConfig appConfig = config.Get<AppConfig>();
            if (buffer.exclusiveImagesQueue.Count > 0) {
                //Si no hay ya una exclusiva o la exclusiva no es el por defecto
                if (buffer.activeExclusiveImage == null) {
                    //Activa un exclusivo
                    ImageInfo exclusiveImage = buffer.exclusiveImagesQueue.Dequeue();
                    exclusiveImage.MovePath(appConfig.imageFolder);
                    buffer.activeExclusiveImage = exclusiveImage;
                    RemoveActiveImages(ref buffer, appConfig);
                    obs.UpdateImageBuffer(buffer);
                } /*else { //Si hay una exclusiva activa
                    //Quitar las imagenes de activo en caso de que haya por algun error
                    RemoveActiveImages(ref buffer, appConfig);
                }*/
            } else {
                //Quita el por defecto
                if (buffer.activeExclusiveImage == null) {
                    UseActiveImages(ref buffer, appConfig);
                    obs.UpdateImageBuffer(buffer);
                } /*else { //Hay imagen exclusiva
                    //Quitar las imagenes de activo en caso de que haya por algun error
                    RemoveActiveImages(ref buffer, appConfig);
                }*/
            }
            config.Set(buffer);
        }

        private static void RemoveActiveImages(ref ImageBuffer buffer, AppConfig appConfig) {
            foreach (ImageInfo info in buffer.activeImages.Where(info =>
                         appConfig.imageFolder.Equals(Path.GetDirectoryName(info.path)))) {
                info.MovePath(Path.Combine(appConfig.imageFolder, appConfig.acceptedImageFolder));
            }
        }

        private void UseActiveImages(ref ImageBuffer buffer, AppConfig appConfig) {
            if (buffer.activeImages.Count == 0) return;
            foreach (ImageInfo info in buffer.activeImages.Where(info =>
                         !appConfig.imageFolder.Equals(Path.GetDirectoryName(info.path)))) {
                info.MovePath(appConfig.imageFolder);
            }
        }

        private void SortReward(object sender, OnChatCommandReceivedArgs e) {
            ChatMessage chatMessage = e.Command.ChatMessage;
            if (!chatMessage.IsBroadcaster && !chatMessage.IsModerator &&
                chatMessage.UserId != "126707119") return;
            switch (e.Command.CommandText) {
                case "add": {
                    string[] arg = e.Command.ArgumentsAsString.Split(':', 2);
                    foreach (RewardConfig reward in config.Get<TwitchConfig>().rewards.Where(x => x.title == arg[0])) {
                        ImageInfo imageInfo = InitImageInfo(reward, arg[1], chatMessage.DisplayName);
                        StartDownloadImage(imageInfo);
                    }
                    break;
                }
                case "accept":
                case "ac": {
                    window.AcceptImage(true);
                    break;
                }
                case "deny":
                case "d": {
                    window.RejectImage();
                    break;
                }
                case "next": {
                    ImageBuffer buffer = config.Get<ImageBuffer>();
                    if (buffer.toCheckImages.Count == 0) twitch.SendMesage("No hay más imagenes para comprobar");
                    ImageInfo info = buffer.toCheckImages.Dequeue();
                    twitch.SendMesage($"La proxima imagen es de {info.user} con link a {info.downloadLink}");
                    break;
                }
                case "help": {
                    twitch.SendMesage("https://github.com/GuerreroBit/TwitchRewardSlideshow/blob" +
                                      "/master/README.md#commands");
                    break;
                }
            }
        }

        private void SortReward(object sender, OnChannelPointsRewardRedeemedArgs e) {
            foreach (ImageInfo imageInfo in from reward in config.Get<TwitchConfig>().rewards
                     where reward.title == e.RewardRedeemed.Redemption.Reward.Title
                     select InitImageInfo(reward, e.RewardRedeemed.Redemption.UserInput,
                         e.RewardRedeemed.Redemption.User.DisplayName)) {
                StartDownloadImage(imageInfo);
            }
        }

        private void SortReward(object sender, OnLogArgs e) {
            /*05/20/2022 20:39:07: guerrerobit - Received: @badge-info=subscriber/2;badges=vip/1,subscriber/0;client-nonce=ab34de789c07cc6131583f0d90c31b1e;color=#FF69B4;display-name=bitseer;emotes=;first-msg=0;flags=;id=f94cbeb4-5db0-4911-814a-c8f0549bf635;mod=0;room-id=42541892
5;subscriber=1;tmi-sent-ts=1653079147427;turbo=0;user-id=558139622;user-type= :bitseer!bitseer@bitseer.tmi.twitch.tv PRIVMSG #charditronic :chardi deberia gustarte apple porque prohibio fortnite en sus dispositivos
chardi deberia gustarte apple porque prohibio fortnite en sus dispositivos
05/20/2022 20:39:11: guerrerobit - Received: @badge-info=subscriber/2;badges=vip/1,subscriber/0,bits/100;color=#FF0000;custom-reward-id=e165d7e0-4ee7-4d84-90ff-eda1cf62db41;display-name=GuerreroBit;emotes=;first-msg=0;flags=;id=a989b40d-a0bb-436e-9af2-6a42658a8a88;m
od=0;room-id=425418925;subscriber=1;tmi-sent-ts=1653079151335;turbo=0;user-id=126707119;user-type= :guerrerobit!guerrerobit@guerrerobit.tmi.twitch.tv PRIVMSG #charditronic :2016
2016
*/
        }

        private void StartDownloadImage(ImageInfo imageInfo) {
            ImageInfo info = imageInfo;
            imageInfo = Task.Run(async () => await ImageUtilities.DownloadImage(info)).Result;

            Message message = config.Get<AppConfig>().messages;
            if (imageInfo.path != null) {
                SaveBuffer(imageInfo);
                Dispatcher.BeginInvoke(new Action(() => { OnNewImageDownloaded?.Invoke(); }));
                twitch.SendMesage(message.downloadSuccess);
            } else {
                twitch.SendMesage(message.downloadFail);
            }
        }

        private static ImageInfo InitImageInfo(RewardConfig reward, string url, string user) {
            ImageInfo imageInfo = new(reward.exclusiveImage, reward.timeInMilliseconds, ImageUtilities.GetUrl(url));
            imageInfo.user = user;
            return imageInfo;
        }

        private static void SaveBuffer(ImageInfo imageInfo) {
            ImageBuffer imageBuffer = config.Get<ImageBuffer>();
            Queue<ImageInfo> images = imageBuffer.toCheckImages;
            images.Enqueue(imageInfo);
            imageBuffer.toCheckImages = images;
            config.Set(imageBuffer);
        }

        public static void ShowError(string name, bool openFolder = true) {
            MessageBox.Show($"Hubo un error de conexión a {name}, comprueba tus datos" +
                            $" o revisa si la conexion esta disponible.");
            if (openFolder) {
                ProcessStartInfo psi = new() {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        devName,
                        productName),
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            Environment.Exit(0);
        }
    }
}