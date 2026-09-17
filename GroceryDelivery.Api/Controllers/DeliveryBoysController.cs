using System.Security.Claims;
using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DeliveryBoysController : ControllerBase
{
    private readonly AppDbContext _db;
    public DeliveryBoysController(AppDbContext db) => _db = db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var admin = User.IsInRole("Admin");
        var q = _db.DeliveryBoys.AsQueryable();
        if (!admin) q = q.Where(d => d.IsActive);
        return Ok(await q.OrderByDescending(d => d.IsAvailable).ThenBy(d => d.Name)
            .Select(d => new
            {
                d.Id, d.Name, d.Phone, d.Vehicle, d.VehicleType, d.VehicleNumber, d.PhotoUrl,
                d.IsAvailable, d.DutyStatus, d.IsActive, d.RatingAverage, d.RatingCount
            }).ToListAsync());
    }

    [HttpGet("preferred")]
    public async Task<IActionResult> Preferred()
    {
        var user = await _db.Users.FindAsync(UserId);
        if (user?.PreferredDeliveryBoyId is null) return Ok(null);
        var d = await _db.DeliveryBoys.FindAsync(user.PreferredDeliveryBoyId);
        return d is null ? Ok(null) : Ok(new { d.Id, d.Name, d.Phone, d.Vehicle, d.PhotoUrl, d.IsAvailable, d.RatingAverage, d.RatingCount });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(DeliveryBoyRequest req)
    {
        var boy = MapBoy(req);
        _db.DeliveryBoys.Add(boy);
        await _db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return BadRequest("Rider login needs a password of at least 6 characters.");
            await UpsertRiderUser(boy, req, createIfMissing: true);
            await _db.SaveChangesAsync();
        }
        return Ok(boy);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DeliveryBoyRequest req)
    {
        var boy = await _db.DeliveryBoys.FindAsync(id);
        if (boy is null) return NotFound();
        boy.Name = req.Name.Trim();
        boy.Phone = req.Phone.Trim();
        boy.VehicleType = string.IsNullOrWhiteSpace(req.VehicleType) ? "Bike" : req.VehicleType.Trim();
        boy.VehicleNumber = req.VehicleNumber?.Trim() ?? "";
        boy.Vehicle = $"{boy.VehicleType} · {boy.VehicleNumber}".Trim(' ', '·');
        boy.IsAvailable = req.IsAvailable;
        boy.DutyStatus = req.IsAvailable ? "Available" : "Offline";
        boy.IsActive = true;
        await UpsertRiderUser(boy, req, createIfMissing: false);
        await _db.SaveChangesAsync();
        return Ok(boy);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var boy = await _db.DeliveryBoys.FindAsync(id);
        if (boy is null) return NotFound();
        var busy = await _db.Orders.AnyAsync(o => o.DeliveryBoyId == id && o.Status != "Delivered" && o.Status != "Cancelled");
        if (busy) return BadRequest("This partner has an active order. Reassign it first.");
        var login = await _db.Users.Where(u => u.DeliveryBoyId == id).ToListAsync();
        foreach (var u in login)
        {
            u.DeliveryBoyId = null;
            u.Role = "Customer";
        }
        _db.DeliveryBoys.Remove(boy);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static DeliveryBoy MapBoy(DeliveryBoyRequest req)
    {
        var type = string.IsNullOrWhiteSpace(req.VehicleType) ? "Bike" : req.VehicleType.Trim();
        var num = req.VehicleNumber?.Trim() ?? "";
        return new DeliveryBoy
        {
            Name = req.Name.Trim(),
            Phone = req.Phone.Trim(),
            VehicleType = type,
            VehicleNumber = num,
            Vehicle = $"{type} · {num}".Trim(' ', '·'),
            DutyStatus = req.IsAvailable ? "Available" : "Offline",
            IsAvailable = req.IsAvailable,
            IsActive = true,
            PhotoUrl = "/images/rider.svg",
            CurrentLatitude = 12.9352,
            CurrentLongitude = 77.6245,
            StoreId = 1
        };
    }

    private async Task UpsertRiderUser(DeliveryBoy boy, DeliveryBoyRequest req, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return;
        var email = req.Email.Trim().ToLower();
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing is null)
        {
            if (!createIfMissing) return;
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                throw new InvalidOperationException("Rider login needs a password of at least 6 characters.");
            _db.Users.Add(new User
            {
                Name = boy.Name,
                Email = email,
                Phone = boy.Phone,
                Role = "DeliveryBoy",
                DeliveryBoyId = boy.Id,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            });
            return;
        }
        existing.Name = boy.Name;
        existing.Phone = boy.Phone;
        existing.Role = "DeliveryBoy";
        existing.DeliveryBoyId = boy.Id;
        if (!string.IsNullOrWhiteSpace(req.Password))
            existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
    }
}
