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

        Dictionary<int, Tuple<OrtcController, ChatPage>> _chatSessions =
            new Dictionary<int, Tuple<OrtcController, ChatPage>>();

        public static ObservableCollection<Message> _messages = new ObservableCollection<Message>();

        public PeersListPage()
        {
            InitializeComponent();

            ApplicationView.PreferredLaunchViewSize = new Size(500, 400);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            //ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(300, 450));

            peersListView.SelectedIndex = -1;
            peersListView.SelectedItem = null;

            _httpSignaler = new HttpSignaler();

            _httpSignaler.SignedIn += Signaler_SignedIn;
            _httpSignaler.ServerConnectionFailed += Signaler_ServerConnectionFailed;
            _httpSignaler.PeerConnected += Signaler_PeerConnected;
            _httpSignaler.PeerDisconnected += Signaler_PeerDisconnected;
            _httpSignaler.MessageFromPeer += Signaler_MessageFromPeer;

            peersListView.Tapped += PeersListView_Tapped;

            InitView();
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
                Debug.WriteLine($"[WARNING] No peer found to direct remote message: {peer.Id} / {message}");
                return;
            }
            tuple.Item1.HandleMessageFromPeer(message);
        }

        private async void InitView()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ConnectPeer.Name = " Connect ";
                DisconnectPeer.Name = "Disconnect";

                ConnectPeer.VerticalAlignment = VerticalAlignment.Top;
                ConnectPeer.HorizontalAlignment = HorizontalAlignment.Left;

                DisconnectPeer.VerticalAlignment = VerticalAlignment.Top;
                DisconnectPeer.HorizontalAlignment = HorizontalAlignment.Right;

                peersListView.VerticalAlignment = VerticalAlignment.Center;
                peersListView.HorizontalAlignment = HorizontalAlignment.Center;

                btnChat.VerticalAlignment = VerticalAlignment.Bottom;
                btnChat.HorizontalAlignment = HorizontalAlignment.Center;

                ConnectPeer.Click += async (sender, args) =>
                {
                    Debug.WriteLine("Connects to server.");
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => await _httpSignaler.Connect()); 

                    //ConnectPeer.IsEnabled = false;
                    //ConnectPeer.BackgroundColor = Color.DarkGray;
                    //DisconnectPeer.IsEnabled = true;
                    //DisconnectPeer.BackgroundColor = Color.Gray;
                };

                DisconnectPeer.Click += async (sender, args) =>
                {
                    Debug.WriteLine("Disconnects from server.");

                    peersListView.Items.Clear();

                    await _httpSignaler.SignOut();

                    //DisconnectPeer.IsEnabled = false;
                    //DisconnectPeer.BackgroundColor = Color.DarkGray;
                    //ConnectPeer.IsEnabled = true;
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
            });
        }

        private async Task SetupPeer(Peer remotePeer, bool isInitiator)
        {
            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(remotePeer.Id, out tuple))
            {
                // Already have a page created
                tuple.Item2.HandleRemotePeerDisconnected();
                tuple.Item1.Dispose();
                _chatSessions.Remove(remotePeer.Id);
            }
            else
            {
                // No chat page created, create a new one
                tuple = new Tuple<OrtcController, ChatPage>(null, new ChatPage(OrtcController.LocalPeer, remotePeer));

                tuple.Item2.SendMessageToRemotePeer += ChatPage_SendMessageToRemotePeer;
            }

            ChatPageParams parameters = new ChatPageParams();
            parameters.LocalPeer = OrtcController.LocalPeer;
            parameters.RemotePeer = remotePeer;



            //Frame.Navigate(typeof(ChatPage), parameters);
            //var viewId = 0;
            await tuple.Item2.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    var frame = new Frame();
                    frame.Navigate(typeof(ChatPage), parameters);
                    Window.Current.Content = frame;

                    //viewId = ApplicationView.GetForCurrentView().Id;

                    Window.Current.Activate();
                });

            //await ApplicationViewSwitcher.TryShowAsStandaloneAsync(viewId);

            // Create a new tuple and carry forward the chat page from the previous tuple
            tuple = new Tuple<OrtcController, ChatPage>(new OrtcController(remotePeer, isInitiator), tuple.Item2);
            _chatSessions.Add(remotePeer.Id, tuple);

            tuple.Item1.DataChannelConnected += OrtcSignaler_OnDataChannelConnected;
            tuple.Item1.DataChannelDisconnected += OrtcSignaler_OnDataChannelDisconnected;
            tuple.Item1.SignalMessageToPeer += OrtcSignaler_OnSignalMessageToPeer;
            tuple.Item1.DataChannelMessage += OrtcSignaler_OnDataChannelMessage;

            await tuple.Item1.SetupAsync();
        }

        private async void OrtcSignaler_OnDataChannelMessage(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Message from remote peer {signaler.RemotePeer.Id}: {message}");

            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => tuple.Item2.HandleMessageFromPeer(signaler.RemotePeer, message));
            }
        }

        private void OrtcSignaler_OnSignalMessageToPeer(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Send message to remote peer {signaler.RemotePeer.Id}: {message}");

            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);
        }

        private void OrtcSignaler_OnDataChannelDisconnected(object sender, EventArgs e)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Remote peer disconnected: {signaler.RemotePeer.Id}");

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                tuple.Item2.HandleRemotePeerDisconnected();
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
            Debug.WriteLine($"Send message to remote peer {message.Recipient.Id}: {message.MessageText}");

            Tuple<OrtcController, ChatPage> tuple;
            if (_chatSessions.TryGetValue(message.Recipient.Id, out tuple))
            {
                tuple.Item1.HandleSendMessageViaDataChannel(message.MessageText);
            }
        }
    }

    public class ChatPageParams
    {
        public Peer LocalPeer { get; set; }
        public Peer RemotePeer { get; set; }
    }
}
