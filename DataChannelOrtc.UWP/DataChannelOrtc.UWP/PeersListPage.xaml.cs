using DataChannelOrtc.Signaling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DataChannelOrtc.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PeersListPage : Page
    {
        private readonly HttpSignaler _httpSignaler;
        private OrtcController _ortcController;

        public Peer RemotePeer { get; set; }
        public static ObservableCollection<Message> _messages = new ObservableCollection<Message>();

        public event EventHandler RemotePeerConnected;
        public event EventHandler RemotePeerDisconnected;
        public event EventHandler<Message> SendMessageToRemotePeer;
        private event EventHandler<Message> MessageFromRemotePeer;

        public void HandleRemotePeerConnected()
        {
            RemotePeerConnected?.Invoke(this, null);
        }

        public void HandleRemotePeerDisconnected()
        {
            RemotePeerDisconnected?.Invoke(this, null);
        }

        public void HandleMessageFromPeer(Peer remotePeer, string message)
        {
            Debug.WriteLine("HandleMessageFromPeer!");

            MessageFromRemotePeer?.Invoke(this, new Message(RemotePeer, DateTime.Now, message));
            _messages.Add(new Message(remotePeer, DateTime.Now, message));
        }

        private bool _isSendReady = false;
        public bool IsSendReady
        {
            get { return _isSendReady; }
            set { _isSendReady = value; }
        }

        public PeersListPage()
        {
            InitializeComponent();

            ApplicationView.PreferredLaunchViewSize = new Size(500, 800);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

            string name = OrtcController.LocalPeer.Name;
            Debug.WriteLine($"Connecting to server from local peer: {name}");

            peersListView.SelectedIndex = -1;
            peersListView.SelectedItem = null;

            _httpSignaler = new HttpSignaler();

            _httpSignaler.SignedIn += Signaler_SignedIn;
            _httpSignaler.ServerConnectionFailed += Signaler_ServerConnectionFailed;
            _httpSignaler.PeerConnected += Signaler_PeerConnected;
            _httpSignaler.PeerDisconnected += Signaler_PeerDisconnected;
            _httpSignaler.MessageFromPeer += Signaler_MessageFromPeer;

            RemotePeerConnected += Signaler_RemoteConnected;
            RemotePeerDisconnected += Signaler_RemoteDisconnected;
            MessageFromRemotePeer += Signaler_MessageFromRemotePeer;

            peersListView.Tapped += PeersListView_Tapped;

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
            _messages.Add(message);
        }

        private void PeersListView_Tapped(object sender, TappedRoutedEventArgs e) =>
            peersListView.SelectedItem = (Peer)((FrameworkElement)e.OriginalSource).DataContext;

        private async void Signaler_SignedIn(object sender, EventArgs e)
        {
            // The signaler will notify all events from the signaler
            // task thread. To prevent concurrency issues, ensure all
            // notifications from this thread are asynchronously
            // forwarded back to the GUI thread for further processing.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HandleSignedIn(sender, e));
        }

        private void HandleSignedIn(object sender, EventArgs e)
        {
            Debug.WriteLine("Peer signed in to server.");
        }

        private async void Signaler_ServerConnectionFailed(object sender, EventArgs e)
        {
            // See method Signaler_SignedIn for concurrency comments.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HandleServerConnectionFailed(sender, e));

        }

        private void HandleServerConnectionFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Server connection failure.");
        }

        private async void Signaler_PeerConnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HandlePeerConnected(sender, peer));
        }

        private void HandlePeerConnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer connected {peer.Name} / {peer.Id}");

            if (peersListView.Items.Contains(peer))
            {
                Debug.WriteLine($"Peer already found in list: {peer.ToString()}");
                return;
            }

            if (OrtcController.LocalPeer.Name == peer.Name)
            {
                Debug.WriteLine($"Peer is our local peer: {peer.ToString()}");
                return;
            }
            peersListView.Items.Add(peer);
        }

        private async void Signaler_PeerDisconnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HandlePeerDisconnected(sender, peer));

        }

        private void HandlePeerDisconnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer disconnected {peer.Name} / {peer.Id}");

            for (int i = 0; i < peersListView.Items.Count(); i++)
            {
                Peer p = (Peer)peersListView.Items[i];
                if (p.Name == peer.Name)
                    peersListView.Items.Remove(peersListView.Items[i]);
            }
        }

        private async void Signaler_MessageFromPeer(object sender, HttpSignalerMessageEvent @event)
        {
            var complete = new ManualResetEvent(false);

            // Exactly like the case of Signaler_SignedIn, this event is fired
            // from the signaler task thread and like the other events,
            // the message must be processed on the GUI thread for concurrency
            // reasons. Unlike the connect / disconnect notifications these
            // events must be processed exactly one at a time and the next
            // message from the server should be held back until the current
            // message is fully processed.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                    Debug.WriteLine($"Message from peer handled: {@event.Message}");
                    complete.Set();
                });
            });
            // By waiting on the async result the signaler's task is blocked
            // from processing the next signaler message until the current
            // message is fully processed. The signaler thread is allowed to
            // be blocked because events are never fired on the signaler's
            // tasks dispatchers.
            //
            // While .BeginInvoke() does ensure that each message is processed
            // by the GUI thread in the order the message is sent to the GUI
            // thread, it does not ensure the message is entirely processed
            // before the next message directed to the GUI thread is processed.
            // The moment an async/awaitable task happens on the GUI thread
            // the next message in the GUI queue is allowed to be processed.
            // Tasks and related methods cause the GUI thread to become
            // re-entrant to processing more messages whenever an async
            // related routine is called.
            complete.WaitOne();
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
            _ortcController.HandleMessageFromPeer(message);
        }

        private void InitView()
        {
            listMessages.ItemsSource = _messages;

            ConnectPeer.Name = " Connect ";
            DisconnectPeer.Name = "Disconnect";

            ConnectPeer.VerticalAlignment = VerticalAlignment.Top;
            ConnectPeer.HorizontalAlignment = HorizontalAlignment.Left;

            DisconnectPeer.VerticalAlignment = VerticalAlignment.Top;
            DisconnectPeer.HorizontalAlignment = HorizontalAlignment.Right;

            peersListView.VerticalAlignment = VerticalAlignment.Top;
            peersListView.HorizontalAlignment = HorizontalAlignment.Center;

            listMessages.VerticalAlignment = VerticalAlignment.Bottom;
            listMessages.HorizontalAlignment = HorizontalAlignment.Center;

            btnChat.VerticalAlignment = VerticalAlignment.Center;
            btnChat.HorizontalAlignment = HorizontalAlignment.Right;

            ConnectPeer.Click += async (sender, args) =>
            {
                Debug.WriteLine("Connects to server.");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await _httpSignaler.Connect());

                ConnectPeer.IsEnabled = false;
                //ConnectPeer.BackgroundColor = Color.DarkGray;
                DisconnectPeer.IsEnabled = true;
                //DisconnectPeer.BackgroundColor = Color.Gray;
            };

            DisconnectPeer.Click += async (sender, args) =>
            {
                Debug.WriteLine("Disconnects from server.");

                peersListView.Items.Clear();

                await _httpSignaler.SignOut();

                DisconnectPeer.IsEnabled = false;
                //DisconnectPeer.BackgroundColor = Color.DarkGray;
                ConnectPeer.IsEnabled = true;
                //ConnectPeer.BackgroundColor = Color.Gray;
            };

            btnChat.Click += async (sender, args) =>
            {
                if (peersListView.SelectedIndex == -1)
                {
                    await new MessageDialog("Please select a peer.").ShowAsync();
                    return;
                }

                Peer remotePeer = peersListView.SelectedItem as Peer;
                if (remotePeer == null) return;

                _httpSignaler.SendToPeer(remotePeer.Id, "OpenDataChannel");
                await SetupPeer(remotePeer, true);
            };

            btnSend.Click += (sender, args) =>
            {
                if (!IsSendReady)
                {
                    Debug.WriteLine("Please wait, connecting...");
                    return;
                }

                Message message = new Message(OrtcController.LocalPeer, DateTime.Now, "text message");

                _messages.Add(message);

                OnSendMessageToRemotePeer(message);
            };

        }

        private void OnSendMessageToRemotePeer(Message message)
        {
            SendMessageToRemotePeer?.Invoke(this, message);
        }

        private async Task SetupPeer(Peer remotePeer, bool isInitiator)
        {
            SendMessageToRemotePeer += PeersListPage_SendMessageToRemotePeer;

            _ortcController = new OrtcController(remotePeer, isInitiator);

            _ortcController.DataChannelConnected += OrtcController_OnDataChannelConnected;
            _ortcController.DataChannelDisconnected += OrtcController_OnDataChannelDisconnected;
            _ortcController.SignalMessageToPeer += OrtcController_OnSignalMessageToPeer;
            _ortcController.DataChannelMessage += OrtcController_OnDataChannelMessage;

            await _ortcController.SetupAsync();
        }

        private void PeersListPage_SendMessageToRemotePeer(object sender, Message message)
        {
            Debug.WriteLine($"Send message to remote peer : {message.MessageText}");

            _ortcController.HandleSendMessageViaDataChannel(message.MessageText);
        }

        private async void OrtcController_OnDataChannelMessage(object sender, string message)
        {
            OrtcController ortcc = (OrtcController)sender;
            Debug.WriteLine($"Message from remote peer {ortcc.RemotePeer.Id}: {message}");

            _httpSignaler.SendToPeer(ortcc.RemotePeer.Id, message);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => HandleMessageFromPeer(ortcc.RemotePeer, message));
        }

        private void OrtcController_OnSignalMessageToPeer(object sender, string message)
        {
            OrtcController ortcc = (OrtcController)sender;
            Debug.WriteLine($"Send message to remote peer {ortcc.RemotePeer.Id}: {message}");

            _httpSignaler.SendToPeer(ortcc.RemotePeer.Id, message);
        }

        private void OrtcController_OnDataChannelDisconnected(object sender, EventArgs e)
        {
            OrtcController ortcc = (OrtcController)sender;
            Debug.WriteLine($"Remote peer disconnected: {ortcc.RemotePeer.Id}");

            HandleRemotePeerDisconnected();
        }

        private void OrtcController_OnDataChannelConnected(object sender, EventArgs e)
        {
            OrtcController ortcc = (OrtcController)sender;
            Debug.WriteLine($"Remote peer connected: {ortcc.RemotePeer.Id}");

            HandleRemotePeerConnected();
        }
    }
}
