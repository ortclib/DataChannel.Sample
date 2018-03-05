using System;
using System.Linq;
using System.Threading.Tasks;
using AppKit;
using DataChannelOrtc.Mac.Signaling;
using Foundation;

namespace DataChannelOrtc.Mac
{
    public partial class ViewController : NSViewController
    {
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

		partial void ChatButtonClicked(NSObject sender)
		{
            throw new NotImplementedException();
		}

		public override void AwakeFromNib()
		{
            base.AwakeFromNib();
		}
	}
}
