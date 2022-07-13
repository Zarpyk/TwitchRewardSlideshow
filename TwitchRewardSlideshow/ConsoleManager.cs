using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace TwitchRewardSlideshow {
    internal class ConsoleManager {
        [DllImport("Kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private static string path;
        private static string logPath;

        internal static void InitConsole() {
            AttachConsole(-1);

            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                App.devName, App.productName);
            logPath = Path.Combine(path, "Console.log");
            
            if (File.Exists(logPath)) {
                if (new FileInfo(logPath).Length > 10485760) {
                    File.Delete(logPath);
                }
            }
            
            DualOut.Init();
        }

        private static class DualOut {
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
            File.Copy(logPath, Path.Combine(path, validatedName), true);
        }
    }
}