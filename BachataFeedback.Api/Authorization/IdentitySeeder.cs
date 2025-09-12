using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace BachataFeedback.Api.Authorization;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var kvp in RolePermissionMap.All)
        {
            var roleName = kvp.Key;
            var neededPerms = kvp.Value;

            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
            }

            var existing = await roleManager.GetClaimsAsync(role);
            var existingPerms = existing.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();

            foreach (var perm in neededPerms)
            {
                if (!existingPerms.Contains(perm))
                {
                    await roleManager.AddClaimAsync(role, new Claim("permission", perm));
                }
            }
        }
    }
}