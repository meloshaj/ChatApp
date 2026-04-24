using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Decorators
{
    public class ValidationMessageProcessor : IMessageProcessor
    {
        private readonly IMessageProcessor innerProcessor;

        public ValidationMessageProcessor(IMessageProcessor innerProcessor)
        {
            this.innerProcessor = innerProcessor;
        }

        public Message Process(Message message)
        {
            if (message is TextMessage textMessage)
            {
                textMessage.Text = textMessage.Text.Trim();
                
                if (textMessage.Text.Length > 5000)
                {
                    textMessage.Text = textMessage.Text[..5000] + "... [truncated]";
                }

                if (string.IsNullOrWhiteSpace(textMessage.Text))
                {
                    textMessage.Text = "[Empty message]";
                }
            }

            return innerProcessor.Process(message);
        }
    }
}
