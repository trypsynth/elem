using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms.Automation;
using Elem.Models;

namespace Elem.UI;

public sealed class PeriodicTableGrid : Control {
	private const int CellW = 58;
	private const int CellH = 36;
	private const int Pad = 4;
	private const int LabelOffsetX = 24; // left margin for period labels
	private const int LabelOffsetY = 18; // top margin for group labels

	private readonly Element?[,] _grid;
	private int _selRow;
	private int _selCol;
	private readonly Font _numFont;
	private readonly Font _symFont;
	// Maps categories to background colors.
	private static readonly IReadOnlyDictionary<string, Color> _catColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase) {
		["alkali metal"] = Color.FromArgb(255, 160, 160),
		["alkaline earth metal"] = Color.FromArgb(255, 210, 160),
		["transition metal"] = Color.FromArgb(255, 255, 153),
		["post-transition metal"] = Color.FromArgb(185, 235, 160),
		["metalloid"] = Color.FromArgb(153, 230, 200),
		["polyatomic nonmetal"] = Color.FromArgb(160, 215, 255),
		["diatomic nonmetal"] = Color.FromArgb(160, 195, 255),
		["noble gas"] = Color.FromArgb(210, 165, 255),
		["lanthanide"] = Color.FromArgb(255, 190, 255),
		["actinide"] = Color.FromArgb(255, 160, 210),
	};

	// Cached accessible object for the focused cell — kept alive so NVDA can hold
	// a stable COM reference to it between events.
	private AccessibleObject? _focusedCellAcc;

	// UIA focus event — bypasses WinForms' internal guard that only fires
	// UiaRaiseAutomationEvent when HandleInternal != 0 (true only for Controls).
	[DllImport("UIAutomationCore.dll", ExactSpelling = true)]
	private static extern int UiaRaiseAutomationEvent(nint provider, int id);
	private const int UIA_AutomationFocusChangedEventId = 20005;

	// Fires UIA_AutomationFocusChangedEventId for any AccessibleObject by obtaining
	// its IRawElementProviderSimple COM interface pointer directly.
	private static void FireUiaFocus(AccessibleObject acc) {
		var iid = new Guid("D6DD68D1-86FD-4332-8666-9ABEDEA2D24C"); // IRawElementProviderSimple
		var pUnk = Marshal.GetIUnknownForObject(acc);
		try {
			if (Marshal.QueryInterface(pUnk, ref iid, out nint pProvider) == 0) {
				UiaRaiseAutomationEvent(pProvider, UIA_AutomationFocusChangedEventId);
				Marshal.Release(pProvider);
			}
		} finally {
			Marshal.Release(pUnk);
		}
	}

	public PeriodicTableGrid() {
		SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		TabStop = true;
		AccessibleName = "Elements";
		AccessibleRole = AccessibleRole.Table;
		_grid = ElementData.BuildGrid();
		_numFont = new Font("Segoe UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
		_symFont = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Point);
		_selRow = 0;
		_selCol = 0;
		var initEl = _grid[0, 0];
		if (initEl is not null) _focusedCellAcc = new CellAccessibleObject(this, initEl, 0, 0);
		Width = LabelOffsetX + CellW * ElementData.GridCols + Pad * 2;
		Height = LabelOffsetY + CellH * ElementData.GridRows + Pad * 2;
	}

	public event EventHandler<Element>? SelectionChanged;

	public Element? SelectedElement => _grid[_selRow, _selCol];

	protected override void OnGotFocus(EventArgs e) {
		base.OnGotFocus(e);
		// WinForms fires a control-level UIA focus event during WM_SETFOCUS. Delay
		// our child-cell event one message-loop tick so NVDA processes them in order:
		// "Elements table" → cell name.
		var cell = _focusedCellAcc;
		var childId = _selRow * ElementData.GridCols + _selCol + 1;
		BeginInvoke(() => {
			if (cell is not null) FireUiaFocus(cell);
			AccessibilityNotifyClients(AccessibleEvents.Focus, childId); // legacy MSAA
			// CurrentThenMostRecent lets "elements table" finish speaking before announcing the cell.
			AnnounceSelected(AutomationNotificationProcessing.CurrentThenMostRecent);
		});
	}

	// Specify that the arrow keys aren't for focus switching on this control.
	protected override bool IsInputKey(Keys keyData) => (keyData & ~Keys.Modifiers) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Return || base.IsInputKey(keyData);

	// Arrow keys are also intercepted at the WndProc level so WinForms' parent-chain never sees them. IsInputKey alone is not sufficient because ProcessKeyPreview bubbles to the Form before IsInputKey is checked on the focused control. Removing either of these causes the arrow key behavior to not work, yay!
	protected override void WndProc(ref Message m) {
		const int WM_KEYDOWN = 0x0100;
		if (m.Msg == WM_KEYDOWN) {
			switch ((Keys)((int)m.WParam & 0xFFFF)) {
				case Keys.Left: TryMove(0, -1); return;
				case Keys.Right: TryMove(0, 1); return;
				case Keys.Up: TryMove(-1, 0); return;
				case Keys.Down: TryMove(1, 0); return;
				case Keys.Home: MoveToRowStart(); return;
				case Keys.End: MoveToRowEnd(); return;
				case Keys.PageUp: MoveToColTop(); return;
				case Keys.PageDown: MoveToColBottom(); return;
				case Keys.Return: OpenDetail(); return;
			}
		}
		base.WndProc(ref m);
	}

	protected override void OnMouseDown(MouseEventArgs e) {
		base.OnMouseDown(e);
		Focus();
		var (r, c) = HitTest(e.Location);
		if (r >= 0 && _grid[r, c] != null) SelectCell(r, c);
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e) {
		base.OnMouseDoubleClick(e);
		var (r, c) = HitTest(e.Location);
		if (r >= 0 && _grid[r, c] != null) OpenDetail();
	}

	protected override void OnPaint(PaintEventArgs e) {
		base.OnPaint(e);
		var g = e.Graphics;
		g.SmoothingMode = SmoothingMode.None;
		using var labelBrush = new SolidBrush(Color.FromArgb(100, 100, 100));
		for (var col = 0; col < ElementData.GridCols; col++) {
			var label = (col + 1).ToString();
			var sz = g.MeasureString(label, _numFont);
			var x = Pad + LabelOffsetX + col * CellW + (CellW - sz.Width) / 2;
			g.DrawString(label, _numFont, labelBrush, x, Pad + 1);
		}
		string[] periodLabels = ["1", "2", "3", "4", "5", "6", "7", "", "La", "Ac"];
		for (var row = 0; row < ElementData.GridRows; row++) {
			var label = periodLabels[row];
			if (label.Length == 0) continue;
			var sz = g.MeasureString(label, _numFont);
			var x = Pad + LabelOffsetX - sz.Width - 3;
			var y = Pad + LabelOffsetY + row * CellH + (CellH - sz.Height) / 2;
			g.DrawString(label, _numFont, labelBrush, x, y);
		}
		for (var row = 0; row < ElementData.GridRows; row++) {
			for (var col = 0; col < ElementData.GridCols; col++) {
				var el = _grid[row, col];
				if (el is null) continue;
				var bounds = CellBounds(row, col);
				if (!e.ClipRectangle.IntersectsWith(bounds)) continue;
				DrawCell(g, bounds, el, row == _selRow && col == _selCol);
			}
		}
		if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(1, 1, Width - 2, Height - 2));
	}

	private void DrawCell(Graphics g, Rectangle b, Element el, bool selected) {
		var bg = selected ? (Focused ? SystemColors.Highlight : SystemColors.InactiveCaption) : (_catColors.TryGetValue(el.Category, out var c) ? c : Color.Silver);
		using (var brush = new SolidBrush(bg))
			g.FillRectangle(brush, b);
		var borderColor = selected ? (Focused ? SystemColors.HighlightText : Color.Gray) : Color.FromArgb(110, 110, 110);
		using (var pen = new Pen(borderColor, selected ? 2f : 1f))
			g.DrawRectangle(pen, b.X, b.Y, b.Width - 1, b.Height - 1);
		var fg = selected && Focused ? SystemColors.HighlightText : Color.Black;
		using (var brush = new SolidBrush(fg))
			g.DrawString(el.AtomicNumber.ToString(), _numFont, brush, b.X + 2, b.Y + 2);
		var symSize = g.MeasureString(el.Symbol, _symFont);
		var sx = b.X + (b.Width - symSize.Width) / 2;
		var sy = b.Y + (b.Height - symSize.Height) / 2 + 2;
		using (var brush = new SolidBrush(fg))
			g.DrawString(el.Symbol, _symFont, brush, sx, sy);
	}

	internal Rectangle CellBounds(int row, int col) =>
		new(Pad + LabelOffsetX + col * CellW, Pad + LabelOffsetY + row * CellH, CellW - 1, CellH - 1);

	private (int row, int col) HitTest(Point pt) {
		var col = (pt.X - Pad - LabelOffsetX) / CellW;
		var row = (pt.Y - Pad - LabelOffsetY) / CellH;
		if (row < 0 || row >= ElementData.GridRows || col < 0 || col >= ElementData.GridCols) return (-1, -1);
		return (row, col);
	}

	private void TryMove(int dRow, int dCol) {
		var r = _selRow + dRow;
		var c = _selCol + dCol;
		if (r >= 0 && r < ElementData.GridRows && c >= 0 && c < ElementData.GridCols)
			SelectCell(r, c);
	}

	private void MoveToRowStart() {
		for (var c = 0; c < ElementData.GridCols; c++) {
			if (_grid[_selRow, c] is not null) {
				SelectCell(_selRow, c);
				return;
			}
		}
	}

	private void MoveToRowEnd() {
		for (var c = ElementData.GridCols - 1; c >= 0; c--) {
			if (_grid[_selRow, c] is not null) {
				SelectCell(_selRow, c);
				return;
			}
		}
	}

	private void MoveToColTop() {
		for (var r = 0; r < ElementData.GridRows; r++) {
			if (_grid[r, _selCol] is not null) {
				SelectCell(r, _selCol);
				return;
			}
		}
	}

	private void MoveToColBottom() {
		for (var r = ElementData.GridRows - 1; r >= 0; r--) {
			if (_grid[r, _selCol] is not null) {
				SelectCell(r, _selCol);
				return;
			}
		}
	}

	private void SelectCell(int row, int col) {
		_selRow = row;
		_selCol = col;
		ScrollCellIntoView(row, col);
		Invalidate();
		var el = _grid[row, col];
		if (el is not null) SelectionChanged?.Invoke(this, el);
		// Cache before firing events so GetFocused() and FireUiaFocus use the same instance.
		_focusedCellAcc = el is not null ? new CellAccessibleObject(this, el, row, col) : null;
		AnnounceSelected(); // UIA notification (Narrator / UIA live-region)
							// Fire UIA AutomationFocusChangedEvent on the cell directly — NVDA uses UIA
							// for WinForms controls and ignores the MSAA EVENT_OBJECT_FOCUS we fire below.
		if (_focusedCellAcc is not null) FireUiaFocus(_focusedCellAcc);
		var childId = row * ElementData.GridCols + col + 1;
		AccessibilityNotifyClients(AccessibleEvents.Focus, childId);     // MSAA legacy
		AccessibilityNotifyClients(AccessibleEvents.Selection, childId); // MSAA legacy
	}

	private void ScrollCellIntoView(int row, int col) {
		// If inside a scroll-able panel, ensure the cell is visible.
		var parent = Parent;
		if (parent is not Panel { AutoScroll: true } panel) return;

		var b = CellBounds(row, col);
		var vis = panel.ClientRectangle;
		var offset = panel.AutoScrollPosition;
		var actual = new Rectangle(b.X + offset.X, b.Y + offset.Y, b.Width, b.Height);

		var newX = -offset.X;
		var newY = -offset.Y;

		if (actual.Left < 0) newX = b.Left;
		else if (actual.Right > vis.Width) newX = b.Right - vis.Width;

		if (actual.Top < 0) newY = b.Top;
		else if (actual.Bottom > vis.Height) newY = b.Bottom - vis.Height;

		panel.AutoScrollPosition = new Point(newX, newY);
	}

	private void AnnounceSelected(AutomationNotificationProcessing processing = AutomationNotificationProcessing.ImportantMostRecent) {
		var el = _grid[_selRow, _selCol];
		var position = $"Row {_selRow + 1}, Column {_selCol + 1}";
		var text = el is not null ? $"{el.AccessibleDescription} {position}." : $"Blank. {position}.";
		AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.ActionCompleted, processing, text);
	}

	private void OpenDetail() => OpenDetail(_selRow, _selCol);

	private void OpenDetail(int row, int col) {
		var el = _grid[row, col];
		if (el is null) return;
		using var dlg = new ElementDetailDialog(el);
		dlg.ShowDialog(FindForm());
	}

	// ── Accessibility ─────────────────────────────────────────────────────────

	protected override AccessibleObject CreateAccessibilityInstance() =>
		new GridAccessibleObject(this);

	protected override void Dispose(bool disposing) {
		if (disposing) {
			_numFont.Dispose();
			_symFont.Dispose();
		}
		base.Dispose(disposing);
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Nested accessible objects
	// ─────────────────────────────────────────────────────────────────────────

	private sealed class GridAccessibleObject : ControlAccessibleObject {
		public GridAccessibleObject(PeriodicTableGrid grid) : base(grid) { }

		private PeriodicTableGrid Grid => (PeriodicTableGrid)Owner!;

		public override AccessibleRole Role => AccessibleRole.Table;

		public override int GetChildCount() => ElementData.GridRows * ElementData.GridCols;

		public override AccessibleObject? GetChild(int index) {
			var row = index / ElementData.GridCols;
			var col = index % ElementData.GridCols;
			var el = Grid._grid[row, col];
			return el is null ? null : new CellAccessibleObject(Grid, el, row, col);
		}

		public override AccessibleObject? Navigate(AccessibleNavigation navdir) {
			if (navdir == AccessibleNavigation.FirstChild) {
				for (var i = 0; i < GetChildCount(); i++) {
					var child = GetChild(i);
					if (child is not null) return child;
				}
				return null;
			}
			if (navdir == AccessibleNavigation.LastChild) {
				for (var i = GetChildCount() - 1; i >= 0; i--) {
					var child = GetChild(i);
					if (child is not null) return child;
				}
				return null;
			}
			return base.Navigate(navdir);
		}

		public override AccessibleObject? GetFocused() => Grid._focusedCellAcc ?? GetChild(Grid._selRow * ElementData.GridCols + Grid._selCol);
	}

	private sealed class CellAccessibleObject : AccessibleObject {
		private readonly PeriodicTableGrid _owner;
		private readonly Element _el;
		private readonly int _row, _col;

		public CellAccessibleObject(PeriodicTableGrid owner, Element el, int row, int col) {
			_owner = owner;
			_el = el;
			_row = row;
			_col = col;
		}

		public override string Name => _el.AccessibleDescription;

		public override AccessibleRole Role => AccessibleRole.Cell;

		public override AccessibleObject? Parent => _owner.AccessibilityObject;

		public override AccessibleStates State {
			get {
				var s = AccessibleStates.Focusable | AccessibleStates.Selectable;
				if (_owner._selRow == _row && _owner._selCol == _col) s |= AccessibleStates.Focused | AccessibleStates.Selected;
				return s;
			}
		}

		public override Rectangle Bounds => _owner.RectangleToScreen(_owner.CellBounds(_row, _col));

		// Allows screen readers to navigate between cells via accNavigate.
		public override AccessibleObject? Navigate(AccessibleNavigation navdir) =>
			navdir switch {
				// Next/Previous walk all cells linearly so review cursors traverse the whole table.
				AccessibleNavigation.Next => LinearCell(+1),
				AccessibleNavigation.Previous => LinearCell(-1),
				// Spatial directions move to the nearest cell in that direction.
				AccessibleNavigation.Right => AdjacentCell(0, 1),
				AccessibleNavigation.Left => AdjacentCell(0, -1),
				AccessibleNavigation.Up => AdjacentCell(-1, 0),
				AccessibleNavigation.Down => AdjacentCell(1, 0),
				AccessibleNavigation.FirstChild or AccessibleNavigation.LastChild => null,
				_ => base.Navigate(navdir),
			};

		private AccessibleObject? LinearCell(int delta) {
			var idx = _row * ElementData.GridCols + _col + delta;
			while (idx >= 0 && idx < ElementData.GridRows * ElementData.GridCols) {
				var r = idx / ElementData.GridCols;
				var c = idx % ElementData.GridCols;
				var el = _owner._grid[r, c];
				if (el is not null) return new CellAccessibleObject(_owner, el, r, c);
				idx += delta;
			}
			return null;
		}

		private AccessibleObject? AdjacentCell(int dRow, int dCol) {
			var r = _row + dRow;
			var c = _col + dCol;
			while (r >= 0 && r < ElementData.GridRows && c >= 0 && c < ElementData.GridCols) {
				var el = _owner._grid[r, c];
				if (el is not null) return new CellAccessibleObject(_owner, el, r, c);
				r += dRow;
				c += dCol;
			}
			return null;
		}

		// Moves the real grid selection when a screen reader selects/focuses a cell.
		public override void Select(AccessibleSelection flags) {
			if ((flags & (AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus)) != 0) {
				_owner.SelectCell(_row, _col);
				_owner.Focus();
			}
		}

		public override void DoDefaultAction() => _owner.OpenDetail(_row, _col);

		public override string DefaultAction => "Open details";
	}
}
