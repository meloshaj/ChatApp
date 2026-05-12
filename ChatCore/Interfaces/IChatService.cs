using ChatCore.Models;

namespace ChatCore.Interfaces
{
    public interface IChatService
    {
        Task SendMessageAsync(User sender, Message message);
        Task SendDirectMessageAsync(User recipient, Message message);
    }
}
