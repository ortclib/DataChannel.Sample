using DataChannelOrtc.Signaling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DataChannelOrtc
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PeersListPage : ContentPage
    {
        private readonly HttpSignaler _httpSignaler;
        public HttpSignaler HttpSignaler => _httpSignaler;

        Dictionary<int, Tuple<OrtcController, ChatPage>> _chatSessions = new Dictionary<int, Tuple<OrtcController, ChatPage>>();

        static PeersListPage()
        {
        }

        public PeersListPage()
        {
            InitializeComponent();

            var name = OrtcController.LocalPeer.Name;
            Debug.WriteLine($"Connecting to server from local peer: {name}");

            _httpSignaler =
                new HttpSignaler("peercc-server.ortclib.org", 8888, name);

            _httpSignaler.SignedIn += Signaler_SignedIn;
            _httpSignaler.ServerConnectionFailed += Signaler_ServerConnectionFailed;
            _httpSignaler.PeerConnected += Signaler_PeerConnected;
            _httpSignaler.PeerDisconnected += Signaler_PeerDisconnected;
            _httpSignaler.MessageFromPeer += Signaler_MessageFromPeer;

            InitView();
        }

        private void Signaler_SignedIn(object sender, EventArgs e)
        {
            // The signaler will notify all events from the signaler
            // task thread. To prevent concurrency issues, ensure all
            // notifications from this thread are asynchronously
            // forwarded back to the GUI thread for further processing.
            Device.BeginInvokeOnMainThread(() => HandleSignedIn(sender, e));
        }

        private void HandleSignedIn(object sender, EventArgs e)
        {
            Debug.WriteLine("Peer signed in to server.");
        }

        private void Signaler_ServerConnectionFailed(object sender, EventArgs e)
        {
            // See method Signaler_SignedIn for concurrency comments.
            Device.BeginInvokeOnMainThread(() => HandleServerConnectionFailed(sender, e));
        }

        private void HandleServerConnectionFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Server connection failure.");
        }

        private void Signaler_PeerConnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            Device.BeginInvokeOnMainThread(() => HandlePeerConnected(sender, peer));
        }

        private void HandlePeerConnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer connected {peer.Name} / {peer.Id}");
        }

        private void Signaler_PeerDisconnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            Device.BeginInvokeOnMainThread(() => HandlePeerDisconnected(sender, peer));
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
            Device.BeginInvokeOnMainThread(() =>
            {
                // Do not invoke a .Wait() on the task result of
                // HandleMessageFromPeer! While this might seem as a
                // reasonable solution to processing the entire event from the
                // GUI thread before processing the next message in the GUI
                // thread queue, a .Wait() would also block any and all
                // events targeted toward the GUI thread.
                //
                // This would actually create a deadlock in some cases. All
                // events from ORTC are fired at the GUI thread queue and the
                // creation of a RTCCertificate requires an event notification
                // to indicate when the RTCCertificate awaited task is
                // complete. Because .Wait() would block the GUI thread the
                // notification that the RTCCertificate is completed would 
                // never fire and the RTCCertificate generation task result
                // would never complete which the .Wait() is waiting upon
                // to complete.
                //
                // Bottom line, don't block the GUI thread using a .Wait()
                // on a task from the GUI thread - ever!
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

            if (message.StartsWith("OpenDataChannel"))
            {
                Debug.WriteLine("contains OpenDataChannel");
                await SetupPeer(peer, false);
            }

            Tuple<OrtcController, ChatPage> tuple;
            if (!_chatSessions.TryGetValue(peer.Id, out tuple))
            {
                Debug.WriteLine($"[WARNING] No peer found to direct remote message: {peer.Id} / " + message);
                return;
            }

            await tuple.Item1.HandleMessageFromPeer(message);
        }

        private void InitView()
        {
            peersListView.ItemsSource = HttpSignaler._peers;

            // Page structure
            Content = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        HorizontalOptions = LayoutOptions.CenterAndExpand,
                        Children =
                        {
                            ConnectPeer,
                            DisconnectPeer
                        }
                    },
                    peersListView,
                    btnChat
                }
            };

            ConnectPeer.Clicked += async (sender, args) =>
            {
                Debug.WriteLine("Connects to server!");

                await _httpSignaler.Connect();

                ConnectPeer.IsEnabled = false;
                DisconnectPeer.IsEnabled = true;
            };

            DisconnectPeer.Clicked += async (sender, args) =>
            {
                Debug.WriteLine("Disconnects from server!");

                await _httpSignaler.SignOut();

                DisconnectPeer.IsEnabled = false;
                ConnectPeer.IsEnabled = true;
            };

            btnChat.Clicked += async (sender, args) =>
            {
                Peer remotePeer = peersListView.SelectedItem as Peer;
                if (remotePeer == null)
                    return;

                _httpSignaler.SendToPeer(remotePeer.Id, "OpenDataChannel");

                await SetupPeer(remotePeer, true);
            };
        }

        private async Task SetupPeer(Peer remotePeer, bool isInitiator)
        {
            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(remotePeer.Id, out tuple))
            {
                // Already have a page created
                tuple.Item2.HandleRemotePeerDisonnected();
                tuple.Item1.Dispose();
                _chatSessions.Remove(remotePeer.Id);
            }
            else
            {
                // No chat page created, create a new one
                tuple = new Tuple<OrtcController, ChatPage>(null, new ChatPage(OrtcController.LocalPeer, remotePeer));

                tuple.Item2.SendMessageToRemotePeer += ChatPage_SendMessageToRemotePeer;
            }

            Device.BeginInvokeOnMainThread(async () => await Navigation.PushAsync(tuple.Item2));
            
            // Create a new tuple and carry forward the chat page from the previous tuple
            tuple = new Tuple<OrtcController, ChatPage>(new OrtcController(remotePeer, isInitiator), tuple.Item2);
            _chatSessions.Add(remotePeer.Id, tuple);

            tuple.Item1.DataChannelConnected += OrtcSignaler_OnDataChannelConnected;
            tuple.Item1.DataChannelDisconnected += OrtcSignaler_OnDataChannelDisconnected;
            tuple.Item1.SignalMessageToPeer += OrtcSignaler_OnSignalMessageToPeer;
            tuple.Item1.DataChannelMessage += OrtcSignaler_OnDataChannelMessage;

            await tuple.Item1.SetupAsync();
        }

        private void OrtcSignaler_OnDataChannelMessage(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Message from remote peer {signaler.RemotePeer.Id}: " + message);

            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                tuple.Item2.HandleMessageFromPeer(message);
            }
        }

        private void OrtcSignaler_OnSignalMessageToPeer(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Send message to remote peer {signaler.RemotePeer.Id}: " + message);
            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message); 
        }

        private void OrtcSignaler_OnDataChannelDisconnected(object sender, EventArgs e)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Remote peer disconnected: {signaler.RemotePeer.Id}");

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                tuple.Item2.HandleRemotePeerDisonnected();
            }
        }

        private void OrtcSignaler_OnDataChannelConnected(object sender, EventArgs e)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Remote peer connected: {signaler.RemotePeer.Id}");

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                tuple.Item2.HandleRemotePeerConnected();
            }
        }

        private void ChatPage_SendMessageToRemotePeer(object sender, Message message)
        {
            ChatPage page = (ChatPage)sender;
            Debug.WriteLine($"Send message to remote peer {message.Recipient.Id}: " + message.Text);

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(message.Recipient.Id, out tuple))
            {
                tuple.Item1.HandleSendMessageViaDataChannel(message.Text);
            }
        }
    }
}