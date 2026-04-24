namespace ChatCore.Observers
{
    public class TypingNotification
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public bool IsTyping { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
