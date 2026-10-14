using System.Security.Claims;
using ApiConControllers.Entities;
using Microsoft.AspNetCore.Identity;
namespace ApiConControllers.Auth;

public static class UserManagerExtensions
{
    // Deja al usuario con UN solo claim de ese tipo y ese valor (alta o cambio).
    // Lo usan el seeder y el endpoint set-claim, así la regla no se duplica.
    public static async Task<IdentityResult> SetClaimAsync(
        this UserManager<ApplicationUser> userManager, ApplicationUser user, string type, string value)
    {
        var current = (await userManager.GetClaimsAsync(user)).Where(c => c.Type == type).ToList();

        if (current.Count == 1 && current[0].Value == value)
        {
            return IdentityResult.Success;   // ya está como se pide
        }

        if (current.Count > 0)
        {
            var removed = await userManager.RemoveClaimsAsync(user, current);
            if (!removed.Succeeded)
            {
                return removed;
            }
        }

        return await userManager.AddClaimAsync(user, new Claim(type, value));
    }
}
