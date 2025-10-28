using System;
using System.Text;
using Velopack.Logging;

namespace Numpus.Services;

public sealed class VelopackLogUpdatedEventArgs : EventArgs
{
    public VelopackLogUpdatedEventArgs(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

public sealed class VelopackMemoryLogger : IVelopackLogger
{
    private readonly StringBuilder _buffer = new();

    public event EventHandler<VelopackLogUpdatedEventArgs>? LogUpdated;

    public override string ToString()
    {
        lock (_buffer)
        {
            return _buffer.ToString();
        }
    }

    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
    {
        if (logLevel < VelopackLogLevel.Information)
        {
            return;
        }

        lock (_buffer)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] [{logLevel}] {message}";
            if (exception != null)
            {
                line += Environment.NewLine + exception;
            }

            _buffer.AppendLine(line);
            LogUpdated?.Invoke(this, new VelopackLogUpdatedEventArgs(_buffer.ToString()));
        }
    }
}
