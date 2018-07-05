using DataChannelOrtc.Signaling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace DataChannelOrtc.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChatPage : Page
    {
        //public ObservableCollection<Message> _messages = new ObservableCollection<Message>();

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
            //MessageFromRemotePeer?.Invoke(this, new Message(RemotePeer, LocalPeer, DateTime.Now, message));
            MessageFromRemotePeer?.Invoke(this, new Message(RemotePeer, DateTime.Now, message));
            PeersListPage._messages.Add(new Message(remotePeer, DateTime.Now, message));
            //listMessages.Items.Add(new Message(RemotePeer, DateTime.Now, message));
        }

        private bool _isSendReady = false;
        public bool IsSendReady
        {
            get { return _isSendReady; }
            set { _isSendReady = value; }
        }

        public Peer LocalPeer { get; set; }
        public Peer RemotePeer { get; set; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            //ChatPageParams parameters = (ChatPageParams)e.Parameter;

            //LocalPeer = parameters.LocalPeer;
            //RemotePeer = parameters.RemotePeer;

            
        }

        public ChatPage()
        {
            this.InitializeComponent();

            InitView();

            RemotePeerConnected += Signaler_RemoteConnected;
            RemotePeerDisconnected += Signaler_RemoteDisconnected;
            MessageFromRemotePeer += Signaler_MessageFromRemotePeer;

            

            
        }

        public ChatPage(Peer localPeer, Peer remotePeer)
        {
            //this.InitializeComponent();

            //RemotePeerConnected += Signaler_RemoteConnected;
            //RemotePeerDisconnected += Signaler_RemoteDisconnected;
            //MessageFromRemotePeer += Signaler_MessageFromRemotePeer;

            //InitView();
        }

        private void Signaler_RemoteConnected(object sender, EventArgs e)
        {
            IsSendReady = true;
            //btnSend.IsEnabled = true;
            //btnSend.BackgroundColor = Color.Gray;
        }

        private void Signaler_RemoteDisconnected(object sender, EventArgs e)
        {
            IsSendReady = false;
            //btnSend.IsEnabled = false;
            //btnSend.BackgroundColor = Color.DarkGray;
        }

        private void Signaler_MessageFromRemotePeer(object sender, Message message)
        {
            listMessages.Items.Add(new Message(LocalPeer, DateTime.Now, message.MessageText));
        }

        private void InitView()
        {
            listMessages.ItemsSource = PeersListPage._messages;
            listMessages.VerticalAlignment = VerticalAlignment.Center;
            listMessages.HorizontalAlignment = HorizontalAlignment.Center;

            btnSend.VerticalAlignment = VerticalAlignment.Bottom;
            btnSend.HorizontalAlignment = HorizontalAlignment.Right;

            btnSend.Click += (sender, args) =>
            {
                Message message = new Message(OrtcController.LocalPeer, DateTime.Now, "text message");

                PeersListPage._messages.Add(message);

                OnSendMessageToRemotePeer(message);
            };

        }

        private void OnSendMessageToRemotePeer(Message message)
        {
            SendMessageToRemotePeer?.Invoke(this, message);
        }

        private void ScrollMessages()
        {
            //if (_messages.Count > 0)
            //{
            //    Device.BeginInvokeOnMainThread(() =>
            //        listMessages.ScrollTo(_messages[_messages.Count - 1],
            //        ScrollToPosition.End, true));
            //}
        }
    }
}
