using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppKit;
using DataChannelOrtc.Mac.Signaling;
using Foundation;

namespace DataChannelOrtc.Mac
{
    public partial class ViewController : NSViewController
    {
        public Peer LocalPeer { get; set; }
        public Peer RemotePeer { get; set; }
        public static ObservableCollection<MacViewMessage> _messages = new ObservableCollection<MacViewMessage>();

        public event EventHandler<MacViewMessage> SendMessageToRemotePeer;
        private event EventHandler<MacViewMessage> MessageFromRemotePeer;
        public event EventHandler RemotePeerDisconnected;

        public void HandleRemotePeerDisconnected()
        {
            RemotePeerDisconnected?.Invoke(this, null);
        }

        public void HandleMessageFromPeer(string message)
        {
            MessageFromRemotePeer?.Invoke(this, new MacViewMessage(DateTime.Now, RemotePeer.Name , message));
        }

        private readonly HttpSignaler _httpSignaler;
        public HttpSignaler HttpSignaler => _httpSignaler;

        public ViewController(IntPtr handle) : base(handle)
        {
            var name = OrtcController.LocalPeer.Name;
            Debug.WriteLine($"Connecting to server from local peer: {name}");

            _httpSignaler =
                new HttpSignaler("peercc-server.ortclib.org", 8888, name);

            _httpSignaler.SignedIn += Signaler_SignedIn;
            _httpSignaler.ServerConnectionFailed += Signaler_ServerConnectionFailed;
            _httpSignaler.PeerConnected += Signaler_PeerConnected;
            _httpSignaler.PeerDisconnected += Signaler_PeerDisconnected;
            _httpSignaler.MessageFromPeer += Signaler_MessageFromPeer;
        }

        private void Signaler_SignedIn(object sender, EventArgs e)
        {
            // The signaler will notify all events from the signaler
            // task thread. To prevent concurrency issues, ensure all
            // notifications from this thread are asynchronously
            // forwarded back to the GUI thread for further processing.
            this.BeginInvokeOnMainThread(() => HandleSignedIn(sender, e));
        }

        private void HandleSignedIn(object sender, EventArgs e)
        {
            Debug.WriteLine("Peer signed in to server.");
        }

        private void Signaler_ServerConnectionFailed(object sender, EventArgs e)
        {
            // See method Signaler_SignedIn for concurrency comments.
            this.BeginInvokeOnMainThread(() => HandleServerConnectionFailed(sender, e));
        }

        private void HandleServerConnectionFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Server connection failure.");
        }

        private void Signaler_PeerConnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            this.BeginInvokeOnMainThread(() => HandlePeerConnected(sender, peer));
        }

        private void HandlePeerConnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer connected {peer.Name} / {peer.Id}");

            var DataSource = new PeersTableDataSource();
            HttpSignaler._peers.ToList().ForEach(i => DataSource.Peers.Add(new MacViewPeer(i.Id.ToString(), i.Name)));

            // Populate the Peers Table
            PeersTable.DataSource = DataSource;
            PeersTable.Delegate = new PeersTableDelegate(DataSource);
        }

        private void Signaler_PeerDisconnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            this.BeginInvokeOnMainThread(() => HandlePeerDisconnected(sender, peer));
        }

        private void HandlePeerDisconnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer disconnected {peer.Name} / {peer.Id}");
        }

        private void Signaler_MessageFromPeer(object sender, HttpSignalerMessageEvent @event)
        {
            var complete = new ManualResetEvent(false);

            // Exactly like the case of Signaler_SignedIn, this event is fired
            // from the signaler task thread and like the other events,
            // the message must be processed on the GUI thread for concurrency
            // reasons. Unlike the connect / disconnect notifications these
            // events must be processed exactly one at a time and the next
            // message from the server should be held back until the current
            // message is fully processed.
            this.BeginInvokeOnMainThread(() => 
            {
                HandleMessageFromPeer(sender, @event).ContinueWith((antecedent) => 
                {
                    Debug.WriteLine("Message from peer handled: " + @event.Message);
                    complete.Set();
                });
            });
        }

        private async Task HandleMessageFromPeer(object sender, HttpSignalerMessageEvent @event)
        {
            var message = @event.Message;
            var peer = @event.Peer;

            if (message.StartsWith("OpenDataChannel", StringComparison.Ordinal)) 
            {
                Debug.WriteLine("contains OpenDataChannel");
                await SetupPeer(peer, false);
            }
        }

        private async Task SetupPeer(Peer remotePeer, bool isInitiator)
        {
            var ortcController = new OrtcController(remotePeer, isInitiator);

            ortcController.DataChannelConnected += OrtcController_OnDataChannelConnected;
            ortcController.DataChannelDisconnected += OrtcController_OnDataChannelDisconnected;
            ortcController.SignalMessageToPeer += OrtcController_OnSignalMessageToPeer;
            ortcController.DataChannelMessage += OrtcController_OnDataChannelMessage;

            await ortcController.SetupAsync();
        }

        private void OrtcController_OnDataChannelMessage(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Message from remote peer {signaler.RemotePeer.Id}: " + message);

            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);

            HandleMessageFromPeer(message);
        }

        private void OrtcController_OnSignalMessageToPeer(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Send message to remote peer {signaler.RemotePeer.Id}: " + message);
            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);
        }

        private void OrtcController_OnDataChannelDisconnected(object sender, EventArgs message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Remote peer disconnected: {signaler.RemotePeer.Id}");

            HandleRemotePeerDisconnected();
        }

        private void OrtcController_OnDataChannelConnected(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Do any additional setup after loading the view.
        }

        public override NSObject RepresentedObject
        {
            get
            {
                return base.RepresentedObject;
            }
            set
            {
                base.RepresentedObject = value;
                // Update the view, if already loaded.
            }
        }

		async partial void ConnectButtonClicked(NSObject sender)
		{
            await HttpSignaler.Connect();
		}

		async partial void DisconnectButtonClicked(NSObject sender)
		{
            await _httpSignaler.SignOut();
		}

		partial void SendMessageButtonClicked(NSObject sender)
		{
            var messageText = MessageTextField.StringValue;

            if (messageText != string.Empty) 
            {
                var message = new MacViewMessage("Local peer: ", messageText);
                _messages.Add(message);

                var DataSource = new ChatTableDataSource();
                _messages.ToList().ForEach(i => DataSource.Messages.Add(new MacViewMessage(i.Author, i.Text)));

                // Populate the Chat Table 
                ChatTable.DataSource = DataSource;
                ChatTable.Delegate = new ChatTableDelegate(DataSource);

                OnSendMessageToRemotePeer(message);
            }

            MessageTextField.StringValue = String.Empty;
		}

		private void OnSendMessageToRemotePeer(MacViewMessage message)
        {
            SendMessageToRemotePeer?.Invoke(this, message);
        }

        partial void ChatButtonClicked(NSObject sender)
		{
            var x = PeersTable.SelectedRow;
            if (x > -1) {
                var p = HttpSignaler._peers[(int)x];
                _httpSignaler.SendToPeer(p.Id, "OpenDataChannel");
            }
		}

		public override void AwakeFromNib()
		{
            base.AwakeFromNib();
		}
	}
}
