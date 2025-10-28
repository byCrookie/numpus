using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Numpus.Compiler;
using Numpus.Services;
using ReactiveUI;

namespace Numpus.ViewModels;

public sealed class CalculatorLine : ReactiveObject
{
    private string _input = string.Empty;
    private string _result = string.Empty;
    private bool _hasError;
    private bool _isComment;

    public string Input
    {
        get => _input;
        set => this.RaiseAndSetIfChanged(ref _input, value);
    }

    public string Result
    {
        get => _result;
        set
        {
            this.RaiseAndSetIfChanged(ref _result, value);
            this.RaisePropertyChanged(nameof(ShowResult));
            this.RaisePropertyChanged(nameof(ShowError));
            this.RaisePropertyChanged(nameof(ShowComment));
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            this.RaiseAndSetIfChanged(ref _hasError, value);
            this.RaisePropertyChanged(nameof(ShowResult));
            this.RaisePropertyChanged(nameof(ShowError));
            this.RaisePropertyChanged(nameof(ShowComment));
        }
    }

    public bool IsComment
    {
        get => _isComment;
        set
        {
            this.RaiseAndSetIfChanged(ref _isComment, value);
            this.RaisePropertyChanged(nameof(ShowResult));
            this.RaisePropertyChanged(nameof(ShowError));
            this.RaisePropertyChanged(nameof(ShowComment));
        }
    }

    public bool ShowResult => !string.IsNullOrWhiteSpace(Result) && !HasError && !IsComment;

    public bool ShowError => HasError && !string.IsNullOrWhiteSpace(Result);

    public bool ShowComment => IsComment && !string.IsNullOrWhiteSpace(Result);

