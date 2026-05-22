using BusinessAnalytics.Models.Entities;
using BusinessAnalytics.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Controllers
{
    [Authorize]
    public class BusinessController : Controller
    {
        private readonly IBusinessService _businessService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public BusinessController(IBusinessService businessService, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _businessService = businessService;
            _userManager = userManager;
            _env = env;
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BusinessAccount model, IFormFile? LogoFile)
        {
            //if (!ModelState.IsValid) return View(model);

            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images/businesses");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(LogoFile.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(stream);
                }

                model.LogoPath = "/images/businesses/" + fileName;
            }

            model.OwnerId = _userManager.GetUserId(User);
            await _businessService.CreateBusinessAsync(model);

            return RedirectToAction("Index", "Dashboard");
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var business = await _businessService.GetBusinessByIdAsync(id, userId);
            if (business == null) return NotFound();
            return View(business);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(BusinessAccount model, IFormFile? LogoFile)
        {
            //if (!ModelState.IsValid) return View(model);

            var userId = _userManager.GetUserId(User);
            var business = await _businessService.GetBusinessByIdAsync(model.Id, userId);
            if (business == null) return NotFound();

            business.Name = model.Name;
            business.Description = model.Description;
            business.Address = model.Address;
            business.ContactPerson = model.ContactPerson;
            business.PhoneNumber = model.PhoneNumber;
            business.Email = model.Email;
            business.RegistrationCode = model.RegistrationCode;

            if (LogoFile != null && LogoFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images/businesses");
                Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(LogoFile.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await LogoFile.CopyToAsync(stream);
                }

                business.LogoPath = "/images/businesses/" + fileName;
            }

            await _businessService.UpdateBusinessAsync(business);
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            await _businessService.DeleteBusinessAsync(id, userId);
            return RedirectToAction("Index", "Dashboard");
        }
    }
}
