using ChatCore.Models;

namespace ChatCore.States
{
    public class OfflineState : IUserState
    {
        public UserStatus Status => UserStatus.Offline;

        public string GetStatusMessage() => "Offline";

        public bool CanReceiveMessages() => false;

        public bool ShouldSendAutoReply() => false;

        public string? GetAutoReplyMessage() => null;
    }
}
