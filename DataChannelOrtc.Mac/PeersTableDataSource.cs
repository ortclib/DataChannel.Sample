using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AppKit;

namespace DataChannelOrtc.Mac
{
    public class PeersTableDataSource : NSTableViewDataSource
    {
        public ObservableCollection<MacViewPeer> Peers = new ObservableCollection<MacViewPeer>();

        public PeersTableDataSource()
        {
        }

        public override nint GetRowCount(NSTableView tableView)
        {
            return Peers.Count;
        }
    }
}
