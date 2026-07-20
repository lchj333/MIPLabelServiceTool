using System.IO;
using Newtonsoft.Json.Linq;

namespace MIPLabelServiceTool.Helpers
{
    public static class ConfigHelper
    {
        private const string ConfigFileName = "config.json";
        private static readonly Lazy<JObject> Configuration = new(LoadConfiguration);

        public static string GetRequiredValue(string key)
        {
            var value = Configuration.Value.SelectToken(key.Replace(':', '.'))?.Value<string>();

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"'{ConfigFileName}' 파일에 필수 설정값 '{key}'가 없습니다.");
            }

            return value;
        }

        private static JObject LoadConfiguration()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"설정 파일을 찾을 수 없습니다: {configPath}", configPath);
            }

            return JObject.Parse(File.ReadAllText(configPath));
        }
    }
}
