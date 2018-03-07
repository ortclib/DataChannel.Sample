using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AppKit;

namespace DataChannelOrtc.Mac
{
    public class ChatTableDataSource : NSTableViewDataSource
    {
        public ObservableCollection<MacViewMessage> Messages = new ObservableCollection<MacViewMessage>();

        public ChatTableDataSource()
        {
        }

        public override nint GetRowCount(NSTableView tableView)
        {
            return Messages.Count;
        }
    }
}
