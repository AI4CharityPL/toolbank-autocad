using System.Collections.Generic;

namespace AcadMcp.Companion.Agent.Reports;

/// <summary>A one-click report flow: a label plus the instruction sent to the agent.</summary>
public sealed record ReportTemplate(string Title, string Prompt);

/// <summary>Predefined report/counting flows surfaced as quick-action buttons in the UI.</summary>
public static class ReportTemplates
{
    public static IReadOnlyList<ReportTemplate> All { get; } = new[]
    {
        new ReportTemplate(
            "Zestawienie warstw",
            "Policz elementy bieżącego rysunku w rozbiciu na warstwy. Zwróć tabelę Markdown z kolumnami: " +
            "Warstwa, Liczba elementów, Typy elementów. Posortuj malejąco po liczbie."),
        new ReportTemplate(
            "Zestawienie bloków",
            "Zlicz wszystkie odniesienia bloków w rysunku zgrupowane po nazwie bloku. Zwróć tabelę Markdown: " +
            "Nazwa bloku, Liczba wstawień, Warstwy. Posortuj malejąco po liczbie."),
        new ReportTemplate(
            "BOM / stolarka",
            "Przygotuj zestawienie stolarki (drzwi i okna) w bieżącym rysunku. Zwróć tabelę Markdown: " +
            "Typ, Oznaczenie, Wymiary, Liczba sztuk. Jeśli brak danych w rysunku, jasno to napisz."),
        new ReportTemplate(
            "Podsumowanie rysunku",
            "Podaj zwięzłe podsumowanie bieżącego rysunku: nazwa dokumentu, jednostki, łączna liczba elementów, " +
            "lista warstw oraz najczęstsze typy elementów. Zwróć wynik w czytelnej formie z tabelą Markdown."),
    };
}
