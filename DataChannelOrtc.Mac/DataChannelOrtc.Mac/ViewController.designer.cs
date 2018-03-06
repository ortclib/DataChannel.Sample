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
        AppKit.NSTableColumn ChatMessageAuthor { get; set; }

        [Outlet]
        AppKit.NSTableColumn ChatMessageText { get; set; }

        [Outlet]
        AppKit.NSTableView ChatTable { get; set; }

        [Outlet]
        AppKit.NSTextField MessageTextField { get; set; }

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

        [Action ("SendMessageButtonClicked:")]
        partial void SendMessageButtonClicked (Foundation.NSObject sender);
        
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

            if (ChatTable != null) {
                ChatTable.Dispose ();
                ChatTable = null;
            }

            if (ChatMessageAuthor != null) {
                ChatMessageAuthor.Dispose ();
                ChatMessageAuthor = null;
            }

            if (ChatMessageText != null) {
                ChatMessageText.Dispose ();
                ChatMessageText = null;
            }

            if (MessageTextField != null) {
                MessageTextField.Dispose ();
                MessageTextField = null;
            }
        }
    }
}
