using ChatCore.Models;
using ChatCore.Interfaces;
using ChatCore.Factories;

namespace ChatCore.Services
{
    public class CommandService
    {
        private readonly ChatService chatService;
        private readonly FriendService friendService;
        private readonly ConversationService conversationService;
        private readonly AuthenticationService authService;
        private readonly IUserRepository userRepository;
        private readonly IConversationRepository conversationRepository;
        private readonly IMessageRepository messageRepository;
        private readonly IMessageFactory messageFactory;

        public CommandService(
            ChatService chatService,
            FriendService friendService,
            ConversationService conversationService,
            AuthenticationService authService,
            IUserRepository userRepository,
            IConversationRepository conversationRepository,
            IMessageRepository messageRepository,
            IMessageFactory messageFactory)
        {
            this.chatService = chatService;
            this.friendService = friendService;
            this.conversationService = conversationService;
            this.authService = authService;
            this.userRepository = userRepository;
            this.conversationRepository = conversationRepository;
            this.messageRepository = messageRepository;
            this.messageFactory = messageFactory;
        }

        public async Task SendWelcomeAsync(User user)
        {
            await SendSystemMessageAsync(user, $"Welcome back, {user.Username}!");
            await SendSystemMessageAsync(user, "Commands: /users, /friends, /friend add <username>, /chat <username>");
            await SendSystemMessageAsync(user, "/group create <group name>, /group add <group name> <username>, /group chat <group name>, /group members <group name>");
            await SendSystemMessageAsync(user, "/status <online|away|busy>, /edit <messageId> <text>, /delete <messageId>, /undo");
        }

        public async Task LoadUserConversationsAsync(User user)
        {
            var conversations = await conversationRepository.GetUserConversationsAsync(user.Id);
            
            foreach (var conversation in conversations)
            {
                var messages = await chatService.LoadConversationHistoryAsync(conversation.Id, 50);
                var messageCount = messages.Count();
                
                if (messageCount > 0)
                {
                    await SendSystemMessageAsync(user, 
                        $"Loaded {messageCount} messages from {conversation.Name}");
                }
            }
        }

        private async Task ReplayConversationHistoryAsync(User user, Conversation conversation)
        {
            string scope = conversation switch
            {
                PrivateConversation => "Private",
                GroupConversation => "Group",
                _ => "Conversation"
            };

            await SendSystemMessageAsync(user, $"HISTORY_RESET|{scope}|{conversation.Name}");

            int replayedCount = await chatService.ReplayConversationHistoryAsync(user, conversation.Id, 50);
            await SendSystemMessageAsync(user, $"Loaded {replayedCount} previous messages.");
        }

        public async Task HandleInputAsync(User user, string input)
        {
            string text = input.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (text.StartsWith("/"))
            {
                await HandleCommandAsync(user, text);
                return;
            }

            if (string.IsNullOrWhiteSpace(user.ActiveConversationId))
            {
                await SendSystemMessageAsync(user, "Select a chat first with /chat <username>.");
                return;
            }

            var message = messageFactory.CreateTextMessage(user.Id, user.ActiveConversationId, text);
            await chatService.SendMessageAsync(user, message);
        }

        public async Task HandleIncomingImageAsync(User user, byte[] imageData)
        {
            if (string.IsNullOrWhiteSpace(user.ActiveConversationId))
            {
                await SendSystemMessageAsync(user, "Select a chat before sending an image.");
                return;
            }

            var message = messageFactory.CreateImageMessage(
                user.Id, 
                user.ActiveConversationId, 
                "image.bin", 
                imageData);
            await chatService.SendMessageAsync(user, message);
        }

        public async Task HandleIncomingAudioAsync(User user, string fileName, byte[] audioData)
        {
            if (string.IsNullOrWhiteSpace(user.ActiveConversationId))
            {
                await SendSystemMessageAsync(user, "Select a chat before sending audio.");
                return;
            }

            var message = messageFactory.CreateAudioMessage(
                user.Id,
                user.ActiveConversationId,
                string.IsNullOrWhiteSpace(fileName) ? "audio.bin" : fileName,
                audioData);
            await chatService.SendMessageAsync(user, message);
        }

