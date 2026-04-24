using ChatCore.Models;

namespace ChatCore.Observers
{
    public class PresenceNotification
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public UserStatus Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
