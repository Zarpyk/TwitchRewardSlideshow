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
        public List<RewardConfig> rewards { get; set; } = new() {
            new RewardConfig(1800000, false, "TituloExacto30MinutosAlCarrusel"),
            new RewardConfig(3600000, false, "TituloExacto1HoraAlCarrusel"),
            new RewardConfig(7200000, false, "TituloExacto2HorasAlCarrusel"),
            new RewardConfig(3600000, true, "TituloExacto1HoraALaColaDeExclusivo"),
            new RewardConfig(7200000, true, "TituloExacto2HoraALaColaDeExclusivo"),
            new RewardConfig(14400000, true, "TituloExacto4HoraALaColaDeExclusivo"),
        };
    }

    public class RewardConfig {
        public int timeInMilliseconds { get; set; }
        public bool exclusiveImage { get; set; }
        public string title { get; set; }

        public RewardConfig(int timeInMilliseconds, bool exclusiveImage, string title) {
            this.timeInMilliseconds = timeInMilliseconds;
            this.exclusiveImage = exclusiveImage;
            this.title = title;
        }
    }
}