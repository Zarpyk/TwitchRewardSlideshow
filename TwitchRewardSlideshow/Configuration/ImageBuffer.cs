using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TwitchRewardSlideshow.Interfaces;

namespace TwitchRewardSlideshow.Configuration {
    public class ImageBuffer : AppConfiguration.Configuration, ICloneable<ImageBuffer> {
        public Queue<ImageInfo> toCheckImages { get; set; } = new();
        public Queue<ImageInfo> exclusiveImagesQueue { get; set; } = new();
        public Queue<ImageInfo> activeImages { get; set; } = new();
        [JsonIgnore] public Queue<ImageInfo> displayedImages { get; set; } = new();
        public Queue<ImageInfo> defaultImages { get; set; } = new();
        [JsonIgnore] public Queue<ImageInfo> displayedDefaultImages { get; set; } = new();
        public ImageInfo activeExclusiveImage;

        public ImageBuffer Clone() {
            ImageBuffer memberwiseClone = (ImageBuffer)MemberwiseClone();
            memberwiseClone.activeExclusiveImage = activeExclusiveImage?.Clone();
            return memberwiseClone;
        }
    }

    public class ImageInfo : ICloneable<ImageInfo> {
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
            return HashCode.Combine(id);
        }

        public ImageInfo Clone() {
            return (ImageInfo) MemberwiseClone();
        }
    }
}