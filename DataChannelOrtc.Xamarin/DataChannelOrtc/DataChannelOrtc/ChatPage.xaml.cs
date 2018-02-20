using DataChannelOrtc.Signaling;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DataChannelOrtc
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChatPage : ContentPage
    {
        public  ObservableCollection<Message> _messages = new ObservableCollection<Message>();

        public event EventHandler RemotePeerConnected;
        public event EventHandler RemotePeerDisconnected;
        public event EventHandler<Message> SendMessageToRemotePeer;

        private event EventHandler<Message> MessageFromRemotePeer;

        public void HandleRemotePeerConnected()
        {
            RemotePeerConnected?.Invoke(this, null);
        }

        public void HandleRemotePeerDisonnected()
        {
            RemotePeerDisconnected?.Invoke(this, null);
        }

        public void HandleMessageFromPeer(string message)
        {
            MessageFromRemotePeer?.Invoke(this, new Message(RemotePeer, LocalPeer, DateTime.Now, message));
        }

        private bool _isSendReady = false;
        public bool IsSendReady
        {
            get { return _isSendReady; }
            set { _isSendReady = value; }
        }

        public Peer LocalPeer { get; set; }
        public Peer RemotePeer { get; set; }

        public ChatPage(Peer localPeer, Peer remotePeer)
        {
            LocalPeer = localPeer;
            RemotePeer = remotePeer;

            InitializeComponent();

            this.RemotePeerConnected += Signaler_RemoteConnected;
            this.RemotePeerDisconnected += Signaler_RemoteDisconnected;
            this.MessageFromRemotePeer += Signaler_MessageFromRemotePeer;

            InitView();
        }

        private void Signaler_RemoteConnected(object sender, EventArgs e)
        {
            IsSendReady = true;
            btnSend.IsEnabled = true;
        }

        private void Signaler_RemoteDisconnected(object sender, EventArgs e)
        {
            IsSendReady = false;
            btnSend.IsEnabled = false;
        }

        private void Signaler_MessageFromRemotePeer(object sender, Message message)
        {
            _messages.Add(new Message(DateTime.Now, RemotePeer, message.Text));
        }

        private void InitView()
        {
            listMessages.ItemsSource = _messages;
            entryMessage.Text = string.Empty;
            btnSend.IsEnabled = false;

            // Page structure 
            Content = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                        {
                            listMessages,
                            slMessage
                        }
            };

            btnSend.Clicked += (sender, args) =>
            {
                var message = new Message(LocalPeer, RemotePeer, DateTime.Now, entryMessage.Text);
                _messages.Add(new Message(DateTime.Now, LocalPeer, entryMessage.Text));
                OnSendMessageToRemotePeer(message);
            };

        }

        private void OnSendMessageToRemotePeer(Message message)
        {
            SendMessageToRemotePeer?.Invoke(this, message);
        }
    }
}
