using ApiConControllers.Entities;
using Microsoft.AspNetCore.Authorization;
namespace ApiConControllers.Auth;

// Requisito propio: "la suscripción del usuario debe ser al menos X".
// RequireClaim("subscription", "standard", "premium") enumeraría los valores a mano;
// con un requisito + handler la regla "este nivel o superior" vive en un solo lugar.
public class SubscriptionRequirement(string minimum) : IAuthorizationRequirement
{
    public string Minimum { get; } = minimum;
}

public class SubscriptionHandler : AuthorizationHandler<SubscriptionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SubscriptionRequirement requirement)
    {
        var current = context.User.FindFirst(AppClaimTypes.Subscription)?.Value;

        if (Subscriptions.Rank(current) >= Subscriptions.Rank(requirement.Minimum))
        {
            context.Succeed(requirement);
        }

        // Si no cumple NO llamamos a context.Fail(): simplemente no se marca como satisfecho
        // y la política falla. Fail() vetaría la política aunque otro handler diga que sí.
        return Task.CompletedTask;
    }
}
