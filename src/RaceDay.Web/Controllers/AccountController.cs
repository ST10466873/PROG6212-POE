using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Login, registration and logout. Credentials are sent to the Part 2 API; the MVC
/// project never checks a password or touches the database itself.
/// </summary>
public class AccountController : WebControllerBase
{
    public AccountController(ApiClient api) : base(api) { }

    /// <summary>Displays the login form.</summary>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    /// <summary>Authenticates through the API and starts an MVC session.</summary>
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await Api.PostAsync<AuthResponse>("/api/auth/login", new { email = model.Email, password = model.Password });
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Login failed. Please try again.");
            return View(model);
        }

        // Fetch the display name so the navbar can greet the user by name.
        var profile = await Api.GetAsync<ProfileVm>("/api/users/me");
        var name = profile.Data is { } p ? $"{p.FirstName} {p.LastName}" : result.Data.Email;
        HttpContext.Session.SignIn(result.Data.SessionToken, result.Data.UserId, result.Data.Role, name, result.Data.Email);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        if (result.Data.Role == "Organiser") return RedirectToAction("Dashboard", "Organiser");
        return RedirectToAction("Index", "Home");
    }

    /// <summary>Displays the account registration form.</summary>
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    /// <summary>Creates the account through the API, then asks the user to log in.</summary>
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Role is not ("Organiser" or "Participant"))
        {
            ModelState.AddModelError(nameof(model.Role), "Choose Organiser or Participant.");
            return View(model);
        }

        var result = await Api.PostAsync<object>("/api/auth/register", new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            email = model.Email,
            password = model.Password,
            role = model.Role,
            dateOfBirth = model.DateOfBirth.ToString("yyyy-MM-dd"),
            phoneNumber = model.PhoneNumber
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Registration failed. Please try again.");
            return View(model);
        }

        TempData["Success"] = "Account created. Log in to continue.";
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Ends both the API session and the MVC session, then returns home.</summary>
    [HttpPost, RequireSession]
    public async Task<IActionResult> Logout()
    {
        await Api.PostAsync<object>("/api/auth/logout");
        HttpContext.Session.SignOut();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }
}
