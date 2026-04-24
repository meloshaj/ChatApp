using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Decorators
{
    public class LoggingMessageProcessor : IMessageProcessor
    {
        private readonly IMessageProcessor innerProcessor;

        public LoggingMessageProcessor(IMessageProcessor innerProcessor)
        {
            this.innerProcessor = innerProcessor;
        }

        public Message Process(Message message)
        {
            Console.WriteLine($"[LOG] Processing message from {message.SenderId} at {message.Timestamp}");
            var processed = innerProcessor.Process(message);
            Console.WriteLine($"[LOG] Message processed successfully. ID: {processed.Id}");
            return processed;
        }
    }
}
