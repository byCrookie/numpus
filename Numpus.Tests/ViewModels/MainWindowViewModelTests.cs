using System;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Numpus.Compiler;
using Numpus.Services;
using Numpus.ViewModels;
using Xunit;

namespace Numpus.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task ClearCommand_ResetsDocument()
    {
        // Arrange
        var evaluator = new Evaluator();
        var documentService = new TestDocumentService();
        using var viewModel = new MainWindowViewModel(evaluator, documentService, NullLogger<MainWindowViewModel>.Instance);

        viewModel.Document = "5 + 5";

        // Act
        await viewModel.ClearCommand.Execute().ToTask();

        // Assert
        viewModel.Document.Should().BeEmpty();
        viewModel.StatusMessage.Should().Be("Document cleared");
    }

    [Fact]
    public void DocumentServiceAvailability_UpdatesCanUseFileSystem()
    {
        // Arrange
        var evaluator = new Evaluator();
        var documentService = new TestDocumentService();
        using var viewModel = new MainWindowViewModel(evaluator, documentService, NullLogger<MainWindowViewModel>.Instance);

        viewModel.CanUseFileSystem.Should().BeFalse();

        // Act
        documentService.SetAvailable(true);

        // Assert
        viewModel.CanUseFileSystem.Should().BeTrue();
    }

    private sealed class TestDocumentService : IDocumentService
    {
        private bool _isAvailable;

        public event EventHandler? AvailabilityChanged;

        public bool IsAvailable => _isAvailable;

        public void Attach(IStorageProvider storageProvider) => SetAvailable(true);

        public void Detach() => SetAvailable(false);

        public Task<string?> LoadDocumentAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> SaveDocumentAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public void SetAvailable(bool value)
        {
            _isAvailable = value;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
