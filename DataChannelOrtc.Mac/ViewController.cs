using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AppKit;
using DataChannelOrtc.Mac.Signaling;
using Foundation;

namespace DataChannelOrtc.Mac
{
    public partial class ViewController : NSViewController
    {
        public static ObservableCollection<MacViewMessage> _messages = new ObservableCollection<MacViewMessage>();

        public event EventHandler<MacViewMessage> SendMessageToRemotePeer;

        private readonly HttpSignaler _httpSignaler;
        public HttpSignaler HttpSignaler => _httpSignaler;

        public ViewController(IntPtr handle) : base(handle)
        {
            var name = OrtcController.LocalPeer.Name;

            _httpSignaler =
                new HttpSignaler("peercc-server.ortclib.org", 8888, name);

            _httpSignaler.PeerConnected += Signaler_PeerConnected;
        }

        private void Signaler_PeerConnected(object sender, Peer peer)
        {
            var DataSource = new PeersTableDataSource();
            HttpSignaler._peers.ToList().ForEach(i => DataSource.Peers.Add(new MacViewPeer(i.Id.ToString(), i.Name)));

            // Populate the Peers Table
            PeersTable.DataSource = DataSource;
            PeersTable.Delegate = new PeersTableDelegate(DataSource);
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
