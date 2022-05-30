using System;
using System.Globalization;
using TwitchLib.Api;
using TwitchLib.Api.Helix;
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
        private TwitchAPI api = new();
        public Helix helix;
        public TwitchClient client = new();
        public TwitchPubSub pubSubClient = new();

        private const string errorMsg = "Hubo un error de conexión a Twitch, comprueba tus datos " +
                                        "o revisa si la conexión esta disponible.";

        public Twitch() {
            SetupClient();
            SetupPubSub();
        }

        public void RestartAll() {
            client.Disconnect();
            pubSubClient.Disconnect();
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
                api.Settings.ClientId = config.clientId;
                api.Settings.AccessToken = config.oauth;
                helix = api.Helix;
            } catch (Exception) {
                /*App.ShowError(errorMsg);*/
                return;
            }
            var clientOptions = new ClientOptions {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new(clientOptions);

            client = new TwitchClient(customClient);
#if DEBUG
            client.Initialize(credentials, config.destinationChannel);
#endif
#if RELEASE
            client.Initialize(credentials, config.channelName);
#endif
            client.AddChatCommandIdentifier(config.commandPrefix);

            client.OnLog += OnClientLog;
            client.OnIncorrectLogin += OnClientIncorrectLogin;
            client.OnMessageReceived += OnClientMessageReceived;
            client.OnConnectionError += OnClientConnectionError;
        }

        private void OnClientIncorrectLogin(object sender, OnIncorrectLoginArgs e) {
            Console.WriteLine(e.Exception);
            /*App.ShowError(errorMsg);*/
        }

        private void OnClientConnectionError(object sender, OnConnectionErrorArgs e) {
            Console.WriteLine(e.Error);
            /*App.ShowError(errorMsg);*/
        }

        public void SendMesage(string msg, bool withPrefix = true) {
#if DEBUG
            string channel = App.config.Get<TwitchConfig>().destinationChannel;
#endif
#if RELEASE
            string channel = App.config.Get<TwitchConfig>().channelName;
#endif
            client.SendMessage(channel, (withPrefix ? App.config.Get<AppConfig>().appPrefix : "") + msg);
        }

        private void OnClientLog(object sender, OnLogArgs e) {
            Console.WriteLine($"{e.DateTime.ToString(CultureInfo.InvariantCulture)}: {e.BotUsername} - {e.Data}");
        }

        private void OnClientMessageReceived(object sender, OnMessageReceivedArgs e) {
            Console.WriteLine($"{e.ChatMessage.Message}");
        }

        public void PubSubConnect() {
#if DEBUG
            pubSubClient.ListenToChannelPoints(App.config.Get<TwitchConfig>().destinationChannelId);
#endif
#if RELEASE
            pubSubClient.ListenToChannelPoints(App.config.Get<TwitchConfig>().channelId);
#endif
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
            pubSubClient.SendTopics(App.config.Get<TwitchConfig>().oauth);
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