namespace ChatCore.Models
{
    public abstract class Conversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public List<string> ParticipantIds { get; } = new();
        public List<Message> Messages { get; } = new();

        public virtual void AddParticipant(User user)
        {
            if (!ParticipantIds.Contains(user.Id))
            {
                ParticipantIds.Add(user.Id);
            }
        }

        public bool HasParticipant(string userId)
        {
            return ParticipantIds.Contains(userId);
        }

        public void AddMessage(Message message)
        {
            Messages.Add(message);
        }
    }
}


