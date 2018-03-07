namespace DataChannelOrtc.Mac
{
    public class MacViewMessage
    {
        public string Author { get; set; } = "";
        public string Text { get; set; } = "";

        public MacViewMessage()
        {
        }

        public MacViewMessage(string author, string text)
        {
            this.Author = author;
            this.Text = text;
        }
    }
}
