using System.Collections.Generic;

namespace TwitchRewardSlideshow.Json {

    public class TwitchUsersData {
        // ReSharper disable once CollectionNeverUpdated.Global
        public List<TwitchUserData> data { get; set; }
    }
    
    public class TwitchUserData {
        public string id { get; set; }
        public string login { get; set; }
        public string display_name { get; set; }
    }
}