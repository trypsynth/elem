using Elem.Models;

namespace Elem.Forms;

public sealed class ElementDetailDialog : Form {
	public ElementDetailDialog(Element element) {
		SuspendLayout();
		Text = "Details";
		AutoScaleMode = AutoScaleMode.Font;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(480, 400);
		Padding = new Padding(12);
		var list = new ListBox {
			Dock = DockStyle.Fill,
			Font = new Font("Consolas", 9.5f),
			BorderStyle = BorderStyle.FixedSingle,
			ScrollAlwaysVisible = false,
			IntegralHeight = false,
			AccessibleName = $"Properties of {element.Name}",
		};
		foreach (var line in element.DetailLines()) list.Items.Add(line);
		if (list.Items.Count > 0) list.SelectedIndex = 0;
		var closeBtn = new Button {
			Text = "&Close",
			DialogResult = DialogResult.Cancel,
			Dock = DockStyle.Bottom,
			Height = 32,
		};
		Controls.Add(list);
		Controls.Add(closeBtn);
		CancelButton = closeBtn;
		ActiveControl = list;
		ResumeLayout();
	}
}
