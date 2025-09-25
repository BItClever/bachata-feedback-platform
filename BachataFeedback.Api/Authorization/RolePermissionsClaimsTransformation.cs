using System.Security.Claims;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace BachataFeedback.Api.Authorization
{
    // На каждом запросе добавляет клеймы "permission" на основе ролей пользователя.
    // Политики вида [Authorize(Policy = "events.create")] начнут работать сразу после назначения роли.
    public sealed class RolePermissionsClaimsTransformation : IClaimsTransformation
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<User> _userManager;

        public RolePermissionsClaimsTransformation(RoleManager<IdentityRole> roleManager, UserManager<User> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated);
            if (identity == null) return principal;

            // Уже существующие permissions (например, если токен их уже содержит)
            var existingPerms = identity.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Роли из токена
            var roleNames = principal.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Если ролей в токене нет — достанем из БД по userId
            if (roleNames.Count == 0)
            {
                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        var dbRoles = await _userManager.GetRolesAsync(user);
                        roleNames.AddRange(dbRoles);
                    }
                }
            }

            // Соберём permission-клеймы из claims ролей
            var toAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var roleName in roleNames)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null) continue;
                var claims = await _roleManager.GetClaimsAsync(role);
                foreach (var cl in claims)
                {
                    if (cl.Type == "permission" && !string.IsNullOrWhiteSpace(cl.Value) && !existingPerms.Contains(cl.Value))
                        toAdd.Add(cl.Value);
                }
            }

            foreach (var p in toAdd)
                identity.AddClaim(new Claim("permission", p));

            return principal;
        }
    }
}