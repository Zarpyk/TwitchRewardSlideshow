using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TwitchLib.Api.Core.Enums;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Json;
using WebSocketSharp;

namespace TwitchRewardSlideshow.Utilities.TwitchUtilities {
    public class TwitchInfo {
        public static string GetOAuth(string absoluteUri) {
            return absoluteUri.Split('/').Last().Split('&').First().Replace("#access_token=", "");
        }

        public static string GetTokenLink() {
            string link = "https://id.twitch.tv/oauth2/authorize?response_type=token&";
            link += $"client_id={App.config.Get<TwitchConfig>().clientId}&";
            link += "redirect_uri=http://localhost:3000&";
            link += "scope=" + ("chat:read+" +
                                "chat:edit+" +
                                "channel:read:redemptions+" +
                                "channel:manage:redemptions+" +
                                "user:read:email").Replace(":", "%3A");
            return link;
        }

        public static string GetUserInfo(string oauth) {
            HttpClient client = new();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {oauth}");
            client.DefaultRequestHeaders.Add("Client-Id", App.config.Get<TwitchConfig>().clientId);
            return Task.Run(async () => await client.GetStringAsync(new Uri("https://api.twitch.tv/helix/users")))
                       .Result;
        }

        public static bool SaveData(string oauth, TwitchUsersData usersData) {
            if (oauth.IsNullOrEmpty() || usersData == null) return false;
            TwitchConfig config = App.config.Get<TwitchConfig>();
            config.oauth = oauth;
            config.channelName = usersData.data.First().login;
            config.channelId = usersData.data.First().id;
#if DEBUG
            config.destinationChannel = usersData.data.First().login;
            config.destinationChannelId = usersData.data.First().id;
#endif
            App.config.Set(config);
            return true;
        }
    }
}