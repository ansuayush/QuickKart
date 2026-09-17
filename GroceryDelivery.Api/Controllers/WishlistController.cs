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
public class WishlistController : ControllerBase
{
    private readonly AppDbContext _db;
    public WishlistController(AppDbContext db) => _db = db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _db.WishlistItems.Include(w => w.Product).Where(w => w.UserId == UserId).ToListAsync();
        return Ok(items.Select(w => new
        {
            w.Id,
            w.ProductId,
            name = w.Product!.Name,
            price = w.Product.Price,
            unit = w.Product.Unit,
            imageUrl = w.Product.ImageUrl,
            stock = w.Product.Stock
        }));
    }

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> Add(int productId)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId)) return NotFound("Product not found.");
        var exists = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == UserId && w.ProductId == productId);
        if (exists is null)
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = UserId, ProductId = productId });
            await _db.SaveChangesAsync();
        }
        return await Get();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Remove(int productId)
    {
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == UserId && w.ProductId == productId);
        if (item is not null)
        {
            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync();
        }
        return await Get();
    }
}
