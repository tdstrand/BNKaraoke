using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BNKaraoke.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BNKaraoke.Api.Pages.Admin
{
    public class ManageUsersModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ManageUsersModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            Users = new List<UserRoleViewModel>();
            Roles = new List<IdentityRole>();
        }

        public List<UserRoleViewModel> Users { get; set; }
        public List<IdentityRole> Roles { get; set; }

        public async Task OnGetAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRoleViewModels = new List<UserRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoleViewModels.Add(new UserRoleViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? string.Empty
                });
            }

            Users = userRoleViewModels;
            Roles = await _roleManager.Roles.ToListAsync();
        }

        public async Task<IActionResult> OnPostAssignRoleAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, userRoles);
                await _userManager.AddToRoleAsync(user, role);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
            }

            return RedirectToPage();
        }
    }

    public class UserRoleViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}