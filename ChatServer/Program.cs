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
using ChatCore.Security;

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
    private static RsaKeyPair serverIdentityKeys = null!;
    private static readonly AuthenticationRateLimiter authRateLimiter = new();

    static void Main(string[] args)
    {
        InitializeServicesAsync().GetAwaiter().GetResult();

        int port = ParsePort(args);
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();

        Console.WriteLine($"Enhanced Server started on port {port}.");
        Console.WriteLine("Clients on other devices can connect using one of these server addresses:");
        foreach (string address in GetLocalIPv4Addresses())
        {
            Console.WriteLine($"  {address}:{port}");
        }
        Console.WriteLine("Features: Authentication, Message History, Typing Indicators, User Presence");
        Console.WriteLine();

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Task.Run(() => HandleClientAsync(client));
        }
    }

    static int ParsePort(string[] args)
    {
        if (args.Length == 0)
        {
            return 13000;
        }

        if (int.TryParse(args[0], out int port) && port > 0 && port <= 65535)
        {
            return port;
        }

        Console.WriteLine($"Invalid port '{args[0]}'. Using default port 13000.");
        return 13000;
    }

    static IEnumerable<string> GetLocalIPv4Addresses()
    {
        string hostName = Dns.GetHostName();
        return Dns.GetHostAddresses(hostName)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())
            .DefaultIfEmpty("127.0.0.1");
    }

    static async Task InitializeServicesAsync()
    {
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        string databasePath = Path.Combine(dataDirectory, "chatapp.db");
        SqliteDatabase database = new(databasePath);
        await database.InitializeAsync();
        serverIdentityKeys = new ServerIdentityStore(dataDirectory).LoadOrCreate();

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
            user = await AuthenticateClientAsync(client, stream);
            if (user == null)
            {
                client.Close();
                return;
            }

            user.SecureSession = await EstablishSecureSessionAsync(stream);
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

                incoming = UnprotectIncoming(user, incoming);
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

    static async Task<User?> AuthenticateClientAsync(TcpClient client, NetworkStream stream)
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
        IPEndPoint remoteEndpoint = client.Client.RemoteEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.None, 0);

        if (authRateLimiter.IsBlocked(remoteEndpoint, username, out TimeSpan retryAfter))
        {
            await SendAuthFailedAsync(stream, $"Too many failed attempts. Try again in {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))} seconds.");
            return null;
        }

        if (command == "REGISTER")
        {
            var result = await authService.RegisterAsync(username, password);
            if (result.Success && result.UserId != null)
            {
                authRateLimiter.RecordSuccess(remoteEndpoint, username);
                var user = await userRepository.GetByIdAsync(result.UserId);
                await SendAuthSuccessAsync(stream);
                return user;
            }
            else
            {
                authRateLimiter.RecordFailure(remoteEndpoint, username);
                await SendAuthFailedAsync(stream, result.Error ?? "Authentication failed.");
                return null;
            }
        }
        else if (command == "LOGIN")
        {
            var result = await authService.LoginAsync(username, password);
            if (result.Success && result.UserId != null)
            {
                authRateLimiter.RecordSuccess(remoteEndpoint, username);
                var user = await userRepository.GetByIdAsync(result.UserId);
                await SendAuthSuccessAsync(stream);
                return user;
            }
            else
            {
                authRateLimiter.RecordFailure(remoteEndpoint, username);
                await SendAuthFailedAsync(stream, result.Error ?? "Authentication failed.");
                return null;
            }
        }

        return null;
    }

    static async Task SendAuthSuccessAsync(NetworkStream stream)
    {
        await networkManager.SendMessage(stream, new NetworkMessage
        {
            Sender = "System",
            Type = "auth",
            Text = "[System] AUTH_SUCCESS",
            Timestamp = DateTime.UtcNow
        });
    }

    static async Task SendAuthFailedAsync(NetworkStream stream, string error)
    {
        await networkManager.SendMessage(stream, new NetworkMessage
        {
            Sender = "System",
            Type = "auth",
            Text = $"[System] AUTH_FAILED|{error}",
            Timestamp = DateTime.UtcNow
        });
    }

    static async Task<SecureTransportSession> EstablishSecureSessionAsync(NetworkStream stream)
    {
        SecureTransportSession secureSession = new(serverIdentityKeys);
        CryptoHandshake serverHandshake = secureSession.CreateHandshake();

        await networkManager.SendMessage(stream, new NetworkMessage
        {
            Sender = "System",
            Type = "crypto",
            Text = "[System] CRYPTO_REQUIRED|RSA_ECDH_SHA256_AES",
            RsaPublicKey = serverHandshake.RsaPublicKey,
            EcdhPublicKey = serverHandshake.EcdhPublicKey,
            Signature = serverHandshake.Signature,
            Timestamp = DateTime.UtcNow
        });

        NetworkMessage? response = await networkManager.ReceiveMessage(stream);
        if (response == null || !string.Equals(response.Type, "crypto", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Client did not complete secure handshake.");
        }

        CryptoHandshake clientHandshake = new(response.RsaPublicKey, response.EcdhPublicKey, response.Signature);
        if (!secureSession.CompleteHandshake(clientHandshake))
        {
            throw new InvalidOperationException("Client secure handshake signature was invalid.");
        }

        return secureSession;
    }

    static NetworkMessage UnprotectIncoming(User user, NetworkMessage incoming)
    {
        if (!string.Equals(incoming.Type, "secure", StringComparison.OrdinalIgnoreCase))
        {
            return incoming;
        }

        if (user.SecureSession?.HasSharedKey != true)
        {
            throw new InvalidOperationException("Received a secure message before the secure session was ready.");
        }

        string json = user.SecureSession.Unprotect(new EncryptedMessage(incoming.CipherText, incoming.Iv));
        return System.Text.Json.JsonSerializer.Deserialize<NetworkMessage>(json)
            ?? throw new InvalidOperationException("Failed to deserialize secure client message.");
    }

    static async Task HandleIncomingMessageAsync(User user, NetworkMessage incoming)
    {
        if (string.Equals(incoming.Type, "typing", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(user.ActiveConversationId) &&
                bool.TryParse(incoming.Text, out bool isTyping))
            {
                await chatService.NotifyTypingAsync(user, user.ActiveConversationId, isTyping);
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
                    if (onlineUser.SecureSession?.HasSharedKey == true)
                    {
                        EncryptedMessage encrypted = onlineUser.SecureSession.Protect(System.Text.Json.JsonSerializer.Serialize(presenceMsg));
                        presenceMsg = new NetworkMessage
                        {
                            Sender = presenceMsg.Sender,
                            Type = "secure",
                            CipherText = encrypted.CipherText,
                            Iv = encrypted.Iv,
                            Timestamp = DateTime.UtcNow
                        };
                    }

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
