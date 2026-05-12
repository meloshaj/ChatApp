using System.ComponentModel;
using System.Windows.Media;

namespace ChatClient.Wpf.Models;

public sealed class ConversationListItem : INotifyPropertyChanged
{
    private string lastMessage = string.Empty;
    private string timestamp = string.Empty;
    private Brush statusBrush = Brushes.Transparent;
    private string statusText = string.Empty;
    private bool isPinned;
    private int unreadCount;

    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public ConversationTargetType TargetType { get; set; }

    public string LastMessage
    {
        get => lastMessage;
        set { lastMessage = value; OnPropertyChanged(nameof(LastMessage)); }
    }

    public string Timestamp
    {
        get => timestamp;
        set { timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
    }

    public Brush StatusBrush
    {
        get => statusBrush;
        set { statusBrush = value; OnPropertyChanged(nameof(StatusBrush)); }
    }

    public string StatusText
    {
        get => statusText;
        set { statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public bool IsPinned
    {
        get => isPinned;
        set { isPinned = value; OnPropertyChanged(nameof(IsPinned)); }
    }

    public int UnreadCount
    {
        get => unreadCount;
        set
        {
            unreadCount = value;
            OnPropertyChanged(nameof(UnreadCount));
            OnPropertyChanged(nameof(HasUnread));
            OnPropertyChanged(nameof(UnreadLabel));
        }
    }

    public bool HasUnread => unreadCount > 0;
    public string UnreadLabel => unreadCount > 99 ? "99+" : unreadCount.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
