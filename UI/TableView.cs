using Elem.Models;

namespace Elem.UI;

public sealed class TableView : UserControl {
	private readonly PeriodicTableGrid _gridControl;

	public TableView() {
		SuspendLayout();
		_gridControl = new PeriodicTableGrid {
			Location = new Point(8, 8),
		};
		Controls.Add(_gridControl);
		Dock = DockStyle.Fill;
		ResumeLayout();
	}

	public void FocusGrid() => _gridControl.Focus();
}