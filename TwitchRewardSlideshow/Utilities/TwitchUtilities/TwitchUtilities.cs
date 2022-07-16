using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Client.Models;
using TwitchRewardSlideshow.Configuration;
using WebSocketSharp;

namespace TwitchRewardSlideshow.Utilities.TwitchUtilities {
    public static class TwitchUtilities {
        public static readonly HashSet<string> scopes = new() {
            "chat:read",
            "chat:edit",
            "channel:read:redemptions",
            "channel:manage:redemptions"
        };

        public static string GetOAuth(string absoluteUri) {
            return absoluteUri.Split('/').Last().Split('&').First().Replace("#access_token=", "");
        }

        public static string GetTokenLink() {
            string link = "https://id.twitch.tv/oauth2/authorize?response_type=token&";
            link += $"client_id={App.config.Get<TwitchConfig>().clientId}&";
            link += "redirect_uri=http://localhost:3000&";
            link += "scope=";
            int i = 0;
            foreach (string scope in scopes) {
                if (i == scopes.Count - 1) {
                    link += scope.Replace(":", "%3A");
                } else {
                    link += scope.Replace(":", "%3A") + "+";
                }
                i++;
            }
            return link;
        }

        public static ValidateAccessTokenResponse GetUserOAuthInfo(string oauth) {
            TwitchAPI api = new() {
                Settings = {
                    ClientId = App.config.Get<TwitchConfig>().clientId,
                    AccessToken = oauth
                }
            };
            ValidateAccessTokenResponse response =
                Task.Run(async () => await api.Auth.ValidateAccessTokenAsync(oauth)).Result;
            return response;
        }

        public static bool SaveData(string oauth, ValidateAccessTokenResponse info) {
            if (oauth.IsNullOrEmpty() || info == null) return false;
            TwitchConfig config = App.config.Get<TwitchConfig>();
            config.oauth = oauth;
            config.channelName = info.Login;
            config.channelId = info.UserId;
#if DEBUG
            config.destinationChannel = info.Login;
            config.destinationChannelId = info.UserId;
#endif
            App.config.Set(config);
            return true;
        }

        public static void ResetTwitchConfig(TwitchConfig twitchConfig, ValidateAccessTokenResponse info = null) {
            twitchConfig.channelName = "";
            twitchConfig.channelId = "";
            if (twitchConfig.oauth != null) {
                info ??= GetUserOAuthInfo(twitchConfig.oauth);
                if (info == null) goto NoOAuth;
                HttpClient http = new();
                FormUrlEncodedContent content = new(new Dictionary<string, string> {
                    ["client_id"] = twitchConfig.clientId,
                    ["token"] = twitchConfig.oauth
                });
                http.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                try {
                    Task.Run(async () => await http.PostAsync("https://id.twitch.tv/oauth2/revoke", content));
                } catch (Exception) {
                    // ignored
                }
            }
            NoOAuth:
            twitchConfig.oauth = "";
            App.config.Set(twitchConfig);
        }
    }
}