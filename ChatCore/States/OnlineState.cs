using ChatCore.Models;

namespace ChatCore.States
{
    public class OnlineState : IUserState
    {
        public UserStatus Status => UserStatus.Online;

        public string GetStatusMessage() => "Online";

        public bool CanReceiveMessages() => true;

        public bool ShouldSendAutoReply() => false;

        public string? GetAutoReplyMessage() => null;
    }
}
