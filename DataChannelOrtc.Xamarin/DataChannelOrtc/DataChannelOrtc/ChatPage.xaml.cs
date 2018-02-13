using DataChannelOrtc.Signaling;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DataChannelOrtc
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ChatPage : ContentPage
	{
        public ChatPage(Peer peer)
        {
            InitializeComponent();

            //this.MessageAdded += ChatPage_MessageAdded;

            InitView(peer);
        }

        private void ChatPage_MessageAdded(object sender, Message message)
        {
            Debug.WriteLine("ChatPage_MessageAdded: " + message.ToString());
            MainPage._messages.Add(message);
        }

        //public event EventHandler<Message> MessageAdded;

        //public void OnMessageAdded(Message message)
        //{
        //    if (MessageAdded != null)
        //        MessageAdded(this, (message));
        //}

        //public static ObservableCollection<Message> _messages = new ObservableCollection<Message>();

        private void InitView(Peer peer)
        {
            listMessages.ItemsSource = MainPage._messages;

            // Page structure 
            Content = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    listMessages,
                    slMessage
                }
            };

            btnSend.Clicked += (sender, args) =>
            {
                if (entryMessage.Text != string.Empty)
                {
                    string hostname = IPGlobalProperties.GetIPGlobalProperties().HostName;
                    MainPage._messages.Add(new Message(DateTime.Now.ToString("h:mm"), hostname + ": " + entryMessage.Text));
                    MainPage._dataChannel.Send(entryMessage.Text);
                }
                else
                {
                    Debug.Write("Message is empty!");
                }
                entryMessage.Text = string.Empty;
            };
        }
    }
}