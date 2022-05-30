using System.IO;
using System.Windows;
using TwitchRewardSlideshow.Configuration;
using TwitchRewardSlideshow.Windows;

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

namespace TwitchRewardSlideshow.Utilities {
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

        public static void CheckOBSWebSocket(ref AppConfig appConfig) {
            bool accept = false;
            ImageInputDialog webSocketIpDialog = new("Introduce la contraseña del WebSocket y recomiendo deshabilitar" +
                                                     " \"Habilitar alertas en la bandeja de sistema\"",
                                                     "Help_WebSocket.gif", true, true, appConfig.obsInfo.obsPass);
            do {
                if (webSocketIpDialog.ShowDialog() == true) {
                    appConfig.obsInfo.obsPass = webSocketIpDialog.result;
                    accept = true;
                } else {
                    webSocketIpDialog = new("Introduce la contraseña del WebSocket y recomiendo deshabilitar" +
                                            " \"Habilitar alertas en la bandeja de sistema\"",
                                            "Help_WebSocket.gif", true, true, appConfig.obsInfo.obsPass);
                }
            } while (!accept);
        }

        public static void CheckOBSCreateSource(ref AppConfig appConfig) {
            bool accept = false;
            ImageInputDialog webSocketIpDialog = new("Introduce el nombre de la galeria de imagenes",
                                                     "Help_CreateSource.gif", false, true,
                                                     appConfig.obsInfo.gallerySourceName);
            do {
                if (webSocketIpDialog.ShowDialog() == true) {
                    appConfig.obsInfo.gallerySourceName = webSocketIpDialog.result;
                    accept = true;
                }
            } while (!accept);
        }

        public static void CheckOBSConfigureSource(ref AppConfig appConfig) {
            bool accept = false;
            const string text = "1. Introduce el tiempo que quieres que haya entre diapositivas (en ms) " +
                                "(no hace falta hacerlo en OBS, va por separado).\n" +
                                "2. **IMPORTANTE**: Necesitas establecer la relación de aspecto a un " +
                                "valor fijo y no dejarlo en automatico.\n" +
                                "(Por ejemplo puedes poner el tamaño de los A4, que es de \"595x842\", eso si, " +
                                "no se recomienda resoluciones demasiado grandes)";

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

        public static void CheckOBSFilterSource() {
            const string text =
                "Para ajustar el poster al sitio, hay que usar el StreamFX y aplicar el filtro \"Transformación 3D\".\n" +
                "Para ajustar, recomiendo asignar manualmente una imagen (a la galería de imágenes) del mismo tamaño \n" +
                "que se haya puesto anteriormente en la \"relación de aspecto\" de la galería de imágenes e ir poco \n" +
                "a poco adaptandolo al croma para que quede bien.";

            ImageInputDialog webSocketIpDialog =
                new(text, "Help_FilterSource.gif", false, true, "No hace falta nada :)");
            webSocketIpDialog.ShowDialog();
        }

        public static void CheckFolders() {
            MessageBox.Show("A continuación, selecciona una carpeta para guardar las imagenes que se van a descargar");
            AppConfig appConfig = App.config.Get<AppConfig>();
            string imageFolder = appConfig.imageFolder;
            FolderPicker dlg = new() {
                inputPath = Directory.Exists(imageFolder) ? imageFolder : @"C:\"
            };
            if (dlg.ShowDialog() == true) {
                appConfig = App.config.Get<AppConfig>();
                appConfig.imageFolder = dlg.resultPath;
                appConfig.defaultPosterFolder = Path.Combine(dlg.resultPath, "Default");
                Directory.CreateDirectory(appConfig.defaultPosterFolder);
                App.config.Set(appConfig);
            }
        }
    }
}