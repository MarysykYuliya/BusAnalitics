using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IBusinessService _businessService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(IBusinessService businessService, UserManager<ApplicationUser> userManager)
        {
            _businessService = businessService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var businesses = await _businessService.GetUserBusinessesAsync(userId);
            return View(businesses);
        }
    }
}
