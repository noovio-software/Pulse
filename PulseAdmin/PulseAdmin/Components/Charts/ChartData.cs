namespace PulseAdmin.Components.Charts;

/// <summary>Une barre horizontale : <see cref="Swatch"/> est un repère d'identité à côté du libellé (couleur de classe), pas la couleur de la barre.</summary>
public sealed record BarItem(string Label, double Value, string Display, string? Href = null, string? Swatch = null, string? Title = null, string? CssClass = null);

/// <summary>Une colonne, éventuellement empilée. <see cref="Title"/> : infobulle au survol.</summary>
public sealed record ColumnData(string Label, IReadOnlyList<Segment> Segments, string Title)
{
    public double Total => Segments.Sum(s => s.Value);
}

/// <param name="CssClass">Classe de couleur de la série (app-front…), <c>seq</c> pour la teinte séquentielle.</param>
public sealed record Segment(string Name, double Value, string CssClass);

public sealed record Series(string Name, string CssClass, IReadOnlyList<double> Values);