    public int LineNumber { get; set; }
}

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private const string _readyStatusText = "[Ready]";
    private const string _errorStatusText = "[Errors]";
    private const string _clipboardStatusText = "[Clipboard]";

    private static readonly TimeSpan _evaluationDebounce = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _statusResetDelay = TimeSpan.FromSeconds(2);

    private static readonly string _defaultDocument = """
# Numpus Feature Tour
# Comments (like this) are ignored.

# Arithmetic & precedence
1 + 2 * 3
(1 + 2) * 3
2 ^ 5
-4 + +2
1.25e2 / 5

# Variables & reuse
x = 10
y = x ^ 2
(y - x) / 2

# Units & conversions
distance = 2 km
time = 5 min
distance / time
5 m + 50 cm
10 in + 1 ft
width = 2 m
height = 3 m
area = width * height
area
area / width
distance * 2
2 * distance
distance / 2
10 m / 2 m

# Temperature & mass
temp_c = 25 c
temp_f = 77 degf
temp_k = 298 k
mass = 70 kg
mass + 500 g

# Durations & aliases
lap = 90 sec
lap / 45 s
sprint = 100 m / 9.58 s
speed_limit = 60 km/h
speed_limit

# Functions & built-ins
square(n) = n ^ 2
square(8)
hypotenuse(a, b) = sqrt(a ^ 2 + b ^ 2)
hypotenuse(3, 4)
adder(a, b, c) = a + b + c
adder(distance, 500 m, 0.1 km)
max(1, 5, 3, -2)
min(4, 2, 7)
round(2.71828)

# Errors (uncomment)
# undefined_var + 5
# 10 / 0
""";

    private readonly Evaluator _evaluator;
    private readonly IDocumentService _documentService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly CompositeDisposable _cleanup = new();

    private CancellationTokenSource? _statusResetSource;
    private IClipboard? _clipboard;
    private string _document = string.Empty;
    private string _statusMessage = "Ready";
    private string _statusText = _readyStatusText;
    private int _lineCount;
    private int _errorCount;
    private bool _canUseFileSystem;
    private bool _hasClipboard;
    private bool _isBusy;

    public MainWindowViewModel(Evaluator evaluator, IDocumentService documentService, ILogger<MainWindowViewModel> logger)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Lines = new ObservableCollection<CalculatorLine>();

        var canExecuteFileCommands = this.WhenAnyValue(vm => vm.CanUseFileSystem, vm => vm.IsBusy, (available, busy) => available && !busy);

        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync, canExecuteFileCommands);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canExecuteFileCommands);
        ClearCommand = ReactiveCommand.Create(ClearDocument);
        CopyResultCommand = ReactiveCommand.CreateFromTask<string?>(CopyResultAsync, this.WhenAnyValue(vm => vm.HasClipboard));

        this.WhenAnyValue(vm => vm.Document)
            .Throttle(_evaluationDebounce)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => EvaluateDocument())
            .DisposeWith(_cleanup);

        CanUseFileSystem = _documentService.IsAvailable;
        _documentService.AvailabilityChanged += OnDocumentServiceAvailabilityChanged;

        Document = _defaultDocument;
    }

    public ObservableCollection<CalculatorLine> Lines { get; }

    public string Document
    {
        get => _document;
        set => this.RaiseAndSetIfChanged(ref _document, value ?? string.Empty);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public int LineCount
    {
        get => _lineCount;
        private set => this.RaiseAndSetIfChanged(ref _lineCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorCount, value);
            this.RaisePropertyChanged(nameof(HasErrors));
        }
    }

    public bool HasErrors => ErrorCount > 0;

    public bool CanUseFileSystem
    {
        get => _canUseFileSystem;
        private set => this.RaiseAndSetIfChanged(ref _canUseFileSystem, value);
    }

    public bool HasClipboard
    {
        get => _hasClipboard;
        private set => this.RaiseAndSetIfChanged(ref _hasClipboard, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<string?, Unit> CopyResultCommand { get; }

    public void AttachClipboard(IClipboard clipboard)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        HasClipboard = true;
    }

    private void OnDocumentServiceAvailabilityChanged(object? sender, EventArgs e)
    {
        CanUseFileSystem = _documentService.IsAvailable;
    }

    private void EvaluateDocument()
    {
        Lines.Clear();
        _evaluator.Clear();

        if (string.IsNullOrWhiteSpace(Document))
        {
            LineCount = 0;
            ErrorCount = 0;
            UpdateStatus("Ready", _readyStatusText);
            return;
        }

        var normalized = Document.Replace("\r\n", "\n");
        var rawLines = normalized.Split('\n');
        var errorCount = 0;

        for (var index = 0; index < rawLines.Length; index++)
        {
            var rawLine = rawLines[index];
            var line = new CalculatorLine
            {
                LineNumber = index + 1,
                Input = rawLine
            };

            var trimmed = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                Lines.Add(line);
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                line.IsComment = true;
                line.Result = trimmed.Length > 1 ? trimmed[1..].Trim() : string.Empty;
                Lines.Add(line);
                continue;
            }

            var parseResult = NumpusParser.ParseExpression(rawLine);

            if (!parseResult.Success || parseResult.Value is null)
            {
                line.HasError = true;
                line.Result = $"Parse error: {parseResult.Error ?? "Unknown parse error."}";
                errorCount++;
                Lines.Add(line);
                continue;
            }

            var evaluation = _evaluator.Evaluate(parseResult.Value);
            line.Result = evaluation.GetDisplayValue();
            line.HasError = evaluation.HasError;

            if (evaluation.HasError)
            {
                errorCount++;
            }

            Lines.Add(line);
        }

        LineCount = rawLines.Length;
        ErrorCount = errorCount;

        if (errorCount > 0)
        {
            UpdateStatus($"{errorCount} error{(errorCount > 1 ? "s" : string.Empty)} found", _errorStatusText);
        }
        else
        {
            UpdateStatus("Evaluated successfully", _readyStatusText);
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (!CanUseFileSystem)
        {
            UpdateStatus("File system not available", _errorStatusText);
            return;
        }

        try
        {
            IsBusy = true;
            var content = await _documentService.LoadDocumentAsync(cancellationToken);

            if (content is null)
            {
                UpdateStatus("Load cancelled", _readyStatusText, true);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => Document = content, DispatcherPriority.Background, cancellationToken);
            UpdateStatus("File loaded", _readyStatusText, true);
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Load cancelled", _readyStatusText, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load document.");
            UpdateStatus("Failed to load document", _errorStatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanUseFileSystem)
        {
            UpdateStatus("File system not available", _errorStatusText);
            return;
        }

        try
        {
            IsBusy = true;
            var saved = await _documentService.SaveDocumentAsync(Document, cancellationToken);

            UpdateStatus(saved ? "File saved" : "Save cancelled", _readyStatusText, true);
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Save cancelled", _readyStatusText, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document.");
            UpdateStatus("Failed to save document", _errorStatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearDocument()
    {
        Document = string.Empty;
        UpdateStatus("Document cleared", _readyStatusText, true);
    }

    private async Task CopyResultAsync(string? result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return;
        }

        if (_clipboard is null)
        {
            UpdateStatus("Clipboard not available", _clipboardStatusText);
            return;
        }

        try
        {
            await _clipboard.SetTextAsync(result);
            UpdateStatus("Copied to clipboard", _readyStatusText, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy value to clipboard.");
            UpdateStatus("Failed to copy to clipboard", _errorStatusText);
        }
    }

    private void UpdateStatus(string message, string statusText, bool autoReset = false)
    {
        StatusMessage = message;
        StatusText = statusText;

        if (autoReset)
        {
            _statusResetSource?.Cancel();
            _statusResetSource?.Dispose();

            _statusResetSource = new CancellationTokenSource();
            var token = _statusResetSource.Token;

            _ = ResetStatusAsync(token);
        }
    }

    private async Task ResetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_statusResetDelay, cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    StatusMessage = HasErrors
                        ? $"{ErrorCount} error{(ErrorCount > 1 ? "s" : string.Empty)} found"
                        : "Ready";

                    StatusText = HasErrors ? _errorStatusText : _readyStatusText;
                },
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Ignored because a newer status update has superseded this one.
        }
    }

    public void Dispose()
    {
        _documentService.AvailabilityChanged -= OnDocumentServiceAvailabilityChanged;
        _statusResetSource?.Cancel();
        _statusResetSource?.Dispose();
        _cleanup.Dispose();
    }
}
