using EPiServer;
using EPiServer.Core;
using EPiServer.DataAccess;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using Microsoft.Extensions.Configuration;
using OptimizelySite.Models.Pages;

namespace OptimizelySite.Infrastructure;

/// <summary>
/// Creates the site definition and a small content tree the first time the app
/// starts against an empty database, so the host serves an actual Optimizely
/// site instead of a 404. Idempotent: existing content and an existing site
/// definition are reused.
/// </summary>
[InitializableModule]
[ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
public class SiteSeedModule : IInitializableModule
{
    private const string SiteName = "Optimizely Site";
    private const string RootFolderName = "Sites";

    private static readonly object Gate = new();
    private static bool _seeded;

    public void Initialize(InitializationEngine context)
    {
        if (_seeded)
        {
            return;
        }

        lock (Gate)
        {
            if (_seeded)
            {
                return;
            }

            var contentRepository = context.Locate.Advanced.GetInstance<IContentRepository>();
            var contentLoader = context.Locate.Advanced.GetInstance<IContentLoader>();
            var siteRepository = context.Locate.Advanced.GetInstance<ISiteDefinitionRepository>();
            var configuration = context.Locate.Advanced.GetInstance<IConfiguration>();

            // Optimizely expects a content-root folder under the system root; a start
            // page placed directly under the system root does not resolve.
            var folderLink = EnsureRootFolder(contentRepository);
            var site = siteRepository.List().FirstOrDefault(candidate => candidate.Name == SiteName);

            if (site is not null)
            {
                var writable = site.CreateWritableClone();
                var start = contentLoader.Get<PageData>(writable.StartPage);
                if (start is not null && start.ParentLink == ContentReference.RootPage)
                {
                    contentRepository.Move(writable.StartPage, folderLink, AccessLevel.NoAccess, AccessLevel.NoAccess);
                }

                ApplyHosts(configuration, writable);
                siteRepository.Save(writable);
                _seeded = true;
                return;
            }

            var startLink = EnsureStartPage(contentRepository, folderLink);
            EnsureStandardPage(contentRepository, startLink, "About", "About this site",
                "An Optimizely CMS 12 site instrumented for performance diagnostics.");
            EnsureStandardPage(contentRepository, startLink, "Contact", "Contact",
                "Reach the Reignite diagnostics team.");
            EnsureLabPage(contentRepository, startLink);

            var newSite = new SiteDefinition
            {
                Name = SiteName,
                StartPage = startLink
            };
            ApplyHosts(configuration, newSite);
            siteRepository.Save(newSite);
            _seeded = true;
        }
    }

    /// <summary>
    /// Applies the host bindings from configuration. Defaults target the public
    /// custom domain; a local run sets Site:PrimaryHost/Site:SiteUrl so the same
    /// site definition serves localhost without a license.
    /// </summary>
    private static void ApplyHosts(IConfiguration configuration, SiteDefinition site)
    {
        var siteUrl = new Uri(configuration["Site:SiteUrl"] ?? "https://optimizely.reigniteperformance.com/");
        var primaryHost = configuration["Site:PrimaryHost"] ?? "optimizely.reigniteperformance.com";
        var editHost = configuration["Site:EditHost"] ?? "reignite-optimizely-lab.azurewebsites.net";
        var primaryHttps = !primaryHost.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);

        var desired = new List<HostDefinition>
        {
            new() { Name = primaryHost, Type = HostDefinitionType.Primary, UseSecureConnection = primaryHttps }
        };
        if (!string.Equals(editHost, primaryHost, StringComparison.OrdinalIgnoreCase))
        {
            desired.Add(new() { Name = editHost, Type = HostDefinitionType.Edit });
        }

        var same = site.SiteUrl == siteUrl &&
                   site.Hosts.Count == desired.Count &&
                   desired.All(d => site.Hosts.Any(h =>
                       string.Equals(h.Name, d.Name, StringComparison.OrdinalIgnoreCase) &&
                       h.Type == d.Type &&
                       h.UseSecureConnection == d.UseSecureConnection));
        if (same)
        {
            return;
        }

        site.SiteUrl = siteUrl;
        site.Hosts.Clear();
        foreach (var host in desired)
        {
            site.Hosts.Add(host);
        }
    }

    private static ContentReference EnsureRootFolder(IContentRepository repository)
    {
        var existing = repository.GetChildren<ContentFolder>(ContentReference.RootPage)
            .FirstOrDefault(folder => folder.Name == RootFolderName);
        if (existing is not null)
        {
            return existing.ContentLink;
        }

        var folder = repository.GetDefault<ContentFolder>(ContentReference.RootPage);
        folder.Name = RootFolderName;
        return repository.Save(folder, SaveAction.Publish, AccessLevel.NoAccess);
    }

    private static ContentReference EnsureStartPage(IContentRepository repository, ContentReference parent)
    {
        var existing = repository.GetChildren<StartPage>(parent)
            .FirstOrDefault(page => page.Name == "Home");
        if (existing is not null)
        {
            return existing.ContentLink;
        }

        var start = repository.GetDefault<StartPage>(parent);
        start.Name = "Home";
        start.Heading = "Optimizely Performance Engineering";
        start.MainBody = new XhtmlString(
            "<p>This is a live Optimizely CMS 12 site running on .NET 8. " +
            "It is the front door for the Reignite diagnostics test bed.</p>" +
            "<p>Open the <a href=\"/diagnostics-lab\">Diagnostics Lab</a> to trigger faults on purpose.</p>");
        return repository.Save(start, SaveAction.Publish, AccessLevel.NoAccess);
    }

    private static void EnsureStandardPage(
        IContentRepository repository,
        ContentReference parent,
        string name,
        string heading,
        string body)
    {
        if (repository.GetChildren<StandardPage>(parent).Any(page => page.Name == name))
        {
            return;
        }

        var page = repository.GetDefault<StandardPage>(parent);
        page.Name = name;
        page.Heading = heading;
        page.Introduction = body;
        page.MainBody = new XhtmlString($"<p>{body}</p>");
        repository.Save(page, SaveAction.Publish, AccessLevel.NoAccess);
    }

    private static void EnsureLabPage(IContentRepository repository, ContentReference parent)
    {
        if (repository.GetChildren<LabPage>(parent).Any(page => page.Name == "Diagnostics Lab"))
        {
            return;
        }

        var labPage = repository.GetDefault<LabPage>(parent);
        labPage.Name = "Diagnostics Lab";
        labPage.Heading = "Diagnostics Lab";
        labPage.Introduction = new XhtmlString(
            "<p>Trigger deliberate faults on this live Optimizely site. Enter the operator key, " +
            "run a fault, then watch the result in the Reignite diagnostics bot.</p>");
        repository.Save(labPage, SaveAction.Publish, AccessLevel.NoAccess);
    }

    public void Uninitialize(InitializationEngine context)
    {
    }
}
