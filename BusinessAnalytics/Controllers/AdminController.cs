using BusinessAnalytics.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index(string search)
        {
            var users = await _userManager.Users.ToListAsync();

            if (!string.IsNullOrEmpty(search))
            {
                users = users
                    .Where(u => (u.UserName != null && u.UserName.Contains(search, StringComparison.OrdinalIgnoreCase))
                             || (u.Email != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var model = new List<UserWithRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.Add(new UserWithRoleViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    Role = roles.FirstOrDefault() ?? "User"
                });
            }

            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeRole(Guid userId, string newRole)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!string.IsNullOrEmpty(newRole))
                await _userManager.AddToRoleAsync(user, newRole);

            await _userManager.UpdateSecurityStampAsync(user);

            TempData["Message"] = $"Роль користувача {user.UserName} змінено на {newRole}.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class UserWithRoleViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
    }
}
