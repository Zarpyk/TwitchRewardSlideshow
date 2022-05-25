using System;
using System.Globalization;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchRewardSlideshow.Configuration;
using OnLogArgs = TwitchLib.Client.Events.OnLogArgs;

namespace TwitchRewardSlideshow {
    public class Twitch {
        //twitch token 1otztatspp90br6j2m3sj45cfxk9va
        public TwitchPubSub pubSubClient = new();
        public TwitchClient client = new();

        public Twitch() {
            SetupClient();
            SetupPubSub();
        }

        public void Connect() {
            client.Connect();
            PubSubConnect();
        }

        public void SetupClient() {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            ConnectionCredentials credentials;
            try {
                credentials = new ConnectionCredentials(config.channelName, config.oauth);
            } catch (Exception) {
                App.ShowError("Twich");
                return;
            }
            var clientOptions = new ClientOptions {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, config.destinationChannel);
            client.AddChatCommandIdentifier(config.commandPrefix);

            client.OnLog += OnClientLog;
            client.OnIncorrectLogin += OnClientIncorrectLogin;
            client.OnMessageReceived += OnClientMessageReceived;
            client.OnConnectionError += OnClientConnectionError;
        }

        private void OnClientIncorrectLogin(object sender, OnIncorrectLoginArgs e) {
            Console.WriteLine(e.Exception);
            App.ShowError("Twich");
        }

        private void OnClientConnectionError(object sender, OnConnectionErrorArgs e) {
            Console.WriteLine(e.Error);
            App.ShowError("Twich");
        }

        public void SendMesage(string msg, bool withPrefix = true) {
            client.SendMessage(App.config.Get<TwitchConfig>().destinationChannel,
                (withPrefix ? App.config.Get<AppConfig>().appPrefix : "") + msg);
        }

        private void OnClientLog(object sender, OnLogArgs e) {
            Console.WriteLine($"{e.DateTime.ToString(CultureInfo.InvariantCulture)}: {e.BotUsername} - {e.Data}");
        }

        private void OnClientMessageReceived(object sender, OnMessageReceivedArgs e) {
            Console.WriteLine($"{e.ChatMessage.Message}");
        }

        public void PubSubConnect() {
            pubSubClient.ListenToChannelPoints(App.config.Get<TwitchConfig>().destinationChannelId);
            pubSubClient.Connect();
        }

        public void SetupPubSub() {
            pubSubClient.OnListenResponse += OnPubSubListenResponse;
            pubSubClient.OnPubSubServiceConnected += OnPubSubServiceConnected;
            pubSubClient.OnPubSubServiceClosed += OnPubSubServiceClosed;
            pubSubClient.OnPubSubServiceError += OnPubSubServiceError;

            pubSubClient.OnChannelPointsRewardRedeemed += DebugReward;
        }

        private void DebugReward(object sender, OnChannelPointsRewardRedeemedArgs e) {
            Console.WriteLine($"{e.RewardRedeemed.Redemption.Reward.Prompt}\n" +
                              $"{e.RewardRedeemed.Redemption.UserInput}\n" +
                              $"{e.RewardRedeemed.Redemption.Reward.Id}\n" +
                              $"{e.RewardRedeemed.Redemption.Reward.Title}\n");
        }

        private void OnPubSubServiceConnected(object sender, EventArgs e) {
            Console.WriteLine("Connected to pubsub server");
            pubSubClient.SendTopics(App.config.Get<TwitchConfig>().token);
        }

        private void OnPubSubListenResponse(object sender, OnListenResponseArgs e) {
            Console.WriteLine(e.Successful ? $"Successfully verified listening to topic: {e.Topic}"
                : $"Failed to listen! Error: {e.Response.Error}");
        }

        private void OnPubSubServiceClosed(object sender, EventArgs e) {
            Console.WriteLine("Connection closed to pubsub server");
        }

        private void OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e) {
            Console.WriteLine($"{e.Exception.Message}");
        }
    }
}