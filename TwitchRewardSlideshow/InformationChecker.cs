using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Windows;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

namespace TwitchRewardSlideshow {
    public class InformationChecker {
        public static void CheckAll() {
            CheckOBS();
            CheckFolders();
        }

        private static void CheckOBS() {
            AppConfig appConfig = App.config.Get<AppConfig>();
            //WebSocket
            CheckOBSWebSocket(ref appConfig);
            //Create source
            CheckOBSCreateSource(ref appConfig);
            //Config source
            CheckOBSConfigureSource(ref appConfig);

            //Apply filter
            CheckOBSFilterSource();

            App.config.Set(appConfig);
        }

        private static void CheckOBSWebSocket(ref AppConfig appConfig) {
            bool accept = false;
            ImageInputDialog webSocketIpDialog = new("Introduce la contraseña del WebSocket y recomiendo deshabilitar" +
                                                     " \"Habilitar alertas en la bandeja de sistema\"",
                "Help_WebSocket.gif", true, true, appConfig.obsInfo.obsPass);
            do {
                if (webSocketIpDialog.ShowDialog() == true) {
                    appConfig.obsInfo.obsPass = webSocketIpDialog.result;
                    accept = true;
                }
            } while (!accept);
        }

        private static void CheckOBSCreateSource(ref AppConfig appConfig) {
            bool accept = false;
            ImageInputDialog webSocketIpDialog = new("Introduce el nombre de la galeria de imagenes",
                "Help_CreateSource.gif", false, true, appConfig.obsInfo.gallerySourceName);
            do {
                if (webSocketIpDialog.ShowDialog() == true) {
                    appConfig.obsInfo.gallerySourceName = webSocketIpDialog.result;
                    accept = true;
                }
            } while (!accept);
        }

        private static void CheckOBSConfigureSource(ref AppConfig appConfig) {
            bool accept = false;
            const string text = "Introduce el tiempo que quieres que haya entre diapositivas (en ms) " +
                                "(no hace falta hacerlo en OBS, va por separado).\n" +
                                "**IMPORTANTE**: Necesitas establecer la relación de aspecto a un " +
                                "valor fijo y no dejarlo en automatico.\n" +
                                "(Por ejemplo puedes poner el tamaño de los A4, que son \"595x842\"" +
                                ", \"794x1123\", \"1240x1754\" o \"2480x3508\" dependiendo de la " +
                                "resolución que quieres que tenga)";

            ImageInputDialog webSocketIpDialog = new(text, "Help_ConfigSource.gif", false, true,
                appConfig.obsInfo.slideTimeInMilliseconds.ToString());
            do {
                if (webSocketIpDialog.ShowDialog() == true) {
                    if (int.TryParse(webSocketIpDialog.result, out int result)) {
                        appConfig.obsInfo.slideTimeInMilliseconds = result;
                        accept = true;
                    } else {
                        webSocketIpDialog = new(text, "Help_ConfigSource.gif", false, true,
                            appConfig.obsInfo.slideTimeInMilliseconds.ToString());
                    }
                }
            } while (!accept);
        }

        private static void CheckOBSFilterSource() {
            const string text = "Para ajustar el poster al sitio, hay que usar el StreamFX y " +
                                "aplicar el filtro \"Transformación 3D\". Para ajustar, recomiendo " +
                                "asignar manualmente una imagen (a la galería de imágenes) " +
                                "del mismo tamaño que se haya puesto anteriormente en las " +
                                "resoluciones de la galería de imágenes e ir poco a poco " +
                                "adaptandolo al croma para que quede bien.";

            ImageInputDialog webSocketIpDialog =
                new(text, "Help_FilterSource.gif", false, true, "No hace falta nada :)");
            webSocketIpDialog.ShowDialog();
        }

        private static void CheckFolders() {
            //Asign Folder
            throw new System.NotImplementedException();
        }
    }
}