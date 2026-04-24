using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;
using ChatClient.Wpf.Models;
using Microsoft.Win32;

namespace ChatClient.Wpf.Services;

public sealed class ChatClientSession
{
    public enum AttachmentKind
    {
        None,
        Image,
        Audio,
        Voice,
        Video
    }

    private const int MaxVideoBytes = 8 * 1024 * 1024;
    private const int MaxAudioBytes = 8 * 1024 * 1024;
    private readonly List<ChatMessage> allMessages = [];
    private readonly ObservableCollection<ChatMessage> visibleMessages = [];
    private readonly ObservableCollection<ConversationListItem> conversations = [];
    private TcpClient? client;
    private NetworkStream? stream;
    private readonly NetworkManager networkManager = new();
    private CancellationTokenSource? receiveCancellation;
    private bool isConnected;
    private string? currentUsername;
    private string searchText = string.Empty;
    private string? selectedConversationName;
    private ConversationTargetType selectedConversationType;
    private string? pendingAttachmentPath;
    private AttachmentKind pendingAttachmentKind;

    public ObservableCollection<ChatMessage> VisibleMessages => visibleMessages;
    public ObservableCollection<ConversationListItem> Conversations => conversations;
    public bool IsConnected => isConnected;
    public string? CurrentUsername => currentUsername;
    public string? SelectedConversationName => selectedConversationName;
    public ConversationTargetType SelectedConversationType => selectedConversationType;

    public event Action<string>? StatusChanged;
    public event Action? ConversationCollectionChanged;
    public event Action? ConversationSelectionChanged;

    public void SetAuthenticatedConnection(TcpClient authenticatedClient, NetworkStream authenticatedStream, string username)
    {
        client = authenticatedClient;
        stream = authenticatedStream;
        currentUsername = username;
        isConnected = true;
        receiveCancellation = new CancellationTokenSource();
        
        _ = Task.Run(() => ReceiveLoopAsync(receiveCancellation.Token));
        OnStatusChanged($"Authenticated as {username}");
        _ = RefreshListsAsync();
    }

    public async Task ConnectAsync(string host, string portText, string username)
    {
        if (isConnected)
        {
            OnStatusChanged("Already connected.");
            return;
        }

        if (!int.TryParse(portText.Trim(), out int port))
        {
            OnStatusChanged("Port must be a valid number.");
            return;
        }

        try
        {
            client = new TcpClient();
            await client.ConnectAsync(host.Trim(), port);
            stream = client.GetStream();
            receiveCancellation = new CancellationTokenSource();
            isConnected = true;
            _ = Task.Run(() => ReceiveLoopAsync(receiveCancellation.Token));
            OnStatusChanged("Connected.");
            await TrySetUsernameAsync(username);
            await RefreshListsAsync();
        }
        catch (Exception ex)
        {
            OnStatusChanged("Connection failed: " + ex.Message);
            await DisconnectAsync();
        }
    }

    public Task DisconnectAsync()
    {
        receiveCancellation?.Cancel();
        receiveCancellation?.Dispose();
        receiveCancellation = null;
        stream?.Dispose();
        client?.Close();
        stream = null;
        client = null;
        isConnected = false;
        selectedConversationName = null;
        selectedConversationType = ConversationTargetType.None;
        OnStatusChanged("Disconnected.");
        RefreshVisibleMessages();
        ConversationSelectionChanged?.Invoke();
        return Task.CompletedTask;
    }

    public void SetSearchText(string value)
    {
        searchText = value.Trim();
        ConversationCollectionChanged?.Invoke();
    }

    public IEnumerable<ConversationListItem> GetFilteredConversations()
    {
        IEnumerable<ConversationListItem> filtered = conversations
            .OrderByDescending(item => item.IsPinned)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(item => item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }

    public void PreviewConversation(ConversationListItem item)
    {
        if (item.TargetType == ConversationTargetType.Private)
        {
            // only selection preview, actual open done separately
        }

        ConversationSelectionChanged?.Invoke();
    }

    public Task RefreshListsAsync()
    {
        if (!isConnected)
        {
            ConversationCollectionChanged?.Invoke();
            return Task.CompletedTask;
        }

        return RefreshListsConnectedAsync();
    }

    private async Task RefreshListsConnectedAsync()
    {
        await SendCommandAsync("/users");
        await SendCommandAsync("/friends");
        await SendCommandAsync("/groups");
    }

    public async Task AddFriendAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            OnStatusChanged("Enter a username to add.");
            return;
        }

