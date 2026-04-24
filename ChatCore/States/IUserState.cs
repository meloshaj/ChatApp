using ChatCore.Models;

namespace ChatCore.States
{
    public interface IUserState
    {
        UserStatus Status { get; }
        string GetStatusMessage();
        bool CanReceiveMessages();
        bool ShouldSendAutoReply();
        string? GetAutoReplyMessage();
    }
}
