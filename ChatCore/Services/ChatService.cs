using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Observers;
using ChatCore.Factories;
using ChatCore.Commands;
using ChatCore.Security;

namespace ChatCore.Services
{
    public class ChatService : IChatService
    {
        private readonly IMessageRepository messageRepository;
        private readonly IConversationRepository conversationRepository;
        private readonly IUserRepository userRepository;
        private readonly IMessageFactory messageFactory;
        private readonly NotificationService notificationService;
        private readonly NetworkManager networkManager;
        private readonly List<IMessageProcessor> processors = new();
        private readonly Dictionary<string, CommandHistory> userCommandHistories = new();

        public ChatService(
            IMessageRepository messageRepository,
            IConversationRepository conversationRepository,
            IUserRepository userRepository,
            IMessageFactory messageFactory,
            NotificationService notificationService,
            NetworkManager networkManager)
        {
            this.messageRepository = messageRepository;
            this.conversationRepository = conversationRepository;
            this.userRepository = userRepository;
            this.messageFactory = messageFactory;
            this.notificationService = notificationService;
            this.networkManager = networkManager;
        }

        public void AddProcessor(IMessageProcessor processor)
        {
            processors.Add(processor);
        }

        public async Task SendMessageAsync(User sender, Message message)
        {
            // Process message through pipeline
            foreach (var processor in processors)
            {
                message = processor.Process(message);
            }

            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId);
            if (conversation == null)
            {
                return;
            }

            if (!conversation.HasParticipant(sender.Id))
            {
                return;
            }

            // Save to repository
            await messageRepository.AddAsync(message);

            conversation.AddMessage(message);
            await conversationRepository.UpdateAsync(conversation);

            // Send to all participants
            foreach (var participantId in conversation.ParticipantIds)
            {
                var user = await userRepository.GetByIdAsync(participantId);
                if (user != null && user.IsOnline())
                {
                    await SendToUserAsync(user, message);
                    
                    // Handle auto-reply if user is busy/away
                    if (user.Id != sender.Id && user.GetState().ShouldSendAutoReply())
                    {
                        var autoReply = messageFactory.CreateSystemMessage(
                            message.ConversationId,
                            user.GetState().GetAutoReplyMessage() ?? "User is unavailable");
                        await SendToUserAsync(sender, autoReply);
                    }
                }
            }
        }

        public async Task SendDirectMessageAsync(User recipient, Message message)
        {
            await SendToUserAsync(recipient, message);
        }

        public async Task<IEnumerable<Message>> LoadConversationHistoryAsync(string conversationId, int limit = 100)
        {
            return await messageRepository.GetConversationMessagesAsync(conversationId, limit);
        }

        public async Task<int> ReplayConversationHistoryAsync(User recipient, string conversationId, int limit = 100)
        {
            var messages = await messageRepository.GetConversationMessagesAsync(conversationId, limit);
            int replayedCount = 0;

            foreach (var message in messages)
            {
                if (message.IsDeleted)
                {
                    continue;
                }

                await SendToUserAsync(recipient, message);
                replayedCount++;
            }

            return replayedCount;
        }

        public async Task<bool> EditMessageAsync(string userId, string messageId, string newText)
        {
            var message = await messageRepository.GetByIdAsync(messageId);
            if (message == null || message.SenderId != userId || message is not TextMessage)
            {
                return false;
            }

            var commandHistory = GetCommandHistory(userId);
            var editCommand = new EditMessageCommand(messageRepository, messageId, newText);
            await commandHistory.ExecuteAsync(editCommand);

            // Notify participants about the edit
            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId);
            if (conversation != null)
            {
                foreach (var participantId in conversation.ParticipantIds)
                {
                    var user = await userRepository.GetByIdAsync(participantId);
                    if (user != null && user.IsOnline())
                    {
                        var updatedMessage = await messageRepository.GetByIdAsync(messageId);
                        if (updatedMessage != null)
                        {
                            await SendToUserAsync(user, updatedMessage);
                        }
                    }
                }
            }

