using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects MVC pages without duplicating authentication logic in every action.
/// Anonymous visitors are redirected to login; the wrong role sees an access-denied page.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSessionAttribute : ActionFilterAttribute
{
    private readonly string? _requiredRole;

    /// <summary>Creates the filter; omit the role to allow any authenticated user.</summary>
    public RequireSessionAttribute(string? requiredRole = null) => _requiredRole = requiredRole;

    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        var user = session.GetCurrentUser();

        if (user == null)
        {
            var request = context.HttpContext.Request;
            context.Result = new RedirectToActionResult("Login", "Account", new
            {
                returnUrl = request.Path + request.QueryString
            });
            return;
        }

        if (_requiredRole != null && !string.Equals(user.Role, _requiredRole, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
        }
    }
}
