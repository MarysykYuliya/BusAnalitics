using BusinessAnalytics.Data;
using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using BusinessAnalytics.Models.DTO;


namespace BusinessAnalytics.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly IBusinessService _businessService;
        private readonly IProductCategoryService _categoryService;
        private readonly ApplicationDbContext _context;
        private readonly IProductService _productService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TransactionController(
            ITransactionService transactionService,
            IBusinessService businessService,
            IProductCategoryService categoryService,
            IProductService productService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _transactionService = transactionService;
            _businessService = businessService;
            _categoryService = categoryService;
            _productService = productService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(Guid? businessId, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            const int pageSize = 5;
            var userId = _userManager.GetUserId(User);
            var businesses = await _businessService.GetBusinessesByUserAsync(userId);
            ViewBag.Businesses = businesses;

            if (businessId == null && businesses.Any())
                businessId = businesses.First().Id;

            ViewBag.SelectedBusinessId = businessId;

            var to = (endDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);
            var from = (startDate ?? DateTime.Today.AddDays(-30)).Date;

            ViewBag.StartDate = from.ToString("yyyy-MM-dd");
            ViewBag.EndDate = to.ToString("yyyy-MM-dd");

            if (businessId == null)
                return View(new List<Transaction>());

            var query = _context.Transactions
                .Where(t => t.BusinessAccountId == businessId &&
                            t.Date >= from && t.Date <= to)
                .Include(t => t.Product)
                .Include(t => t.ExpenseCategory)
                .OrderByDescending(t => t.Date);

            var totalCount = await query.CountAsync();
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.CurrentPage = page;

            return View(transactions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id, Guid businessId)
        {
            await _transactionService.DeleteTransactionAsync(id, businessId);
            return RedirectToAction(nameof(Index), new { businessId });
        }
        public async Task<IActionResult> Create(Guid? businessId)
        {
            var userId = _userManager.GetUserId(User);
            var businesses = await _businessService.GetBusinessesByUserAsync(userId);
            ViewBag.Businesses = businesses;

            if (businessId != null)
            {
                var categories = await _categoryService.GetCategoriesByBusinessAsync(businessId.Value);
                ViewBag.Categories = categories;

                var products = await _productService.GetProductsAsync(businessId.Value);
                ViewBag.Products = products;
            }

            ViewBag.SelectedBusinessId = businessId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Guid businessId,
            string date,
            TransactionType transactionType,
            List<Guid> productIds,
            List<int> quantities)
        {
            DateTime parsedDate;
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
            {
                if (!DateTime.TryParse(date, out parsedDate))
                {
                    parsedDate = DateTime.Now;
                }
            }

            Console.WriteLine($"[DEBUG] Create POST hit. businessId={businessId}, parsedDate={parsedDate}, type={transactionType}");
            Console.WriteLine($"[DEBUG] productIds.Count={productIds?.Count ?? 0}, quantities.Count={quantities?.Count ?? 0}");

            if (productIds == null || quantities == null)
                return BadRequest("Дані не передано");

            if (productIds.Count != quantities.Count)
                return BadRequest("Невідповідність між кількістю товарів і кількістю одиниць.");

            int savedCount = 0;

            for (int i = 0; i < productIds.Count; i++)
            {
                Console.WriteLine($"[DEBUG] Checking product {productIds[i]} with quantity {quantities[i]}");
                if (quantities[i] <= 0) continue;

                var product = await _productService.GetProductByIdAsync(productIds[i], businessId);
                if (product == null) 
                {
                    Console.WriteLine($"[DEBUG] Product not found for id {productIds[i]}");
                    continue;
                }

                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    BusinessAccountId = businessId,
                    ProductId = product.Id,
                    TransactionType = transactionType,
                    Date = parsedDate,
                    Quantity = quantities[i],
                    UnitPrice = product.FinalPrice,
                    TotalAmount = product.FinalPrice * quantities[i],
                    Description = $"Автоматично створено через груповий чек {DateTime.Now}"
                };

                await _transactionService.CreateTransactionAsync(transaction);
                savedCount++;
                Console.WriteLine($"[DEBUG] Transaction saved for product {product.Name}");
            }

            Console.WriteLine($"[DEBUG] Total transactions saved: {savedCount}");
            return RedirectToAction(nameof(Index), new { businessId });
        }

        [HttpGet]
        public async Task<JsonResult> GetCategoriesByBusiness(Guid businessId)
        {
            if (businessId == Guid.Empty) return Json(new List<object>());
            var categories = await _categoryService.GetCategoriesByBusinessAsync(businessId);
            return Json(categories.Select(c => new { id = c.Id, name = c.Name }));
        }

        [HttpGet]
        public async Task<JsonResult> GetProductsByBusinessAndCategory(Guid businessId, Guid? categoryId)
        {
            if (businessId == Guid.Empty) return Json(new List<object>());
            IEnumerable<Product> products;
            
            if (categoryId == null || categoryId == Guid.Empty)
            {
                products = await _productService.GetProductsAsync(businessId);
            }
            else
            {
                products = await _categoryService.GetProductsByCategoryAsync(businessId, categoryId.Value);
            }

            return Json(products.Select(p => new { id = p.Id, name = p.Name, finalPrice = p.FinalPrice }));
        }

        [HttpPost]
        public async Task<JsonResult> GetProductsByCategory([FromBody] ProductFilterRequest request)
        {
            if (request == null || request.BusinessId == Guid.Empty)
                return Json(new { success = false, message = "Невірні параметри" });

            IEnumerable<Product> products;

            if (request.CategoryId == Guid.Empty)
            {
                products = await _productService.GetProductsAsync(request.BusinessId);
            }
            else
            {
                products = await _categoryService.GetProductsByCategoryAsync(request.BusinessId, request.CategoryId);
            }

            var result = products.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                finalPrice = p.FinalPrice
            });

            return Json(new { success = true, products = result });
        }

        public class ProductFilterRequest
        {
            public Guid BusinessId { get; set; }
            public Guid CategoryId { get; set; }
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadCsv(IFormFile file, Guid businessId)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Будь ласка, оберіть файл.";
                return RedirectToAction(nameof(Index), new { businessId });
            }

            try
            {
                var config = new CsvConfiguration(new CultureInfo("uk-UA"))
                {
                    HasHeaderRecord = true,
                    Delimiter = ";",
                    MissingFieldFound = null
                };

                using var stream = new StreamReader(file.OpenReadStream());
                using var csv = new CsvReader(stream, config);
                
                var records = csv.GetRecords<TransactionCsvRecord>().ToList();
                var transactions = new List<Transaction>();
                
                var batchId = Guid.NewGuid();

                foreach (var record in records)
                {
                    transactions.Add(new Transaction
                    {
                        Id = Guid.NewGuid(),
                        BusinessAccountId = businessId,
                        Date = record.Date,
                        TotalAmount = Math.Abs(record.Amount),
                        TransactionType = record.Amount >= 0 ? TransactionType.Income : TransactionType.Expense,
                        Description = record.Description,
                        ImportBatchId = batchId
                    });
                }

                await _context.Transactions.AddRangeAsync(transactions);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Успішно імпортовано {transactions.Count} транзакцій!";
                TempData["LastBatchId"] = batchId.ToString(); 
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Помилка при читанні файлу. Перевірте формат CSV. Деталі: " + ex.Message;
            }

            return RedirectToAction(nameof(Index), new { businessId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoImport(Guid batchId, Guid businessId)
        {
            var transactionsToDelete = await _context.Transactions
                .Where(t => t.ImportBatchId == batchId)
                .ToListAsync();

            if (transactionsToDelete.Any())
            {
                _context.Transactions.RemoveRange(transactionsToDelete);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Імпорт скасовано. Видалено {transactionsToDelete.Count} транзакцій.";
            }

            return RedirectToAction(nameof(Index), new { businessId });
        }
    }
    
}
