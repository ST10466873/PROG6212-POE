using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Participant-only pages: my events, personal race history and profile management.
/// All data and uploads travel through the API; this controller has no database access.
/// </summary>
[RequireSession("Participant")]
public class ParticipantController : WebControllerBase
{
    public ParticipantController(ApiClient api) : base(api) { }

    /// <summary>All events the Participant has entered, with their confirmation status.</summary>
    [HttpGet]
    public async Task<IActionResult> MyEvents()
    {
        var result = await Api.GetAsync<List<EnrolmentVm>>("/api/enrolments/me");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        return View(result.Data ?? new List<EnrolmentVm>());
    }

    /// <summary>Personal race history with finish times, positions and field sizes.</summary>
    [HttpGet]
    public async Task<IActionResult> MyResults()
    {
        var result = await Api.GetAsync<List<MyResultVm>>("/api/enrolments/me/results");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        return View(result.Data ?? new List<MyResultVm>());
    }

    /// <summary>Displays the editable profile form.</summary>
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var result = await Api.GetAsync<ProfileVm>("/api/users/me");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        var p = result.Data!;
        return View(new ProfileFormViewModel
        {
            FirstName = p.FirstName,
            LastName = p.LastName,
            Email = p.Email,
            PhoneNumber = p.PhoneNumber,
            DateOfBirth = p.DateOfBirth
        });
    }

    /// <summary>Updates profile details and, optionally, uploads a new profile picture.</summary>
    [HttpPost]
    public async Task<IActionResult> Profile(ProfileFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var update = await Api.PutAsync<ProfileVm>("/api/users/me", new
        {
            firstName = model.FirstName,
            lastName = model.LastName,
            email = model.Email,
            phoneNumber = model.PhoneNumber,
            dateOfBirth = model.DateOfBirth.ToString("yyyy-MM-dd")
        });
        var redirect = HandleFailure(update);
        if (redirect != null) return redirect;

        if (model.Picture is { Length: > 0 })
        {
            var upload = await Api.PostFileAsync<object>("/api/users/me/profile-picture", model.Picture);
            var uploadRedirect = HandleFailure(upload);
            if (uploadRedirect != null) return uploadRedirect;
        }

        HttpContext.Session.UpdateIdentity($"{model.FirstName} {model.LastName}", model.Email);
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }
}
