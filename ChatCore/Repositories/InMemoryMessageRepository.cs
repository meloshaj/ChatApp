using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Repositories
{
    public class InMemoryMessageRepository : IMessageRepository
    {
        private readonly Dictionary<string, Message> messages = new();
        private readonly Dictionary<string, HashSet<string>> readReceipts = new();
        private readonly object lockObject = new();

        public Task<Message?> GetByIdAsync(string id)
        {
            lock (lockObject)
            {
                messages.TryGetValue(id, out var message);
                return Task.FromResult(message);
            }
        }

        public Task<IEnumerable<Message>> GetAllAsync()
        {
            lock (lockObject)
            {
                return Task.FromResult<IEnumerable<Message>>(messages.Values.ToList());
            }
        }

        public Task AddAsync(Message entity)
        {
            lock (lockObject)
            {
                messages[entity.Id] = entity;
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Message entity)
        {
            lock (lockObject)
            {
                messages[entity.Id] = entity;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id)
        {
            lock (lockObject)
            {
                messages.Remove(id);
                readReceipts.Remove(id);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string id)
        {
            lock (lockObject)
            {
                return Task.FromResult(messages.ContainsKey(id));
            }
        }

        public Task<IEnumerable<Message>> GetConversationMessagesAsync(string conversationId, int limit = 100)
        {
            lock (lockObject)
            {
                var conversationMessages = messages.Values
                    .Where(m => m.ConversationId == conversationId)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                return Task.FromResult<IEnumerable<Message>>(conversationMessages);
            }
        }

        public Task<IEnumerable<Message>> GetMessagesSinceAsync(string conversationId, DateTime since)
        {
            lock (lockObject)
            {
                var recentMessages = messages.Values
                    .Where(m => m.ConversationId == conversationId && m.Timestamp > since)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
                return Task.FromResult<IEnumerable<Message>>(recentMessages);
            }
        }

        public Task MarkAsReadAsync(string messageId, string userId)
        {
            lock (lockObject)
            {
                if (!readReceipts.ContainsKey(messageId))
                {
                    readReceipts[messageId] = new HashSet<string>();
                }
                readReceipts[messageId].Add(userId);
            }
            return Task.CompletedTask;
        }
    }
}
