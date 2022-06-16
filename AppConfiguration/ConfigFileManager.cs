using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;

namespace AppConfiguration {
    internal class ConfigFileManager<T> where T : Configuration, new() {
        private T configValues;
        private string settingsPath;

        internal ConfigFileManager(string customConfigPath, string configName = "config.json") {
            settingsPath = customConfigPath;
            if (!Directory.Exists(customConfigPath)) Directory.CreateDirectory(customConfigPath);
            settingsPath = Path.Combine(settingsPath, configName);
            Sync();
        }

        internal bool setConfig(T configValues) {
            try {
                this.configValues = configValues;
                Save();
                return true;
            } catch (Exception e) {
                Console.WriteLine($"Error setting config: {configValues}");
                Console.WriteLine(e);
                return false;
            }
        }

        internal J Get<J>(Expression<Func<T, J>> variable) {
            Func<T, J> compiled = variable.Compile();
            return compiled(configValues);
        }

        internal bool Set<J>(Expression<Func<T, J>> variable, J value) {
            configValues = GetConfig();
            string varName = variable.Body.ToString().Split('.')[1];
            IEnumerable<PropertyInfo> properties = configValues.GetType().GetProperties();
            foreach (PropertyInfo info in properties)
                if (info.Name == varName) {
                    if (info.CanWrite)
                        try {
                            info.SetValue(configValues, value);
                            Save();
                            return true;
                        } catch (Exception e) {
                            Console.WriteLine($"Error: Can't set \"{variable}\" with the value \"{value}\"");
                            Console.WriteLine(e);
                            return false;
                        }
                    break;
                }
            Console.WriteLine($"Error: Can't set \"{variable}\" with the value \"{value}\"");
            return false;
        }

        internal T GetConfig() {
            Sync();
            return configValues;
        }

        internal void Save() {
            using (StreamWriter writer = new(settingsPath, false)) {
                writer.WriteAsync(JsonConvert.SerializeObject(configValues, Formatting.Indented));
            }
        }

        internal void Sync() {
            bool newValues = false;
            StreamReader reader = !File.Exists(settingsPath) ?
                new StreamReader(File.Create(settingsPath))
                : new StreamReader(settingsPath);
            using (reader) {
                string json = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(json)) {
                    try {
                        configValues = JsonConvert.DeserializeObject<T>(json);
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        throw;
                    }
                } else {
                    configValues = new T();
                    newValues = true;
                }
            }
            if (newValues) Save();
        }
    }
}