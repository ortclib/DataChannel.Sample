using System;

namespace DataChannelOrtc.Mac.Signaling 
{
    public abstract class OrtcControllerEvents
    {
        public event EventHandler DataChannelConnected;
        public event EventHandler DataChannelDisconnected;
        public event EventHandler<string> SignalMessageToPeer;
        public event EventHandler<string> DataChannelMessage;

        protected void OnDataChannelConnected()
        {
            DataChannelConnected?.Invoke(this, null);
        }

        protected void OnDataChannelDisconnected()
        {
            DataChannelDisconnected?.Invoke(this, null);
        }

        protected void OnSignalMessageToPeer(string message)
        {
            SignalMessageToPeer?.Invoke(this, message);
        }

        protected void OnDataChannelMessage(string message)
        {
            DataChannelMessage?.Invoke(this, message);
        }
    }
}