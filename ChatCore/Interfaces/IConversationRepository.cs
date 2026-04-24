using ChatCore.Models;

namespace ChatCore.Interfaces
{
    public interface IConversationRepository : IRepository<Conversation>
    {
        Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId);
        Task<PrivateConversation?> GetPrivateConversationAsync(string userId1, string userId2);
        Task<GroupConversation?> GetGroupByNameAsync(string groupName);
    }
}
