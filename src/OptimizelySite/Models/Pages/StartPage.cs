using System.ComponentModel.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAnnotations;

namespace OptimizelySite.Models.Pages;

[ContentType(
    DisplayName = "Start Page",
    GUID = "6f2a9e2c-1c4a-4c1e-9b7a-3a1f2e6d8c01",
    Description = "The site's start page.")]
public class StartPage : PageData
{
    [CultureSpecific]
    [Display(Name = "Heading", Order = 10)]
    public virtual string Heading { get; set; } = string.Empty;

    [CultureSpecific]
    [Display(Name = "Main body", Order = 20)]
    public virtual XhtmlString MainBody { get; set; } = new();
}
