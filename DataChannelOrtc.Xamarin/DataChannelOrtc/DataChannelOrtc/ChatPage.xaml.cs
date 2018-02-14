using DataChannelOrtc.Signaling;
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace DataChannelOrtc
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ChatPage : ContentPage
	{
        public event EventHandler MessageFromRemotePeer;

        public void OnMessageFromRemotePeer()
        {
            Debug.WriteLine("OnMessageFromRemotePeer!!!");
            MessageFromRemotePeer?.Invoke(this, (null));
            if (MainPage._messages.Count > 0)
            {
                var target = MainPage._messages[MainPage._messages.Count - 1];
                Device.BeginInvokeOnMainThread(() =>
                {
                    listMessages.ScrollTo(target, ScrollToPosition.End, true);
                });
            }
        }

        public ChatPage(Peer peer)
        {
            InitializeComponent();

            InitView(peer);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (MainPage._messages.Count > 0)
            {
                var target = MainPage._messages[MainPage._messages.Count - 1];
                Device.BeginInvokeOnMainThread(() =>
                {
                    listMessages.ScrollTo(target, ScrollToPosition.End, true);
                });
            }
        }

        private void InitView(Peer peer)
        {
            listMessages.ItemsSource = MainPage._messages;
            entryMessage.Text = string.Empty;

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
                if (MainPage._IsSendEnabled != false)
                {
                    if (entryMessage.Text != string.Empty)
                    {
                        string hostname = IPGlobalProperties.GetIPGlobalProperties().HostName;
                        MainPage._messages.Add(new Message(DateTime.Now.ToString("h:mm"), hostname + ": " + entryMessage.Text));

                        if (MainPage._messages.Count > 0)
                        {
                            var target = MainPage._messages[MainPage._messages.Count - 1];
                            Device.BeginInvokeOnMainThread(() =>
                            {
                                listMessages.ScrollTo(target, ScrollToPosition.End, true);
                            });
                        }

                        MainPage._dataChannel.Send(entryMessage.Text);
                    }
                    else
                    {
                        Debug.WriteLine("Message box is empty, write something...");
                    }
                    entryMessage.Text = string.Empty;
                }
                else
                {
                    Debug.Write("Please wait, connecting...");
                }
            };
        }
    }
}