using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BusinessAnalytics.Controllers
{
    [Authorize]
    public class UtilitiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UtilitiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Utilities?businessId=...&month=...&year=...
        public async Task<IActionResult> Index(Guid? businessId, int? month, int? year)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var businesses = await _context.BusinessAccounts
                .Where(b => b.OwnerId == userId)
                .ToListAsync();

            ViewBag.Businesses = businesses;
            ViewBag.SelectedBusinessId = businessId;

            if (businessId == null || !businesses.Any(b => b.Id == businessId))
            {
                return View(new List<Premises>());
            }

            var premises = await _context.Premises
                .Where(p => p.BusinessAccountId == businessId)
                .Include(p => p.UtilityRecords)
                    .ThenInclude(r => r.UtilityType)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var utilityTypes = await _context.UtilityTypes
                .Where(u => u.BusinessAccountId == businessId)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.UtilityTypes = utilityTypes;
            ViewBag.CurrentMonth = month ?? DateTime.Now.Month;
            ViewBag.CurrentYear = year ?? DateTime.Now.Year;

            return View(premises);
        }

        // POST: /Utilities/CreatePremises
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePremises(Guid businessId, string name, string? address)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();

            _context.Premises.Add(new Premises
            {
                Id = Guid.NewGuid(),
                BusinessAccountId = businessId,
                Name = name,
                Address = address
            });
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { businessId });
        }

        // POST: /Utilities/DeletePremises
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePremises(Guid id, Guid businessId)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();
            var p = await _context.Premises.FindAsync(id);
            if (p != null)
            {
                _context.Premises.Remove(p);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", new { businessId });
        }

        // POST: /Utilities/CreateUtilityType
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUtilityType(Guid businessId, string name)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();

            _context.UtilityTypes.Add(new UtilityType
            {
                Id = Guid.NewGuid(),
                BusinessAccountId = businessId,
                Name = name
            });
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { businessId });
        }

        // POST: /Utilities/DeleteUtilityType
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUtilityType(Guid id, Guid businessId)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();
            var ut = await _context.UtilityTypes.FindAsync(id);
            if (ut != null)
            {
                _context.UtilityTypes.Remove(ut);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", new { businessId });
        }

        // POST: /Utilities/SaveRecord
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecord(Guid businessId, Guid premisesId, Guid utilityTypeId,
            int year, int month, decimal amount, string? note)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();

            var existing = await _context.UtilityRecords
                .FirstOrDefaultAsync(r => r.PremisesId == premisesId
                    && r.UtilityTypeId == utilityTypeId
                    && r.Year == year && r.Month == month);

            if (existing != null)
            {
                existing.Amount = amount;
                existing.Note = note;
                
                if (existing.IsPaid && existing.LinkedTransactionId.HasValue)
                {
                    var tx = await _context.Transactions.FindAsync(existing.LinkedTransactionId.Value);
                    if (tx != null)
                    {
                        tx.TotalAmount = amount;
                    }
                }
            }
            else
            {
                _context.UtilityRecords.Add(new UtilityRecord
                {
                    Id = Guid.NewGuid(),
                    PremisesId = premisesId,
                    UtilityTypeId = utilityTypeId,
                    Year = year,
                    Month = month,
                    Amount = amount,
                    Note = note
                });
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { businessId });
        }

        // POST: /Utilities/MarkPaid
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(Guid id, Guid businessId)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();

            var record = await _context.UtilityRecords
                .Include(r => r.Premises)
                .Include(r => r.UtilityType)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null || record.IsPaid) return RedirectToAction("Index", new { businessId });

            record.IsPaid = true;
            record.PaidAt = DateTime.UtcNow;

            // Auto-create expense transaction
            var monthNames = new[] { "", "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень",
                "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень" };

            // Construct a date in the target month.
            // If it's the current month and year, we use today's date.
            // If it's a historical month, we use the 1st day of that month so it correctly lands in that period's analytics.
            var now = DateTime.Now;
            var transactionDate = (record.Year == now.Year && record.Month == now.Month)
                ? now
                : new DateTime(record.Year, record.Month, 1, 12, 0, 0);

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                BusinessAccountId = businessId,
                TransactionType = TransactionType.Expense,
                Date = transactionDate,
                TotalAmount = record.Amount,
                Description = $"{record.UtilityType.Name} — {record.Premises.Name} ({monthNames[record.Month]} {record.Year})"
            };

            _context.Transactions.Add(transaction);
            record.LinkedTransactionId = transaction.Id;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"✓ Оплату зафіксовано і додано в транзакції ({record.Amount:N2} ₴)";
            return RedirectToAction("Index", new { businessId });
        }

        // POST: /Utilities/MarkUnpaid
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnpaid(Guid id, Guid businessId)
        {
            if (!await OwnsBusinessAsync(businessId)) return Forbid();

            var record = await _context.UtilityRecords.FindAsync(id);
            if (record == null || !record.IsPaid) return RedirectToAction("Index", new { businessId });

            // Remove linked transaction
            if (record.LinkedTransactionId.HasValue)
            {
                var tx = await _context.Transactions.FindAsync(record.LinkedTransactionId.Value);
                if (tx != null) _context.Transactions.Remove(tx);
            }

            record.IsPaid = false;
            record.PaidAt = null;
            record.LinkedTransactionId = null;

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { businessId });
        }

        private async Task<bool> OwnsBusinessAsync(Guid businessId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _context.BusinessAccounts.AnyAsync(b => b.Id == businessId && b.OwnerId == userId);
        }
    }
}
