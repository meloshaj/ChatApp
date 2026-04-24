namespace ChatCore.Models
{
    public abstract class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
        public HashSet<string> ReadBy { get; } = new();

        public abstract string Serialize();
        
        public virtual Message Clone()
        {
            return (Message)MemberwiseClone();
        }
    }
}
