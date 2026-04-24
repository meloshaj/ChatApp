using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Commands
{
    public class DeleteMessageCommand : ICommand
    {
        private readonly IMessageRepository messageRepository;
        private readonly string messageId;
        private Message? deletedMessage;

        public DeleteMessageCommand(IMessageRepository messageRepository, string messageId)
        {
            this.messageRepository = messageRepository;
            this.messageId = messageId;
        }

        public async Task ExecuteAsync()
        {
            deletedMessage = await messageRepository.GetByIdAsync(messageId);
            if (deletedMessage != null)
            {
                deletedMessage.IsDeleted = true;
                await messageRepository.UpdateAsync(deletedMessage);
            }
        }

        public async Task UndoAsync()
        {
            if (deletedMessage != null)
            {
                deletedMessage.IsDeleted = false;
                await messageRepository.UpdateAsync(deletedMessage);
            }
        }

        public string GetDescription() => $"Delete message {messageId}";
    }
}
