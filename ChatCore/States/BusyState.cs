using ChatCore.Models;

namespace ChatCore.States
{
    public class BusyState : IUserState
    {
        public UserStatus Status => UserStatus.Busy;

        public string GetStatusMessage() => "Busy - Do Not Disturb";

        public bool CanReceiveMessages() => true;

        public bool ShouldSendAutoReply() => true;

        public string? GetAutoReplyMessage() => "I'm busy right now. Please leave a message.";
    }
}
