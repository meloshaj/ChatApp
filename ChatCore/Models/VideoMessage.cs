namespace ChatCore.Models
{
    public class VideoMessage : Message
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public byte[] VideoData { get; set; } = [];

        public override string Serialize()
        {
            return $"[Video] {FileName}";
        }
    }
}
