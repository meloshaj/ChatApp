using System.Net;
using System.Net.Sockets;
using ChatCore.Models;
using ChatCore.Services;
using ChatCore.Repositories;
using ChatCore.Factories;
using ChatCore.Decorators;
using ChatCore.States;
using ChatCore.Observers;
using ChatCore.Persistence;

class Program
{
    private static SqliteUserRepository userRepository = null!;
    private static SqliteConversationRepository conversationRepository = null!;
    private static SqliteMessageRepository messageRepository = null!;
    private static AuthenticationService authService = null!;
    private static ChatService chatService = null!;
    private static NotificationService notificationService = null!;
    private static MessageFactory messageFactory = null!;
    private static NetworkManager networkManager = null!;
    private static FriendService friendService = null!;
    private static ConversationService conversationService = null!;
    private static CommandService commandService = null!;

    static void Main()
    {
        InitializeServicesAsync().GetAwaiter().GetResult();

        TcpListener server = new TcpListener(IPAddress.Loopback, 13000);
        server.Start();

        Console.WriteLine("Enhanced Server started on port 13000...");
        Console.WriteLine("Features: Authentication, Message History, Typing Indicators, User Presence");
        Console.WriteLine();

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Task.Run(() => HandleClientAsync(client));
        }
    }

    static async Task InitializeServicesAsync()
    {
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        string databasePath = Path.Combine(dataDirectory, "chatapp.db");
        SqliteDatabase database = new(databasePath);
        await database.InitializeAsync();

        // Initialize repositories
        userRepository = new SqliteUserRepository(database);
        conversationRepository = new SqliteConversationRepository(database);
        messageRepository = new SqliteMessageRepository(database);
        await userRepository.ResetPresenceAsync();

        // Initialize core services
        networkManager = new NetworkManager();
        messageFactory = new MessageFactory();
        notificationService = new NotificationService();
        authService = new AuthenticationService(userRepository);
        friendService = new FriendService();
        conversationService = new ConversationService(friendService, conversationRepository);

        // Initialize enhanced chat service
        chatService = new ChatService(
            messageRepository,
            conversationRepository,
            userRepository,
            messageFactory,
            notificationService,
            networkManager);

        // Build message processing pipeline with decorators
        var badWordFilter = new BadWordFilter();
        var validationProcessor = new ValidationMessageProcessor(badWordFilter);
        var loggingProcessor = new LoggingMessageProcessor(validationProcessor);
        var spamFilter = new SpamFilterProcessor(loggingProcessor, maxMessagesPerMinute: 20);

        chatService.AddProcessor(spamFilter);

        // Initialize command service
        commandService = new CommandService(
            chatService,
            friendService,
            conversationService,
            authService,
            userRepository,
            conversationRepository,
            messageRepository,
            messageFactory);
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        User? user = null;
        NetworkStream stream = client.GetStream();

        try
        {
            // Authentication phase
            user = await AuthenticateClientAsync(stream);
            if (user == null)
            {
                client.Close();
                return;
            }

            user.Client = client;
            user.SetState(new OnlineState());
            await userRepository.UpdateAsync(user);

            Console.WriteLine($"{user.Username} connected and authenticated.");

            // Send welcome and load message history
            await commandService.SendWelcomeAsync(user);
            await commandService.LoadUserConversationsAsync(user);

            // Notify others about presence
            await NotifyPresenceChangeAsync(user, UserStatus.Online);

            // Message loop
            while (true)
            {
                NetworkMessage? incoming = await networkManager.ReceiveMessage(stream);
                if (incoming == null)
                {
                    break;
                }

                await HandleIncomingMessageAsync(user, incoming);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            if (user != null)
            {
                user.SetState(new OfflineState());
                user.Client = null;
                await userRepository.UpdateAsync(user);
                await NotifyPresenceChangeAsync(user, UserStatus.Offline);
                Console.WriteLine($"{user.Username} disconnected.");
            }
            client.Close();
        }
    }

    static async Task<User?> AuthenticateClientAsync(NetworkStream stream)
    {
        // Send authentication prompt
        var authPrompt = new NetworkMessage
        {
            Sender = "System",
            Type = "auth",
            Text = "[System] AUTH_REQUIRED|Enter: LOGIN username password OR REGISTER username password",
            Timestamp = DateTime.UtcNow
        };
        await networkManager.SendMessage(stream, authPrompt);

        // Wait for auth response
        var response = await networkManager.ReceiveMessage(stream);
        if (response == null)
        {
            return null;
        }

        var parts = response.Text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        string command = parts[0].ToUpperInvariant();
        string username = parts[1];
        string password = parts[2];

        if (command == "REGISTER")
        {
            var result = await authService.RegisterAsync(username, password);
            if (result.Success && result.UserId != null)
            {
                var user = await userRepository.GetByIdAsync(result.UserId);
                var successMsg = new NetworkMessage
                {
                    Sender = "System",
                    Type = "auth",
                    Text = $"[System] AUTH_SUCCESS|{result.SessionToken}",
                    Timestamp = DateTime.UtcNow
                };
                await networkManager.SendMessage(stream, successMsg);
                return user;
            }
            else
            {
                var errorMsg = new NetworkMessage
                {
                    Sender = "System",
                    Type = "auth",
                    Text = $"[System] AUTH_FAILED|{result.Error}",
                    Timestamp = DateTime.UtcNow
                };
                await networkManager.SendMessage(stream, errorMsg);
                return null;
            }
        }
        else if (command == "LOGIN")
        {
            var result = await authService.LoginAsync(username, password);
            if (result.Success && result.UserId != null)
            {
                var user = await userRepository.GetByIdAsync(result.UserId);
                var successMsg = new NetworkMessage
                {
                    Sender = "System",
                    Type = "auth",
                    Text = $"[System] AUTH_SUCCESS|{result.SessionToken}",
                    Timestamp = DateTime.UtcNow
                };
                await networkManager.SendMessage(stream, successMsg);
                return user;
            }
            else
            {
                var errorMsg = new NetworkMessage
                {
                    Sender = "System",
                    Type = "auth",
                    Text = $"[System] AUTH_FAILED|{result.Error}",
                    Timestamp = DateTime.UtcNow
                };
                await networkManager.SendMessage(stream, errorMsg);
                return null;
            }
        }

        return null;
    }

    static async Task HandleIncomingMessageAsync(User user, NetworkMessage incoming)
    {
        if (string.Equals(incoming.Type, "typing", StringComparison.OrdinalIgnoreCase))
        {
            var parts = incoming.Text.Split('|');
            if (parts.Length >= 2)
            {
                string conversationId = parts[0];
                bool isTyping = parts.Length > 1 && bool.Parse(parts[1]);
                await chatService.NotifyTypingAsync(user, conversationId, isTyping);
            }
            return;
        }

        if (string.Equals(incoming.Type, "image", StringComparison.OrdinalIgnoreCase))
        {
            await commandService.HandleIncomingImageAsync(user, incoming.ImageData);
            return;
        }

        if (string.Equals(incoming.Type, "audio", StringComparison.OrdinalIgnoreCase))
        {
            await commandService.HandleIncomingAudioAsync(user, incoming.FileName, incoming.AudioData);
            return;
        }

        if (string.Equals(incoming.Type, "video", StringComparison.OrdinalIgnoreCase))
        {
            await commandService.HandleIncomingVideoAsync(user, incoming.FileName, incoming.VideoData);
            return;
        }

        if (string.Equals(incoming.Type, "voice", StringComparison.OrdinalIgnoreCase))
        {
            await commandService.HandleIncomingVoiceAsync(user, incoming.FileName, incoming.DurationSeconds, incoming.AudioData);
            return;
        }

        if (string.Equals(incoming.Type, "status", StringComparison.OrdinalIgnoreCase))
        {
            await HandleStatusChangeAsync(user, incoming.Text);
            return;
        }

        if (!string.IsNullOrWhiteSpace(incoming.Text))
        {
            await commandService.HandleInputAsync(user, incoming.Text);
        }
    }

    static async Task HandleStatusChangeAsync(User user, string statusText)
    {
        var status = statusText.ToUpperInvariant() switch
        {
            "ONLINE" => UserStatus.Online,
            "AWAY" => UserStatus.Away,
            "BUSY" => UserStatus.Busy,
            _ => UserStatus.Online
        };

        IUserState newState = status switch
        {
            UserStatus.Online => new OnlineState(),
            UserStatus.Away => new AwayState(),
            UserStatus.Busy => new BusyState(),
            _ => new OnlineState()
        };

        user.SetState(newState);
        await userRepository.UpdateAsync(user);
        await NotifyPresenceChangeAsync(user, status);
    }

    static async Task NotifyPresenceChangeAsync(User user, UserStatus status)
    {
        var notification = new PresenceNotification
        {
            UserId = user.Id,
            Username = user.Username,
            Status = status
        };

        await notificationService.NotifyAsync(notification);

        // Notify all online users
        var onlineUsers = await userRepository.GetOnlineUsersAsync();
        foreach (var onlineUser in onlineUsers)
        {
            if (onlineUser.Id == user.Id) continue;

            try
            {
                var stream = onlineUser.GetStream();
                if (stream != null)
                {
                    var presenceMsg = new NetworkMessage
                    {
                        Sender = "System",
                        Type = "presence",
                        Text = $"{user.Username}|{status}",
                        Timestamp = DateTime.UtcNow
                    };
                    await networkManager.SendMessage(stream, presenceMsg);
                }
            }
            catch
            {
                // Ignore notification failures
            }
        }
    }
}
