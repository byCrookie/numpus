using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Numpus.Services;

public interface IDocumentService
{
    event EventHandler? AvailabilityChanged;

    bool IsAvailable { get; }

    void Attach(IStorageProvider storageProvider);

    void Detach();

    Task<string?> LoadDocumentAsync(CancellationToken cancellationToken = default);

    Task<bool> SaveDocumentAsync(string content, CancellationToken cancellationToken = default);
}
