﻿using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Numpus.Services;
using Velopack;
using Velopack.Logging;

namespace Numpus;

internal static class Program
{
    internal static VelopackMemoryLogger VelopackLog { get; } = new();

    [STAThread]
    public static void Main(string[] args)
    {
        // Handle Velopack lifecycle hooks (install/update/uninstall) before regular startup.
        VelopackApp.Build()
            .OnFirstRun(version => VelopackLog.Log(VelopackLogLevel.Information, $"First run on {version}.", null))
            .OnRestarted(version => VelopackLog.Log(VelopackLogLevel.Information, $"Restarted on {version}.", null))
            .SetLogger(VelopackLog)
            .Run();

        using var host = CreateHost(args);
        host.Start();

        App.ConfigureServices(host.Services);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        ConfigureServices(builder.Services);

        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        return builder.Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Compiler.Evaluator>();
        services.AddSingleton<IDocumentService, StorageProviderDocumentService>();
        services.AddSingleton(VelopackLog);
        services.AddHostedService<VelopackUpdateService>();
        services.AddSingleton<ViewModels.MainWindowViewModel>();
        services.AddSingleton<Views.MainWindow>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}

