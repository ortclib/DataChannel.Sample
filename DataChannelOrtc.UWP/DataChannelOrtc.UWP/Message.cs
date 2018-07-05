using DataChannelOrtc.Signaling;
using System;

namespace DataChannelOrtc.UWP
{
    public class Message
    {
        public string MessageText { get; set; }
        public string AuthorName { get; set; }
        public string TimeStr { get; set; }

        public Message() { }

        public Message(Peer author, DateTime time, string text)
        {
            AuthorName = author.Name;
            TimeStr = time.ToString("h:mm");
            MessageText = text;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
