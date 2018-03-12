using System;

namespace DataChannelOrtc.Mac
{
    public class MacViewMessage
    {
        public string Author { get; set; } = "";
        public string Text { get; set; } = "";
        public string Time { get; set; }

        public MacViewMessage()
        {
        }

        public MacViewMessage(string author, string text)
        {
            this.Author = author;
            this.Text = text;
        }

        public MacViewMessage(DateTime date, string author, string text)
        {
            Time = date.ToString("h:mm");
            Author = author;
            Text = text;
        }
    }
}
