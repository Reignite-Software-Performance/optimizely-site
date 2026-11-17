using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace OptimizelySite.Lab;

/// <summary>
/// Deliberate fault endpoints for the live diagnostics test bed. Every one is
/// gated by <see cref="LabOptions.Enabled"/> + a shared key, is bounded by the
/// configured maxima, and reports what it did so an analyst can correlate the
/// symptom with the injected cause.
/// </summary>
public static class LabEndpoints
{
    public static IEndpointRouteBuilder MapLabEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/lab", (IOptions<LabOptions> options) => Html(Home(options.Value)));
        endpoints.MapGet("/lab/health", () => Results.Json(new { status = "ok", utc = DateTime.UtcNow }));

        endpoints.MapGet("/lab/memory", async (HttpContext ctx, IOptions<LabOptions> options, int? mb, int? hold) =>
        {
            var o = options.Value;
            if (!Allowed(ctx, o)) return Results.NotFound();
            var megabytes = Math.Clamp(mb ?? 256, 16, o.MaxMemoryMb);
            var seconds = Math.Clamp(hold ?? 15, 1, o.MaxHoldSeconds);
            var held = new List<byte[]>(megabytes);
            for (var i = 0; i < megabytes; i++)
            {
                var block = new byte[1024 * 1024];
                Array.Fill(block, (byte)0x5A);
                held.Add(block);
            }
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            held.Clear();
            GC.Collect();
            return Html(Page("Memory pressure", $"Held {megabytes} MB for {seconds}s, then released. Peak working set: {Environment.WorkingSet / (1024 * 1024)} MB."));
        });

        endpoints.MapGet("/lab/cpu", async (HttpContext ctx, IOptions<LabOptions> options, int? seconds) =>
        {
            var o = options.Value;
            if (!Allowed(ctx, o)) return Results.NotFound();
            var duration = Math.Clamp(seconds ?? 10, 1, o.MaxCpuSeconds);
            var end = DateTime.UtcNow.AddSeconds(duration);
            double sink = 0;
            while (DateTime.UtcNow < end)
            {
                for (var i = 0; i < 100_000; i++) sink += Math.Sqrt(sink + i + 1);
            }
            await Task.Yield();
            return Html(Page("CPU burn", $"Burned one core for {duration}s (sink={sink:F0})."));
        });

        endpoints.MapGet("/lab/threads", async (HttpContext ctx, IOptions<LabOptions> options, int? count, int? hold) =>
        {
            var o = options.Value;
            if (!Allowed(ctx, o)) return Results.NotFound();
            var workers = Math.Clamp(count ?? 32, 1, o.MaxThreads);
            var seconds = Math.Clamp(hold ?? 10, 1, o.MaxHoldSeconds);
            var tasks = Enumerable.Range(0, workers)
                .Select(_ => Task.Run(() => Thread.Sleep(TimeSpan.FromSeconds(seconds))))
                .ToArray();
            await Task.WhenAll(tasks);
            return Html(Page("Thread-pool starvation", $"Blocked {workers} pool workers synchronously for {seconds}s."));
        });

        endpoints.MapGet("/lab/alloc", async (HttpContext ctx, IOptions<LabOptions> options, int? mb, int? seconds) =>
        {
            var o = options.Value;
            if (!Allowed(ctx, o)) return Results.NotFound();
            var perTick = Math.Clamp(mb ?? 32, 1, o.MaxMemoryMb);
            var duration = Math.Clamp(seconds ?? 10, 1, o.MaxHoldSeconds);
            var end = DateTime.UtcNow.AddSeconds(duration);
            var ticks = 0;
            while (DateTime.UtcNow < end)
            {
                var churn = new List<byte[]>(perTick);
                for (var i = 0; i < perTick; i++) churn.Add(new byte[1024 * 1024]);
                churn.Clear();
                ticks++;
                await Task.Delay(100);
            }
            return Html(Page("Allocation / GC pressure", $"Churned {perTick} MB per 100 ms for {duration}s ({ticks} ticks)."));
        });

        endpoints.MapGet("/lab/exception", (HttpContext ctx, IOptions<LabOptions> options) =>
        {
            if (!Allowed(ctx, options.Value)) return Results.NotFound();
            throw new InvalidOperationException(
                "Intentional lab fault: first-chance and unhandled exception raised on purpose for diagnostics capture.");
        });

        endpoints.MapGet("/lab/slow-sql", async (HttpContext ctx, IOptions<LabOptions> options, IConfiguration config, int? ms) =>
        {
            var o = options.Value;
            if (!Allowed(ctx, o)) return Results.NotFound();
            var delay = Math.Clamp(ms ?? 5000, 100, o.MaxSqlDelayMs);
            var connectionString = config.GetConnectionString("EPiServerDB");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Results.Json(new { error = "No EPiServerDB connection string configured." }, statusCode: 503);
            }
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ctx.RequestAborted);
            await using var command = connection.CreateCommand();
            command.CommandText = $"WAITFOR DELAY '00:00:{delay / 1000.0:00.000}'; SELECT 1;";
            command.CommandTimeout = (delay / 1000) + 30;
            await command.ExecuteScalarAsync(ctx.RequestAborted);
            return Html(Page("Slow SQL", $"Ran WAITFOR DELAY for {delay} ms against the EPiServerDB connection."));
        });

        return endpoints;
    }

    private static bool Allowed(HttpContext context, LabOptions options)
    {
        if (!options.IsUsable)
        {
            return false;
        }

        var supplied = context.Request.Query["key"].ToString();
        if (string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(supplied);
        var b = Encoding.UTF8.GetBytes(options.Key!);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static IResult Html(string html) => Results.Content(html, "text/html; charset=utf-8");

    private static string Home(LabOptions o)
    {
        var status = o.IsUsable ? "enabled" : "disabled (set Lab:Enabled=true and Lab:Key)";
        var body = $@"
            <h1>Optimizely diagnostics lab</h1>
            <p>Status: <strong>{status}</strong></p>
            <p>Each fault endpoint requires <code>?key=…</code>. They are bounded by the server's Lab settings.</p>
            <ul>
              <li><a href=""/lab/health"">/lab/health</a> — baseline (no key)</li>
              <li>/lab/memory?mb=256&amp;hold=15 — retained memory</li>
              <li>/lab/cpu?seconds=10 — CPU burn</li>
              <li>/lab/threads?count=32&amp;hold=10 — thread-pool starvation</li>
              <li>/lab/alloc?mb=32&amp;seconds=10 — GC pressure</li>
              <li>/lab/exception — unhandled exception</li>
              <li>/lab/slow-sql?ms=5000 — slow SQL against EPiServerDB</li>
            </ul>";
        return Page("Lab", body);
    }

    private static string Page(string title, string body) => $@"<!doctype html>
        <html lang=""en"">
        <head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
        <title>{title} · Optimizely lab</title>
        <style>body{{font:16px/1.5 system-ui,sans-serif;max-width:52rem;margin:3rem auto;padding:0 1rem;color:#111}}
        code{{background:#f2f4f7;padding:.1rem .35rem;border-radius:4px}} a{{color:#1d4ed8}}</style>
        </head><body><h1>{title}</h1>{body}<p><a href=""/lab"">← lab index</a></p></body></html>";
}
