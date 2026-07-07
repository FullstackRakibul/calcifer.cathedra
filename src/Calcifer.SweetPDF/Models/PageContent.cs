namespace Calcifer.SweetPDF.Models;

/// <summary>Content extracted from a single page.</summary>
public sealed record PageContent(
    int PageNumber,
    string Text,
    IReadOnlyList<WordDto> Words,
    IReadOnlyList<string> Lines,
    double Width,
    double Height);
