namespace ChatCore.Models
{
    public class TextMessage : Message
    {
        public string Text { get; set; } = string.Empty;

        public override string Serialize()
        {
            return Text;
        }
    }
}
