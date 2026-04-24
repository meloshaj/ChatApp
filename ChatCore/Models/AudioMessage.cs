namespace ChatCore.Models
{
    public class AudioMessage : Message
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public byte[] AudioData { get; set; } = [];

        public override string Serialize()
        {
            return $"[Audio] {FileName}";
        }
    }
}
