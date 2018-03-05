// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace DataChannelOrtc.Mac
{
	[Register ("ViewController")]
	partial class ViewController
	{
		[Outlet]
		AppKit.NSTableColumn PeerId { get; set; }

		[Outlet]
		AppKit.NSTableColumn PeerName { get; set; }

		[Outlet]
		AppKit.NSTableView PeersTable { get; set; }

		[Action ("ChatButtonClicked:")]
		partial void ChatButtonClicked (Foundation.NSObject sender);

		[Action ("ConnectButtonClicked:")]
		partial void ConnectButtonClicked (Foundation.NSObject sender);

		[Action ("DisconnectButtonClicked:")]
		partial void DisconnectButtonClicked (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (PeerId != null) {
				PeerId.Dispose ();
				PeerId = null;
			}

			if (PeerName != null) {
				PeerName.Dispose ();
				PeerName = null;
			}

			if (PeersTable != null) {
				PeersTable.Dispose ();
				PeersTable = null;
			}
		}
	}
}
