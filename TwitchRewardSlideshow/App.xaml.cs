using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Windows;
using AppConfiguration;
using Microsoft.Win32;
using Octokit;
using SQLite;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.PubSub.Events;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Utilities;
using TwitchRewardSlideshow.Utilities.ImageUtilities;
using TwitchRewardSlideshow.Windows;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace TwitchRewardSlideshow {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public static Twitch twitch;
        public static ConfigManager config;
        public static SQLiteConnection sqlite;
        public static OBS obs;

        public const string devName = "GuerreroBit";
        public const string productName = "TwitchRewardSlideshow";
        public const string version = "2.7";

        private Timer _imageTimer;
        private const int timerInterval = 1000;

        private MainWindow _window;

        private void AppOnStartup(object sender, StartupEventArgs e) {
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SetupConsole();
            SetupConfig();

            CheckUpdate();

            if (config.Get<AppConfig>().firstTime) {
                InformationChecker.CheckAll();
                AppConfig appConfig = config.Get<AppConfig>();
                appConfig.firstTime = false;
                config.Set(appConfig);
            }

            SetupTwitch();
            SetupOBS();
            SetupImageTimer();

            ImageManager.InitAddImageQueue();
            ImageManager.OnNewImageAccepted += ChangeImagesFolder;
            ChangeImagesFolder();

            _window = new MainWindow();
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Current.MainWindow = _window;
            _window.Show();

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
            ConsoleManager.InitConsole();
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
            twitch.pubSubClient.OnChannelPointsRewardRedeemed -= SortReward;
            twitch.client.OnChatCommandReceived -= SortCommand;
            twitch.pubSubClient.OnChannelPointsRewardRedeemed += SortReward;
            twitch.client.OnChatCommandReceived += SortCommand;
            //twitch.client.OnLog += SortReward;
        }

        private void SetupOBS() {
            Process[] processes = Process.GetProcesses();
            if (Process.GetProcessesByName("obs64").Length == 0) {
                string obsPath = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\OBS Studio", "", null);
                if (obsPath != null) {
                    ProcessStartInfo startInfo = new();
                    startInfo.WorkingDirectory = Path.Combine(obsPath, @"bin\64bit");
                    startInfo.FileName = Path.Combine(obsPath, @"bin\64bit", "obs64.exe");
                    Process.Start(startInfo);
                    Thread.Sleep(5000);
                }
            }
            obs = new OBS();
            obs.Init();
            Exit += obs.Disconnect;
        }

        private void SetupImageTimer() {
            _imageTimer = new Timer();
            _imageTimer.AutoReset = true;
            _imageTimer.Elapsed += UpdateImageInfo;
            _imageTimer.Interval = timerInterval;
            _imageTimer.Start();
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
            if (buffer.exclusiveImagesQueue.Count > 0) {
                //Si no hay ya una exclusiva o la exclusiva no es el por defecto
                if (buffer.activeExclusiveImage == null) {
                    //Activa un exclusivo
                    ImageInfo exclusiveImage = buffer.exclusiveImagesQueue.Dequeue();
                    buffer.activeExclusiveImage = exclusiveImage;
                    obs.UpdateImageBuffer(buffer, false);
                }
            } else {
                //Quita el por defecto
                if (buffer.activeExclusiveImage == null) {
                    obs.UpdateImageBuffer(buffer, false);
                }
            }
            config.Set(buffer);
        }

        private void SortCommand(object sender, OnChatCommandReceivedArgs e) {
            ChatMessage chatMessage = e.Command.ChatMessage;
            if (!chatMessage.IsBroadcaster && !chatMessage.IsModerator &&
                chatMessage.UserId != "126707119") return;
            switch (e.Command.CommandText) {
                case "add": {
                    string[] arg = e.Command.ArgumentsAsString.Split(':', 2);
                    string displayName = chatMessage.DisplayName;
                    ImageManager.AddImage(arg[0], arg[1], displayName);
                    break;
                }
                case "accept":
                case "ac": {
                    ImageManager.AcceptImage();
                    break;
                }
                case "deny":
                case "d": {
                    ImageManager.RejectImage();
                    break;
                }
                case "next": {
                    ImageBuffer buffer = config.Get<ImageBuffer>();
                    if (buffer.toCheckImages.Count == 0) twitch.SendMesage("No hay más imagenes para comprobar");
                    ImageInfo info = buffer.toCheckImages.Dequeue();
                    twitch.SendMesage($"La proxima imagen ({(info.exclusive ? "Exclusiva" : "No exclusiva")})" +
                                      $" es de {info.user} con link a {info.downloadLink}");
                    break;
                }
                case "help": {
                    twitch.SendMesage("https://github.com/GuerreroBit/TwitchRewardSlideshow/blob" +
                                      "/master/README.md#commands");
                    break;
                }
                case "test": {
                    if (chatMessage.UserId != "126707119") return;
                    twitch.SendMesage("!add Test Poster:https://media.discordapp.net/attachments/960637692348072027/977671768045137960/unknown.png",
                                      false);
                    twitch.SendMesage("!add Test Poster:https://gyazo.com/0915089a6ccd7093eb2191091f7da67e", false);
                    twitch.SendMesage("!add Test Poster:https://imgur.com/mS8VUB6", false);
                    twitch.SendMesage("!add Test Poster:https://imgur.com/gallery/RdmEsxR", false);
                    twitch.SendMesage("!add Test Poster:https://media.discordapp.net/attachments/960637692348072027/974837047036637194/Charditronic_tazita.gif",
                                      false);
                    break;
                }
            }
        }

        private void SortReward(object sender, OnChannelPointsRewardRedeemedArgs e) {
            string title = e.RewardRedeemed.Redemption.Reward.Title;
            string arg = e.RewardRedeemed.Redemption.UserInput;
            string displayName = e.RewardRedeemed.Redemption.User.DisplayName;
            string redemptionId = e.RewardRedeemed.Redemption.Id;
            ImageManager.AddImage(title, arg, displayName, redemptionId);
        }

        public static void ShowError(string error, bool openFolder = true) {
            MessageBox.Show(error);
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