using ExpenseManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Testcontainers.PostgreSql;

namespace ExpenseManager.Api.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> DockerUnavailableReason = new(CheckDocker);

    public PostgreSqlFactAttribute()
    {
        Skip = DockerUnavailableReason.Value;
    }

    private static string? CheckDocker()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format {{.ServerVersion}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
                return "Docker is unavailable; PostgreSQL integration test skipped.";
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return "Docker did not respond within five seconds; PostgreSQL integration test skipped.";
            }
            return process.ExitCode == 0
                ? null
                : $"Docker is unavailable; PostgreSQL integration test skipped: {process.StandardError.ReadToEnd().Trim()}";
        }
        catch (Exception exception)
        {
            return $"Docker is unavailable; PostgreSQL integration test skipped: {exception.Message}";
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection
    : ICollectionFixture<PostgreSqlIntegrationFixture>
{
    public const string Name = "PostgreSQL integration";
}

/// <summary>
/// Starts a real PostgreSQL instance for tests that depend on PostgreSQL-only
/// behavior (bytea, row concurrency and the actual EF migrations). Tests are
/// reported as skipped when Docker is not available instead of silently using
/// EF's in-memory provider.
/// </summary>
public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private PostgreSqlContainer? _container;
    private bool _startAttempted;
    private bool _started;
    private string? _unavailableReason;

    public PostgreSqlContainer Container => _container ??
        throw new InvalidOperationException("PostgreSQL container has not started.");
    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_started)
            await Container.DisposeAsync();
        _startLock.Dispose();
    }

    public async Task<AppDbContext> ResetAndCreateDbAsync()
    {
        await RequireDockerAsync();
        var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    public async Task<AppDbContext> CreateEmptyDbAsync()
    {
        await RequireDockerAsync();
        var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
        return db;
    }

    public AppDbContext CreateDb(string? connectionString = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString ?? ConnectionString)
            .EnableDetailedErrors()
            .Options;
        return new AppDbContext(options);
    }

    private async Task RequireDockerAsync()
    {
        await _startLock.WaitAsync();
        try
        {
            if (!_startAttempted)
            {
                _startAttempted = true;
                try
                {
                    _container = new PostgreSqlBuilder()
                        .WithImage("postgres:16-alpine")
                        .WithDatabase("expense_manager_integration")
                        .WithUsername("expense_test")
                        .WithPassword("expense-test-password")
                        .Build();
                    await _container.StartAsync();
                    _started = true;
                }
                catch (Exception exception)
                {
                    _unavailableReason =
                        $"Docker/PostgreSQL test container unavailable: {exception.GetBaseException().Message}";
                }
            }
        }
        finally
        {
            _startLock.Release();
        }

        if (!_started)
            throw new InvalidOperationException(_unavailableReason ??
                "Docker/PostgreSQL test container unavailable.");
    }
}
