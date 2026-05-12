using ChatCore.Models;
using ChatCore.Interfaces;

namespace ChatCore.Services
{
    public class ConversationService
    {
        private readonly FriendService friendService;
        private readonly IConversationRepository conversationRepository;

        public ConversationService(FriendService friendService, IConversationRepository conversationRepository)
        {
            this.friendService = friendService;
            this.conversationRepository = conversationRepository;
        }

        public async Task<PrivateConversation?> GetOrCreatePrivateConversationAsync(User firstUser, User secondUser)
        {
            var existing = await conversationRepository.GetPrivateConversationAsync(firstUser.Id, secondUser.Id);
            if (existing != null)
            {
                return existing;
            }

            var conversation = new PrivateConversation(firstUser, secondUser);
            await conversationRepository.AddAsync(conversation);
            return conversation;
        }

        public async Task<GroupConversation?> CreateGroupConversationAsync(User owner, string groupName)
        {
            string normalizedGroupName = groupName.Trim();
            if (string.IsNullOrWhiteSpace(normalizedGroupName))
            {
                return null;
            }

            var existing = await conversationRepository.GetGroupByNameAsync(normalizedGroupName);
            if (existing != null)
            {
                return null;
            }

            var conversation = new GroupConversation(owner, normalizedGroupName);
            await conversationRepository.AddAsync(conversation);
            return conversation;
        }

        public async Task<GroupConversation?> GetGroupConversationAsync(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return null;
            }

            return await conversationRepository.GetGroupByNameAsync(groupName.Trim());
        }

        public bool AddUserToGroup(GroupConversation conversation, User requester, User newMember)
        {
            if (conversation.OwnerId != requester.Id)
            {
                return false;
            }

            if (conversation.HasParticipant(newMember.Id))
            {
                return false;
            }

            conversation.AddParticipant(newMember);
            return true;
        }

        public bool RemoveUserFromGroup(GroupConversation conversation, User requester, User member)
        {
            if (conversation.OwnerId != requester.Id || member.Id == requester.Id)
            {
                return false;
            }

            return conversation.ParticipantIds.Remove(member.Id);
        }

        public async Task<bool> RenameGroupAsync(GroupConversation conversation, User requester, string newName)
        {
            string normalizedName = newName.Trim();
            if (conversation.OwnerId != requester.Id || string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            var existing = await conversationRepository.GetGroupByNameAsync(normalizedName);
            if (existing != null && existing.Id != conversation.Id)
            {
                return false;
            }

            conversation.Name = normalizedName;
            await conversationRepository.UpdateAsync(conversation);
            return true;
        }
    }
}
