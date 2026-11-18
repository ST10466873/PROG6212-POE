using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Participant-facing event browsing: full event information, available categories
/// and the "Enter Event" action, all delegated to the Part 2 API.
/// </summary>
public class EventsController : WebControllerBase
{
    public EventsController(ApiClient api) : base(api) { }

    /// <summary>Event detail page including categories and the enrolment state of the visitor.</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ev = await Api.GetAsync<EventVm>($"/api/events/{id}");
        if (!ev.Success)
        {
            var redirect = HandleFailure(ev);
            if (redirect != null) return redirect;
            if (ev.StatusCode == 404) return NotFound();
            return View(new EventVm(id, 0, "Event", "", DateTime.Today, "", 0, "Run", null, "", null));
        }

        var categories = await Api.GetAsync<List<CategoryVm>>($"/api/events/{id}/categories");
        var results = await Api.GetAsync<List<ResultVm>>($"/api/events/{id}/results");

        var isParticipant = HttpContext.Session.IsInRole("Participant");
        var enrolled = false;
        if (isParticipant)
        {
            var mine = await Api.GetAsync<List<EnrolmentVm>>("/api/enrolments/me");
            enrolled = mine.Data?.Any(e => e.EventId == id) == true;
        }

        ViewBag.Categories = categories.Data ?? new List<CategoryVm>();
        ViewBag.Results = results.Data ?? new List<ResultVm>();
        ViewBag.Enrolled = enrolled;
        ViewBag.IsParticipant = isParticipant;
        return View(ev.Data!);
    }

    /// <summary>Enrols the signed-in Participant in the event using the chosen category.</summary>
    [HttpPost, RequireSession("Participant")]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        var result = await Api.PostAsync<EnrolmentVm>($"/api/events/{eventId}/enrolments", new { categoryId });
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        TempData["Success"] = "You are enrolled. Good luck on race day!";
        return RedirectToAction("MyEvents", "Participant");
    }

    /// <summary>Cancels the Participant's own enrolment through the API.</summary>
    [HttpPost, RequireSession("Participant")]
    public async Task<IActionResult> Cancel(int enrolmentId)
    {
        var result = await Api.DeleteAsync($"/api/enrolments/{enrolmentId}");
        var redirect = HandleFailure(result);
        if (redirect != null) return redirect;
        TempData["Success"] = "Enrolment cancelled.";
        return RedirectToAction("MyEvents", "Participant");
    }
}
