using System.Text.Json;
using CALAC.Domain.Entities;
using CALAC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CALAC.Infrastructure.BackgroundServices;

public class OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                logger.LogInformation("Processing outbox message {MessageId} of type {Type}", message.Id, message.Type);

                // Simulate dispatching to different handlers based on message type
                await SimulateDispatchAsync(message, ct);

                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                message.Error = ex.Message;
            }
        }

        if (messages.Any())
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SimulateDispatchAsync(OutboxMessage message, CancellationToken ct)
    {
        // This simulates what a real dispatcher (MediatR or custom) would do
        switch (message.Type)
        {
            case "StockUpdated":
                logger.LogDebug("Syncing stock change to external ERP via IEcommerceAdapter...");
                break;
            case "GoodsReceiptCompleted":
                logger.LogDebug("Notifying external Logistics Hub of new stock arrivals...");
                break;
            default:
                logger.LogDebug("Generic message dispatch for {Type}", message.Type);
                break;
        }

        await Task.Delay(50, ct); // Simulate I/O
    }
}
