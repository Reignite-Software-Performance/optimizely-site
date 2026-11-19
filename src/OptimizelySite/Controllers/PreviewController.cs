using EPiServer.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptimizelySite.Models.Pages;

namespace OptimizelySite.Controllers;

/// <summary>
/// Renders the site's page views directly with sample models, bypassing CMS
/// content routing. Useful for local design review while the Optimizely license
/// (which gates content routes on a public host) is being obtained.
/// </summary>
[AllowAnonymous]
public class PreviewController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View("~/Views/StartPage/Index.cshtml", new StartPage
    {
        Heading = "Optimizely Performance Engineering",
        MainBody = new XhtmlString(
            "<p>This is a live Optimizely CMS 12 site running on .NET 8. " +
            "It is the front door for the Reignite diagnostics test bed.</p>" +
            "<p>Open the <a href=\"/diagnostics-lab\">Diagnostics Lab</a> to trigger faults on purpose.</p>")
    });

    [HttpGet("/about")]
    public IActionResult About() => View("~/Views/StandardPage/Index.cshtml", new StandardPage
    {
        Heading = "About this site",
        Introduction = "An Optimizely CMS 12 site instrumented for performance diagnostics.",
        MainBody = new XhtmlString(
            "<p>This site is the live test bed for the Reignite profiler, diagnostics control plane, " +
            "and Teams diagnostics bot.</p>")
    });

    [HttpGet("/contact")]
    public IActionResult Contact() => View("~/Views/StandardPage/Index.cshtml", new StandardPage
    {
        Heading = "Contact",
        Introduction = "Reach the Reignite diagnostics team.",
        MainBody = new XhtmlString("<p>diagnostics@reigniteperformance.com</p>")
    });

    [HttpGet("/diagnostics-lab")]
    public IActionResult Lab() => View("~/Views/LabPage/Index.cshtml", new LabPage
    {
        Heading = "Diagnostics Lab",
        Introduction = new XhtmlString(
            "<p>Trigger deliberate faults on this live Optimizely site. Enter the operator key, " +
            "run a fault, then watch the result in the Reignite diagnostics bot.</p>")
    });
}
