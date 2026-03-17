using System.Globalization;

namespace Elem.Models;

public sealed record Element(
	int AtomicNumber,
	string Symbol,
	string Name,
	double AtomicMass,
	string Category,
	int Group,      // 1-18; 0 = f-block (lanthanide / actinide)
	int Period,
	string Phase,
	string ElectronConfiguration,
	double? Electronegativity,
	double? FirstIonizationEnergy,
	double? Density,
	double? MeltingPoint,
	double? BoilingPoint
) {
	public string CategoryDisplay => TitleCase(Category);

	public string GroupDisplay => Group > 0 ? Group.ToString() : "f-block";

	public string ListDescription => $"{AtomicNumber}. {Name} ({Symbol}), {CategoryDisplay}";

	public string AccessibleDescription => $"{Name}. Symbol {Symbol}. Atomic number {AtomicNumber}. {CategoryDisplay}. Period {Period}" + (Group > 0 ? $", Group {Group}." : ".");

	public IEnumerable<string> DetailLines() {
		yield return $"Atomic Number:          {AtomicNumber}";
		yield return $"Symbol:                 {Symbol}";
		yield return $"Name:                   {Name}";
		yield return $"Atomic Mass:            {AtomicMass:G6} u";
		yield return $"Category:               {CategoryDisplay}";
		yield return $"Group:                  {GroupDisplay}";
		yield return $"Period:                 {Period}";
		yield return $"Phase at STP:           {Phase}";
		yield return $"Electron Configuration: {ElectronConfiguration}";
		if (Electronegativity.HasValue)
			yield return $"Electronegativity:      {Electronegativity.Value:G4} (Pauling)";
		if (FirstIonizationEnergy.HasValue)
			yield return $"1st Ionization Energy:  {FirstIonizationEnergy.Value} kJ/mol";
		if (Density.HasValue)
			yield return $"Density:                {Density.Value} g/cm\u00b3";
		if (MeltingPoint.HasValue)
			yield return $"Melting Point:          {MeltingPoint.Value} K  ({MeltingPoint.Value - 273.15:F1} \u00b0C)";
		if (BoilingPoint.HasValue)
			yield return $"Boiling Point:          {BoilingPoint.Value} K  ({BoilingPoint.Value - 273.15:F1} \u00b0C)";
	}

	private static string TitleCase(string s) => string.IsNullOrWhiteSpace(s) ? s : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
}
