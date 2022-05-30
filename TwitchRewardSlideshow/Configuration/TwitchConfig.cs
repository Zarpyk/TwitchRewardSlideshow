using System.Collections.Generic;
using Newtonsoft.Json;

namespace TwitchRewardSlideshow.Configuration {
    public class TwitchConfig : AppConfiguration.Configuration {
        public string channelName { get; set; } = "Nombre exacto del canal";

        public string channelId { get; set; } = "https://www.streamweasels.com/tools/" +
                                                "convert-twitch-username-to-user-id/ hay que poner " +
                                                "el nombre exacto del canal y te da el id";

        public string oauth { get; set; } = "https://twitchapps.com/tmi/ no filtres el token";
        public string clientId { get; set; } = "eg9uc3o0ngoo7ohl3n1a3fjtpoi1j8";
#if DEBUG
        public string destinationChannel { get; set; } = "Nombre exacto del canal que mirara las recompensas";
        public string destinationChannelId { get; set; } = "https://www.streamweasels.com/tools/" +
                                                           "convert-twitch-username-to-user-id/ hay que poner " +
                                                           "el nombre exacto del canal y te da el id";
#endif
        public char commandPrefix { get; set; } = '!';

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<RewardConfig> rewards { get; set; } = new();
    }

    public class RewardConfig {
        public string id { get; set; }
        public string title { get; set; }
        public bool exclusiveImage { get; set; }
        public int timeInMilliseconds { get; set; }
        public int points { get; set; }

        public RewardConfig(string title, int timeInMilliseconds, bool exclusiveImage, string id = null,
                            int points = 0) {
            this.title = title;
            this.timeInMilliseconds = timeInMilliseconds;
            this.exclusiveImage = exclusiveImage;
            this.id = id;
            this.points = points;
        }
    }
}