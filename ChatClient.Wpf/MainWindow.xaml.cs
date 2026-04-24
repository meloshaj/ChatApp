using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatClient.Wpf.Models;
using ChatClient.Wpf.Services;

namespace ChatClient.Wpf;

public partial class MainWindow : Window
{
    private readonly ChatClientSession session = new();
    private readonly AuthenticationClient authClient;
    private readonly string authenticatedUsername;

    public MainWindow(AuthenticationClient authClient, string username)
    {
        InitializeComponent();
        
        this.authClient = authClient;
        this.authenticatedUsername = username;
        
        session.StatusChanged += HandleStatusChanged;
        session.ConversationCollectionChanged += RefreshConversationList;
        session.ConversationSelectionChanged += () => RefreshHeader();
        MessagesItemsControl.ItemsSource = session.VisibleMessages;
        
        // Set authenticated connection
        session.SetAuthenticatedConnection(authClient.GetClient()!, authClient.GetStream()!, username);
        
        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
        SearchTextBox.Text = string.Empty;
        
        // Hide connection controls since we're already authenticated
        ConnectionPanel.Visibility = Visibility.Collapsed;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await session.ConnectAsync(HostTextBox.Text, PortTextBox.Text, UsernameTextBox.Text);
        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await session.DisconnectAsync();
        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await session.RefreshListsAsync();
        RefreshConversationList();
    }

