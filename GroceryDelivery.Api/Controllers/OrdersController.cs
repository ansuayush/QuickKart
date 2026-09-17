using System.Security.Claims;
using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using GroceryDelivery.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    public const double StoreLat = 12.9352;
    public const double StoreLng = 77.6245;

    private readonly AppDbContext _db;
    public OrdersController(AppDbContext db) => _db = db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    private async Task<bool> CanAccessOrder(Order order)
    {
        if (IsAdmin || order.UserId == UserId) return true;
        var me = await _db.Users.FindAsync(UserId);
        return me?.DeliveryBoyId is int boyId && order.DeliveryBoyId == boyId;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var q = _db.Orders.Include(o => o.Items).Include(o => o.Address).Include(o => o.DeliveryBoy).Include(o => o.User).AsQueryable();
        if (!IsAdmin)
        {
            var me = await _db.Users.FindAsync(UserId);
            if (me?.DeliveryBoyId is int boyId)
                q = q.Where(o => o.DeliveryBoyId == boyId);
            else
                q = q.Where(o => o.UserId == UserId);
        }
        return Ok(await q.OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).Include(o => o.Address).Include(o => o.DeliveryBoy).Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (!await CanAccessOrder(order)) return Forbid();
        return Ok(order);
    }

    [HttpGet("{id:int}/tracking")]
    public async Task<IActionResult> Tracking(int id)
    {
        var order = await _db.Orders.Include(o => o.Address).Include(o => o.DeliveryBoy).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (!await CanAccessOrder(order)) return Forbid();

        var destLat = order.Address?.Latitude ?? 12.9716;
        var destLng = order.Address?.Longitude ?? 77.5946;
        var storeLat = order.StoreLat == 0 ? StoreLat : order.StoreLat;
        var storeLng = order.StoreLng == 0 ? StoreLng : order.StoreLng;
        double t = 0;
        if (order.Status == "Delivered") t = 1;
        else if (order.Status is "OutForDelivery" or "PickedUp")
        {
            var start = order.OutForDeliveryAt ?? order.PickedUpAt ?? order.CreatedAt;
            t = Math.Clamp((DateTime.UtcNow - start).TotalSeconds / 180.0, 0.15, 0.95);
        }
        else if (order.Status is "Accepted" or "ArrivedAtStore" or "Assigned")
            t = 0.08;

        var driverLat = storeLat + (destLat - storeLat) * t;
        var driverLng = storeLng + (destLng - storeLng) * t;
        var elapsedMin = Math.Max(0, (int)(DateTime.UtcNow - order.CreatedAt).TotalMinutes);
        var slaMin = 12;
        var deliveredAt = order.DeliveredAt;
        var totalMin = order.Status == "Delivered" && deliveredAt is not null
            ? Math.Max(0, (int)(deliveredAt.Value - order.CreatedAt).TotalMinutes)
            : elapsedMin;
        var onTime = order.Status == "Cancelled" ? (bool?)null : totalMin <= slaMin;
        string? issue = null;
        if (order.Status == "Cancelled") issue = "Order was cancelled.";
        else if (order.DeliveryBoyId is null && order.Status is "Packed" or "Picking")
            issue = "Waiting for a delivery partner to be assigned.";
        else if (order.Status == "Assigned" && order.AssignedAt is not null && DateTime.UtcNow - order.AssignedAt > TimeSpan.FromMinutes(8))
            issue = "Assigned rider has not accepted yet.";
        else if (onTime == false && order.Status != "Delivered")
            issue = $"Running late versus the {slaMin}-minute delivery promise.";
        else if (onTime == false && order.Status == "Delivered")
            issue = "Delivered after the promised time.";

        var etaMin = order.Status == "Delivered" ? 0 : Math.Max(1, slaMin - elapsedMin);
        var mapUrl = $"https://www.google.com/maps?saddr={driverLat:F6},{driverLng:F6}&daddr={destLat:F6},{destLng:F6}&output=embed";

        return Ok(new
        {
            order.Status,
            order.OrderNumber,
            destination = new { lat = destLat, lng = destLng, label = $"{order.Address?.Line1}, {order.Address?.City}" },
            store = new { lat = storeLat, lng = storeLng },
            rider = new { lat = driverLat, lng = driverLng },
            deliveryBoy = order.DeliveryBoy is null ? null : new
            {
                order.DeliveryBoy.Id,
                order.DeliveryBoy.Name,
                order.DeliveryBoy.Phone,
                order.DeliveryBoy.Vehicle,
                order.DeliveryBoy.PhotoUrl,
                order.DeliveryBoy.RatingAverage
            },
            etaMinutes = etaMin,
            elapsedMinutes = elapsedMin,
            slaMinutes = slaMin,
            onTime,
            issue,
            mapUrl,
            googleMapsLink = $"https://www.google.com/maps/dir/{driverLat:F6},{driverLng:F6}/{destLat:F6},{destLng:F6}"
        });
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutRequest req)
    {
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == req.AddressId && a.UserId == UserId);
        if (address is null) return BadRequest("Select a delivery address.");

        var cart = await _db.CartItems.Include(c => c.Product).Where(c => c.UserId == UserId).ToListAsync();
        if (cart.Count == 0) return BadRequest("Cart is empty.");

        foreach (var line in cart)
        {
            if (line.Product is null || !line.Product.IsActive)
                return BadRequest($"{line.Product?.Name ?? "Item"} is unavailable.");
            if (line.Quantity > line.Product.Stock)
                return BadRequest($"Only {line.Product.Stock} left for {line.Product.Name}.");
        }

        var subtotal = cart.Sum(c => c.Product!.Price * c.Quantity);
        var delivery = subtotal >= 199 ? 0m : 25m;
        var method = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "COD" : req.PaymentMethod.ToUpperInvariant();
        if (method is "UPI" or "CARD" && string.IsNullOrWhiteSpace(req.PaymentRef))
            return BadRequest("Complete UPI or card payment first.");

        var paid = method is "UPI" or "CARD" || !string.IsNullOrWhiteSpace(req.PaymentRef);
        var order = new Order
        {
            OrderNumber = $"QK{DateTime.UtcNow:yyyyMMddHHmmss}{UserId}",
            UserId = UserId,
            AddressId = address.Id,
            Subtotal = subtotal,
            DeliveryFee = delivery,
            Total = subtotal + delivery,
            PaymentMethod = method,
            PaymentStatus = paid ? "Paid" : "Pending",
            PaymentRef = req.PaymentRef ?? "",
            Status = paid ? "Confirmed" : "Placed",
            StoreLat = StoreLat,
            StoreLng = StoreLng,
            Items = cart.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                ProductName = c.Product!.Name,
                Unit = c.Product.Unit,
                UnitPrice = c.Product.Price,
                Quantity = c.Quantity
            }).ToList()
        };

        foreach (var line in cart)
            line.Product!.Stock -= line.Quantity;

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart);
        await _db.SaveChangesAsync();
        return Ok(await _db.Orders.Include(o => o.Items).Include(o => o.DeliveryBoy).FirstAsync(o => o.Id == order.Id));
    }

    [HttpPut("{id:int}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assign(int id, AssignRiderRequest req)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        var boy = await _db.DeliveryBoys.FindAsync(req.DeliveryBoyId);
        if (boy is null) return BadRequest("Delivery partner not found.");
        if (order.DeliveryBoyId is int oldId && oldId != boy.Id)
        {
            var previous = await _db.DeliveryBoys.FindAsync(oldId);
            if (previous is not null) { previous.DutyStatus = "Available"; previous.IsAvailable = true; }
        }
        order.DeliveryBoyId = boy.Id;
        order.Status = "Assigned";
        order.AssignedAt = DateTime.UtcNow;
        boy.DutyStatus = "Assigned";
        boy.IsAvailable = false;
        await _db.SaveChangesAsync();
        return Ok(await _db.Orders.Include(o => o.Items).Include(o => o.Address).Include(o => o.DeliveryBoy).FirstAsync(o => o.Id == id));
    }

    [HttpPost("{id:int}/rating")]
    public async Task<IActionResult> Rate(int id, RatingRequest req)
    {
        var order = await _db.Orders.Include(o => o.DeliveryBoy).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (order.UserId != UserId && !IsAdmin) return Forbid();
        if (order.Status == "Cancelled") return BadRequest("Cancelled orders cannot be rated.");
        if (req.Stars is < 1 or > 5) return BadRequest("Rating must be 1 to 5 stars.");
        if (order.Rating is not null) return BadRequest("This order is already rated.");
        order.Rating = req.Stars;
        order.Review = req.Review?.Trim() ?? "";
        order.RatedAt = DateTime.UtcNow;
        if (order.DeliveryBoy is not null)
        {
            var total = order.DeliveryBoy.RatingAverage * order.DeliveryBoy.RatingCount + req.Stars;
            order.DeliveryBoy.RatingCount += 1;
            order.DeliveryBoy.RatingAverage = Math.Round(total / order.DeliveryBoy.RatingCount, 2);
            if (req.PreferSameRider)
            {
                order.PreferSameRider = true;
                var customer = await _db.Users.FindAsync(order.UserId);
                if (customer is not null) customer.PreferredDeliveryBoyId = order.DeliveryBoyId;
            }
        }
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpPost("{id:int}/prefer-rider")]
    public async Task<IActionResult> PreferRider(int id)
    {
        var order = await _db.Orders.Include(o => o.DeliveryBoy).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        if (order.UserId != UserId && !IsAdmin) return Forbid();
        if (order.Status != "Delivered") return BadRequest("You can save a partner after the order is delivered.");
        if (order.DeliveryBoyId is null) return BadRequest("No delivery partner on this order.");
        order.PreferSameRider = true;
        var customer = await _db.Users.FindAsync(order.UserId);
        if (customer is not null) customer.PreferredDeliveryBoyId = order.DeliveryBoyId;
        await _db.SaveChangesAsync();
        return Ok(new { saved = true, deliveryBoy = order.DeliveryBoy });
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusBody body)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        var allowed = new[] { "Placed", "Confirmed", "Picking", "Packed", "Assigned", "Accepted", "ArrivedAtStore", "PickedUp", "OutForDelivery", "Delivered", "Cancelled" };
        if (!allowed.Contains(body.Status)) return BadRequest("Invalid status.");
        if (body.Status == "Cancelled" && order.Status != "Delivered")
        {
            var items = await _db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync();
            foreach (var item in items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product is not null) product.Stock += item.Quantity;
            }
            if (order.DeliveryBoyId is int cancelBoy)
            {
                var prev = await _db.DeliveryBoys.FindAsync(cancelBoy);
                if (prev is not null) { prev.DutyStatus = "Available"; prev.IsAvailable = true; }
            }
        }
        order.Status = body.Status;
        if (body.Status == "OutForDelivery") order.OutForDeliveryAt ??= DateTime.UtcNow;
        if (body.Status == "PickedUp") order.PickedUpAt ??= DateTime.UtcNow;
        if (body.Status == "Delivered")
        {
            order.PaymentStatus = "Paid";
            order.DeliveredAt = DateTime.UtcNow;
            if (order.DeliveryBoyId is int doneBoy)
            {
                var prev = await _db.DeliveryBoys.FindAsync(doneBoy);
                if (prev is not null) { prev.DutyStatus = "Available"; prev.IsAvailable = true; }
            }
        }
        if (body.Status == "Packed")
            await DeliveryAssignment.AssignNearestAsync(_db, order);
        await _db.SaveChangesAsync();
        return Ok(await _db.Orders.Include(o => o.Items).Include(o => o.Address).Include(o => o.DeliveryBoy).FirstAsync(o => o.Id == id));
    }

    [HttpGet("{id:int}/available-riders")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AvailableRiders(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        var storeLat = order.StoreLat == 0 ? StoreLat : order.StoreLat;
        var storeLng = order.StoreLng == 0 ? StoreLng : order.StoreLng;
        var list = await _db.DeliveryBoys.Where(d => d.IsActive).ToListAsync();
        return Ok(list.Select(d => new
        {
            d.Id,
            d.Name,
            d.Phone,
            d.Vehicle,
            d.VehicleNumber,
            d.DutyStatus,
            d.IsAvailable,
            d.RatingAverage,
            km = Math.Round(DeliveryAssignment.DistanceKm(storeLat, storeLng, d.CurrentLatitude, d.CurrentLongitude), 2)
        }).OrderBy(d => d.km));
    }

    [HttpGet("rider-jobs")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> RiderJobs()
    {
        var me = await _db.Users.FindAsync(UserId);
        if (me?.DeliveryBoyId is null && !IsAdmin) return Forbid();
        var q = _db.Orders.Include(o => o.Items).Include(o => o.Address).Include(o => o.User).Include(o => o.DeliveryBoy).AsQueryable();
        if (me?.DeliveryBoyId is int boyId && !IsAdmin)
            q = q.Where(o => o.DeliveryBoyId == boyId && o.Status != "Cancelled");
        return Ok(await q.OrderByDescending(o => o.CreatedAt).ToListAsync());
    }

    [HttpPost("{id:int}/accept")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> Accept(int id) => await RiderAdvance(id, "Accepted", boy => boy.DutyStatus = "PickingUp");

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> Reject(int id)
    {
        var order = await RiderOrder(id);
        if (order is null) return NotFound();
        if (order.Status is not "Assigned") return BadRequest("This order is not waiting for acceptance.");
        var skip = string.IsNullOrWhiteSpace(order.RejectedRiderIds) ? new List<int>() : order.RejectedRiderIds.Split(',').Where(s => int.TryParse(s, out _)).Select(int.Parse).ToList();
        if (order.DeliveryBoyId is int rid)
        {
            skip.Add(rid);
            var boy = await _db.DeliveryBoys.FindAsync(rid);
            if (boy is not null) { boy.DutyStatus = "Available"; boy.IsAvailable = true; }
        }
        order.RejectedRiderIds = string.Join(",", skip.Distinct());
        order.DeliveryBoyId = null;
        order.Status = "Packed";
        await DeliveryAssignment.AssignNearestAsync(_db, order, skip);
        await _db.SaveChangesAsync();
        return Ok(await LoadOrder(id));
    }

    [HttpPost("{id:int}/arrive-store")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> Arrive(int id) => await RiderAdvance(id, "ArrivedAtStore", null);

    [HttpPost("{id:int}/pickup")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> Pickup(int id) => await RiderAdvance(id, "PickedUp", boy => boy.DutyStatus = "PickingUp");

    [HttpPost("{id:int}/start-delivery")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> StartDelivery(int id) => await RiderAdvance(id, "OutForDelivery", boy => boy.DutyStatus = "OutForDelivery");

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = "DeliveryBoy,Admin")]
    public async Task<IActionResult> Complete(int id) => await RiderAdvance(id, "Delivered", boy =>
    {
        boy.DutyStatus = "Available";
        boy.IsAvailable = true;
    });

    private async Task<Order?> RiderOrder(int id)
    {
        var order = await _db.Orders.Include(o => o.DeliveryBoy).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return null;
        var me = await _db.Users.FindAsync(UserId);
        if (!IsAdmin && me?.DeliveryBoyId != order.DeliveryBoyId) return null;
        return order;
    }

    private async Task<IActionResult> RiderAdvance(int id, string status, Action<DeliveryBoy>? onBoy)
    {
        var order = await RiderOrder(id);
        if (order is null) return NotFound();
        order.Status = status;
        if (status == "PickedUp") order.PickedUpAt ??= DateTime.UtcNow;
        if (status == "OutForDelivery") order.OutForDeliveryAt ??= DateTime.UtcNow;
        if (status == "Delivered")
        {
            order.DeliveredAt = DateTime.UtcNow;
            order.PaymentStatus = "Paid";
        }
        if (order.DeliveryBoy is not null) onBoy?.Invoke(order.DeliveryBoy);
        await _db.SaveChangesAsync();
        return Ok(await LoadOrder(id));
    }

    private Task<Order> LoadOrder(int id) =>
        _db.Orders.Include(o => o.Items).Include(o => o.Address).Include(o => o.User).Include(o => o.DeliveryBoy).FirstAsync(o => o.Id == id);

    public record StatusBody(string Status);
}
