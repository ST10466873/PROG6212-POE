using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Organiser-only interface: dashboard, event CRUD (including banner upload), categories,
/// enrolment viewing and race-day result capture. Every action delegates to the Part 2 API.
/// </summary>
[RequireSession("Organiser")]
public class OrganiserController : WebControllerBase
{
    public OrganiserController(ApiClient api) : base(api) { }

    /// <summary>Summary of the Organiser's events, enrolments and upcoming race dates.</summary>
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetAsync<DashboardVm>("/api/organisers/dashboard");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        return View(result.Data ?? new DashboardVm(0, 0, 0, 0, new List<EventSummaryVm>()));
    }

    /// <summary>Events created by the signed-in Organiser (from the owner-only dashboard API).</summary>
    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var result = await Api.GetAsync<DashboardVm>("/api/organisers/dashboard");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        return View(result.Data?.Events ?? new List<EventSummaryVm>());
    }

    /// <summary>Displays the create-event form.</summary>
    [HttpGet]
    public IActionResult Create() => View("EventForm", new EventFormViewModel());

    /// <summary>Creates the event, then uploads its banner through the API when supplied.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        var created = await Api.PostAsync<EventVm>("/api/events", EventPayload(model));
        var redirect = HandleFailure(created);
        if (redirect != null) return redirect;

        if (model.Banner is { Length: > 0 } && created.Data != null)
        {
            var upload = await Api.PostFileAsync<object>($"/api/events/{created.Data.EventId}/banner", model.Banner);
            if (!upload.Success) TempData["Error"] = "Event created, but the banner upload failed: " + upload.Error;
        }

        TempData["Success"] = "Event created.";
        return RedirectToAction(nameof(Events));
    }

    /// <summary>Displays the edit form for an owned event.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await Api.GetAsync<EventVm>($"/api/events/{id}");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        if (result.Data == null || !IsOwned(result.Data)) return RedirectToAction("AccessDenied", "Home");

        var e = result.Data;
        return View("EventForm", new EventFormViewModel
        {
            EventId = e.EventId,
            Name = e.EventName,
            Description = e.Description,
            Date = e.EventDate,
            Location = e.Location,
            DistanceKm = e.DistanceKm,
            EventType = e.EventType,
            BannerImageUrl = e.BannerImageUrl
        });
    }

    /// <summary>Updates an owned event and optionally replaces its banner.</summary>
    [HttpPost]
    public async Task<IActionResult> Edit(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await Api.PutAsync<EventVm>($"/api/events/{model.EventId}", EventPayload(model));
        var redirect = HandleFailure(updated);
        if (redirect != null) return redirect;

        if (model.Banner is { Length: > 0 })
        {
            var upload = await Api.PostFileAsync<object>($"/api/events/{model.EventId}/banner", model.Banner);
            var uploadRedirect = HandleFailure(upload);
            if (uploadRedirect != null) return uploadRedirect;
        }

        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Events));
    }

    /// <summary>Deletes an owned event; the API cascades categories, enrolments and results.</summary>
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Api.DeleteAsync($"/api/events/{id}");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        TempData["Success"] = "Event deleted.";
        return RedirectToAction(nameof(Events));
    }

    /// <summary>Manages age/distance categories for one event.</summary>
    [HttpGet]
    public async Task<IActionResult> Categories(int eventId)
    {
        var owned = await LoadOwnedEvent(eventId);
        if (owned.Redirect != null) return owned.Redirect;

        var categories = await Api.GetAsync<List<CategoryVm>>($"/api/events/{eventId}/categories");
        var redirect = HandleFailure(categories);
        if (redirect != null) return redirect;

        ViewBag.Event = owned.Event!;
        return View(categories.Data ?? new List<CategoryVm>());
    }

    /// <summary>Adds or updates a category; CategoryId == 0 means create.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Categories), new { eventId = model.EventId });

        var payload = new { categoryName = model.CategoryName, minAge = model.MinAge, maxAge = model.MaxAge };
        var result = model.CategoryId > 0
            ? await Api.PutAsync<CategoryVm>($"/api/categories/{model.CategoryId}", payload)
            : await Api.PostAsync<CategoryVm>($"/api/events/{model.EventId}/categories", payload);

        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        TempData["Success"] = model.CategoryId > 0 ? "Category updated." : "Category added.";
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    /// <summary>Removes a category that has no enrolments (API enforces the guard).</summary>
    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int categoryId, int eventId)
    {
        var result = await Api.DeleteAsync($"/api/categories/{categoryId}");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        TempData["Success"] = "Category removed.";
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    /// <summary>Lists every Participant enrolled for one of the Organiser's events.</summary>
    [HttpGet]
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var owned = await LoadOwnedEvent(eventId);
        if (owned.Redirect != null) return owned.Redirect;

        var enrolments = await Api.GetAsync<List<EnrolmentVm>>($"/api/events/{eventId}/enrolments");
        var redirect = HandleFailure(enrolments);
        if (redirect != null) return redirect;
        var results = await Api.GetAsync<List<ResultVm>>($"/api/events/{eventId}/results");

        ViewBag.Event = owned.Event!;
        ViewBag.Results = results.Data ?? new List<ResultVm>();
        return View(enrolments.Data ?? new List<EnrolmentVm>());
    }

    /// <summary>Result-entry screen with one capture row per enrolled Participant.</summary>
    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var owned = await LoadOwnedEvent(eventId);
        if (owned.Redirect != null) return owned.Redirect;

        var enrolments = await Api.GetAsync<List<EnrolmentVm>>($"/api/events/{eventId}/enrolments");
        var redirect = HandleFailure(enrolments);
        if (redirect != null) return redirect;
        var results = await Api.GetAsync<List<ResultVm>>($"/api/events/{eventId}/results");

        var rows = (enrolments.Data ?? new List<EnrolmentVm>()).Select(e =>
        {
            var existing = results.Data?.FirstOrDefault(r => r.EnrolmentId == e.EnrolmentId);
            return new ResultFormViewModel
            {
                EnrolmentId = e.EnrolmentId,
                EventId = eventId,
                ParticipantName = e.ParticipantName,
                CategoryName = e.CategoryName,
                ExistingResultId = existing?.ResultId,
                FinishTime = existing?.FinishTime ?? "00:00:00.000",
                Position = existing?.FinishingPosition ?? 1
            };
        }).ToList();

        ViewBag.Event = owned.Event!;
        ViewBag.Published = results.Data ?? new List<ResultVm>();
        return View(rows);
    }

    /// <summary>Captures a new result or corrects an existing one through the API.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveResult(ResultFormViewModel model)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Results), new { eventId = model.EventId });

        var payload = new { finishTime = model.FinishTime, position = model.Position };
        var result = model.ExistingResultId.HasValue
            ? await Api.PutAsync<ResultVm>($"/api/results/{model.ExistingResultId.Value}", payload)
            : await Api.PostAsync<ResultVm>($"/api/enrolments/{model.EnrolmentId}/result", payload);

        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        TempData["Success"] = model.ExistingResultId.HasValue ? "Result corrected." : "Result published.";
        return RedirectToAction(nameof(Results), new { eventId = model.EventId });
    }

    /// <summary>Builds the JSON payload shared by create and edit.</summary>
    private static object EventPayload(EventFormViewModel model) => new
    {
        name = model.Name,
        description = model.Description,
        date = model.Date.ToString("yyyy-MM-dd"),
        location = model.Location,
        distanceKm = model.DistanceKm,
        eventType = model.EventType
    };

    /// <summary>Guards detail pages so an Organiser cannot manage another organiser's event.</summary>
    private bool IsOwned(EventVm ev)
        => ev.OrganiserId == HttpContext.Session.GetCurrentUser()?.UserId;

    /// <summary>Loads an event and verifies ownership before showing management screens.</summary>
    private async Task<(EventVm? Event, IActionResult? Redirect)> LoadOwnedEvent(int eventId)
    {
        var result = await Api.GetAsync<EventVm>($"/api/events/{eventId}");
        var redirect = HandleFailure(result);
        if (redirect != null) return (null, redirect);
        if (result.Data == null || !IsOwned(result.Data))
            return (null, RedirectToAction("AccessDenied", "Home"));
        return (result.Data, null);
    }
}
