using Microsoft.Extensions.Logging;

namespace Calcifer.Cathedra.Logging;

/// <summary>
/// Settings for the file-based logger, bound from the <c>Cathedra:FileLogging</c> configuration
/// section. Every value has a sensible default so the logger works out-of-the-box with no config.
/// </summary>
public sealed class FileLogOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "Cathedra:FileLogging";

    /// <summary>
    /// Directory for log files, relative to <see cref="AppContext.BaseDirectory"/> unless an
    /// absolute path is given. Default: <c>logs/cathedra</c>.
    /// </summary>
    public string LogPath { get; set; } = "logs/cathedra";

    /// <summary>Minimum level written to the file. Entries below this are dropped. Default: Information.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Size cap per file in megabytes. When the current file exceeds this, a new part is started
    /// (<c>cathedra-YYYY-MM-DD.NNN.log</c>). Set to 0 to disable size rollover. Default: 10.
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>Delete log files older than this many days. Set to 0 to keep forever. Default: 30.</summary>
    public int RetainDays { get; set; } = 30;

    /// <summary>Start a new file each UTC day (<c>cathedra-YYYY-MM-DD.log</c>). Default: true.</summary>
    public bool UseDailyRotation { get; set; } = true;

    /// <summary>Filename prefix before the date. Default: <c>cathedra</c>.</summary>
    public string FilePrefix { get; set; } = "cathedra";
}
