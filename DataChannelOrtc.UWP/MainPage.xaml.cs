using DataChannelOrtc.Signaling;
using Org.Ortc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
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
    public sealed partial class MainPage : Page
    {
        private readonly HttpSignaler _httpSignaler;
        private readonly Dictionary<int, Tuple<OrtcController, List<string>>> _chatSessions = new Dictionary<int, Tuple<OrtcController, List<string>>>();

        public ObservableCollection<Peer> Peers { get; private set; } = new ObservableCollection<Peer>();
        public Peer SelectedPeer { get; set; }

        public ObservableCollection<string> Messages { get; set; } = new ObservableCollection<string>();

        public MainPage()
        {
            this.InitializeComponent();

            RTCIceGatherer gatherer;
            gatherer = new RTCIceGatherer(new RTCIceGatherOptions());

            _httpSignaler = new HttpSignaler();

            _httpSignaler.SignedIn += Signaler_SignedIn;
            _httpSignaler.ServerConnectionFailed += Signaler_ServerConnectionFailed;
            _httpSignaler.PeerConnected += Signaler_PeerConnected;
            _httpSignaler.PeerDisconnected += Signaler_PeerDisconnected;
            _httpSignaler.MessageFromPeer += Signaler_MessageFromPeer;
        }

        private void Signaler_SignedIn(object sender, EventArgs e)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { Debug.WriteLine("Peer signed in to server"); });
        }

        private void Signaler_ServerConnectionFailed(object sender, EventArgs e)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { Debug.WriteLine("Server connection failed"); });
        }

        private void Signaler_PeerConnected(object sender, Peer peer)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Debug.WriteLine($"Peer connected {peer.Name}");

                Peers.Add(peer);
            });
        }

        private void Signaler_PeerDisconnected(object sender, Peer peer)
        {
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Debug.WriteLine($"Peer disconnected {peer.Name}");

                Peers.Remove(Peers.Where(p => p.Id == peer.Id).First());
            });
        }

        private void Signaler_MessageFromPeer(object sender, HttpSignalerMessageEvent e)
        {
            throw new NotImplementedException();
        }

        private async void uxConnect_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Connecting to signaling server.");

            await _httpSignaler.Connect();

            uxConnect.IsEnabled = false;
            uxDisconnect.IsEnabled = true;
        }

        private async void uxDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Disconnecting from signaling server.");

            await _httpSignaler.SignOut();

            Peers.Clear();
            uxDisconnect.IsEnabled = false;
            uxConnect.IsEnabled = true;
        }

        private async void uxChat_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPeer == null)
            {
                var dialog = new MessageDialog("Please select a peer.");
                await dialog.ShowAsync();
                return;
            }

            _httpSignaler.SendToPeer(SelectedPeer.Id, "OpenDataChannel");
            await SetupPeer(SelectedPeer, true);
        }

        private async Task SetupPeer(Peer remotePeer, bool isInitiator)
        {
            //var found = lstPeers.FindString(remotePeer.ToString());
            //if (ListBox.NoMatches != found)
            //{
            //    remotePeer = Peer.CreateFromString(lstPeers.GetItemText(lstPeers.Items[found]));
            //}

            Tuple<OrtcController, List<string>> tuple;
            if (_chatSessions.TryGetValue(remotePeer.Id, out tuple))
            {
                // already have a form created
                //tuple.Item2.HandleRemotePeerDisconnected();
                //tuple.Item2.BringToFront();
                //tuple.Item1.Dispose();
                //_chatSessions.Remove(remotePeer.Id);
            }
            else
            {
                //if (!Properties.Settings.Default.MultipleConnections)
                //{
                //    if (_chatSessions.Count > 0)
                //    {
                //        // Clear out existing chat forms as only one chat form
                //        // is allowed.
                //        DisposeChatForms();
                //    }
                //}

                // no chat form created, spawn a new one
                tuple = new Tuple<OrtcController, List<string>>(null, new List<string>());

                //tuple.Item2.SendMessageToRemotePeer += ChatForm_SendMessageToRemotePeer;

                // Invoking .ShowDialog() would block the signaler's task
                // being waited upon. Show() block the current task
                // from continuing until the dialog is dismissed but does
                // not block other events from processing on the GUI
                // thread. Not blocking events is insufficient though. The
                // task is waited by the signaler to complete before the
                // next signaler message from the server is allowed to be
                // processed so a dialog cannot be spawned from within the
                // processing of a peer's message task.
                //
                // The solution though to the blocking of the signaler's
                // task when bringing up a dialog is rather simple: invoke
                // the .ShowDialog() method asynchronously on the GUI
                // thread and not from within the current signaler message
                // task. This allows the GUI to be displayed and events are
                // processed as normal including other signaler messages
                // from peers.
                //BeginInvoke((Action)(() =>
                //{
                //    if (Properties.Settings.Default.MultipleConnections)
                //    {
                //        // show the form non model
                //        tuple.Item2.Show();
                //    }
                //    else
                //    {
                //        // invoke this on the main thread without blocking the
                //        // current thread from continuing
                //        tuple.Item2.ShowDialog();
                //    }
                //}));
            }

            // create a new tuple and carry forward the chat form from the previous tuple
            tuple = new Tuple<OrtcController, List<string>>(new OrtcController(remotePeer, isInitiator), tuple.Item2);
            _chatSessions.Add(remotePeer.Id, tuple);

            tuple.Item1.DataChannelConnected += OrtcSignaler_OnDataChannelConnected;
            tuple.Item1.DataChannelDisconnected += OrtcSignaler_OnDataChannelDisconnected;
            tuple.Item1.SignalMessageToPeer += OrtcSignaler_OnSignalMessageToPeer;
            tuple.Item1.DataChannelMessage += OrtcSignaler_OnDataChannelMessage;

            await tuple.Item1.SetupAsync();
        }

        private void OrtcSignaler_OnDataChannelConnected(object sender, EventArgs e)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Remote peer connected: {signaler.RemotePeer.Id}");

            Tuple<OrtcController, List<string>> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                //tuple.Item2.HandleRemotePeerConnected();
            }
        }

        private void OrtcSignaler_OnDataChannelDisconnected(object sender, EventArgs e)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Remote peer disconnected: {signaler.RemotePeer.Id}");

            Tuple<OrtcController, List<string>> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                //tuple.Item2.HandleRemotePeerDisconnected();
            }
        }

        private void OrtcSignaler_OnSignalMessageToPeer(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Send message to remote peer {signaler.RemotePeer.Id}: {message}");

            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);
        }

        private void OrtcSignaler_OnDataChannelMessage(object sender, string message)
        {
            OrtcController signaler = (OrtcController)sender;
            Debug.WriteLine($"Message from remote peer {signaler.RemotePeer.Id}: {message}");

            _httpSignaler.SendToPeer(signaler.RemotePeer.Id, message);

            Tuple<OrtcController, List<string>> tuple;
            if (_chatSessions.TryGetValue(signaler.RemotePeer.Id, out tuple))
            {
                //tuple.Item2.HandleMessageFromPeer(message);
            }
        }
    }
}
