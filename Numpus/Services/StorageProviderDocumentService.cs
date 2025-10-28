using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

namespace Numpus.Services;

public sealed class StorageProviderDocumentService : IDocumentService
{
    private readonly ILogger<StorageProviderDocumentService> _logger;
    private readonly object _syncRoot = new();
    private IStorageProvider? _storageProvider;

    public StorageProviderDocumentService(ILogger<StorageProviderDocumentService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler? AvailabilityChanged;

    public bool IsAvailable
    {
        get
        {
            lock (_syncRoot)
            {
                return _storageProvider is not null;
            }
        }
    }

    public void Attach(IStorageProvider storageProvider)
    {
        if (storageProvider is null)
        {
            throw new ArgumentNullException(nameof(storageProvider));
        }

        lock (_syncRoot)
        {
            _storageProvider = storageProvider;
        }

        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Detach()
    {
        lock (_syncRoot)
        {
            _storageProvider = null;
        }

        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string?> LoadDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetStorageProviderOrThrow();

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Numpus Document",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Numpus Document")
                {
                    Patterns = new[] { "*.numpus", "*.txt" },
                    AppleUniformTypeIdentifiers = new[] { "public.plain-text" },
                    MimeTypes = new[] { "text/plain" }
                }
            }
        }).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        var content = await reader.ReadToEndAsync().ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return content;
    }

    public async Task<bool> SaveDocumentAsync(string content, CancellationToken cancellationToken = default)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetStorageProviderOrThrow();

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Numpus Document",
            SuggestedFileName = "calculation.numpus",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Numpus Document")
                {
                    Patterns = new[] { "*.numpus" },
                    AppleUniformTypeIdentifiers = new[] { "public.plain-text" },
                    MimeTypes = new[] { "text/plain" }
                }
            }
        }).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(false);
        stream.SetLength(0);

        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);

        return true;
    }

    private IStorageProvider GetStorageProviderOrThrow()
    {
        lock (_syncRoot)
        {
            if (_storageProvider is null)
            {
                _logger.LogWarning("Storage provider requested before being attached.");
                throw new InvalidOperationException("Storage provider is not available.");
            }

            return _storageProvider;
        }
    }
}
