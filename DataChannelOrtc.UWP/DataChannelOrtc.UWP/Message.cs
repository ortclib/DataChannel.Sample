using DataChannelOrtc.Signaling;
using System;

namespace DataChannelOrtc.UWP
{
    public class Message
    {
        //public Peer Author { get; set; }
        public Peer Recipient { get; set; }
        //public Peer SendingPeer { get; set; }

        //public DateTime Time { get; set; }
        public string MessageText { get; set; }
        public string AuthorName { get; set; }
        public string TimeStr { get; set; }

        public Message() { }

        public Message(Peer author, Peer recipient, DateTime date, string text)
        {
            AuthorName = author.Name;
            //Recipient = recipient;
            //Time = date;
            TimeStr = date.ToString("h:mm");
            MessageText = text;
        }

        public Message(Peer author, DateTime time, string text)
        {
            AuthorName = author.Name;
            TimeStr = time.ToString("h:mm");
            MessageText = text;
        }

        public override string ToString()
        {
            return "[" + AuthorName + "] " + TimeStr + ":  " + MessageText;
        }
    }
}
