namespace ChatCore.Models
{
    public class ImageMessage : Message
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public byte[] ImageData { get; set; } = [];

        public override string Serialize()
        {
            return $"[Image] {FileName} ({FilePath})";
        }
    }
}
