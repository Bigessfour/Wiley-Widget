using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization; // Install via 'dotnet add package YamlDotNet'

namespace WileyWidget.Tests.Utilities
{
    public class GrokAiHelper
    {
        private readonly string _apiKey;
        private readonly Uri _apiUrl = new Uri("https://api.x.ai/v1/chat/completions"); // Adjust if needed

        public GrokAiHelper()
        {
            // Load from config/grok-config.yaml (parse the relevant section)
            var yamlText = File.ReadAllText("config/grok-config.yaml");
            var deserializer = new DeserializerBuilder().Build();
            var configs = deserializer.Deserialize<dynamic>(yamlText);
            _apiKey = configs[0]["models"][0]["apiKey"]; // Assumes first config is Grok-4
        }

        public async Task<string> GenerateTestScenarioAsync(string userStory)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var payload = new
            {
                model = "grok-4",
                messages = new[] { new { role = "user", content = $"Generate an E2E test scenario for WPF app: {userStory}" } }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            // Parse and return the generated text (simplified)
            return JsonDocument.Parse(responseJson).RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
    }
}
