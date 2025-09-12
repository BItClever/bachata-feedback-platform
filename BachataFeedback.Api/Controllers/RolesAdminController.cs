using BachataFeedback.Api.Authorization;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = SystemRoles.Admin)]
public class RolesAdminController : ControllerBase
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<User> _userManager;

    public RolesAdminController(RoleManager<IdentityRole> roleManager, UserManager<User> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    // GET: /api/admin/roles
    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var roles = _roleManager.Roles.ToList();
        var result = new List<object>();

        foreach (var role in roles)
        {
            var claims = await _roleManager.GetClaimsAsync(role);
            var perms = claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray();

            result.Add(new
            {
                role.Id,
                role.Name,
                permissions = perms
            });
        }

        return Ok(result);
    }

    // GET: /api/admin/roles/permissions
    [HttpGet("permissions")]
    public IActionResult GetAllPermissions()
    {
        return Ok(Permissions.All);
    }

    // POST: /api/admin/roles/sync   (обновляет роли/permissions согласно RolePermissionMap)
    [HttpPost("sync")]
    public async Task<IActionResult> SyncRoles()
    {
        foreach (var kvp in RolePermissionMap.All)
        {
            var roleName = kvp.Key;
            var targetPerms = new HashSet<string>(kvp.Value);

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName);
                var created = await _roleManager.CreateAsync(role);
                if (!created.Succeeded)
                    return BadRequest(new { success = false, message = $"Failed to create role {roleName}" });
            }

            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingPerms = existingClaims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();

            // Add missing
            foreach (var perm in targetPerms.Except(existingPerms))
            {
                var res = await _roleManager.AddClaimAsync(role, new Claim("permission", perm));
                if (!res.Succeeded)
                    return BadRequest(new { success = false, message = $"Failed to add permission {perm} to role {roleName}" });
            }

            // Remove extra (опционально)
            foreach (var extra in existingPerms.Except(targetPerms))
            {
                var claim = existingClaims.First(c => c.Type == "permission" && c.Value == extra);
                var res = await _roleManager.RemoveClaimAsync(role, claim);
                if (!res.Succeeded)
                    return BadRequest(new { success = false, message = $"Failed to remove permission {extra} from role {roleName}" });
            }
        }

        return Ok(new { success = true, message = "Roles synchronized" });
    }

    public class AssignRoleDto
    {
        public string Role { get; set; } = string.Empty;
    }

    // POST: /api/admin/roles/users/{userId}/assign
    [HttpPost("users/{userId}/assign")]
    public async Task<IActionResult> AssignRoleToUser(string userId, [FromBody] AssignRoleDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        var roleExists = await _roleManager.RoleExistsAsync(dto.Role);
        if (!roleExists) return BadRequest(new { success = false, message = "Role does not exist" });

        var result = await _userManager.AddToRoleAsync(user, dto.Role);
        if (!result.Succeeded)
            return BadRequest(new { success = false, message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { success = true, message = $"Role {dto.Role} assigned to user" });
    }

    // POST: /api/admin/roles/users/{userId}/revoke
    [HttpPost("users/{userId}/revoke")]
    public async Task<IActionResult> RevokeRoleFromUser(string userId, [FromBody] AssignRoleDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        var roleExists = await _roleManager.RoleExistsAsync(dto.Role);
        if (!roleExists) return BadRequest(new { success = false, message = "Role does not exist" });

        var result = await _userManager.RemoveFromRoleAsync(user, dto.Role);
        if (!result.Succeeded)
            return BadRequest(new { success = false, message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { success = true, message = $"Role {dto.Role} revoked from user" });
    }

    // GET: /api/admin/roles/users/{userId}
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserRolesAndPermissions(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        var roles = await _userManager.GetRolesAsync(user);

        // Собираем permissions из role claims
        var perms = new HashSet<string>();
        foreach (var r in roles)
        {
            var role = await _roleManager.FindByNameAsync(r);
            if (role == null) continue;
            var claims = await _roleManager.GetClaimsAsync(role);
            foreach (var c in claims.Where(c => c.Type == "permission"))
                perms.Add(c.Value);
        }

        return Ok(new
        {
            userId = user.Id,
            roles,
            permissions = perms.ToArray()
        });
    }
}