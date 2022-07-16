using System;
using System.Collections.Generic;
using System.Globalization;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Utilities.TwitchUtilities;
using OnLogArgs = TwitchLib.Client.Events.OnLogArgs;

namespace TwitchRewardSlideshow {
    public class Twitch {
        //twitch token 1otztatspp90br6j2m3sj45cfxk9va
        private TwitchAPI _api = new();
        public Helix helix;
        public TwitchClient client;
        public TwitchPubSub pubSubClient = new();

        public Twitch() {
            ValidConfig();

            var clientOptions = new ClientOptions {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new(clientOptions);
            client = new TwitchClient(customClient);

            SetupApi();
            SetupClient();
            SetupPubSub();
        }

        public void RestartAll() {
            client.Disconnect();
            pubSubClient.Disconnect();
            SetupApi();
            SetupClient();
            SetupPubSub();
            Connect();
        }

        public void Connect() {
            if (client.ConnectionCredentials != null) client.Connect();
            PubSubConnect();
        }

        private bool ValidConfig() {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            if (string.IsNullOrWhiteSpace(config.channelName) ||
                string.IsNullOrWhiteSpace(config.channelId) ||
                string.IsNullOrWhiteSpace(config.oauth)) {
                TwitchUtilities.ResetTwitchConfig(config);
                return false;
            }
            ValidateAccessTokenResponse info = TwitchUtilities.GetUserOAuthInfo(config.oauth);
            if (info == null) {
                TwitchUtilities.ResetTwitchConfig(config);
                return false;
            }
            HashSet<string> scopes = new(info.Scopes);
            if (!string.Equals(info.ClientId, config.clientId, StringComparison.InvariantCultureIgnoreCase) ||
                !string.Equals(info.Login, config.channelName, StringComparison.InvariantCultureIgnoreCase) ||
                !string.Equals(info.UserId, config.channelId, StringComparison.InvariantCultureIgnoreCase) ||
                info.ExpiresIn <= 86400 ||
                !scopes.SetEquals(TwitchUtilities.scopes)) {
                TwitchUtilities.ResetTwitchConfig(config, info);
                return false;
            }
            return true;
        }

        private void SetupApi() {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            try {
                _api.Settings.ClientId = config.clientId;
                _api.Settings.AccessToken = config.oauth;
                helix = _api.Helix;
            } catch (Exception) {
                /*App.ShowError(errorMsg);*/
            }
        }

        public void SetupClient() {
            TwitchConfig config = App.config.Get<TwitchConfig>();
            ConnectionCredentials credentials;
            try {
                credentials = new ConnectionCredentials(config.channelName, config.oauth);
            } catch (Exception) {
                /*App.ShowError(errorMsg);*/
                return;
            }
#if DEBUG
            client.Initialize(credentials, config.destinationChannel);
#endif
#if RELEASE
            client.Initialize(credentials, config.channelName);
#endif
            client.AddChatCommandIdentifier(config.commandPrefix);

            client.OnLog -= OnClientLog;
            client.OnIncorrectLogin -= OnClientIncorrectLogin;
            client.OnMessageReceived -= OnClientMessageReceived;
            client.OnConnectionError -= OnClientConnectionError;
            client.OnNoPermissionError -= OnClientNoPermissionError;
            client.OnLog += OnClientLog;
            client.OnIncorrectLogin += OnClientIncorrectLogin;
            client.OnMessageReceived += OnClientMessageReceived;
            client.OnConnectionError += OnClientConnectionError;
            client.OnNoPermissionError += OnClientNoPermissionError;
        }

        private void OnClientIncorrectLogin(object sender, OnIncorrectLoginArgs e) {
            Console.WriteLine(e.Exception);
        }

        private void OnClientConnectionError(object sender, OnConnectionErrorArgs e) {
            Console.WriteLine(e.Error);
        }

        private void OnClientNoPermissionError(object sender, EventArgs e) {
            Console.WriteLine(e.ToString());
            if (!ValidConfig()) {
                App.ShowError(App.config.Get<AppConfig>().messages.noPermissionMsg);
                Environment.Exit(0);
            } else {
                App.ShowError("BUG: No hay permiso suficiente para alguna acción F, contactame :'( otro bug más.");
                Environment.Exit(0);
            }
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
            pubSubClient.OnListenResponse -= OnPubSubListenResponse;
            pubSubClient.OnPubSubServiceConnected -= OnPubSubServiceConnected;
            pubSubClient.OnPubSubServiceClosed -= OnPubSubServiceClosed;
            pubSubClient.OnPubSubServiceError -= OnPubSubServiceError;
            pubSubClient.OnChannelPointsRewardRedeemed -= DebugReward;

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
            Console.WriteLine(e.Successful ?
                                  $"Successfully verified listening to topic: {e.Topic}" :
                                  $"Failed to listen! Error: {e.Response.Error}");
        }

        private void OnPubSubServiceClosed(object sender, EventArgs e) {
            Console.WriteLine("Connection closed to pubsub server");
        }

        private void OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e) {
            Console.WriteLine($"{e.Exception.Message}");
        }
    }
}