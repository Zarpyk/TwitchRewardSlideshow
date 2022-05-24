using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace TwitchRewardSlideshow {
    public class ConsoleManager {
        [DllImport("Kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        private static string logPath;

        public ConsoleManager() {
#if DEBUG
            AttachConsole(-1);
#endif
            logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                App.devName, App.productName, "Console.log");
            DualOut.Init();
        }

        internal static class DualOut {
            private static TextWriter _current;

            private class OutputWriter : TextWriter {
                public override Encoding Encoding => _current.Encoding;

                public override void WriteLine(string value) {
                    _current.WriteLine(value);
                    File.AppendAllText(logPath, value + (string.IsNullOrWhiteSpace(value) ? "" : $" {DateTime.Now}\n"));
                }
            }

            public static void Init() {
                _current = Console.Out;
                Console.SetOut(new OutputWriter());
            }
        }

        internal static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            File.AppendAllText(logPath, e.Exception + "\n");
            backupFile();
        }

        internal static void backupFile() {
            string validatedName = $"Console[{DateTime.Now:dd-MM-yyyy_HH-mm-ss}].log";
            if (!File.Exists(logPath)) File.Create(logPath);
            File.Copy(logPath, validatedName, true);
        }
    }
}