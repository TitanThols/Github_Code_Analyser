using System.Security.Claims;

namespace OSSDependencyAnalyzer.API.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in account id. Only call from endpoints that require authorization.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid user id claim.");
}