    private void OpenAnotherClientButton_Click(object sender, RoutedEventArgs e)
    {
        string executablePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            HandleStatusChanged("Could not locate the client executable.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
            UseShellExecute = true
        });
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        session.SetSearchText(SearchTextBox.Text);
        RefreshConversationList();
    }

    private async void ContactsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContactsListBox.SelectedItem is not ConversationListItem item)
        {
            return;
        }

        if (item.TargetType == ConversationTargetType.Private)
        {
            FriendUserTextBox.Text = item.Name;
        }
        else
        {
            GroupNameTextBox.Text = item.Name;
        }

        if (session.IsConnected)
        {
            if (item.TargetType == ConversationTargetType.Private)
            {
                await session.OpenPrivateChatAsync(item.Name);
            }
            else
            {
                await session.OpenGroupChatAsync(item.Name);
            }
        }

        RefreshConversationList();
        RefreshHeader(item);
        UpdateUiState();
    }

    private async void ContactsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ContactsListBox.SelectedItem is not ConversationListItem item)
        {
            return;
        }

        if (item.TargetType == ConversationTargetType.Private)
        {
            await session.OpenPrivateChatAsync(item.Name);
            FriendUserTextBox.Text = item.Name;
        }
        else
        {
            await session.OpenGroupChatAsync(item.Name);
            GroupNameTextBox.Text = item.Name;
        }

        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
    }

    private async void AddFriendButton_Click(object sender, RoutedEventArgs e)
    {
        await session.AddFriendAsync(FriendUserTextBox.Text);
        RefreshConversationList();
    }

    private async void OpenPrivateChatButton_Click(object sender, RoutedEventArgs e)
    {
        await session.OpenPrivateChatAsync(FriendUserTextBox.Text);
        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
    }

    private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
    {
        await session.CreateGroupAsync(GroupNameTextBox.Text, GroupMemberTextBox.Text);
        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
    }

    private async void AddToGroupButton_Click(object sender, RoutedEventArgs e)
    {
        await session.AddToGroupAsync(GroupNameTextBox.Text, GroupMemberTextBox.Text);
        RefreshConversationList();
    }

    private async void OpenGroupChatButton_Click(object sender, RoutedEventArgs e)
    {
        await session.OpenGroupChatAsync(GroupNameTextBox.Text);
        RefreshConversationList();
        RefreshHeader();
        UpdateUiState();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await session.SendTextMessageAsync(MessageInputTextBox.Text);
        MessageInputTextBox.Clear();
    }

    private async void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            await session.SendTextMessageAsync(MessageInputTextBox.Text);
            MessageInputTextBox.Clear();
        }
    }

    private async void SendImageButton_Click(object sender, RoutedEventArgs e)
    {
        await session.SendPendingAttachmentAsync(VoiceDurationTextBox.Text);
    }

    private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChatClientSession.TryPickFile("Image files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*", out string filePath))
        {
            ImagePathTextBox.Text = filePath;
            session.SetPendingAttachment(ChatClientSession.AttachmentKind.Image, filePath);
            HandleStatusChanged("Image file selected.");
        }
    }

    private void SendVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChatClientSession.TryPickFile("Audio files|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac|All files|*.*", out string filePath))
        {
            VoicePathTextBox.Text = filePath;
            session.SetPendingAttachment(ChatClientSession.AttachmentKind.Voice, filePath);
            HandleStatusChanged("Voice file selected.");
        }
    }

    private async void SendVideoButton_Click(object sender, RoutedEventArgs e)
    {
        await session.SendPendingAttachmentAsync(VoiceDurationTextBox.Text);
    }

    private void BrowseVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChatClientSession.TryPickFile("Audio files|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac|All files|*.*", out string filePath))
        {
            VoicePathTextBox.Text = filePath;
            session.SetPendingAttachment(ChatClientSession.AttachmentKind.Audio, filePath);
            HandleStatusChanged("Audio file selected.");
        }
    }

    private void BrowseVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChatClientSession.TryPickFile("Video files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.webm|All files|*.*", out string filePath))
        {
            VideoPathTextBox.Text = filePath;
            session.SetPendingAttachment(ChatClientSession.AttachmentKind.Video, filePath);
            HandleStatusChanged("Video file selected.");
        }
    }

    private void OpenVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ChatClient.Wpf.Models.ChatMessage message } || string.IsNullOrWhiteSpace(message.VideoPath))
        {
            return;
        }

        OpenMediaPath(message.VideoPath, "Video");
    }

    private void OpenAudioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ChatClient.Wpf.Models.ChatMessage message } || string.IsNullOrWhiteSpace(message.AudioPath))
        {
            return;
        }

        OpenMediaPath(message.AudioPath, "Audio");
    }

    private void HandleStatusChanged(string text)
    {
        StatusTextBlock.Text = text;
        ConnectionStateTextBlock.Text = session.IsConnected ? "Connected" : "Disconnected";
        CurrentUserTextBlock.Text = session.CurrentUsername ?? "Me";
        CurrentUserInitialsTextBlock.Text = ChatClientSession.GetInitials(session.CurrentUsername ?? "Me");
        RefreshHeader();
        UpdateUiState();
    }

    private void RefreshConversationList()
    {
        ContactsListBox.ItemsSource = session.GetFilteredConversations();
    }

    private void RefreshHeader(ConversationListItem? item = null)
    {
        if (item != null)
        {
            ChatHeaderName.Text = item.DisplayName;
            ChatHeaderInitials.Text = item.Initials;
            ChatHeaderStatus.Text = item.TargetType == ConversationTargetType.Group ? "Group conversation" : item.StatusText;
            return;
        }

        ChatHeaderName.Text = session.GetHeaderName();
        ChatHeaderInitials.Text = session.GetHeaderInitials();
        ChatHeaderStatus.Text = session.GetHeaderStatus();
    }

    private void UpdateUiState()
    {
        bool canUseCommands = session.IsConnected;
        bool canSend = session.IsConnected && session.SelectedConversationType != ConversationTargetType.None;
        ConnectButton.IsEnabled = !session.IsConnected;
        DisconnectButton.IsEnabled = session.IsConnected;
        HostTextBox.IsEnabled = !session.IsConnected;
        PortTextBox.IsEnabled = !session.IsConnected;
        UsernameTextBox.IsEnabled = !session.IsConnected;
        RefreshButton.IsEnabled = canUseCommands;
        AddFriendButton.IsEnabled = canUseCommands;
        OpenPrivateChatButton.IsEnabled = canUseCommands;
        CreateGroupButton.IsEnabled = canUseCommands;
        AddToGroupButton.IsEnabled = canUseCommands;
        OpenGroupChatButton.IsEnabled = canUseCommands;
        SendButton.IsEnabled = canSend;
        SendImageButton.IsEnabled = canSend;
        SendVoiceButton.IsEnabled = canSend;
        SendVideoButton.IsEnabled = canSend;
        BrowseImageButton.IsEnabled = canSend;
        BrowseVoiceButton.IsEnabled = canSend;
        BrowseVideoButton.IsEnabled = canSend;
        MessageInputTextBox.IsEnabled = canSend;
        ImagePathTextBox.IsEnabled = canSend;
        VoicePathTextBox.IsEnabled = canSend;
        VoiceDurationTextBox.IsEnabled = canSend;
        VideoPathTextBox.IsEnabled = canSend;
        ContactsListBox.IsEnabled = canUseCommands;
    }

    protected override async void OnClosed(EventArgs e)
    {
        await session.DisconnectAsync();
        base.OnClosed(e);
    }

    private void OpenMediaPath(string path, string mediaType)
    {
        if (!File.Exists(path))
        {
            HandleStatusChanged($"{mediaType} file is no longer available.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
