using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Ical.Net;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;

namespace ZeMail.Infrastructure.Mail;

public class CalDavSyncService
{
    private readonly IZeMailDbContext           _db;
    private readonly ILogger<CalDavSyncService> _log;
    private readonly HttpClient                 _http;

    private static readonly XNamespace Dav  = "DAV:";
    private static readonly XNamespace CDav = "urn:ietf:params:xml:ns:caldav";

    public CalDavSyncService(IZeMailDbContext db, ILogger<CalDavSyncService> log)
    {
        _db   = db;
        _log  = log;
        _http = new HttpClient();
    }

    // ── Initialer Sync ───────────────────────────────────────────────────────

    public async Task InitialSyncAsync(Account account, string calDavUrl, CancellationToken ct = default)
    {
        _log.LogInformation("CalDAV initial sync for account {Id} @ {Url}", account.Id, calDavUrl);

        var request  = BuildReportRequest();
        var response = await SendCalDavAsync(account, calDavUrl, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning("CalDAV REPORT failed: {Status}", response.StatusCode);
            return;
        }

        var xml   = await response.Content.ReadAsStringAsync(ct);
        var items = ParseMultiStatus(xml);

        foreach (var (href, etag, iCal) in items)
            await UpsertEventAsync(account.Id, href, etag, iCal, ct);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("CalDAV initial sync complete — {Count} events", items.Count);
    }

    // ── Delta-Sync via CTag ──────────────────────────────────────────────────

    public async Task DeltaSyncAsync(Account account, string calDavUrl, CancellationToken ct = default)
    {
        var ctag = await GetCTagAsync(account, calDavUrl, ct);
        _log.LogDebug("CalDAV CTag: {CTag}", ctag);

        // Vereinfachung: bei jedem Aufruf Vollsync —
        // echter Delta-Sync würde CTag persistieren und nur bei Änderung neu laden
        await InitialSyncAsync(account, calDavUrl, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task UpsertEventAsync(
        Guid accountId, string href, string etag, string iCal, CancellationToken ct)
    {
        var existing = _db.CalendarEvents.FirstOrDefault(e => e.CalDavHref == href);

        if (existing is not null && existing.CalDavEtag == etag) return;

        CalendarEvent ev;
        try
        {
            ev = ParseICalEvent(iCal, accountId, href, etag);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "iCal parse failed for {Href}", href);
            return;
        }

        if (existing is null)
        {
            _db.Add(ev);
        }
        else
        {
            existing.Title          = ev.Title;
            existing.Description    = ev.Description;
            existing.Location       = ev.Location;
            existing.StartUtc       = ev.StartUtc;
            existing.EndUtc         = ev.EndUtc;
            existing.IsAllDay       = ev.IsAllDay;
            existing.RecurrenceRule = ev.RecurrenceRule;
            existing.ICalRaw        = ev.ICalRaw;
            existing.CalDavEtag     = ev.CalDavEtag;
            existing.UpdatedAtUtc   = DateTime.UtcNow;
        }
    }

    private static CalendarEvent ParseICalEvent(
        string iCal, Guid accountId, string href, string etag)
    {
        var calendar = Calendar.Load(iCal);
        var vevent   = calendar.Events.OfType<IcalEvent>().First();

        return new CalendarEvent
        {
            Id             = Guid.NewGuid(),
            AccountId      = accountId,
            Title          = vevent.Summary     ?? "(kein Titel)",
            Description    = vevent.Description,
            Location       = vevent.Location,
            StartUtc       = vevent.DtStart.AsUtc,
            EndUtc         = vevent.DtEnd?.AsUtc ?? vevent.DtStart.AsUtc.AddHours(1),
            IsAllDay       = vevent.IsAllDay,
            RecurrenceRule = vevent.RecurrenceRules?.FirstOrDefault()?.ToString(),
            ICalRaw        = iCal,
            CalDavHref     = href,
            CalDavEtag     = etag,
            CreatedAtUtc   = DateTime.UtcNow,
            UpdatedAtUtc   = DateTime.UtcNow
        };
    }

    private static HttpContent BuildReportRequest()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <C:calendar-query xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
              <D:prop>
                <D:getetag/>
                <C:calendar-data/>
              </D:prop>
              <C:filter>
                <C:comp-filter name="VCALENDAR">
                  <C:comp-filter name="VEVENT"/>
                </C:comp-filter>
              </C:filter>
            </C:calendar-query>
            """;
        return new StringContent(xml, Encoding.UTF8, "application/xml");
    }

    private async Task<HttpResponseMessage> SendCalDavAsync(
        Account account, string url, HttpContent body, CancellationToken ct)
    {
        var req = new HttpRequestMessage(new HttpMethod("REPORT"), url)
        {
            Content = body
        };
        req.Headers.Add("Depth", "1");

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{account.Username}:{account.Password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return await _http.SendAsync(req, ct);
    }

    private async Task<string> GetCTagAsync(Account account, string url, CancellationToken ct)
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <D:propfind xmlns:D="DAV:" xmlns:CS="http://calendarserver.org/ns/">
              <D:prop><CS:getctag/></D:prop>
            </D:propfind>
            """;

        var req = new HttpRequestMessage(new HttpMethod("PROPFIND"), url)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        };
        req.Headers.Add("Depth", "0");

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{account.Username}:{account.Password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        var doc  = XDocument.Parse(body);
        var ctag = doc.Descendants()
                      .FirstOrDefault(x => x.Name.LocalName == "getctag")?.Value;
        return ctag ?? string.Empty;
    }

    private static List<(string Href, string Etag, string ICal)> ParseMultiStatus(string xml)
    {
        var result = new List<(string, string, string)>();
        var doc    = XDocument.Parse(xml);

        foreach (var response in doc.Descendants(Dav + "response"))
        {
            var href = response.Element(Dav + "href")?.Value ?? "";
            var etag = response.Descendants(Dav + "getetag")
                               .FirstOrDefault()?.Value ?? "";
            var iCal = response.Descendants(CDav + "calendar-data")
                               .FirstOrDefault()?.Value ?? "";

            if (!string.IsNullOrEmpty(iCal))
                result.Add((href, etag, iCal));
        }

        return result;
    }
}