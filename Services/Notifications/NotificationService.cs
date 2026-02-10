//using FlowOS.Api.Data;
//using FlowOS.Api.Models;
//using Microsoft.EntityFrameworkCore;

//namespace FlowOS.Api.Services.Notifications
//{
//    public interface INotificationService
//    {
//        System.Threading.Tasks.Task RegisterDeviceAsync(string userId, string expoToken, string platform);
//    }

//    public class NotificationService : INotificationService
//    {
//        private readonly FlowOSContext _db;

//        public NotificationService(FlowOSContext db)
//        {
//            _db = db;
//        }

//        public async System.Threading.Tasks.Task RegisterDeviceAsync(string userId, string expoToken, string platform)
//        {
//            var existing = await _db.UserDeviceTokens
//                .FirstOrDefaultAsync(x => x.ExpoPushToken == expoToken);

//            if (existing != null)
//            {
//                existing.IsActive = true;
//                existing.RegisteredAt = DateTime.UtcNow;
//            }
//            else
//            {
//                _db.UserDeviceTokens.Add(new UserDeviceToken
//                {
//                    UserId = userId,
//                    ExpoPushToken = expoToken,
//                    Platform = platform,
//                });
//            }

//            await _db.SaveChangesAsync();
//        }
//    }
//}

using FlowOS.Api.Data;
using FlowOS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlowOS.Api.Services.Notifications
{
    public interface INotificationService
    {
        System.Threading.Tasks.Task RegisterDeviceAsync(string userId, string expoToken, string platform);
        System.Threading.Tasks.Task DeactivateDeviceAsync(string userId, string expoToken); // optional but useful
    }

    public class NotificationService : INotificationService
    {
        private readonly FlowOSContext _db;

        public NotificationService(FlowOSContext db)
        {
            _db = db;
        }

        public async System.Threading.Tasks.Task RegisterDeviceAsync(string userId, string expoToken, string platform)
        {
            expoToken = expoToken.Trim();
            platform = string.IsNullOrWhiteSpace(platform) ? "unknown" : platform.Trim();

            // ✅ Match by UserId + Token
            var existing = await _db.UserDeviceTokens
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ExpoPushToken == expoToken);

            if (existing != null)
            {
                existing.IsActive = true;
                existing.Platform = platform;
                existing.RegisteredAt = DateTime.UtcNow;
            }
            else
            {
                _db.UserDeviceTokens.Add(new UserDeviceToken
                {
                    UserId = userId,
                    ExpoPushToken = expoToken,
                    Platform = platform,
                    RegisteredAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            await _db.SaveChangesAsync();
        }

        // ✅ Optional: call on logout to disable
        public async System.Threading.Tasks.Task DeactivateDeviceAsync(string userId, string expoToken)
        {
            expoToken = expoToken.Trim();

            var existing = await _db.UserDeviceTokens
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ExpoPushToken == expoToken);

            if (existing == null) return;

            existing.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }
}
