namespace ChatCore.Models
{
    public sealed class NetworkMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Text { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public byte[] ImageData { get; set; } = [];
        public byte[] AudioData { get; set; } = [];
        public byte[] VideoData { get; set; } = [];
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
