namespace FlowOS.Api.Models
{
    public class UserDeviceToken
    {
        public int Id { get; set; }

        public string UserId { get; set; } = null!;
        public string ExpoPushToken { get; set; } = null!;
        public string Platform { get; set; } = "unknown"; // android | ios | web
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
