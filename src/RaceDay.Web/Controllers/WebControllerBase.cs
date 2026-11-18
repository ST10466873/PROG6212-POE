using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Shared API error handling for all controllers: expired sessions are redirected to
/// login and every other API failure is surfaced as a friendly validation message.
/// </summary>
public abstract class WebControllerBase : Controller
{
    protected readonly ApiClient Api;

    protected WebControllerBase(ApiClient api) => Api = api;

    /// <summary>Returns a redirect when the API reports 401; otherwise null.</summary>
    protected IActionResult? SessionExpired(IApiResult result)
    {
        if (result.Success || result.StatusCode != 401) return null;
        HttpContext.Session.SignOut();
        TempData["Error"] = "Your session expired. Please log in again.";
        return RedirectToAction("Login", "Account");
    }

    /// <summary>Adds an API error to ModelState and TempData for the alert banner.</summary>
    protected void Report(IApiResult result)
    {
        var message = result.Error ?? "The request could not be completed.";
        ModelState.AddModelError(string.Empty, message);
        TempData["Error"] = message;
    }

    /// <summary>Convenience helper combining both behaviours above.</summary>
    protected IActionResult? HandleFailure(IApiResult result)
    {
        if (result.Success) return null;
        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;
        Report(result);
        return null;
    }
}
