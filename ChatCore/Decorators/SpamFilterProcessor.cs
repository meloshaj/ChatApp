using ChatCore.Interfaces;
using ChatCore.Models;

namespace ChatCore.Decorators
{
    public class SpamFilterProcessor : IMessageProcessor
    {
        private readonly IMessageProcessor innerProcessor;
        private readonly Dictionary<string, Queue<DateTime>> userMessageTimes = new();
        private readonly int maxMessagesPerMinute;
        private readonly object lockObject = new();

        public SpamFilterProcessor(IMessageProcessor innerProcessor, int maxMessagesPerMinute = 20)
        {
            this.innerProcessor = innerProcessor;
            this.maxMessagesPerMinute = maxMessagesPerMinute;
        }

        public Message Process(Message message)
        {
            lock (lockObject)
            {
                if (!userMessageTimes.ContainsKey(message.SenderId))
                {
                    userMessageTimes[message.SenderId] = new Queue<DateTime>();
                }

                var times = userMessageTimes[message.SenderId];
                var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);

                while (times.Count > 0 && times.Peek() < oneMinuteAgo)
                {
                    times.Dequeue();
                }

                if (times.Count >= maxMessagesPerMinute)
                {
                    if (message is TextMessage textMessage)
                    {
                        textMessage.Text = "[Message blocked: Rate limit exceeded]";
                    }
                    return message;
                }

                times.Enqueue(DateTime.UtcNow);
            }

            return innerProcessor.Process(message);
        }
    }
}
