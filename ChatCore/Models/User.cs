using System.Net.Sockets;
using ChatCore.States;

namespace ChatCore.Models
{
    public class User
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string RsaPublicKey { get; set; } = string.Empty;
        public string RsaPrivateKey { get; set; } = string.Empty;
        public HashSet<string> FriendIds { get; } = new();
        public string? ActiveConversationId { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Offline;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public bool IsTyping { get; set; }
        public string? TypingInConversationId { get; set; }

        private IUserState currentState = new OfflineState();
        public TcpClient? Client { get; set; }

        public void SetState(IUserState state)
        {
            currentState = state;
            Status = state.Status;
        }

        public IUserState GetState() => currentState;

        public NetworkStream? GetStream()
        {
            return Client?.GetStream();
        }

        public bool IsOnline() => Client != null && Status != UserStatus.Offline;
    }
}
