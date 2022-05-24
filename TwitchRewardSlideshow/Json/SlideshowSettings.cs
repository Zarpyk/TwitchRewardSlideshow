using System.Collections.Generic;

namespace TwitchRewardSlideshow.Json {
    public class SlideshowSettings {
        public List<FileSettings> files { get; set; }
        public bool hide { get; set; }
        public bool loop { get; set; }
        public int slide_time { get; set; }
        public string transition { get; set; }
        public string use_custom_size { get; set; }
    }

    public class FileSettings {
        public bool hidden { get; set; }
        public bool selected { get; set; }
        public string value { get; set; }

        public FileSettings(bool hidden, bool selected, string value) {
            this.hidden = hidden;
            this.selected = selected;
            this.value = value;
        }
    }
}