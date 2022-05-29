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
        public int maxImageSize { get; set; } = 10;
        public int maxGifSize { get; set; } = 10;
        public float securitySize { get; set; } = 1;
        public int slideTimeInMilliseconds { get; set; } = 5000;
    }

    public class Message {
        public string downloadSuccess = "La imagen se ha añadido correctamente";
        public string downloadFail = "La imagen no se ha podido descargar correctamente, hay que subir la imagen a " +
                                     "gyazo.com, imgur.com o subir la imagen a discord y copiar el enlace de la imagen";
        public string invalidSize = "La imagen es demasiada grande";
        public string invalidImageFormat = "Solo esta permitido PNG, JPG y GIF";
    }
}