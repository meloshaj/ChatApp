using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Repositories
{
    public class InMemoryConversationRepository : IConversationRepository
    {
        private readonly Dictionary<string, Conversation> conversations = new();
        private readonly List<(string ConversationId, string UserId, DateTime JoinedAt, DateTime? LeftAt)> membershipHistory = new();
        private readonly object lockObject = new();

        public Task<Conversation?> GetByIdAsync(string id)
        {
            lock (lockObject)
            {
                conversations.TryGetValue(id, out var conversation);
                return Task.FromResult(conversation);
            }
        }

        public Task<IEnumerable<Conversation>> GetAllAsync()
        {
            lock (lockObject)
            {
                return Task.FromResult<IEnumerable<Conversation>>(conversations.Values.ToList());
            }
        }

        public Task AddAsync(Conversation entity)
        {
            lock (lockObject)
            {
                conversations[entity.Id] = entity;
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Conversation entity)
        {
            lock (lockObject)
            {
                conversations[entity.Id] = entity;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            lock (lockObject)
            {
                conversations.Remove(id);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string id)
        {
            lock (lockObject)
            {
                return Task.FromResult(conversations.ContainsKey(id));
            }
        }

        public Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId)
        {
            lock (lockObject)
            {
                var userConversations = conversations.Values
                    .Where(c => c.HasParticipant(userId))
                    .ToList();
                return Task.FromResult<IEnumerable<Conversation>>(userConversations);
            }
        }

        public Task<PrivateConversation?> GetPrivateConversationAsync(string userId1, string userId2)
        {
            lock (lockObject)
            {
                var conversation = conversations.Values
                    .OfType<PrivateConversation>()
                    .FirstOrDefault(c => c.HasParticipant(userId1) && c.HasParticipant(userId2));
                return Task.FromResult(conversation);
            }
        }

        public Task<GroupConversation?> GetGroupByNameAsync(string groupName)
        {
            lock (lockObject)
            {
                var group = conversations.Values
                    .OfType<GroupConversation>()
                    .FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(group);
            }
        }

        public Task RecordUserJoinedAsync(string conversationId, string userId, DateTime timestamp)
        {
            lock (lockObject)
            {
                membershipHistory.Add((conversationId, userId, timestamp, null));
            }
            return Task.CompletedTask;
        }

        public Task RecordUserLeftAsync(string conversationId, string userId, DateTime timestamp)
        {
            lock (lockObject)
            {
                for (int i = membershipHistory.Count - 1; i >= 0; i--)
                {
                    if (membershipHistory[i].ConversationId == conversationId && 
                        membershipHistory[i].UserId == userId && 
                        membershipHistory[i].LeftAt == null)
                    {
                        membershipHistory[i] = (conversationId, userId, membershipHistory[i].JoinedAt, timestamp);
                        break;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<(DateTime JoinedAt, DateTime? LeftAt)>> GetMembershipHistoryAsync(string conversationId, string userId)
        {
            lock (lockObject)
            {
                var history = membershipHistory
                    .Where(h => h.ConversationId == conversationId && h.UserId == userId)
                    .Select(h => (h.JoinedAt, h.LeftAt))
                    .ToList();
                return Task.FromResult<IEnumerable<(DateTime JoinedAt, DateTime? LeftAt)>>(history);
            }
        }
    }
}
