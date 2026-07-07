using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Calcifer.SweetPDF.Parsers;

public sealed partial class TextStructureParser : ITextStructureParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private readonly ILogger<TextStructureParser> _logger;

    public TextStructureParser(ILogger<TextStructureParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyDictionary<string, object> Parse(string text)
    {
        var result = new Dictionary<string, object>();

        AddMatches(result, "Emails", text,
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        AddMatches(result, "PhoneNumbers", text,
            @"\+?\d[\d\s\-()]{8,14}\d");
        AddMatches(result, "Dates", text,
            @"\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}|\d{4}[/\-]\d{1,2}[/\-]\d{1,2}");
        AddMatches(result, "InvoiceNumbers", text,
            @"INV[-]?\d{4,10}", RegexOptions.IgnoreCase);
        AddMatches(result, "Amounts", text,
            @"[$€£]\s?\d{1,3}(?:,\d{3})*(?:\.\d{2})?|\d{1,3}(?:,\d{3})*\.\d{2}\s?(?:USD|EUR|GBP|BDT)");

        _logger.LogDebug("Parsed {Count} structured field groups from text", result.Count);
        return result;
    }

    private void AddMatches(Dictionary<string, object> result, string key, string text,
        string pattern, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var values = Regex.Matches(text, pattern, options, RegexTimeout)
                .Select(m => m.Value.Trim())
                .Distinct()
                .ToList();

            if (values.Count > 0)
                result[key] = values;
        }
        catch (RegexMatchTimeoutException)
        {
            // A pathological document must not take the whole request down; skip this field group.
            _logger.LogWarning("Structure parsing for {Key} timed out; skipping", key);
        }
    }
}
