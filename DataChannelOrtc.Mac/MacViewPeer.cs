namespace DataChannelOrtc.Mac
{
    public class MacViewPeer
    {
        public string PeerId { get; set; } = "";
        public string PeerName { get; set; } = "";

        public MacViewPeer()
        {
        }

        public MacViewPeer(string peerId, string peerName)
        {
            this.PeerId = peerId;
            this.PeerName = peerName;
        }
    }
}