        public async Task HandleIncomingVideoAsync(User user, string fileName, byte[] videoData)
        {
            if (string.IsNullOrWhiteSpace(user.ActiveConversationId))
            {
                await SendSystemMessageAsync(user, "Select a chat before sending a video.");
                return;
            }

            var message = messageFactory.CreateVideoMessage(
                user.Id,
                user.ActiveConversationId,
                string.IsNullOrWhiteSpace(fileName) ? "video.bin" : fileName,
                videoData);
            await chatService.SendMessageAsync(user, message);
        }

        public async Task HandleIncomingVoiceAsync(User user, string fileName, int durationSeconds, byte[] audioData)
        {
            if (string.IsNullOrWhiteSpace(user.ActiveConversationId))
            {
                await SendSystemMessageAsync(user, "Select a chat before sending a voice message.");
                return;
            }

            var message = messageFactory.CreateVoiceMessage(
                user.Id,
                user.ActiveConversationId,
                string.IsNullOrWhiteSpace(fileName) ? "voice.bin" : fileName,
                durationSeconds,
                audioData);
            await chatService.SendMessageAsync(user, message);
        }

        private async Task HandleCommandAsync(User user, string input)
        {
            string[] parts = input.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            string command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "/users":
                    await HandleUsersCommandAsync(user);
                    break;
                case "/friends":
                    await HandleFriendsCommandAsync(user);
                    break;
                case "/friend" when parts.Length >= 3 && parts[1].ToLowerInvariant() == "add":
                    await HandleAddFriendAsync(user, parts[2]);
                    break;
                case "/chat" when parts.Length >= 2:
                    await HandleOpenPrivateChatAsync(user, parts[1]);
                    break;
                case "/groups":
                    await HandleGroupsCommandAsync(user);
                    break;
                case "/group":
                    await HandleGroupCommandAsync(user, parts);
                    break;
                case "/edit" when parts.Length >= 3:
                    await HandleEditMessageAsync(user, parts[1], string.Join(" ", parts.Skip(2)));
                    break;
                case "/delete" when parts.Length >= 2:
                    await HandleDeleteMessageAsync(user, parts[1]);
                    break;
                case "/undo":
                    await HandleUndoAsync(user);
                    break;
                case "/status" when parts.Length >= 2:
                    await SendSystemMessageAsync(user, $"Status change to {parts[1]} (handled by server)");
                    break;
                default:
                    await SendSystemMessageAsync(user, "Unknown command.");
                    break;
            }
        }

        private async Task HandleGroupCommandAsync(User user, string[] parts)
        {
            if (parts.Length < 2)
            {
                await SendSystemMessageAsync(user, "Usage: /group create <group>, /group add <group> <username>, /group remove <group> <username>, /group chat <group>.");
                return;
            }

            string action = parts[1].ToLowerInvariant();
            string arguments = GetRemainingArguments(parts);

            switch (action)
            {
                case "create" when !string.IsNullOrWhiteSpace(arguments):
                    await HandleCreateGroupAsync(user, arguments);
                    break;
                case "add" when TrySplitGroupAndUsername(arguments, out string groupName, out string username):
                    await HandleAddToGroupAsync(user, groupName, username);
                    break;
                case "remove" when TrySplitGroupAndUsername(arguments, out string groupName, out string username):
                    await HandleRemoveFromGroupAsync(user, groupName, username);
                    break;
                case "chat" when !string.IsNullOrWhiteSpace(arguments):
                    await HandleOpenGroupChatAsync(user, arguments);
                    break;
                case "members" when !string.IsNullOrWhiteSpace(arguments):
                    await HandleGroupMembersAsync(user, arguments);
                    break;
                default:
                    await SendSystemMessageAsync(user, "Usage: /group create <group>, /group add <group> <username>, /group remove <group> <username>, /group chat <group>.");
                    break;
            }
        }

        private static string GetRemainingArguments(string[] parts)
        {
            return parts.Length switch
            {
                < 3 => string.Empty,
                3 => parts[2].Trim(),
                _ => $"{parts[2]} {parts[3]}".Trim()
            };
        }

