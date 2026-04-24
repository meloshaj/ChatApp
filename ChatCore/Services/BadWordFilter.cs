using ChatCore.Models;
using ChatCore.Interfaces;

namespace ChatCore.Services
{
    public class BadWordFilter : IMessageProcessor
    {
        public Message Process(Message message)
        {
            if (message is TextMessage txt)
            {
                txt.Text = txt.Text.Replace("badword", "***");
            }

            return message;
        }
    }
}