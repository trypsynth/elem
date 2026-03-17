using Elem.Views;

namespace Elem.Forms;

public sealed class MainForm : Form {
	private readonly TableView _tableView;
	private readonly ElementListView _listView;
	private readonly TabControl _tabs;

	public MainForm() {
		SuspendLayout();
		Text = "Elem";
		AutoScaleMode = AutoScaleMode.Font;
		AutoScaleDimensions = new SizeF(7f, 15f);
		ClientSize = new Size(1090, 560);
		MinimumSize = new Size(900, 480);
		StartPosition = FormStartPosition.CenterScreen;
		_tableView = new TableView();
		_listView = new ElementListView();
		var tablePage = new TabPage("Periodic Table") {
			UseVisualStyleBackColor = true,
		};
		tablePage.Controls.Add(_tableView);
		var listPage = new TabPage("Element List") {
			UseVisualStyleBackColor = true,
		};
		listPage.Controls.Add(_listView);
		_tabs = new TabControl {
			Dock = DockStyle.Fill,
		};
		_tabs.TabPages.Add(tablePage);
		_tabs.TabPages.Add(listPage);
		_tabs.AccessibleName = "Views";
		Controls.Add(_tabs);
		ResumeLayout();
	}
}
