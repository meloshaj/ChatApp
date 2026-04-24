namespace ChatCore.Models
{
    public class VoiceMessage : Message
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public byte[] AudioData { get; set; } = [];

        public override string Serialize()
        {
            return DurationSeconds > 0
                ? $"[Voice] {FileName} ({DurationSeconds}s)"
                : $"[Voice] {FileName}";
        }
    }
}
