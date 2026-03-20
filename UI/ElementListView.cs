using System.Collections.Generic;
using System.Linq;
using Elem.Models;

namespace Elem.UI;

public sealed class ElementListView : UserControl {
	private readonly ListBox _listBox;
	private readonly ComboBox _sortCombo;
	private List<Element> _sorted;

	private static readonly string[] SortLabels = [
		"Atomic Number",
		"Name (alphabetical)",
		"Category",
		"Period then Group",
		"Phase",
	];

	public ElementListView() {
		SuspendLayout();
		_sorted = new List<Element>(ElementData.All);
		var toolbar = new Panel {
			Dock = DockStyle.Top,
			Height = 36,
			Padding = new Padding(4, 4, 4, 0),
		};
		var sortLabel = new Label {
			Text = "&Sort by",
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Location = new Point(4, 8),
		};
		_sortCombo = new ComboBox {
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(62, 4),
			Width = 200,
		};
		_sortCombo.Items.AddRange(SortLabels);
		_sortCombo.SelectedIndex = 0;
		_sortCombo.SelectedIndexChanged += (_, _) => Refresh();
		toolbar.Controls.Add(sortLabel);
		toolbar.Controls.Add(_sortCombo);
		_listBox = new ListBox {
			Dock = DockStyle.Fill,
			Font = new Font("Consolas", 9.5f),
			IntegralHeight = false,
			ScrollAlwaysVisible = true,
			AccessibleName = "Elements",
		};
		_listBox.KeyDown += OnListKeyDown;
		Controls.Add(_listBox);
		Controls.Add(toolbar);
		Dock = DockStyle.Fill;
		ResumeLayout();
		Refresh();
	}

	public sealed override void Refresh() {
		_sorted = (_sortCombo.SelectedIndex switch {
			1 => ElementData.All.OrderBy(e => e.Name),
			2 => ElementData.All.OrderBy(e => e.Category).ThenBy(e => e.AtomicNumber),
			3 => ElementData.All.OrderBy(e => e.Period).ThenBy(e => e.Group == 0 ? 99 : e.Group).ThenBy(e => e.AtomicNumber),
			4 => ElementData.All.OrderBy(e => e.Phase).ThenBy(e => e.AtomicNumber),
			_ => ElementData.All.OrderBy(e => e.AtomicNumber),
		}).ToList();
		var prev = SelectedElement;
		_listBox.BeginUpdate();
		_listBox.Items.Clear();
		foreach (var el in _sorted)
			_listBox.Items.Add(el.ListDescription);
		_listBox.EndUpdate();
		if (prev is not null) {
			var idx = _sorted.IndexOf(prev);
			if (idx >= 0) _listBox.SelectedIndex = idx;
		} else if (_listBox.Items.Count > 0) {
			_listBox.SelectedIndex = 0;
		}
		base.Refresh();
	}

	private Element? SelectedElement => _listBox.SelectedIndex >= 0 ? _sorted[_listBox.SelectedIndex] : null;

	private void OnListKeyDown(object? sender, KeyEventArgs e) {
		if (e.KeyCode is Keys.Enter or Keys.Return && SelectedElement is { } el) {
			e.Handled = true;
			using var dlg = new ElementDetailDialog(el);
			dlg.ShowDialog(FindForm());
		}
	}
}