        private static bool TrySplitGroupAndUsername(string arguments, out string groupName, out string username)
        {
            groupName = string.Empty;
            username = string.Empty;

            string trimmedArguments = arguments.Trim();
            int separatorIndex = trimmedArguments.LastIndexOf(' ');
            if (separatorIndex <= 0 || separatorIndex >= trimmedArguments.Length - 1)
            {
                return false;
            }

            groupName = trimmedArguments[..separatorIndex].Trim();
            username = trimmedArguments[(separatorIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(groupName) && !string.IsNullOrWhiteSpace(username);
        }

        private async Task HandleUsersCommandAsync(User user)
        {
            var users = await userRepository.GetAllAsync();
            var usernames = string.Join(", ", users.Select(u => $"{u.Username} ({u.Status})"));
            await SendSystemMessageAsync(user, "Connected users: " + usernames);
        }

        private async Task HandleFriendsCommandAsync(User user)
        {
            var allUsers = await userRepository.GetAllAsync();
            var friends = allUsers.Where(u => user.FriendIds.Contains(u.Id))
                .Select(u => $"{u.Username} ({u.Status})")
                .OrderBy(name => name);
            
            var friendList = friends.Any() ? string.Join(", ", friends) : "none";
            await SendSystemMessageAsync(user, "Friends: " + friendList);
        }

        private async Task HandleAddFriendAsync(User user, string friendUsername)
        {
            var friend = await userRepository.GetByUsernameAsync(friendUsername);
            if (friend == null)
            {
                await SendSystemMessageAsync(user, "User not found.");
                return;
            }

            if (friendService.AddFriend(user, friend))
            {
                await userRepository.UpdateAsync(user);
                await userRepository.UpdateAsync(friend);
                await SendSystemMessageAsync(user, $"{friend.Username} added as a friend.");
                
                if (friend.IsOnline())
                {
                    await SendSystemMessageAsync(friend, $"{user.Username} added you as a friend.");
                }
            }
            else
            {
                await SendSystemMessageAsync(user, "Could not add that friend.");
            }
        }

        private async Task HandleOpenPrivateChatAsync(User user, string friendUsername)
        {
            var friend = await userRepository.GetByUsernameAsync(friendUsername);
            if (friend == null)
            {
                await SendSystemMessageAsync(user, "User not found.");
                return;
            }

            var conversation = await conversationService.GetOrCreatePrivateConversationAsync(user, friend);
            if (conversation == null)
            {
                await SendSystemMessageAsync(user, "Could not open chat.");
                return;
            }

            user.ActiveConversationId = conversation.Id;
            await userRepository.UpdateAsync(user);
            await SendSystemMessageAsync(user, $"Chat selected: {friend.Username}.");
            await ReplayConversationHistoryAsync(user, conversation);
        }

        private async Task HandleGroupsCommandAsync(User user)
        {
            var allConversations = await conversationRepository.GetAllAsync();
            var groups = allConversations.OfType<GroupConversation>()
                .Where(g => g.HasParticipant(user.Id))
                .Select(g => g.Name)
                .OrderBy(name => name);
            
            var groupList = groups.Any() ? string.Join(", ", groups) : "none";
            await SendSystemMessageAsync(user, "Groups: " + groupList);
        }

        private async Task HandleCreateGroupAsync(User user, string groupName)
        {
            var group = await conversationService.CreateGroupConversationAsync(user, groupName);
            if (group == null)
            {
                await SendSystemMessageAsync(user, "Could not create group. Name may already be in use.");
                return;
            }

            user.ActiveConversationId = group.Id;
            await userRepository.UpdateAsync(user);
            await SendSystemMessageAsync(user, $"Group created: {group.Name}.");
        }

        private async Task HandleAddToGroupAsync(User user, string groupName, string username)
        {
            var group = await conversationService.GetGroupConversationAsync(groupName);
            if (group == null)
            {
                await SendSystemMessageAsync(user, "Group not found.");
                return;
            }

            var friend = await userRepository.GetByUsernameAsync(username);
            if (friend == null)
            {
                await SendSystemMessageAsync(user, "User not found.");
                return;
            }

            if (conversationService.AddUserToGroup(group, user, friend))
            {
                await conversationRepository.UpdateAsync(group);
                await SendSystemMessageAsync(user, $"{friend.Username} added to group {group.Name}.");
                
                if (friend.IsOnline())
                {
                    await SendSystemMessageAsync(friend, $"You were added to group {group.Name}.");
                }
            }
            else
            {
                await SendSystemMessageAsync(user, "Could not add user to group.");
            }
        }

        private async Task HandleOpenGroupChatAsync(User user, string groupName)
        {
            var group = await conversationService.GetGroupConversationAsync(groupName);
            if (group == null)
            {
                await SendSystemMessageAsync(user, "Group not found.");
                return;
            }

            if (!group.HasParticipant(user.Id))
            {
                await SendSystemMessageAsync(user, "You are not a member of that group.");
                return;
            }

            user.ActiveConversationId = group.Id;
            await userRepository.UpdateAsync(user);
            await SendSystemMessageAsync(user, $"Group chat selected: {group.Name}.");
            await ReplayConversationHistoryAsync(user, group);
            await SendGroupMembersAsync(user, group);
        }

        private async Task HandleRemoveFromGroupAsync(User user, string groupName, string username)
        {
            var group = await conversationService.GetGroupConversationAsync(groupName);
            var member = await userRepository.GetByUsernameAsync(username);
            if (group == null || member == null)
            {
                await SendSystemMessageAsync(user, group == null ? "Group not found." : "User not found.");
                return;
            }

            if (!group.HasParticipant(member.Id))
            {
                await SendSystemMessageAsync(user, "User not found.");
                return;
            }

            if (conversationService.RemoveUserFromGroup(group, user, member))
            {
                await conversationRepository.UpdateAsync(group);
                await SendSystemMessageAsync(user, $"{member.Username} removed from group {group.Name}.");
                await SendGroupMembersAsync(user, group);
                if (member.IsOnline())
                {
                    await SendSystemMessageAsync(member, $"You were removed from group {group.Name}.");
                }
            }
            else
            {
                await SendSystemMessageAsync(user, "Could not remove user from group.");
            }
        }

        private async Task HandleGroupMembersAsync(User user, string groupName)
        {
            var group = await conversationService.GetGroupConversationAsync(groupName);
            if (group == null)
            {
                await SendSystemMessageAsync(user, "Group not found.");
                return;
            }

            if (!group.HasParticipant(user.Id))
            {
                await SendSystemMessageAsync(user, "You are not a member of that group.");
                return;
            }

            await SendGroupMembersAsync(user, group);
        }

        private async Task SendGroupMembersAsync(User user, GroupConversation group)
        {
            var allUsers = await userRepository.GetAllAsync();
            var memberNames = allUsers
                .Where(member => group.ParticipantIds.Contains(member.Id))
                .Select(member => member.Username)
                .OrderBy(username => username, StringComparer.OrdinalIgnoreCase);

            string members = memberNames.Any() ? string.Join(", ", memberNames) : "none";
            await SendSystemMessageAsync(user, $"GROUP_MEMBERS|{group.Name}|{members}");
        }

        private async Task HandleEditMessageAsync(User user, string messageId, string newText)
        {
            bool success = await chatService.EditMessageAsync(user.Id, messageId, newText);
            if (success)
            {
                await SendSystemMessageAsync(user, "Message edited successfully.");
            }
            else
            {
                await SendSystemMessageAsync(user, "Could not edit message. Check message ID and permissions.");
            }
        }

        private async Task HandleDeleteMessageAsync(User user, string messageId)
        {
            bool success = await chatService.DeleteMessageAsync(user.Id, messageId);
            if (success)
            {
                await SendSystemMessageAsync(user, "Message deleted successfully.");
            }
            else
            {
                await SendSystemMessageAsync(user, "Could not delete message. Check message ID and permissions.");
            }
        }

        private async Task HandleUndoAsync(User user)
        {
            bool success = await chatService.UndoLastActionAsync(user.Id);
            if (success)
            {
                await SendSystemMessageAsync(user, "Last action undone.");
            }
            else
            {
                await SendSystemMessageAsync(user, "Nothing to undo.");
            }
        }

        private async Task SendSystemMessageAsync(User recipient, string text)
        {
            var message = messageFactory.CreateSystemMessage(string.Empty, text);
            await chatService.SendDirectMessageAsync(recipient, message);
        }
    }
}
