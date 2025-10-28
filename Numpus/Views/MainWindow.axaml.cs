using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Numpus.Services;
using Numpus.ViewModels;

namespace Numpus.Views;

public partial class MainWindow : Window
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow()
        : this(new DesignTimeMainWindowViewModel(), new DesignStubDocumentService(), NullLogger<MainWindow>.Instance)
    {
        if (!Design.IsDesignMode)
        {
            throw new InvalidOperationException("The parameterless constructor is reserved for design-time support.");
        }
    }

    public MainWindow(MainWindowViewModel viewModel, IDocumentService documentService, ILogger<MainWindow> logger)
    {
        InitializeComponent();

        DataContext = viewModel;
        _documentService = documentService;
        _logger = logger;

        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            if (Clipboard is not null)
            {
                viewModel.AttachClipboard(Clipboard);
            }
            else
            {
                _logger.LogWarning("Clipboard service is not available on this platform.");
            }
        }

        if (StorageProvider is not null)
        {
            _documentService.Attach(StorageProvider);
        }
        else
        {
            _logger.LogWarning("Storage provider is not available; load and save actions will be disabled.");
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _documentService.Detach();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void TitleBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleWindowState();
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object? sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private sealed class DesignStubDocumentService : IDocumentService
    {
        public event EventHandler? AvailabilityChanged;

        public bool IsAvailable => false;

        public void Attach(Avalonia.Platform.Storage.IStorageProvider storageProvider) => AvailabilityChanged?.Invoke(this, EventArgs.Empty);

        public void Detach() => AvailabilityChanged?.Invoke(this, EventArgs.Empty);

        public Task<string?> LoadDocumentAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<bool> SaveDocumentAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}

