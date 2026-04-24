using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Commands
{
    public class EditMessageCommand : ICommand
    {
        private readonly IMessageRepository messageRepository;
        private readonly string messageId;
        private readonly string newText;
        private string? originalText;

        public EditMessageCommand(IMessageRepository messageRepository, string messageId, string newText)
        {
            this.messageRepository = messageRepository;
            this.messageId = messageId;
            this.newText = newText;
        }

        public async Task ExecuteAsync()
        {
            var message = await messageRepository.GetByIdAsync(messageId);
            if (message is TextMessage textMessage)
            {
                originalText = textMessage.Text;
                textMessage.Text = newText;
                textMessage.IsEdited = true;
                textMessage.EditedAt = DateTime.UtcNow;
                await messageRepository.UpdateAsync(textMessage);
            }
        }

        public async Task UndoAsync()
        {
            if (originalText == null) return;

            var message = await messageRepository.GetByIdAsync(messageId);
            if (message is TextMessage textMessage)
            {
                textMessage.Text = originalText;
                textMessage.IsEdited = false;
                textMessage.EditedAt = null;
                await messageRepository.UpdateAsync(textMessage);
            }
        }

        public string GetDescription() => $"Edit message {messageId}";
    }
}
