using ChatCore.Models;

namespace ChatCore.Interfaces
{
    public interface IMessageProcessor
    {
        Message Process(Message message);
    }
}