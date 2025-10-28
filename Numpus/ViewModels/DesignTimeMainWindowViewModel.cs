using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Numpus.Compiler;
using Numpus.Services;

namespace Numpus.ViewModels;

public sealed class DesignTimeMainWindowViewModel : MainWindowViewModel
{
    public DesignTimeMainWindowViewModel()
        : base(new Evaluator(), new DesignDocumentService(), NullLogger<MainWindowViewModel>.Instance)
    {
    }

    private sealed class DesignDocumentService : IDocumentService
    {
        public event EventHandler? AvailabilityChanged;

        public bool IsAvailable => false;

        public void Attach(IStorageProvider storageProvider) => AvailabilityChanged?.Invoke(this, EventArgs.Empty);

        public void Detach() => AvailabilityChanged?.Invoke(this, EventArgs.Empty);

        public Task<string?> LoadDocumentAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> SaveDocumentAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
