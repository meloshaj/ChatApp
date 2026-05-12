namespace ChatClient.Wpf.Models;

public sealed class NetworkMessage
{
    public string Sender { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RsaPublicKey { get; set; } = string.Empty;
    public string EcdhPublicKey { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public byte[] ImageData { get; set; } = [];
    public byte[] AudioData { get; set; } = [];
    public byte[] VideoData { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
