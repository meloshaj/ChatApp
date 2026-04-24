using ChatCore.Models;

namespace ChatCore.Factories
{
    public interface IMessageFactory
    {
        Message CreateTextMessage(string senderId, string conversationId, string text);
        Message CreateImageMessage(string senderId, string conversationId, string fileName, byte[] imageData);
        Message CreateAudioMessage(string senderId, string conversationId, string fileName, byte[] audioData);
        Message CreateVideoMessage(string senderId, string conversationId, string fileName, byte[] videoData);
        Message CreateVoiceMessage(string senderId, string conversationId, string fileName, int durationSeconds, byte[] audioData);
        Message CreateSystemMessage(string conversationId, string text);
    }
}
