namespace ChatCore.Models
{
    public class PrivateConversation : Conversation
    {
        public PrivateConversation()
        {
        }

        public PrivateConversation(User firstUser, User secondUser)
        {
            Name = $"{firstUser.Username}-{secondUser.Username}";
            AddParticipant(firstUser);
            AddParticipant(secondUser);
        }

        public override void AddParticipant(User user)
        {
            if (ParticipantIds.Count >= 2 && !ParticipantIds.Contains(user.Id))
            {
                throw new InvalidOperationException("A private conversation can only have two participants.");
            }

            base.AddParticipant(user);
        }
    }
}
