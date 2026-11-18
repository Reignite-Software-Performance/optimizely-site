using System.ComponentModel.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAnnotations;

namespace OptimizelySite.Models.Pages;

[ContentType(
    DisplayName = "Diagnostics Lab",
    GUID = "9c5d2e6f-3a4b-4c7d-8e9f-0a1b2c3d4e03",
    Description = "Fault-injection controls rendered as a page in the site.")]
public class LabPage : PageData
{
    [CultureSpecific]
    [Display(Name = "Heading", Order = 10)]
    public virtual string Heading { get; set; } = string.Empty;

    [CultureSpecific]
    [Display(Name = "Introduction", Order = 20)]
    public virtual XhtmlString Introduction { get; set; } = new();
}
