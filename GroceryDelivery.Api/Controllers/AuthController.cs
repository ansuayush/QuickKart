using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using GroceryDelivery.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    public AuthController(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest("Email and a password of at least 6 characters are required.");
        if (await _db.Users.AnyAsync(u => u.Email == req.Email.Trim().ToLower()))
            return Conflict("An account with this email already exists.");

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = req.Email.Trim().ToLower(),
            Phone = req.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "Customer"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new AuthResponse(_tokens.Create(user), user.Id, user.Name, user.Email, user.Role));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.Trim().ToLower());
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password.");
        return Ok(new AuthResponse(_tokens.Create(user), user.Id, user.Name, user.Email, user.Role));
    }
}