        await SendCommandAsync($"/friend add {username.Trim()}");
        await SendCommandAsync("/friends");
    }

    public async Task OpenPrivateChatAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            OnStatusChanged("Select a user to open a chat.");
            return;
        }

        if (IsSelectedConversation(username.Trim(), ConversationTargetType.Private))
        {
            SetSelectedConversation(username.Trim(), ConversationTargetType.Private);
            return;
        }

        if (await SendCommandAsync($"/chat {username.Trim()}"))
        {
            SetSelectedConversation(username.Trim(), ConversationTargetType.Private);
        }
    }

    public async Task CreateGroupAsync(string groupName, string initialMemberUsername)
    {
        string trimmedGroupName = groupName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedGroupName))
        {
            OnStatusChanged("Enter a group name.");
            return;
        }

        await SendCommandAsync($"/group create {trimmedGroupName}");

        string trimmedMemberUsername = initialMemberUsername.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedMemberUsername) &&
            !string.Equals(trimmedMemberUsername, currentUsername, StringComparison.OrdinalIgnoreCase))
        {
            await SendCommandAsync($"/group add {trimmedGroupName} {trimmedMemberUsername}");
        }

        await SendCommandAsync($"/group chat {trimmedGroupName}");
        await SendCommandAsync("/groups");
    }

    public async Task AddToGroupAsync(string groupName, string username)
    {
        if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(username))
        {
            OnStatusChanged("Enter a group name and member username.");
            return;
        }

        await SendCommandAsync($"/group add {groupName.Trim()} {username.Trim()}");
        await SendCommandAsync("/groups");
    }

    public async Task OpenGroupChatAsync(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            OnStatusChanged("Select a group to open.");
            return;
        }

        if (IsSelectedConversation(groupName.Trim(), ConversationTargetType.Group))
        {
            SetSelectedConversation(groupName.Trim(), ConversationTargetType.Group);
            return;
        }

        if (await SendCommandAsync($"/group chat {groupName.Trim()}"))
        {
            SetSelectedConversation(groupName.Trim(), ConversationTargetType.Group);
        }
    }

    public async Task SendTextMessageAsync(string text)
    {
        if (!EnsureConversationSelected())
        {
            return;
        }

        string trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        await SendRawAsync(trimmed);
    }

    public async Task SendImageAsync(string filePath)
    {
        if (!EnsureConversationSelected())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            OnStatusChanged("Choose an image file.");
            return;
        }

        string trimmedPath = filePath.Trim();
        if (!File.Exists(trimmedPath))
        {
            OnStatusChanged("Image file not found.");
            return;
        }

        byte[] imageBytes = File.ReadAllBytes(trimmedPath);
        bool sent = await SendRawMessageAsync(new NetworkMessage
        {
            Sender = currentUsername ?? "Me",
            Type = "image",
            Text = $"/image {Path.GetFileName(trimmedPath)}",
            FileName = Path.GetFileName(trimmedPath),
            ImageData = imageBytes,
            Timestamp = DateTime.UtcNow
        });

        if (sent)
        {
            ClearPendingIfMatches(trimmedPath);
        }
    }

    public async Task SendAudioAsync(string filePath)
    {
        if (!EnsureConversationSelected())
        {
            return;
        }

        if (!TryValidateAttachment(filePath, "Audio", MaxAudioBytes, out string trimmedPath, out FileInfo? fileInfo))
        {
            return;
        }

        OnStatusChanged($"Sending audio: {fileInfo!.Name} ({Math.Max(1, fileInfo.Length / 1024)} KB)...");

        byte[] audioBytes = await File.ReadAllBytesAsync(trimmedPath);
        bool sent = await SendRawMessageAsync(new NetworkMessage
        {
            Sender = currentUsername ?? "Me",
            Type = "audio",
            Text = $"/audio {Path.GetFileName(trimmedPath)}",
            FileName = Path.GetFileName(trimmedPath),
            AudioData = audioBytes,
            Timestamp = DateTime.UtcNow
        });

        if (sent)
        {
            OnStatusChanged("Audio sent.");
            ClearPendingIfMatches(trimmedPath);
        }
    }

    public async Task SendVideoAsync(string filePath)
    {
        if (!EnsureConversationSelected())
        {
            return;
        }

        if (!TryValidateAttachment(filePath, "Video", MaxVideoBytes, out string trimmedPath, out FileInfo? fileInfo))
        {
            return;
        }

        OnStatusChanged($"Sending video: {fileInfo!.Name} ({Math.Max(1, fileInfo.Length / 1024)} KB)...");

        byte[] videoBytes = await File.ReadAllBytesAsync(trimmedPath);
        bool sent = await SendRawMessageAsync(new NetworkMessage
        {
            Sender = currentUsername ?? "Me",
            Type = "video",
            Text = $"/video {Path.GetFileName(trimmedPath)}",
            FileName = Path.GetFileName(trimmedPath),
            VideoData = videoBytes,
            Timestamp = DateTime.UtcNow
        });

        if (sent)
        {
            OnStatusChanged("Video sent.");
            ClearPendingIfMatches(trimmedPath);
        }
    }

    public async Task SendVoiceAsync(string filePath, string durationText)
    {
        if (!EnsureConversationSelected())
        {
            return;
        }

        if (!TryValidateAttachment(filePath, "Voice", MaxAudioBytes, out string trimmedPath, out FileInfo? fileInfo))
        {
            return;
        }

        if (!int.TryParse(durationText.Trim(), out int durationSeconds))
        {
            OnStatusChanged("Voice duration must be a whole number.");
            return;
        }

        OnStatusChanged($"Sending voice: {fileInfo!.Name} ({Math.Max(1, fileInfo.Length / 1024)} KB)...");

        byte[] audioBytes = await File.ReadAllBytesAsync(trimmedPath);
        bool sent = await SendRawMessageAsync(new NetworkMessage
        {
            Sender = currentUsername ?? "Me",
            Type = "voice",
            Text = $"/voice {Path.GetFileName(trimmedPath)}",
            FileName = Path.GetFileName(trimmedPath),
            DurationSeconds = durationSeconds,
            AudioData = audioBytes,
            Timestamp = DateTime.UtcNow
        });

        if (sent)
        {
            OnStatusChanged("Voice message sent.");
            ClearPendingIfMatches(trimmedPath);
        }
    }

    public void SetPendingAttachment(AttachmentKind kind, string filePath)
    {
        pendingAttachmentKind = kind;
        pendingAttachmentPath = filePath.Trim();
    }

    public void ClearPendingAttachment()
    {
        pendingAttachmentKind = AttachmentKind.None;
        pendingAttachmentPath = null;
    }

    public async Task SendPendingAttachmentAsync(string voiceDurationText)
    {
        switch (pendingAttachmentKind)
        {
            case AttachmentKind.Image:
                await SendImageAsync(pendingAttachmentPath ?? string.Empty);
                break;
            case AttachmentKind.Audio:
                await SendAudioAsync(pendingAttachmentPath ?? string.Empty);
                break;
            case AttachmentKind.Voice:
                await SendVoiceAsync(pendingAttachmentPath ?? string.Empty, voiceDurationText);
                break;
            case AttachmentKind.Video:
                await SendVideoAsync(pendingAttachmentPath ?? string.Empty);
                break;
            default:
                OnStatusChanged("Select an image, audio, voice file, or video first.");
                return;
        }
    }

    public static bool TryPickFile(string filter, out string filePath)
    {
        OpenFileDialog dialog = new()
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        bool? result = dialog.ShowDialog();
        filePath = result == true ? dialog.FileName : string.Empty;
        return result == true;
    }

    public static string GetInitials(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "NA";
        }

        string[] parts = value.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    public string GetHeaderName() => string.IsNullOrWhiteSpace(selectedConversationName) ? "No conversation selected" : selectedConversationName;
    public string GetHeaderInitials() => string.IsNullOrWhiteSpace(selectedConversationName) ? "NA" : GetInitials(selectedConversationName);
    public string GetHeaderStatus()
    {
        if (string.IsNullOrWhiteSpace(selectedConversationName))
        {
            return isConnected ? "Choose a friend or group" : "Connect, then choose a friend or group";
        }

        ConversationListItem? selected = conversations.FirstOrDefault(item =>
            item.TargetType == selectedConversationType &&
            item.Name.Equals(selectedConversationName, StringComparison.OrdinalIgnoreCase));

        return selectedConversationType == ConversationTargetType.Group
            ? "Group conversation"
            : selected?.StatusText ?? "Direct conversation";
    }

    private bool EnsureConversationSelected()
    {
        if (selectedConversationType != ConversationTargetType.None && !string.IsNullOrWhiteSpace(selectedConversationName))
        {
            return true;
        }

        OnStatusChanged("Select a direct chat or group first.");
        return false;
    }

    private async Task TrySetUsernameAsync(string username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            await SendCommandAsync($"/name {username.Trim()}");
        }
    }

    private async Task<bool> SendCommandAsync(string command) => await SendRawAsync(command);

    private async Task<bool> SendRawAsync(string text)
    {
        if (!isConnected)
        {
            OnStatusChanged("Connect to the server first.");
            return false;
        }

        return await SendRawMessageAsync(new NetworkMessage
        {
            Sender = currentUsername ?? "Me",
            Type = "text",
            Text = text,
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task<bool> SendRawMessageAsync(NetworkMessage message)
    {
        if (!isConnected || stream == null)
        {
            OnStatusChanged("Connect to the server first.");
            return false;
        }

        try
        {
            await networkManager.SendMessage(stream, message);
            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged("Send failed: " + ex.Message);
            await DisconnectAsync();
            return false;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && stream != null)
            {
                NetworkMessage? incoming = await networkManager.ReceiveMessage(stream, cancellationToken);
                if (incoming == null)
                {
                    break;
                }

                await Application.Current.Dispatcher.InvokeAsync(() => HandleIncomingMessage(incoming));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => OnStatusChanged("Connection closed: " + ex.Message));
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (isConnected)
                {
                    await DisconnectAsync();
                }
            });
        }
    }

    private void HandleIncomingMessage(NetworkMessage incoming)
    {
        if (string.Equals(incoming.Type, "image", StringComparison.OrdinalIgnoreCase))
        {
            ChatMessage? imageMessage = TryParseImageMessage(incoming);
            if (imageMessage != null)
            {
                allMessages.Add(imageMessage);
                RefreshVisibleMessages();
                ConversationCollectionChanged?.Invoke();
                OnStatusChanged("Image received.");
            }

            return;
        }

        if (string.Equals(incoming.Type, "audio", StringComparison.OrdinalIgnoreCase))
        {
            ChatMessage? audioMessage = TryParseAudioMessage(incoming, "Audio attachment");
            if (audioMessage != null)
            {
                allMessages.Add(audioMessage);
                RefreshVisibleMessages();
                ConversationCollectionChanged?.Invoke();
                OnStatusChanged("Audio received.");
            }

            return;
        }

        if (string.Equals(incoming.Type, "video", StringComparison.OrdinalIgnoreCase))
        {
            ChatMessage? videoMessage = TryParseVideoMessage(incoming);
            if (videoMessage != null)
            {
                allMessages.Add(videoMessage);
                RefreshVisibleMessages();
                ConversationCollectionChanged?.Invoke();
                OnStatusChanged("Video received.");
            }

            return;
        }

        if (string.Equals(incoming.Type, "voice", StringComparison.OrdinalIgnoreCase))
        {
            string kindLabel = incoming.DurationSeconds > 0
                ? $"Voice message • {incoming.DurationSeconds}s"
                : "Voice message";
            ChatMessage? voiceMessage = TryParseAudioMessage(incoming, kindLabel);
            if (voiceMessage != null)
            {
                allMessages.Add(voiceMessage);
                RefreshVisibleMessages();
                ConversationCollectionChanged?.Invoke();
                OnStatusChanged("Voice message received.");
            }

            return;
        }

        string line = incoming.Text;
        if (line.StartsWith("[System] ", StringComparison.Ordinal))
        {
            ProcessSystemMessage(line[9..]);
            return;
        }

        if (line.StartsWith("[Chat|", StringComparison.Ordinal))
        {
            ChatMessage? message = TryParseConversationMessage(incoming);
            if (message != null)
            {
                allMessages.Add(message);
                ConversationListItem? item = conversations.FirstOrDefault(existing =>
                    existing.TargetType == message.ConversationType &&
                    existing.Name.Equals(message.ConversationName, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.LastMessage = message.Text;
                    item.Timestamp = message.Timestamp;
                }

                RefreshVisibleMessages();
                ConversationCollectionChanged?.Invoke();
            }
        }
    }

    private ChatMessage? TryParseImageMessage(NetworkMessage incoming)
    {
        string line = incoming.Text;
        int closingIndex = line.IndexOf("] ", StringComparison.Ordinal);
        if (!line.StartsWith("[Chat|", StringComparison.Ordinal) || closingIndex < 0)
        {
            return null;
        }

        string header = line[6..closingIndex];
        string[] parts = header.Split('|', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        ConversationTargetType conversationType = parts[0] switch
        {
            "Private" => ConversationTargetType.Private,
            "Group" => ConversationTargetType.Group,
            _ => ConversationTargetType.None
        };

        if (conversationType == ConversationTargetType.None)
        {
            return null;
        }

        string conversationName = parts[1];
        string body = line[(closingIndex + 2)..];
        int separatorIndex = body.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        string sender = body[..separatorIndex];
        bool isOutgoing = string.Equals(sender, currentUsername, StringComparison.OrdinalIgnoreCase);
        return isOutgoing
            ? ChatMessage.CreateOutgoingImage(incoming.ImageData, conversationType, conversationName, incoming.Timestamp)
            : ChatMessage.CreateIncomingImage(sender, incoming.ImageData, conversationType, conversationName, incoming.Timestamp);
    }

    private ChatMessage? TryParseVideoMessage(NetworkMessage incoming)
    {
        string line = incoming.Text;
        int closingIndex = line.IndexOf("] ", StringComparison.Ordinal);
        if (!line.StartsWith("[Chat|", StringComparison.Ordinal) || closingIndex < 0)
        {
            return null;
        }

        string header = line[6..closingIndex];
        string[] parts = header.Split('|', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        ConversationTargetType conversationType = parts[0] switch
        {
            "Private" => ConversationTargetType.Private,
            "Group" => ConversationTargetType.Group,
            _ => ConversationTargetType.None
        };

        if (conversationType == ConversationTargetType.None)
        {
            return null;
        }

        string conversationName = parts[1];
        string body = line[(closingIndex + 2)..];
        int separatorIndex = body.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        string sender = body[..separatorIndex];
        bool isOutgoing = string.Equals(sender, currentUsername, StringComparison.OrdinalIgnoreCase);
        string fileName = string.IsNullOrWhiteSpace(incoming.FileName) ? "video.bin" : incoming.FileName;
        string videoPath = SaveIncomingVideo(fileName, incoming.VideoData);

        return isOutgoing
            ? ChatMessage.CreateOutgoingVideo(fileName, videoPath, conversationType, conversationName, incoming.Timestamp)
            : ChatMessage.CreateIncomingVideo(sender, fileName, videoPath, conversationType, conversationName, incoming.Timestamp);
    }

    private ChatMessage? TryParseAudioMessage(NetworkMessage incoming, string kindLabel)
    {
        string line = incoming.Text;
        int closingIndex = line.IndexOf("] ", StringComparison.Ordinal);
        if (!line.StartsWith("[Chat|", StringComparison.Ordinal) || closingIndex < 0)
        {
            return null;
        }

        string header = line[6..closingIndex];
        string[] parts = header.Split('|', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        ConversationTargetType conversationType = parts[0] switch
        {
            "Private" => ConversationTargetType.Private,
            "Group" => ConversationTargetType.Group,
            _ => ConversationTargetType.None
        };

        if (conversationType == ConversationTargetType.None)
        {
            return null;
        }

        string conversationName = parts[1];
        string body = line[(closingIndex + 2)..];
        int separatorIndex = body.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        string sender = body[..separatorIndex];
        bool isOutgoing = string.Equals(sender, currentUsername, StringComparison.OrdinalIgnoreCase);
        string fileName = string.IsNullOrWhiteSpace(incoming.FileName) ? "audio.bin" : incoming.FileName;
        string audioPath = SaveIncomingMedia("Audio", fileName, incoming.AudioData);

        return isOutgoing
            ? ChatMessage.CreateOutgoingAudio(fileName, audioPath, kindLabel, conversationType, conversationName, incoming.Timestamp)
            : ChatMessage.CreateIncomingAudio(sender, fileName, audioPath, kindLabel, conversationType, conversationName, incoming.Timestamp);
    }

    private void ResetConversationHistory(string message)
    {
        string[] parts = message.Split('|', 3);
        if (parts.Length != 3)
        {
            return;
        }

        ConversationTargetType conversationType = parts[1] switch
        {
            "Private" => ConversationTargetType.Private,
            "Group" => ConversationTargetType.Group,
            _ => ConversationTargetType.None
        };

        if (conversationType == ConversationTargetType.None)
        {
            return;
        }

        string conversationName = parts[2];
        allMessages.RemoveAll(existing =>
            existing.ConversationType == conversationType &&
            existing.ConversationName.Equals(conversationName, StringComparison.OrdinalIgnoreCase));
        RefreshVisibleMessages();
        ConversationCollectionChanged?.Invoke();
    }

    private void ProcessSystemMessage(string message)
    {
        if (message.StartsWith("Welcome ", StringComparison.OrdinalIgnoreCase) && message.EndsWith('.'))
        {
            currentUsername = message[8..^1];
            OnStatusChanged(message);
            ConversationSelectionChanged?.Invoke();
            return;
        }

        if (message.StartsWith("Username set: ", StringComparison.OrdinalIgnoreCase) && message.EndsWith('.'))
        {
            currentUsername = message[14..^1];
            OnStatusChanged(message);
            ConversationSelectionChanged?.Invoke();
            return;
        }

        if (message.StartsWith("Connected users: ", StringComparison.OrdinalIgnoreCase))
        {
            MergeConversationTargets(ParseList(message[17..]), ConversationTargetType.Private, false);
            OnStatusChanged("Users refreshed.");
            return;
        }

        if (message.StartsWith("Friends: ", StringComparison.OrdinalIgnoreCase))
        {
            MergeConversationTargets(ParseList(message[9..]), ConversationTargetType.Private, true);
            OnStatusChanged("Friends refreshed.");
            return;
        }

        if (message.StartsWith("Groups: ", StringComparison.OrdinalIgnoreCase))
        {
            MergeConversationTargets(ParseList(message[8..]), ConversationTargetType.Group, true);
            OnStatusChanged("Groups refreshed.");
            return;
        }

        if (message.StartsWith("Chat selected: ", StringComparison.OrdinalIgnoreCase) && message.EndsWith('.'))
        {
            SetSelectedConversation(message[15..^1], ConversationTargetType.Private);
            OnStatusChanged(message);
            return;
        }

        if (message.StartsWith("Group created: ", StringComparison.OrdinalIgnoreCase) && message.EndsWith('.'))
        {
            string groupName = message[15..^1];
            EnsureConversation(groupName, ConversationTargetType.Group, true);
            SetSelectedConversation(groupName, ConversationTargetType.Group);
            OnStatusChanged(message);
            return;
        }

        if (message.StartsWith("Group chat selected: ", StringComparison.OrdinalIgnoreCase) && message.EndsWith('.'))
        {
            string groupName = message[21..^1];
            EnsureConversation(groupName, ConversationTargetType.Group, true);
            SetSelectedConversation(groupName, ConversationTargetType.Group);
            OnStatusChanged(message);
            return;
        }

        if (message.StartsWith("HISTORY_RESET|", StringComparison.Ordinal))
        {
            ResetConversationHistory(message);
            return;
        }

        if (message.StartsWith("You were added to group ", StringComparison.OrdinalIgnoreCase) && message.EndsWith('.'))
        {
            EnsureConversation(message[23..^1], ConversationTargetType.Group, true);
            OnStatusChanged(message);
            return;
        }

        OnStatusChanged(message);
    }

    private void MergeConversationTargets(IEnumerable<string> names, ConversationTargetType type, bool pin)
    {
        foreach (string rawName in names)
        {
            ParsedConversationTarget target = ParseConversationTarget(rawName, type);
            string name = target.Name;
            if (string.Equals(name, currentUsername, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EnsureConversation(name, type, pin, target.StatusText, target.StatusBrush);
        }

        ConversationCollectionChanged?.Invoke();
    }

    private void EnsureConversation(string name, ConversationTargetType type, bool pin, string? statusText = null, SolidColorBrush? statusBrush = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string resolvedStatusText = statusText ?? GetDefaultStatusText(type, pin);
        SolidColorBrush resolvedStatusBrush = statusBrush ?? GetDefaultStatusBrush(type, pin);

        ConversationListItem? existing = conversations.FirstOrDefault(item => item.TargetType == type && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.IsPinned = existing.IsPinned || pin;
            existing.StatusText = resolvedStatusText;
            existing.StatusBrush = resolvedStatusBrush;
            return;
        }

        conversations.Add(new ConversationListItem
        {
            Name = name,
            DisplayName = name,
            Initials = GetInitials(name),
            LastMessage = type == ConversationTargetType.Group ? "Group ready" : "Double-click to open chat",
            Timestamp = string.Empty,
            TypeLabel = type == ConversationTargetType.Group ? "Group" : (pin ? "Friend" : "User"),
            TargetType = type,
            IsPinned = pin,
            StatusText = resolvedStatusText,
            StatusBrush = resolvedStatusBrush
        });
    }

    private void SetSelectedConversation(string name, ConversationTargetType type)
    {
        selectedConversationName = name;
        selectedConversationType = type;
        EnsureConversation(name, type, true);
        RefreshVisibleMessages();
        ConversationSelectionChanged?.Invoke();
        ConversationCollectionChanged?.Invoke();
    }

    private bool IsSelectedConversation(string name, ConversationTargetType type)
    {
        return selectedConversationType == type &&
               !string.IsNullOrWhiteSpace(selectedConversationName) &&
               selectedConversationName.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshVisibleMessages()
    {
        visibleMessages.Clear();
        foreach (ChatMessage message in allMessages.Where(message =>
                     message.ConversationType == selectedConversationType &&
                     string.Equals(message.ConversationName, selectedConversationName, StringComparison.OrdinalIgnoreCase)))
        {
            visibleMessages.Add(message);
        }
    }

    private ChatMessage? TryParseConversationMessage(NetworkMessage incoming)
    {
        string line = incoming.Text;
        int closingIndex = line.IndexOf("] ", StringComparison.Ordinal);
        if (closingIndex < 0)
        {
            return null;
        }

        string header = line[6..closingIndex];
        string[] parts = header.Split('|', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        ConversationTargetType conversationType = parts[0] switch
        {
            "Private" => ConversationTargetType.Private,
            "Group" => ConversationTargetType.Group,
            _ => ConversationTargetType.None
        };

        if (conversationType == ConversationTargetType.None)
        {
            return null;
        }

        string conversationName = parts[1];
        string body = line[(closingIndex + 2)..];
        int separatorIndex = body.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            string sender = body[..separatorIndex];
            string text = body[(separatorIndex + 2)..];
            return string.Equals(sender, currentUsername, StringComparison.OrdinalIgnoreCase)
                ? ChatMessage.CreateOutgoing(text, conversationType, conversationName, incoming.Timestamp)
                : ChatMessage.CreateIncoming(sender, text, conversationType, conversationName, incoming.Timestamp);
        }

        return ChatMessage.CreateIncoming(
            conversationType == ConversationTargetType.Group ? conversationName : "Attachment",
            body,
            conversationType,
            conversationName,
            incoming.Timestamp);
    }

    private static IEnumerable<string> ParseList(string input)
    {
        if (string.Equals(input.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Empty<string>();
        }

        return input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }

    private static string GetDefaultStatusText(ConversationTargetType type, bool pin)
    {
        return type == ConversationTargetType.Group ? "Group" : (pin ? "Friend" : "Online");
    }

    private static SolidColorBrush GetDefaultStatusBrush(ConversationTargetType type, bool pin)
    {
        return type == ConversationTargetType.Group
            ? BrushFrom("#F59E0B")
            : BrushFrom(pin ? "#3B82F6" : "#10B981");
    }

    private static ParsedConversationTarget ParseConversationTarget(string value, ConversationTargetType type)
    {
        string trimmed = value.Trim();
        if (type != ConversationTargetType.Private)
        {
            return new ParsedConversationTarget(trimmed, GetDefaultStatusText(type, true), GetDefaultStatusBrush(type, true));
        }

        int statusStart = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
        if (statusStart > 0 && trimmed.EndsWith(')'))
        {
            string name = trimmed[..statusStart].Trim();
            string rawStatus = trimmed[(statusStart + 2)..^1].Trim();
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(rawStatus))
            {
                return new ParsedConversationTarget(name, rawStatus, GetUserStatusBrush(rawStatus));
            }
        }

        return new ParsedConversationTarget(trimmed, GetDefaultStatusText(type, false), GetDefaultStatusBrush(type, false));
    }

    private static SolidColorBrush GetUserStatusBrush(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "online" => BrushFrom("#10B981"),
            "away" => BrushFrom("#F59E0B"),
            "busy" => BrushFrom("#EF4444"),
            "offline" => BrushFrom("#94A3B8"),
            _ => BrushFrom("#10B981")
        };
    }

    private readonly record struct ParsedConversationTarget(string Name, string StatusText, SolidColorBrush StatusBrush);

    private static string SaveIncomingVideo(string fileName, byte[] data)
    {
        return SaveIncomingMedia("Videos", fileName, data);
    }

    private static string SaveIncomingMedia(string folderName, string fileName, byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        string safeFileName = string.IsNullOrWhiteSpace(fileName) ? "video.bin" : Path.GetFileName(fileName);
        string targetDirectory = Path.Combine(Path.GetTempPath(), "ChatApp", folderName);
        Directory.CreateDirectory(targetDirectory);

        string baseName = Path.GetFileNameWithoutExtension(safeFileName);
        string extension = Path.GetExtension(safeFileName);
        string outputPath = Path.Combine(targetDirectory, $"{baseName}_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(outputPath, data);
        return outputPath;
    }

    private void ClearPendingIfMatches(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(pendingAttachmentPath) &&
            string.Equals(pendingAttachmentPath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            ClearPendingAttachment();
        }
    }

    private bool TryValidateAttachment(string filePath, string label, int maxBytes, out string trimmedPath, out FileInfo? fileInfo)
    {
        trimmedPath = filePath.Trim();
        fileInfo = null;

        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            OnStatusChanged($"Choose a {label.ToLowerInvariant()} file.");
            return false;
        }

        if (!File.Exists(trimmedPath))
        {
            OnStatusChanged($"{label} file not found.");
            return false;
        }

        fileInfo = new FileInfo(trimmedPath);
        if (fileInfo.Length > maxBytes)
        {
            OnStatusChanged($"{label} is too large. Limit: {maxBytes / (1024 * 1024)} MB.");
            return false;
        }

        return true;
    }

    private void OnStatusChanged(string text)
    {
        StatusChanged?.Invoke(text);
    }
}
