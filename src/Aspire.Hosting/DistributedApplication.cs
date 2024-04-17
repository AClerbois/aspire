// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Represents a distributed application that implements the <see cref="IHost"/> and <see cref="IAsyncDisposable"/> interfaces.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="DistributedApplication"/> is an implementation of the <see cref="IHost"/> interface that orchestrates
/// a .NET Aspire application. To build an instance of the <see cref="DistributedApplication"/> class, use the
/// <see cref="DistributedApplication.CreateBuilder()"/> method to create an instance of the <see cref="IDistributedApplicationBuilder"/>
/// interface. Using the <see cref="IDistributedApplicationBuilder"/> interface you can configure the resources
/// that comprise the distributed application and describe the dependencies between them.
/// </para>
/// <para>
/// Once the distributed application has been defined use the <see cref="IDistributedApplicationBuilder.Build()"/> method
/// to create an instance of the <see cref="DistributedApplication"/> class. The <see cref="DistributedApplication"/> class
/// exposes a <see cref="DistributedApplication.Run"/> method which then starts the distributed application and its
/// resources.
/// </para>
/// <para>
/// The <see cref="CreateBuilder(Aspire.Hosting.DistributedApplicationOptions)"/> method provides additional options for
/// constructing the <see cref="IDistributedApplicationBuilder"/> including disabling the .NET Aspire dashboard (see <see cref="DistributedApplicationOptions.DisableDashboard"/>) or
/// allowing unsecured communication between the browser and dashboard, and dashboard and AppHost (see <see cref="DistributedApplicationOptions.AllowUnsecuredTransport"/>.
/// </para>
/// </remarks>
/// <example>
/// The following example shows creating a Postgres server resource with a database and referencing that
/// database in a .NET project.
/// <code>
/// var builder = DistributedApplication.CreateBuilder(args);
/// var inventoryDatabase = builder.AddPostgres("mypostgres").AddDatabase("inventory");
/// builder.AddProject&lt;Projects.InventoryService&gt;()
///        .WithReference(inventoryDatabase);
///
/// builder.Build().Run();
/// </code>
/// </example>
[DebuggerDisplay("{_host}")]
public class DistributedApplication : IHost, IAsyncDisposable
{
    private readonly IHost _host;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedApplication"/> class.
    /// </summary>
    /// <param name="host">The <see cref="IHost"/> instance.</param>
    public DistributedApplication(IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="IDistributedApplicationBuilder"/> interface.
    /// </summary>
    /// <returns>A new instance of the <see cref="IDistributedApplicationBuilder"/> interface.</returns>
    public static IDistributedApplicationBuilder CreateBuilder() => CreateBuilder([]);

    /// <summary>
    /// Creates a new instance of <see cref="IDistributedApplicationBuilder"/> with the specified command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments to use when building the distributed application.</param>
    /// <returns>A new instance of <see cref="IDistributedApplicationBuilder"/>.</returns>
    public static IDistributedApplicationBuilder CreateBuilder(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var builder = new DistributedApplicationBuilder(new DistributedApplicationOptions() { Args = args });
        return builder;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="IDistributedApplicationBuilder"/> interface with the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The <see cref="DistributedApplicationOptions"/> to use for configuring the builder.</param>
    /// <returns>A new instance of the <see cref="IDistributedApplicationBuilder"/> interface.</returns>
    public static IDistributedApplicationBuilder CreateBuilder(DistributedApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new DistributedApplicationBuilder(options);
        return builder;
    }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance configured for the application.
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Disposes the distributed application by disposing the <see cref="IHost"/>.
    /// </summary>
    public virtual void Dispose()
    {
        _host.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the distributed application by disposing the <see cref="IHost"/>.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public virtual ValueTask DisposeAsync()
    {
        return ((IAsyncDisposable)_host).DisposeAsync();
    }

    /// <inheritdoc cref="IHost.StartAsync" />
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteBeforeStartHooksAsync(cancellationToken).ConfigureAwait(false);
        await _host.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IHost.StopAsync" />
    public virtual async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="HostingAbstractionsHostExtensions.RunAsync" />
    public virtual async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteBeforeStartHooksAsync(cancellationToken).ConfigureAwait(false);
        await _host.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the distributed application and only completes when the token is triggered or shutdown is triggered.
    /// </summary>
    public void Run()
    {
        RunAsync().Wait();
    }

    // Internal for testing
    internal async Task ExecuteBeforeStartHooksAsync(CancellationToken cancellationToken)
    {
        AspireEventSource.Instance.AppBeforeStartHooksStart();

        try
        {
            var lifecycleHooks = _host.Services.GetServices<IDistributedApplicationLifecycleHook>();
            var appModel = _host.Services.GetRequiredService<DistributedApplicationModel>();

            foreach (var lifecycleHook in lifecycleHooks)
            {
                await lifecycleHook.BeforeStartAsync(appModel, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            AspireEventSource.Instance.AppBeforeStartHooksStop();
        }
    }

    Task IHost.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);

    Task IHost.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);
}
