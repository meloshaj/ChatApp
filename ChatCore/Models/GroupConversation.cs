namespace ChatCore.Models
{
    public class GroupConversation : Conversation
    {
        public string OwnerId { get; set; } = string.Empty;

        public GroupConversation()
        {
        }

        public GroupConversation(User owner, string name)
        {
            OwnerId = owner.Id;
            Name = name;
            AddParticipant(owner);
        }
    }
}
