using ChatCore.Models;

namespace ChatCore.Interfaces
{
    public interface IChatService
    {
        void SendMessage(User sender, Message message);
        void SendDirectMessage(User recipient, Message message);
    }
}
