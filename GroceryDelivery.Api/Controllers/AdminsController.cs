using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admins")]
public class AdminsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var list = await _db.Users.Where(u => u.Role == "Admin")
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.Phone, u.Role })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AdminUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest("Email and a password of at least 6 characters are required.");
        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict("An account with this email already exists.");
        var user = new User
        {
            Name = req.Name.Trim(),
            Email = email,
            Phone = req.Phone?.Trim() ?? "",
            Role = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Name, user.Email, user.Phone, user.Role });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AdminUserRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Admin");
        if (user is null) return NotFound();
        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id))
            return Conflict("An account with this email already exists.");
        user.Name = req.Name.Trim();
        user.Email = email;
        user.Phone = req.Phone?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            if (req.Password.Length < 6) return BadRequest("Password must be at least 6 characters.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Name, user.Email, user.Phone, user.Role });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (id == userId) return BadRequest("You cannot delete your own admin account.");
        var admins = await _db.Users.CountAsync(u => u.Role == "Admin");
        if (admins <= 1) return BadRequest("At least one admin must remain.");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Admin");
        if (user is null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
