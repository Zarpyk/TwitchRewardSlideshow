using System;
using System.Collections.Generic;
using System.IO;
using SQLite;

namespace TwitchRewardSlideshow.Configuration {
    public class ImageBuffer : AppConfiguration.Configuration {
        public Queue<ImageInfo> toCheckImages { get; set; } = new();
        public Queue<ImageInfo> exclusiveImagesQueue { get; set; } = new();
        public List<ImageInfo> activeImages { get; set; } = new();
        public List<ImageInfo> displayedImages { get; set; } = new();
        public List<ImageInfo> defaultImages { get; set; } = new();
        public List<ImageInfo> displayedDefaultImages { get; set; } = new();
        public ImageInfo activeExclusiveImage = null;
    }

    public class ImageInfo {
        [PrimaryKey]
        public string id => Path.GetFileName(path);
        public string path { get; set; }
        public bool exclusive { get; set; }
        public double totalActiveTime { get; set; }
        public double usedTime { get; set; }
        public string downloadLink { get; set; }
        public string user { get; set; }
        public string rewardId { get; set; }
        public string redemptionId { get; set; }

        public ImageInfo(bool exclusive, double totalActiveTime, string downloadLink) {
            this.exclusive = exclusive;
            this.totalActiveTime = totalActiveTime;
            this.downloadLink = downloadLink;
        }

        public override bool Equals(object obj) {
            return obj is ImageInfo item && id.Equals(item.id);
        }

        public override int GetHashCode() {
            return HashCode.Combine(id, downloadLink);
        }
    }
}