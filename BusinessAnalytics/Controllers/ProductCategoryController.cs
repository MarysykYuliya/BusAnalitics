using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class ProductCategoryController : Controller
{
    private readonly IProductCategoryService _categoryService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBusinessService _businessService;

    public ProductCategoryController(IProductCategoryService categoryService,
                                     UserManager<ApplicationUser> userManager,
                                     IBusinessService businessService)
    {
        _categoryService = categoryService;
        _userManager = userManager;
        _businessService = businessService;
    }

    public async Task<IActionResult> Index(Guid? businessId)
    {
        var userId = _userManager.GetUserId(User);
        var businesses = await _businessService.GetUserBusinessesAsync(userId);
        if (!businesses.Any())
            return RedirectToAction("Create", "Business"); 

        var selectedBusinessId = businessId ?? businesses.First().Id;
        var categories = await _categoryService.GetCategoriesByBusinessAsync(selectedBusinessId);

        ViewBag.Businesses = businesses;
        ViewBag.SelectedBusinessId = selectedBusinessId;

        return View(categories);
    }

    public IActionResult Create(Guid businessId)
    {
        ViewBag.BusinessId = businessId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCategory model, Guid businessId)
    {
        //if (!ModelState.IsValid) return View(model);

        await _categoryService.CreateCategoryAsync(model, businessId);
        return RedirectToAction("Index", new { businessId });
    }

    public async Task<IActionResult> Edit(Guid id, Guid businessId)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id, businessId);
        if (category == null) return NotFound();

        ViewBag.BusinessId = businessId;
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductCategory model, Guid businessId)
    {
        //if (!ModelState.IsValid) return View(model);

        await _categoryService.UpdateCategoryAsync(model);
        return RedirectToAction("Index", new { businessId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid businessId)
    {
        await _categoryService.DeleteCategoryAsync(id, businessId);
        return RedirectToAction("Index", new { businessId });
    }
}
