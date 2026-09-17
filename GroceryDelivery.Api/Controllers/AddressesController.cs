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
public class AddressesController : ControllerBase
{
    private readonly AppDbContext _db;
    public AddressesController(AppDbContext db) => _db = db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await _db.Addresses.Where(a => a.UserId == UserId).OrderByDescending(a => a.IsDefault).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(AddressRequest req)
    {
        if (req.IsDefault)
            await _db.Addresses.Where(a => a.UserId == UserId).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

        var address = new Address
        {
            UserId = UserId,
            Label = req.Label,
            Line1 = req.Line1,
            City = req.City,
            Pincode = req.Pincode,
            Phone = req.Phone,
            IsDefault = req.IsDefault || !await _db.Addresses.AnyAsync(a => a.UserId == UserId),
            Latitude = req.Latitude is > 0 ? req.Latitude.Value : 12.9716,
            Longitude = req.Longitude is > 0 ? req.Longitude.Value : 77.5946
        };
        _db.Addresses.Add(address);
        await _db.SaveChangesAsync();
        return Ok(address);
    }
}
