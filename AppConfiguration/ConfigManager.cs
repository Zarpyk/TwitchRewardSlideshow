using System;
using System.IO;
using System.Linq.Expressions;

namespace AppConfiguration {
    public class ConfigManager {
        private string settingsPath;

        public ConfigManager(string companyName, string productName) {
            InitPath(companyName, productName);
        }

        private void InitPath(string companyName, string productName) {
            if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(productName))
                throw new NullReferenceException("Company Name or Product Name is Null or WhiteSpace");
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                companyName, productName, "Configuration");
            if (!Directory.Exists(settingsPath)) Directory.CreateDirectory(settingsPath);
        }

        public T Get<T>() where T : Configuration, new() {
            ConfigFileManager<T> config = new(settingsPath, typeof(T).Name.ToLower() + ".json");
            return config.GetConfig();
        }

        /*public bool Set<T>(Expression<Func<T, object>> variable, object value) where T : Configuration, new() {
            ConfigFileManager<T> config = new(settingsPath, typeof(T).Name.ToLower() + ".json");
            return config.Set(variable, value);
        }*/

        public bool Set<T>(T value) where T : Configuration, new() {
            ConfigFileManager<T> config = new(settingsPath, typeof(T).Name.ToLower() + ".json");
            return config.setConfig(value);
        }
    }

    public class ConfigTest : Configuration {
        public string a { get; set; }
    }
}