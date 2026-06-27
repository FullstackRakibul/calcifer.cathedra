namespace Calcifer.Cathedra.Logging;

/// <summary>
/// An <see cref="ILogWriter"/> that fans every call out to several inner writers — typically the
/// console writer (<see cref="LogWriter"/>, which flows through <c>ILogger</c> and its scopes) and
/// the <see cref="FileLogWriter"/>. This is what makes "inject <c>ILogWriter</c>, get both console
/// and file" work without the caller knowing about either sink. A failure in one writer never stops
/// the others.
/// </summary>
public sealed class CompositeLogWriter : ILogWriter
{
    private readonly IReadOnlyList<ILogWriter> _writers;

    public CompositeLogWriter(params ILogWriter[] writers) => _writers = writers;

    public CompositeLogWriter(IEnumerable<ILogWriter> writers) => _writers = writers.ToList();

    public void Debug(string message, params object?[] args) =>
        Each(w => w.Debug(message, args));

    public void Info(string message, params object?[] args) =>
        Each(w => w.Info(message, args));

    public void Warn(string message, params object?[] args) =>
        Each(w => w.Warn(message, args));

    public void Error(string message, params object?[] args) =>
        Each(w => w.Error(message, args));

    public void Error(Exception exception, string message, params object?[] args) =>
        Each(w => w.Error(exception, message, args));

    private void Each(Action<ILogWriter> action)
    {
        foreach (var writer in _writers)
        {
            try { action(writer); }
            catch { /* one sink failing must not stop the others */ }
        }
    }
}
