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
public class CartController : ControllerBase
{
    private readonly AppDbContext _db;
    public CartController(AppDbContext db) => _db = db;
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _db.CartItems.Include(c => c.Product).Where(c => c.UserId == UserId).ToListAsync();
        var mapped = items.Select(c => new
        {
            c.Id,
            c.ProductId,
            c.Quantity,
            name = c.Product!.Name,
            price = c.Product.Price,
            unit = c.Product.Unit,
            imageUrl = c.Product.ImageUrl,
            stock = c.Product.Stock,
            lineTotal = c.Product.Price * c.Quantity
        });
        var subtotal = items.Sum(c => c.Product!.Price * c.Quantity);
        return Ok(new { items = mapped, subtotal, deliveryFee = subtotal >= 199 ? 0 : 25, total = subtotal + (subtotal >= 199 ? 0 : (subtotal == 0 ? 0 : 25)) });
    }

    [HttpPost]
    public async Task<IActionResult> Add(CartItemRequest req)
    {
        var product = await _db.Products.FindAsync(req.ProductId);
        if (product is null || !product.IsActive) return NotFound("Product not found.");
        var qty = Math.Max(1, req.Quantity);
        if (qty > product.Stock) return BadRequest("Not enough stock.");

        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == req.ProductId);
        if (item is null)
        {
            item = new CartItem { UserId = UserId, ProductId = req.ProductId, Quantity = qty };
            _db.CartItems.Add(item);
        }
        else
        {
            if (item.Quantity + qty > product.Stock) return BadRequest("Not enough stock.");
            item.Quantity += qty;
        }
        await _db.SaveChangesAsync();
        return await Get();
    }

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> Update(int productId, CartItemRequest req)
    {
        var item = await _db.CartItems.Include(c => c.Product).FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == productId);
        if (item is null) return NotFound();
        if (req.Quantity <= 0)
            _db.CartItems.Remove(item);
        else if (req.Quantity > item.Product!.Stock)
            return BadRequest("Not enough stock.");
        else
            item.Quantity = req.Quantity;
        await _db.SaveChangesAsync();
        return await Get();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _db.CartItems.Where(c => c.UserId == UserId).ExecuteDeleteAsync();
        return await Get();
    }
}
