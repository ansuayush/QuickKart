using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Services;

public class DeliveryAssignment
{
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static async Task<DeliveryBoy?> AssignNearestAsync(AppDbContext db, Order order, IEnumerable<int>? excludeIds = null)
    {
        var skip = (excludeIds ?? Array.Empty<int>()).ToHashSet();
        if (!string.IsNullOrWhiteSpace(order.RejectedRiderIds))
        {
            foreach (var part in order.RejectedRiderIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out var id)) skip.Add(id);
        }

        var storeLat = order.StoreLat == 0 ? OrdersControllerStore.Lat : order.StoreLat;
        var storeLng = order.StoreLng == 0 ? OrdersControllerStore.Lng : order.StoreLng;

        var boys = await db.DeliveryBoys
            .Where(d => d.IsActive && (d.DutyStatus == "Available" || d.IsAvailable) && !skip.Contains(d.Id))
            .ToListAsync();
        if (boys.Count == 0) return null;

        var customer = await db.Users.FindAsync(order.UserId);
        DeliveryBoy? chosen = null;
        if (customer?.PreferredDeliveryBoyId is int preferId)
            chosen = boys.FirstOrDefault(b => b.Id == preferId);

        chosen ??= boys
            .OrderBy(b => DistanceKm(storeLat, storeLng, b.CurrentLatitude, b.CurrentLongitude))
            .First();

        order.DeliveryBoyId = chosen.Id;
        order.Status = "Assigned";
        order.AssignedAt = DateTime.UtcNow;
        chosen.DutyStatus = "Assigned";
        chosen.IsAvailable = false;
        return chosen;
    }
}

public static class OrdersControllerStore
{
    public const double Lat = 12.9352;
    public const double Lng = 77.6245;
}
