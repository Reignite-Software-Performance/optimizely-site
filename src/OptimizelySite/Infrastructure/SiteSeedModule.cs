using EPiServer;
using EPiServer.Applications;
using EPiServer.Core;
using EPiServer.DataAccess;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using Microsoft.Extensions.Configuration;
using OptimizelySite.Models.Pages;

namespace OptimizelySite.Infrastructure;

/// <summary>
/// Creates the site application and a small content tree the first time the app
/// starts against an empty database, so the host serves an actual Optimizely
/// site instead of a 404. Idempotent: existing content and an existing website
/// application are reused.
/// </summary>
/// <remarks>
/// CMS 13 replaced the old <c>ISiteDefinitionRepository</c> surface with the
/// application model. A routable site is an <see cref="Website"/>, saved through
/// <see cref="IApplicationRepository"/>; the seed root is the site's content
/// folder and the entry point is the start page.
/// </remarks>
[InitializableModule]
[ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
public class SiteSeedModule : IInitializableModule
{
    private const string ApplicationName = "optimizely-site";
    private const string SiteDisplayName = "Optimizely Site";
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
            var applicationRepository = context.Locate.Advanced.GetInstance<IApplicationRepository>();
            var configuration = context.Locate.Advanced.GetInstance<IConfiguration>();

            // Optimizely expects a content-root folder under the system root; a start
            // page placed directly under the system root does not resolve.
            var folderLink = EnsureRootFolder(contentRepository);
            var startLink = EnsureStartPage(contentRepository, folderLink);
            EnsureStandardPage(contentRepository, startLink, "About", "About this site",
                "An Optimizely CMS 13 site instrumented for performance diagnostics.");
            EnsureStandardPage(contentRepository, startLink, "Contact", "Contact",
                "Reach the Reignite diagnostics team.");
            EnsureLabPage(contentRepository, startLink);

            var hosts = BuildHosts(configuration);
            var existing = applicationRepository.List()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, ApplicationName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.DisplayName, SiteDisplayName, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                var website = new Website(ApplicationName, folderLink)
                {
                    DisplayName = SiteDisplayName,
                    EntryPoint = startLink
                };
                foreach (var host in hosts)
                {
                    website.Hosts.Add(host);
                }

                applicationRepository.SaveAsync(website, CancellationToken.None)
                    .GetAwaiter().GetResult();
                applicationRepository.EnableAssetsAsync(website, true, CancellationToken.None)
                    .GetAwaiter().GetResult();
                applicationRepository.MakeDefaultAsync(website, true, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            else if (existing is Website website)
            {
                var changed = website.EntryPoint != startLink || !HostsMatch(website.Hosts, hosts);
                if (changed)
                {
                    website.EntryPoint = startLink;
                    website.Hosts.Clear();
                    foreach (var host in hosts)
                    {
                        website.Hosts.Add(host);
                    }

                    applicationRepository.SaveAsync(website, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
            }

            _seeded = true;
        }
    }

    /// <summary>
    /// Builds the host bindings from configuration. Defaults target the public
    /// custom domain; a local run sets Site:PrimaryHost so the same application
    /// serves localhost without a license.
    /// </summary>
    private static List<ApplicationHost> BuildHosts(IConfiguration configuration)
    {
        var primaryHost = configuration["Site:PrimaryHost"] ?? "optimizely.reigniteperformance.com";
        var editHost = configuration["Site:EditHost"] ?? "reignite-optimizely-lab.azurewebsites.net";
        var primaryHttps = !primaryHost.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);

        var hosts = new List<ApplicationHost>
        {
            new(primaryHost)
            {
                Type = ApplicationHostType.Primary,
                PreferredUrlScheme = primaryHttps ? UrlScheme.Https : UrlScheme.Http
            }
        };

        if (!string.Equals(editHost, primaryHost, StringComparison.OrdinalIgnoreCase))
        {
            hosts.Add(new ApplicationHost(editHost) { Type = ApplicationHostType.Edit });
        }

        return hosts;
    }

    private static bool HostsMatch(IList<ApplicationHost> existing, IList<ApplicationHost> desired) =>
        existing.Count == desired.Count &&
        desired.All(d => existing.Any(h =>
            string.Equals(h.Authority, d.Authority, StringComparison.OrdinalIgnoreCase) &&
            h.Type == d.Type));

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
            "<p>This is a live Optimizely CMS 13 site running on .NET 10. " +
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
