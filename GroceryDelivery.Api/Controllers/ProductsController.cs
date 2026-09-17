using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts(string? search = null, int? categoryId = null)
    {
        var q = _db.Products.Include(p => p.Category).Where(p => p.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(term) || p.Description.ToLower().Contains(term));
        }
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId.Value);
        return Ok(await q.OrderBy(p => p.Name).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var p = await _db.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : Ok(p);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductRequest req)
    {
        var product = Map(req);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductRequest req)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();
        product.Name = req.Name;
        product.Description = req.Description;
        product.Price = req.Price;
        product.Unit = req.Unit;
        product.ImageUrl = req.ImageUrl;
        product.Stock = req.Stock;
        product.IsActive = req.IsActive;
        product.CategoryId = req.CategoryId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return NotFound();
        p.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static Product Map(ProductRequest req) => new()
    {
        Name = req.Name,
        Description = req.Description,
        Price = req.Price,
        Unit = req.Unit,
        ImageUrl = req.ImageUrl,
        Stock = req.Stock,
        IsActive = req.IsActive,
        CategoryId = req.CategoryId
    };
}
