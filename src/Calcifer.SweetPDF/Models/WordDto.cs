namespace Calcifer.SweetPDF.Models;

/// <summary>
/// A word with its bounding box on the page (PDF coordinate space: origin bottom-left, points).
/// </summary>
public sealed record WordDto(
    string Text,
    double Left,
    double Bottom,
    double Width,
    double Height,
    double FontSize,
    string? FontName);
