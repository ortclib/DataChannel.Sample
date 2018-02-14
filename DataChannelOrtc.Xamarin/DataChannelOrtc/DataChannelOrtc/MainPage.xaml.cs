using DataChannelOrtc.Signaling;
using Org.Ortc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace DataChannelOrtc
{
    public partial class MainPage : ContentPage
    {
        RTCIceGatherer _gatherer;
        RTCIceTransport _ice;   // Ice transport for the currently selected peer.
        RTCDtlsTransport _dtls;
        RTCSctpTransport _sctp;
        public static RTCDataChannel _dataChannel;    // Data channel for the currently selected peer.
        bool _isInitiator = true;      // True for the client that started the connection.
        public static bool _IsSendEnabled = false;

        private static readonly string _localPeerRandom = new Func<string>(() =>
        {
            Random random = new Random();   // WARNING: NOT cryptographically strong!
            const string chars = "abcdefghijklmnopqrstuvxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            const int length = 5;
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        })();

        RTCDataChannelParameters _dataChannelParams = new RTCDataChannelParameters
        {
            Label = "channel1",
            Negotiated = false,
            Ordered = true,
            Protocol = "ship"
        };

        private readonly HttpSignaler _httpSignaler;
        public HttpSignaler HttpSignaler => _httpSignaler;

        public static ObservableCollection<Message> _messages = new ObservableCollection<Message>();

        Dictionary<Peer, ChatPage> chatSession = new Dictionary<Peer, ChatPage>();

        private Peer _remotePeer;
        public Peer RemotePeer
        {
            get
            {
                if (_remotePeer == null)
                    _remotePeer = SelectedPeer;
                return _remotePeer;
            }
            set
            {
                if (_remotePeer == value)
                    return;
                _remotePeer = value;
            }
        }

        private Peer _selectedPeer;
        public Peer SelectedPeer
        {
            get { return _selectedPeer; }
            set
            {
                if (_selectedPeer == value)
                    return;

                var oldValue = _selectedPeer;
                _selectedPeer = value;
                SelectedPeerChanged(oldValue, value);
            }
        }

        public Peer LocalPeer
        {
            get
            {
                string hostname = IPGlobalProperties.GetIPGlobalProperties().HostName;

                // A random string is added to the peer name to easily filter
                // our local peer by name when the server re-announces the
                // local peer to itself. Thus two peers with the same hostname
                // will never be the same and running the application again
                // causes a slightly different peer name on the peer list to
                // distinguish a new peer from an old zombie peer still not
                // yet purged from the server.
                string peerName = (hostname != null ? hostname : "<unknown host>") + "-" + _localPeerRandom + "-data";

                return new Peer(-1, peerName);
            }
        }

        private string _message = string.Empty;
        public string Message
        {
            get { return _message; }
            set
            {
                if (_message == value)
                    return;

                _message = value;
            }
        }

        private void SelectedPeerChanged(Peer oldValue, Peer value)
        {
            if (_dataChannel != null)
            {
                _dataChannel.Close();
                _sctp.Stop();
                _dtls.Stop();
                _ice.Stop();
            }

            Message = string.Empty;
        }

        private async Task InitializeORTC()
        {
            var gatherOptions = new RTCIceGatherOptions()
            {
                IceServers = new List<RTCIceServer>()
                {
                    new RTCIceServer { Urls = new string[] { "stun.l.google.com:19302" } },
                    new RTCIceServer { Username = "redmond", Credential = "redmond123",
                    CredentialType = RTCIceCredentialType.Password,
                    Urls = new string[] { "turn:turn-testdrive.cloudapp.net:3478?transport=udp" } }
                }
            };

            _gatherer = new RTCIceGatherer(gatherOptions);

            _gatherer.OnStateChange += IceGatherer_OnStateChange;

            _gatherer.OnLocalCandidate += (@event) =>
            {
                _httpSignaler.SendToPeer(RemotePeer.Id, @event.Candidate.ToJson().ToString());
            };
            _gatherer.OnLocalCandidateComplete += (@event) =>
            {
                _httpSignaler.SendToPeer(RemotePeer.Id, @event.Candidate.ToJson().ToString());
            };

            var cert = await RTCCertificate.GenerateCertificate();

            _ice = new RTCIceTransport(_gatherer);
            _ice.OnStateChange += IceTransport_OnStateChange;

            _dtls = new RTCDtlsTransport(_ice, new RTCCertificate[] { cert });
            _dtls.OnStateChange += Dtls_OnStateChange;

            _sctp = new RTCSctpTransport(_dtls);

            _gatherer.Gather(null);
        }

        private void IceGatherer_OnStateChange(RTCIceGathererStateChangeEvent evt)
        {
            Debug.WriteLine("IceGatherer State Change: " + evt.State);
        }

        private void IceTransport_OnStateChange(RTCIceTransportStateChangeEvent evt)
        {
            Debug.WriteLine("IceTransport State Change: " + evt.State);
        }

        private void Dtls_OnStateChange(RTCDtlsTransportStateChangeEvent evt)
        {
            Debug.WriteLine("Dtls State Change: " + evt.State);
        }

        static MainPage()
        {
            OrtcLib.Setup();
            Settings.ApplyDefaults();
#if ENABLE_ORTCLIB_LOGGING
            Logger.InstallEventingListener("secret", 58888, TimeSpan.FromSeconds(60));
            Logger.SetLogLevel(Org.Ortc.Log.Component.All, Org.Ortc.Log.Level.Trace);
            Logger.SetLogLevel(Org.Ortc.Log.Component.Eventing, Org.Ortc.Log.Level.Basic);
            Logger.InstallDebuggerLogger();
#endif

        }

        public MainPage()
        {
            InitializeComponent();

            var name = LocalPeer.Name;
            Debug.WriteLine($"Connecting to server from local peer: {name}");

            _httpSignaler =
                new HttpSignaler("peercc-server.ortclib.org", 8888, name);

            _httpSignaler.SignedIn += Signaler_SignedIn;
            _httpSignaler.ServerConnectionFailed += Signaler_ServerConnectionFailed;
            _httpSignaler.PeerConnected += Signaler_PeerConnected;
            _httpSignaler.MessageFromPeer += Signaler_MessageFromPeer;

            InitView();
        }

        private void Signaler_SignedIn(object sender, EventArgs e)
        {
            // The signaler will notify all events from the signaler
            // task thread. To prevent concurrency issues, ensure all
            // notifications from this thread are asynchronously
            // forwarded back to the GUI thread for further processing.
            Device.BeginInvokeOnMainThread(() => { HandleSignedIn(sender, e); });
        }

        private void HandleSignedIn(object sender, EventArgs e)
        {
            Debug.WriteLine("Peer signed in to server.");
        }

        private void Signaler_ServerConnectionFailed(object sender, EventArgs e)
        {
            // See method Signaler_SignedIn for concurrency comments.
            Device.BeginInvokeOnMainThread(() => { HandleServerConnectionFailed(sender, e); });
        }

        private void HandleServerConnectionFailed(object sender, EventArgs e)
        {
            Debug.WriteLine("Server connection failure.");
        }

        private void Signaler_PeerConnected(object sender, Peer peer)
        {
            // See method Signaler_SignedIn for concurrency comments.
            Device.BeginInvokeOnMainThread(() => { HandlePeerConnected(sender, peer); });
        }

        private void HandlePeerConnected(object sender, Peer peer)
        {
            Debug.WriteLine($"Peer connected {peer.Name} / {peer.Id}");
            if (LocalPeer.Name == peer.Name)
            {
                Debug.WriteLine("Peer is our local peer: " + peer.ToString());
                return;
            }
        }

        private void Signaler_MessageFromPeer(object sender, Peer peer)
        {
            var complete = new ManualResetEvent(false);

            Device.BeginInvokeOnMainThread(() =>
            {
                HandleMessageFromPeer(sender, peer).ContinueWith((antecedent) =>
                {
                    Debug.WriteLine("Message from peer handled: " + peer.Message);
                    complete.Set();
                });
            });
            complete.WaitOne();
        }

        private async Task HandleMessageFromPeer(object sender, Peer peer)
        {
            var message = peer.Message;

            peer.Name = (HttpSignaler._peers.SingleOrDefault(p => 
                p.Id == HttpSignaler._peerId)).Name;

            Debug.WriteLine($"peer.Message {peer.Message}");
            Debug.WriteLine($"peer.Id: {HttpSignaler._peerId}");
            Debug.WriteLine($"peer.Name: {peer.Name}");

            if (message.StartsWith("OpenDataChannel"))
            {
                Debug.WriteLine("contains OpenDataChannel");

                await InitializeORTC();

                // A peer has let us know that they have begun initiating a
                // data channel.  In this scenario, we are the "remote" peer
                // so make sure _isInitiator is false. 
                _isInitiator = false;

                RemotePeer = peer;

                // Begin gathering ice candidates.
                OpenDataChannel(peer);

                if (!chatSession.ContainsKey(RemotePeer))
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        // invoke this on the main thread without blocking the current thread from continuing
                        ChatPage chatPage = new ChatPage(RemotePeer);
                        chatSession.Add(RemotePeer, chatPage);
                        await Navigation.PushAsync(chatPage);
                    });
                }
                    
                return;
            }

            if (message.StartsWith("{\"candidate"))
            {
                Debug.WriteLine("contains IceCandidate (or IceCandidateComplete)");

                Json jsonMessage = new Json(message);
                RTCIceGathererCandidate remoteCandidate = RTCIceGathererCandidate.Create(jsonMessage);
                _ice.AddRemoteCandidate(remoteCandidate);
                return;
            }
            if (message.StartsWith("{\"RTCIceParameters\":"))
            {
                Debug.WriteLine("contains IceParameters");

                Json jsonMessage = new Json(message);
                RTCIceParameters iceParameters = new RTCIceParameters(jsonMessage);

                // Start the ice transport with the appropriate role based on whether this is the initiator of the call.
                var role = _isInitiator ? RTCIceRole.Controlling : RTCIceRole.Controlled;
                _ice.Start(_gatherer, iceParameters, role);
                return;
            }
            if (message.StartsWith("{\"RTCDtlsParameters\":"))
            {
                Debug.WriteLine("contains DtlsParameters");

                Json jsonMessage = new Json(message);
                RTCDtlsParameters dtlsParameters = new RTCDtlsParameters(jsonMessage);

                _dtls.Start(dtlsParameters);
                Debug.WriteLine("Dtls start called.");
                return;
            }
            // this order guarantees:
            if (message.StartsWith("{\"RTCSctpCapabilities\":"))
            {
                Debug.WriteLine("contains SctpCapabilities");
                Json jsonMessage = new Json(message);
                // Message ordering: alice -> bob; bob.start(); bob -> alice; alice.start(); alice -> datachannel -> bob
                RTCSctpCapabilities sctpCaps = new RTCSctpCapabilities(jsonMessage);

                if (!_isInitiator)
                {
                    Debug.WriteLine("Receiver: Waiting for OnDataChannel event and starting sctp.");

                    // The remote side will receive notification when the data channel is opened.
                    _sctp.OnDataChannel += Sctp_OnDataChannel;
                    _sctp.OnStateChange += Sctp_OnStateChange;

                    _sctp.Start(sctpCaps);

                    RTCSctpCapabilities caps = RTCSctpTransport.GetCapabilities();

                    _httpSignaler.SendToPeer(RemotePeer.Id, caps.ToJson().ToString());
                }
                else
                {
                    // The initiator has received sctp caps back from the remote peer, 
                    // which means the remote peer has already called sctp.start(). 
                    // It's now safe to open a data channel, which will fire the 
                    // Sctp_OnDataChannel event on the remote side.
                    Debug.WriteLine("Initiator: Creating the data channel and starting sctp.");

                    _sctp.OnStateChange += Sctp_OnStateChange;
                    _sctp.Start(sctpCaps);

                    RTCDataTransport data = RTCDataTransport.Cast(_sctp);

                    _dataChannel = new RTCDataChannel(data, _dataChannelParams);
                    _dataChannel.OnMessage += DataChannel_OnMessage;
                    _dataChannel.OnError += DataChannel_OnError;
                    _dataChannel.OnStateChange += DataChannel_OnStateChange;
                }
            }
        }

        private void DataChannel_OnError(ErrorEvent evt)
        {
            Debug.WriteLine("DataChannel error: " + evt.Error);
        }

        private void DataChannel_OnMessage(RTCMessageEvent evt)
        {
            Debug.WriteLine("Datachannel message: " + evt.Text);
            _messages.Add(new Message(DateTime.Now.ToString("h:mm"), RemotePeer.Name + ": " + evt.Text));
            chatSession[RemotePeer].OnMessageFromRemotePeer();
        }

        private void Sctp_OnStateChange(RTCSctpTransportStateChangeEvent evt)
        {
            Debug.WriteLine("Sctp State Change: " + evt.State);
        }

        private void Sctp_OnDataChannel(RTCDataChannelEvent evt)
        {
            Debug.WriteLine("Sctp OnDataChannel");

            _dataChannel = evt.DataChannel;
            _dataChannel.OnMessage += DataChannel_OnMessage;
            _dataChannel.OnError += DataChannel_OnError;
            _dataChannel.OnStateChange += DataChannel_OnStateChange;
        }

        private void DataChannel_OnStateChange(RTCDataChannelStateChangeEvent @event)
        {
            Debug.WriteLine("DataChannel state: " + @event.State);
            if (@event.State.ToString() == "Open")
            {
                chatSession[RemotePeer].OnTwoPeersConnected();
                _IsSendEnabled = true;
            }
        }

        /// <summary>
        /// Establishes a DataChannel with the parameter peer.
        /// </summary>
        private void OpenDataChannel(Peer peer)
        {
            Debug.WriteLine($"Opening data channel to peer id: {peer.Id}");

            var iceParams = _gatherer.LocalParameters;
            _httpSignaler.SendToPeer(peer.Id, iceParams.ToJson().ToString());

            // this order guarantees: alice -> bob; bob.start(); bob -> alice; alice.start(); alice -> datachannel -> bob
            if (_isInitiator)
            {
                var sctpCaps = RTCSctpTransport.GetCapabilities();
                _httpSignaler.SendToPeer(peer.Id, sctpCaps.ToJson().ToString());
            }
            var dtlsParams = _dtls.LocalParameters;
            _httpSignaler.SendToPeer(peer.Id, dtlsParams.ToJson().ToString());
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

                HttpSignaler._peers.Remove(p => p.Name == LocalPeer.Name);

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
                await InitializeORTC();

                SelectedPeer = peersListView.SelectedItem as Peer;
                if (SelectedPeer == null)
                    return;

                RemotePeer = SelectedPeer;

                _httpSignaler.SendToPeer(RemotePeer.Id, "OpenDataChannel");

                OpenDataChannel(RemotePeer);

                if (!chatSession.ContainsKey(RemotePeer))
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        ChatPage chatPage = new ChatPage(RemotePeer);
                        chatSession.Add(RemotePeer, chatPage);
                        await Navigation.PushAsync(chatPage);
                    });
                }
            };
        }
    }
}
