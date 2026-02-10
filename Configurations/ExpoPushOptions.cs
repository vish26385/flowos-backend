namespace FlowOS.Api.Configurations
{
    public class ExpoPushOptions
    {
        public string? AccessToken { get; set; }
        public string SendUrl { get; set; } = "https://exp.host/--/api/v2/push/send";
        public string ReceiptUrl { get; set; } = "https://exp.host/--/api/v2/push/getReceipts";
        public int PollingSeconds { get; set; } = 20;
        public int BatchSize { get; set; } = 50;
    }
}
