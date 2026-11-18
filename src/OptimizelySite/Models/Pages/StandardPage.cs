using System.ComponentModel.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAnnotations;

namespace OptimizelySite.Models.Pages;

[ContentType(
    DisplayName = "Standard Page",
    GUID = "7a3b1d4e-2f5c-4b8a-a1c3-5d7e9f0b2c02",
    Description = "A standard content page.")]
public class StandardPage : PageData
{
    [CultureSpecific]
    [Display(Name = "Heading", Order = 10)]
    public virtual string Heading { get; set; } = string.Empty;

    [CultureSpecific]
    [Display(Name = "Introduction", Order = 20)]
    public virtual string Introduction { get; set; } = string.Empty;

    [CultureSpecific]
    [Display(Name = "Main body", Order = 30)]
    public virtual XhtmlString MainBody { get; set; } = new();
}
