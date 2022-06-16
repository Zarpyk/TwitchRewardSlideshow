using System;

namespace TwitchRewardSlideshow.Configuration {
    public class AppConfig : AppConfiguration.Configuration {
        public string imageFolder { get; set; } = string.Empty;
        public string defaultPosterFolder { get; set; } = string.Empty;

        public string lastAddedImageFolder { get; set; } = string.Empty;

        //public string tempImageFolder { get; set; } = "Temp";
        //public string acceptedImageFolder { get; set; } = "Accepted";
        public string appPrefix { get; set; } = "[Bot] ";
        public OBSInfo obsInfo { get; set; } = new();
        public Message messages { get; set; } = new();
        public bool firstTime { get; set; } = true;
    }

    public class OBSInfo {
        public string obsIP { get; set; } = "ws://127.0.0.1:4444";
        public string obsPass { get; set; } = "changeme";
        public string gallerySourceName { get; set; } = "Poster Magico";
        public int maxImageSize { get; set; } = 5;
        public int maxGifSize { get; set; } = 5;
        public float securitySize { get; set; } = 1;
        public int slideTimeInMilliseconds { get; set; } = 5000;
        public AspectRatio aspectRatio { get; set; } = new();
    }

    public class Message {
        public string downloadSuccess = "La imagen se ha añadido correctamente";
        public string downloadFail = "La imagen no se ha podido descargar correctamente, hay que subir la imagen a " +
                                     "gyazo.com, imgur.com o subir la imagen a discord y copiar el enlace de la imagen";
        public string invalidHost = "Hay que subir la imagen a gyazo.com o subir la imagen a discord y copiar " +
                                    "el enlace de la imagen";
        public string invalidSize = "La imagen es demasiada grande";
        public string invalidImageFormat = "Solo esta permitido PNG, JPG y GIF";
        public string rewardMsg = "Pon el enlace de la imagen para canjearlo, puedes subirlo a discord y " +
                                  "copiar su link o subirlo a sitios como gyazo.com o imgur.com. " +
                                  "Se recomienda usar una imagen del tamaño: %aspect_ratio%";
    }

    public class AspectRatio {
        public int width { get; set; } = 595;
        public int height { get; set; } = 842;

        public AspectRatio() { }

        public AspectRatio(int width, int height) {
            this.width = width;
            this.height = height;
        }

        public AspectRatio(string aspectRatio) {
            string[] strings = aspectRatio.Split('x');
            width = int.Parse(strings[0]);
            height = int.Parse(strings[1]);
        }

        public override string ToString() {
            return $"{width}x{height}";
        }
    }
}