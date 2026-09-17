using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContactController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ContactInfo>> Get()
    {
        var row = await _db.ContactInfos.OrderBy(c => c.Id).FirstOrDefaultAsync();
        return Ok(row ?? new ContactInfo());
    }

    [Authorize(Roles = "Admin")]
    [HttpPut]
    public async Task<ActionResult<ContactInfo>> Update(ContactRequest req)
    {
        var row = await _db.ContactInfos.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (row is null)
        {
            row = new ContactInfo();
            _db.ContactInfos.Add(row);
        }
        row.Company = req.Company;
        row.Email = req.Email;
        row.Phone = req.Phone;
        row.WhatsApp = req.WhatsApp;
        row.Address = req.Address;
        row.Hours = req.Hours;
        row.Note = req.Note;
        await _db.SaveChangesAsync();
        return Ok(row);
    }
}
