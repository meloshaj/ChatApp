using ChatCore.Models;

namespace ChatCore.States
{
    public class AwayState : IUserState
    {
        public UserStatus Status => UserStatus.Away;

        public string GetStatusMessage() => "Away";

        public bool CanReceiveMessages() => true;

        public bool ShouldSendAutoReply() => true;

        public string? GetAutoReplyMessage() => "I'm currently away. I'll respond when I'm back.";
    }
}
