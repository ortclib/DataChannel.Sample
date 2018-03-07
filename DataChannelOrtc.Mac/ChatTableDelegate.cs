using System;
using AppKit;

namespace DataChannelOrtc.Mac
{
    public class ChatTableDelegate : NSTableViewDelegate
    {
        private const string CellIdentifier = "ChatCell";
        private ChatTableDataSource DataSource;

        public ChatTableDelegate(ChatTableDataSource datasource)
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
                case "Author":
                    view.StringValue = DataSource.Messages[(int)row].Author;
                    break;
                case "Message":
                    view.StringValue = DataSource.Messages[(int)row].Text;
                    break;
            }

            return view;
        }
    }
}
