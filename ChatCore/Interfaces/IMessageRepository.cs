using ChatCore.Models;

namespace ChatCore.Interfaces
{
    public interface IMessageRepository : IRepository<Message>
    {
        Task<IEnumerable<Message>> GetConversationMessagesAsync(string conversationId, int limit = 100);
        Task<IEnumerable<Message>> GetMessagesSinceAsync(string conversationId, DateTime since);
        Task MarkAsReadAsync(string messageId, string userId);
    }
}
