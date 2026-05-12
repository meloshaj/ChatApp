using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChatClient.Wpf.Models;

public sealed class ChatMessage
{
    public string Label { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
    public bool IsSent { get; init; }
    public ConversationTargetType ConversationType { get; init; }
    public string ConversationName { get; init; } = string.Empty;
    public BitmapImage? InlineImage { get; init; }
    public string AudioPath { get; init; } = string.Empty;
    public string AudioTitle { get; init; } = string.Empty;
    public string AudioKindLabel { get; init; } = string.Empty;
    public string VideoPath { get; init; } = string.Empty;
    public string VideoTitle { get; init; } = string.Empty;
    public bool HasImage => InlineImage != null;
    public bool HasAudio => !string.IsNullOrWhiteSpace(AudioPath);
    public bool HasVideo => !string.IsNullOrWhiteSpace(VideoPath);
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public HorizontalAlignment Alignment => IsSent ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Brush MessageBackground => IsSent ? BrushFrom("#3B82F6") : BrushFrom("#ECECF0");
    public Brush TextForeground => IsSent ? Brushes.White : BrushFrom("#242424");
    public Brush TimestampForeground => IsSent ? BrushFrom("#D4FFFF") : BrushFrom("#717182");
    public CornerRadius CornerRadius => IsSent ? new CornerRadius(16, 16, 4, 16) : new CornerRadius(16, 16, 16, 4);

    public static ChatMessage CreateIncoming(string sender, string text, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = sender, MessageId = messageId, Text = text, Timestamp = FormatTimestamp(timestamp), IsSent = false, ConversationType = type, ConversationName = conversationName };

    public static ChatMessage CreateOutgoing(string text, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = "You", MessageId = messageId, Text = text, Timestamp = FormatTimestamp(timestamp), IsSent = true, ConversationType = type, ConversationName = conversationName };

    public static ChatMessage CreateIncomingImage(string sender, byte[] imageData, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = sender, MessageId = messageId, Text = string.Empty, Timestamp = FormatTimestamp(timestamp), IsSent = false, ConversationType = type, ConversationName = conversationName, InlineImage = ToBitmap(imageData) };

    public static ChatMessage CreateOutgoingImage(byte[] imageData, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = "You", MessageId = messageId, Text = string.Empty, Timestamp = FormatTimestamp(timestamp), IsSent = true, ConversationType = type, ConversationName = conversationName, InlineImage = ToBitmap(imageData) };

    public static ChatMessage CreateIncomingAudio(string sender, string fileName, string audioPath, string kindLabel, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = sender, MessageId = messageId, Text = string.Empty, Timestamp = FormatTimestamp(timestamp), IsSent = false, ConversationType = type, ConversationName = conversationName, AudioPath = audioPath, AudioTitle = fileName, AudioKindLabel = kindLabel };

    public static ChatMessage CreateOutgoingAudio(string fileName, string audioPath, string kindLabel, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = "You", MessageId = messageId, Text = string.Empty, Timestamp = FormatTimestamp(timestamp), IsSent = true, ConversationType = type, ConversationName = conversationName, AudioPath = audioPath, AudioTitle = fileName, AudioKindLabel = kindLabel };

    public static ChatMessage CreateIncomingVideo(string sender, string fileName, string videoPath, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = sender, MessageId = messageId, Text = string.Empty, Timestamp = FormatTimestamp(timestamp), IsSent = false, ConversationType = type, ConversationName = conversationName, VideoPath = videoPath, VideoTitle = fileName };

    public static ChatMessage CreateOutgoingVideo(string fileName, string videoPath, ConversationTargetType type, string conversationName, DateTime? timestamp = null, string messageId = "") =>
        new() { Label = "You", MessageId = messageId, Text = string.Empty, Timestamp = FormatTimestamp(timestamp), IsSent = true, ConversationType = type, ConversationName = conversationName, VideoPath = videoPath, VideoTitle = fileName };

    private static string FormatTimestamp(DateTime? timestamp)
    {
        DateTime local = (timestamp ?? DateTime.Now).ToLocalTime();
        return local.ToString("HH:mm");
    }

    private static Brush BrushFrom(string hex) => (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;

    private static BitmapImage? ToBitmap(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        using MemoryStream stream = new(bytes);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
