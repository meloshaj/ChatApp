using ChatCore.Models;

namespace ChatCore.Factories
{
    public class MessageFactory : IMessageFactory
    {
        public Message CreateTextMessage(string senderId, string conversationId, string text)
        {
            return new TextMessage
            {
                SenderId = senderId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                Text = text
            };
        }

        public Message CreateImageMessage(string senderId, string conversationId, string fileName, byte[] imageData)
        {
            return new ImageMessage
            {
                SenderId = senderId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                FileName = fileName,
                FilePath = "from-network",
                ImageData = imageData
            };
        }

        public Message CreateAudioMessage(string senderId, string conversationId, string fileName, byte[] audioData)
        {
            return new AudioMessage
            {
                SenderId = senderId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                FileName = fileName,
                FilePath = "from-network",
                AudioData = audioData
            };
        }

        public Message CreateVideoMessage(string senderId, string conversationId, string fileName, byte[] videoData)
        {
            return new VideoMessage
            {
                SenderId = senderId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                FileName = fileName,
                FilePath = "from-network",
                VideoData = videoData
            };
        }

        public Message CreateVoiceMessage(string senderId, string conversationId, string fileName, int durationSeconds, byte[] audioData)
        {
            return new VoiceMessage
            {
                SenderId = senderId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                FileName = fileName,
                DurationSeconds = durationSeconds,
                FilePath = "from-network",
                AudioData = audioData
            };
        }

        public Message CreateSystemMessage(string conversationId, string text)
        {
            return new TextMessage
            {
                SenderId = "System",
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                Text = $"[System] {text}"
            };
        }
    }
}