            return true;
        }

        public async Task<bool> DeleteMessageAsync(string userId, string messageId)
        {
            var message = await messageRepository.GetByIdAsync(messageId);
            if (message == null || message.SenderId != userId)
            {
                return false;
            }

            var commandHistory = GetCommandHistory(userId);
            var deleteCommand = new DeleteMessageCommand(messageRepository, messageId);
            await commandHistory.ExecuteAsync(deleteCommand);

            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId);
            if (conversation != null)
            {
                foreach (var participantId in conversation.ParticipantIds)
                {
                    var user = await userRepository.GetByIdAsync(participantId);
                    if (user != null && user.IsOnline())
                    {
                        await SendDeleteNotificationAsync(user, messageId);
                    }
                }
            }

            return true;
        }

        public async Task<bool> UndoLastActionAsync(string userId)
        {
            var commandHistory = GetCommandHistory(userId);
            return await commandHistory.UndoAsync();
        }

        public async Task NotifyTypingAsync(User user, string conversationId, bool isTyping)
        {
            user.IsTyping = isTyping;
            user.TypingInConversationId = isTyping ? conversationId : null;
            await userRepository.UpdateAsync(user);

            var notification = new TypingNotification
            {
                UserId = user.Id,
                Username = user.Username,
                ConversationId = conversationId,
                IsTyping = isTyping
            };

            await notificationService.NotifyAsync(notification);

            // Send to conversation participants
            var conversation = await conversationRepository.GetByIdAsync(conversationId);
            if (conversation != null)
            {
                foreach (var participantId in conversation.ParticipantIds)
                {
                    if (participantId == user.Id) continue;

                    var participant = await userRepository.GetByIdAsync(participantId);
                    if (participant != null && participant.IsOnline())
                    {
                        await SendTypingNotificationAsync(participant, notification);
                    }
                }
            }
        }

        private async Task SendToUserAsync(User user, Message message)
        {
            try
            {
                var stream = user.GetStream();
                if (stream == null) return;

                var networkMessage = await BuildNetworkMessageAsync(user, message);
                await networkManager.SendMessage(stream, ProtectForUser(user, networkMessage));
            }
            catch
            {
                // Connection lost - mark user as offline
                user.Status = UserStatus.Offline;
                await userRepository.UpdateAsync(user);
            }
        }

        private async Task SendTypingNotificationAsync(User user, TypingNotification notification)
        {
            try
            {
                var stream = user.GetStream();
                if (stream == null) return;

                var conversation = await conversationRepository.GetByIdAsync(notification.ConversationId);
                string scope = conversation is GroupConversation ? "Group" : "Private";
                string name = conversation is PrivateConversation privateConversation
                    ? await GetPrivateConversationLabelAsync(privateConversation, user)
                    : conversation?.Name ?? notification.ConversationId;

                var networkMessage = new NetworkMessage
                {
                    Sender = "System",
                    Type = "typing",
                    Text = $"{notification.Username}|{scope}|{name}|{notification.IsTyping}",
                    Timestamp = notification.Timestamp
                };

                await networkManager.SendMessage(stream, ProtectForUser(user, networkMessage));
            }
            catch
            {
                // Ignore typing notification failures
            }
        }

        private async Task SendDeleteNotificationAsync(User user, string messageId)
        {
            try
            {
                var stream = user.GetStream();
                if (stream == null) return;

                var networkMessage = new NetworkMessage
                {
                    Sender = "System",
                    MessageId = messageId,
                    Type = "delete",
                    Text = "MESSAGE_DELETED",
                    Timestamp = DateTime.UtcNow
                };

                await networkManager.SendMessage(stream, ProtectForUser(user, networkMessage));
            }
            catch
            {
                // Ignore delete notification failures
            }
        }

        private async Task<NetworkMessage> BuildNetworkMessageAsync(User recipient, Message message)
        {
            string body = await FormatMessageForUserAsync(recipient, message);
            
            if (message is ImageMessage imageMessage && imageMessage.ImageData.Length > 0)
            {
                return new NetworkMessage
                {
                    Sender = message.SenderId,
                    MessageId = message.Id,
                    Type = "image",
                    Text = body,
                    ImageData = imageMessage.ImageData,
                    Timestamp = message.Timestamp
                };
            }

            if (message is AudioMessage audioMessage && audioMessage.AudioData.Length > 0)
            {
                return new NetworkMessage
                {
                    Sender = message.SenderId,
                    MessageId = message.Id,
                    Type = "audio",
                    Text = body,
                    FileName = audioMessage.FileName,
                    AudioData = audioMessage.AudioData,
                    Timestamp = message.Timestamp
                };
            }

            if (message is VideoMessage videoMessage && videoMessage.VideoData.Length > 0)
            {
                return new NetworkMessage
                {
                    Sender = message.SenderId,
                    MessageId = message.Id,
                    Type = "video",
                    Text = body,
                    FileName = videoMessage.FileName,
                    VideoData = videoMessage.VideoData,
                    Timestamp = message.Timestamp
                };
            }

            if (message is VoiceMessage voiceMessage && voiceMessage.AudioData.Length > 0)
            {
                return new NetworkMessage
                {
                    Sender = message.SenderId,
                    MessageId = message.Id,
                    Type = "voice",
                    Text = body,
                    FileName = voiceMessage.FileName,
                    DurationSeconds = voiceMessage.DurationSeconds,
                    AudioData = voiceMessage.AudioData,
                    Timestamp = message.Timestamp
                };
            }

            return new NetworkMessage
            {
                Sender = message.SenderId,
                MessageId = message.Id,
                Type = "text",
                Text = body,
                Timestamp = message.Timestamp
            };
        }

        private static NetworkMessage ProtectForUser(User user, NetworkMessage message)
        {
            if (user.SecureSession?.HasSharedKey != true)
            {
                return message;
            }

            EncryptedMessage encrypted = user.SecureSession.Protect(System.Text.Json.JsonSerializer.Serialize(message));
            return new NetworkMessage
            {
                Sender = message.Sender,
                MessageId = message.MessageId,
                Type = "secure",
                CipherText = encrypted.CipherText,
                Iv = encrypted.Iv,
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<string> FormatMessageForUserAsync(User recipient, Message message)
        {
            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId);
            if (conversation == null)
            {
                return message.Serialize();
            }

            string scope = conversation switch
            {
                PrivateConversation => "Private",
                GroupConversation => "Group",
                _ => "Conversation"
            };

            string conversationName = conversation is PrivateConversation privateConversation
                ? await GetPrivateConversationLabelAsync(privateConversation, recipient)
                : conversation.Name;

            string body = message.Serialize();
            if (message.IsEdited)
            {
                body += " (edited)";
            }

            if (!string.Equals(message.SenderId, "System", StringComparison.Ordinal))
            {
                var sender = await userRepository.GetByIdAsync(message.SenderId);
                if (sender != null)
                {
                    body = $"{sender.Username}: {body}";
                }
            }

            return $"[Chat|{scope}|{conversationName}] {body}";
        }

        private async Task<string> GetPrivateConversationLabelAsync(PrivateConversation conversation, User recipient)
        {
            string? otherParticipantId = conversation.ParticipantIds.FirstOrDefault(id => id != recipient.Id);
            var otherParticipant = otherParticipantId == null ? null : await userRepository.GetByIdAsync(otherParticipantId);
            return otherParticipant?.Username ?? conversation.Name;
        }

        private CommandHistory GetCommandHistory(string userId)
        {
            if (!userCommandHistories.ContainsKey(userId))
            {
                userCommandHistories[userId] = new CommandHistory();
            }
            return userCommandHistories[userId];
        }
    }
}
