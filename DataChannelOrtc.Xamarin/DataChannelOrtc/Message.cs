using DataChannelOrtc.Signaling;
using System;

namespace DataChannelOrtc
{
    public class Message
    {
        public Peer Author { get; set; }
        public Peer Recipient { get; set; }
        public Peer SendingPeer { get; set; }

        public DateTime DateTime { get; set; }

        public string Time { get; set; }
        public string Text { get; set; }
        public string AuthorName { get; set; }

        public Message() { }

        public Message(DateTime date, Peer author, string text)
        {
            Time = date.ToString("h:mm");
            AuthorName = author.Name;
            Text = text;
        }

        public Message(Peer author, Peer recipient, DateTime date, string text)
        {
            Author = author;
            Recipient = recipient;
            DateTime = date;
            Text = text;
        }
    }
}
