using Microsoft.Extensions.Options;

namespace ExpenseManager.Api.Services;

public sealed class ReceiptProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReceiptProcessingOptions> options,
    ILogger<ReceiptProcessingWorker> logger) : BackgroundService
{
    private readonly ReceiptProcessingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IReceiptProcessingService>();
                processed = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Receipt processing worker iteration failed");
            }

            if (!processed)
            {
                try
                {
                    await Task.Delay(_options.PollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
