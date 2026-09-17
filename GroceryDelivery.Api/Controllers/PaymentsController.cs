using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroceryDelivery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    [HttpPost("charge")]
    public IActionResult Charge(ChargeRequest req)
    {
        var method = (req.Method ?? "").ToUpperInvariant();
        if (req.Amount <= 0) return BadRequest("Invalid amount.");

        if (method == "UPI")
        {
            var vpa = (req.UpiId ?? "quickkart@upi").Trim().ToLower();
            if (!vpa.Contains('@') || vpa.Length < 5)
                return BadRequest("Enter a valid UPI ID such as name@upi.");
            return Ok(new
            {
                paid = true,
                method = "UPI",
                paymentRef = $"UPI{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
                message = "UPI payment captured in test mode (no bank charge)."
            });
        }

        if (method == "CARD")
        {
            var pan = new string((req.CardNumber ?? "").Where(char.IsDigit).ToArray());
            var allowed = new[] { "4111111111111111", "4242424242424242", "2223003122003222" };
            if (!allowed.Contains(pan))
                return BadRequest("Use test card 4111 1111 1111 1111 (any future expiry, any CVV).");
            if (string.IsNullOrWhiteSpace(req.Expiry) || string.IsNullOrWhiteSpace(req.Cvv) || req.Cvv.Length < 3)
                return BadRequest("Enter expiry and CVV.");
            return Ok(new
            {
                paid = true,
                method = "CARD",
                paymentRef = $"CARD{DateTime.UtcNow:yyyyMMddHHmmss}{pan[^4..]}",
                last4 = pan[^4..],
                message = "Card authorized in test mode (no real charge)."
            });
        }

        return BadRequest("Choose UPI or CARD.");
    }
}
