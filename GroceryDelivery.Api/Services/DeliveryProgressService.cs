using GroceryDelivery.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Services;

public class DeliveryProgressService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    public DeliveryProgressService(IServiceScopeFactory scopes) => _scopes = scopes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var open = await db.Orders.Where(o => o.Status != "Delivered" && o.Status != "Cancelled").ToListAsync(stoppingToken);
                var now = DateTime.UtcNow;
                foreach (var order in open)
                {
                    var age = now - order.CreatedAt;
                    if (order.Status == "Placed" && age > TimeSpan.FromSeconds(12))
                        order.Status = "Confirmed";
                    else if (order.Status == "Confirmed" && age > TimeSpan.FromSeconds(28))
                        order.Status = "Picking";
                    else if (order.Status == "Picking" && age > TimeSpan.FromSeconds(45))
                    {
                        order.Status = "Packed";
                        await DeliveryAssignment.AssignNearestAsync(db, order);
                    }
                    else if (order.Status == "Packed" && order.DeliveryBoyId is null && age > TimeSpan.FromSeconds(50))
                        await DeliveryAssignment.AssignNearestAsync(db, order);
                }
                await db.SaveChangesAsync(stoppingToken);
            }
            catch
            {
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
