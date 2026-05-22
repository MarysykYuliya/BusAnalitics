using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class ProductController : Controller
{
    private readonly IProductService _productService;
    private readonly IBusinessService _businessService;
    private readonly IProductCategoryService _categoryService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProductController(IProductService productService,
                             IBusinessService businessService,
                             IProductCategoryService categoryService,
                             UserManager<ApplicationUser> userManager)
    {
        _productService = productService;
        _businessService = businessService;
        _categoryService = categoryService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(Guid? businessId)
    {
        var userId = _userManager.GetUserId(User);
        var businesses = await _businessService.GetUserBusinessesAsync(userId);

        if (!businesses.Any())
            return RedirectToAction("Create", "Business");

        var selectedBusinessId = businessId ?? businesses.First().Id;

        var products = await _productService.GetProductsAsync(selectedBusinessId);
        var categories = await _categoryService.GetCategoriesByBusinessAsync(selectedBusinessId);

        ViewBag.Businesses = businesses;
        ViewBag.SelectedBusinessId = selectedBusinessId;
        ViewBag.Categories = categories;

        return View(products);
    }

    public async Task<IActionResult> Create(Guid businessId)
    {
        var categories = await _categoryService.GetCategoriesByBusinessAsync(businessId);
        ViewBag.BusinessId = businessId;
        ViewBag.Categories = categories;
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model, Guid businessId)
    {
        //if (!ModelState.IsValid) return View(model);

        model.BusinessAccountId = businessId;
        await _productService.CreateProductAsync(model);
        return RedirectToAction("Index", new { businessId });
    }
    public async Task<IActionResult> Edit(Guid id, Guid businessId)
    {
        var product = await _productService.GetProductByIdAsync(id, businessId);
        if (product == null) return NotFound();

        var categories = await _categoryService.GetCategoriesByBusinessAsync(businessId);
        ViewBag.Categories = categories;
        ViewBag.BusinessId = businessId;
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Product model, Guid businessId)
    {
        //if (!ModelState.IsValid) return View(model);

        await _productService.UpdateProductAsync(model);
        return RedirectToAction("Index", new { businessId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid businessId)
    {
        await _productService.DeleteProductAsync(id, businessId);
        return RedirectToAction("Index", new { businessId });
    }
    public async Task<IActionResult> GetCategoriesByBusiness(Guid businessId)
    {
        var categories = await _categoryService.GetCategoriesByBusinessAsync(businessId);
        var result = categories.Select(c => new { id = c.Id, name = c.Name });
        return Json(result);
    }

    public async Task<IActionResult> FilterProducts(Guid businessId, Guid? categoryId)
    {
        var products = await _productService.GetProductsAsync(businessId);

        if (categoryId.HasValue)
            products = products.Where(p => p.ProductCategoryId == categoryId).ToList();

        return PartialView("_ProductTablePartial", products);
    }
}
