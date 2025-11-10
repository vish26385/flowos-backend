namespace FlowOS.Api.Configurations
{
    public class OpenAISettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4.1-mini";
        // Add this ↓ if not present
        public string? Organization { get; set; }
    }
}
