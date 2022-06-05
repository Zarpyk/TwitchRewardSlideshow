using System;
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
            CheckOBSConfigureSource2(ref appConfig);
            //Config twitch Reward Msg
            CheckTwitchRewardMsg(ref appConfig);
            //Apply filter
            CheckOBSFilterSource();

            App.config.Set(appConfig);
        }

        public static void CheckOBSWebSocket(ref AppConfig appConfig) {
            const string text = "1. Introduce la contraseña del WebSocket que has configurado en el campo de abajo.\n" +
                                "2. Recomiendo deshabilitar \"Habilitar alertas en la bandeja de sistema\"";
            ImageInputDialog webSocketIpDialog = new(text, "Help_WebSocket.gif", true, true, appConfig.obsInfo.obsPass);

            if (webSocketIpDialog.ShowDialog() == true) {
                appConfig.obsInfo.obsPass = webSocketIpDialog.result;
            } else {
                Environment.Exit(0);
            }
        }

        public static void CheckOBSCreateSource(ref AppConfig appConfig) {
            const string text = "Crea e introduce el nombre de la galeria de imagenes (Nombre exacto)";
            ImageInputDialog webSocketIpDialog = new(text, "Help_CreateSource.gif", false, true,
                                                     appConfig.obsInfo.gallerySourceName);
            if (webSocketIpDialog.ShowDialog() == true) {
                appConfig.obsInfo.gallerySourceName = webSocketIpDialog.result.Trim();
            } else {
                Environment.Exit(0);
            }
        }

        public static void CheckOBSConfigureSource(ref AppConfig appConfig) {
            const string text = "Introduce el tiempo que quieres que haya entre diapositivas (en ms) \n" +
                                "(Solo ponerlo aqui, no hace falta hacerlo en OBS, va por separado).\n" +
                                "(Si no ha quedado claro, es el tiempo que estara cada poster visible)";
            InputDialog webSocketIpDialog =
                new(text, false, true, appConfig.obsInfo.slideTimeInMilliseconds.ToString());
            do {
                if (webSocketIpDialog.ShowDialog() == true) {
                    if (int.TryParse(webSocketIpDialog.result.Trim(), out int result)) {
                        appConfig.obsInfo.slideTimeInMilliseconds = result;
                        return;
                    }
                    webSocketIpDialog = new(text, false, true,
                                            appConfig.obsInfo.slideTimeInMilliseconds.ToString());
                } else {
                    Environment.Exit(0);
                }
            } while (true);
        }

        public static void CheckOBSConfigureSource2(ref AppConfig appConfig) {
            const string text =
                "**IMPORTANTE**: En la galeria de imagenes, necesitas establecer la relación de aspecto " +
                "a un valor fijo y no dejarlo en \"Automatico\", como se muestra en el gif de abajo.\n " +
                "(Por ejemplo puedes poner el tamaño de los A4, que es de \"595x842\", eso si, no se " +
                "recomienda resoluciones demasiado grandes).\n\n" +
                "Introduce la relación de aspecto que has puesto:";
            ImageInputDialog webSocketIpDialog = new(text, "Help_ConfigSource.gif", false, true,
                                                     appConfig.obsInfo.aspectRatio);
            if (webSocketIpDialog.ShowDialog() == true) {
                appConfig.obsInfo.aspectRatio = webSocketIpDialog.result.Trim();
            } else {
                Environment.Exit(0);
            }
        }

        public static void CheckTwitchRewardMsg(ref AppConfig appConfig) {
            const string text = "Introduce el mensaje que quieres que aparezca de descripción de las recompensas.\n" +
                                "(Recuerda que tiene que estar el placeholder %aspect_ ratio% para que pueda ser " +
                                "sustituida por la relación de aspecto que se ha configurado en el paso anterior)";
            InputDialog webSocketIpDialog = new(text, false, true, appConfig.messages.rewardMsg);
            if (webSocketIpDialog.ShowDialog() == true) {
                appConfig.messages.rewardMsg = webSocketIpDialog.result.Trim();
            } else {
                Environment.Exit(0);
            }
        }

        public static void CheckOBSFilterSource() {
            const string text =
                "Para ajustar el poster al sitio, hay que usar el StreamFX y aplicar el filtro \"Transformación 3D\".\n" +
                "Para ajustar, recomiendo asignar manualmente una imagen (a la galería de imágenes) del mismo tamaño que se haya\n" +
                "puesto anteriormente en la \"relación de aspecto\" de la galería de imágenes e ir poco a poco adaptandolo al croma para\n" +
                "que quede bien.";

            ImageInputDialog webSocketIpDialog = new(text, "Help_FilterSource.gif", false, true,
                                                     "No hace falta nada :) (Me da pereza hacer otro tipo de dialogo)");
            if (webSocketIpDialog.ShowDialog() == false) Environment.Exit(0);
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
            } else {
                Environment.Exit(0);
            }
        }
    }
}