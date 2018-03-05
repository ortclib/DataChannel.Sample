using System;
using AppKit;

namespace DataChannelOrtc.Mac
{
    public class PeersTableDelegate : NSTableViewDelegate
    {
        private const string CellIdentifier = "PeerCell";
        private PeersTableDataSource DataSource;

        public PeersTableDelegate(PeersTableDataSource datasource)
        {
            this.DataSource = datasource;
        }

        public override NSView GetViewForItem(NSTableView tableView, NSTableColumn tableColumn, nint row)
        {
            NSTextField view = (NSTextField)tableView.MakeView(CellIdentifier, this);
            if (view == null)
            {
                view = new NSTextField();
                view.Identifier = CellIdentifier;
                view.BackgroundColor = NSColor.Clear;
                view.Bordered = false;
                view.Selectable = false;
                view.Editable = false;
            }

            // Setup view based on the column selected 
            switch (tableColumn.Title)
            {
                case "PeerId":
                    view.StringValue = DataSource.Peers[(int)row].PeerId;
                    break;
                case "PeerName":
                    view.StringValue = DataSource.Peers[(int)row].PeerName;
                    break;
            }

            return view;
        }
    }
}
