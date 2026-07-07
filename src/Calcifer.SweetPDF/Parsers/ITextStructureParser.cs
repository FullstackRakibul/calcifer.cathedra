namespace Calcifer.SweetPDF.Parsers;

/// <summary>
/// Pulls structured fields (emails, phone numbers, dates, invoice numbers, amounts) out of raw
/// extracted text.
/// </summary>
public interface ITextStructureParser
{
    IReadOnlyDictionary<string, object> Parse(string text);
}
