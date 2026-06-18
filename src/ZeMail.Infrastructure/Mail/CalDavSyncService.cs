using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Ical.Net;
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent    = Ical.Net.CalendarComponents.CalendarEvent;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeCalendar = ZeMail.Core.Entities.Calendar;

namespace ZeMail.Infrastructure.Mail;

public class CalDavSyncService : ICalendarSyncService
{
    private readonly IZeMailDbContext           _db;
    private readonly ILogger<CalDavSyncService> _log;

    private static readonly XNamespace Dav  = "DAV:";
    private static readonly XNamespace CDav = "urn:ietf:params:xml:ns:caldav";

    public CalDavSyncService(IZeMailDbContext db, ILogger<CalDavSyncService> log)
    {
        _db  = db;
        _log = log;
    }

    // ── ICalendarSyncService ────────────────────────────────────────────────

    public async Task SyncCalendarAsync(ZeCalendar calendar, CancellationToken ct = default)
    {
        if (calendar.Type != CalendarType.CalDav
            || string.IsNullOrWhiteSpace(calendar.ServerUrl)
            || string.IsNullOrWhiteSpace(calendar.Username)
            || string.IsNullOrWhiteSpace(calendar.PasswordEncrypted))
        {
            _log.LogWarning("Kalender {Id} ist kein CalDAV-Kalender oder hat keine Zugangsdaten.", calendar.Id);
            return;
        }

        try
        {
            var password   = DecryptPassword(calendar.PasswordEncrypted);
            using var http = BuildHttpClient(calendar.ServerUrl, calendar.Username, password);

            var remoteItems = await FetchRemoteItemsAsync(http, calendar.ServerUrl, ct);
            await MergeEventsAsync(calendar, remoteItems, ct);

            calendar.LastSyncedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Kalender {Name} synchronisiert – {Count} Einträge.", calendar.Name, remoteItems.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Fehler beim Synchronisieren von Kalender {Id}.", calendar.Id);
        }
    }

    public async Task<string?> TestConnectionAsync(string serverUrl, string username, string password, CancellationToken ct = default)
    {
        try
        {
            using var http  = BuildHttpClient(serverUrl, username, password);
            var request     = new HttpRequestMessage(new HttpMethod("PROPFIND"), serverUrl);
            request.Headers.Add("Depth", "0");
            request.Content = new StringContent(PropfindBody, Encoding.UTF8, "application/xml");

            var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.MultiStatus or HttpStatusCode.OK)
                return null;

            return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ── Normalisierung ───────────────────────────────────────────────────────

    /// <summary>
    /// Normalisiert einen CalDAV-href auf den Dateinamen.
    /// /dav.php/calendars/sascha/calprivate/test.ics → test.ics
    /// </summary>
    private static string NormalizeHref(string href)
    {
        var trimmed = href.TrimEnd('/');
        var idx     = trimmed.LastIndexOf('/');
        return idx >= 0 ? trimmed[(idx + 1)..] : trimmed;
    }

    // ── Fetch ───────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, (string Etag, string ICal, string FullHref)>> FetchRemoteItemsAsync(
        HttpClient http, string url, CancellationToken ct)
    {
        var reportReq = new HttpRequestMessage(new HttpMethod("REPORT"), url);
        reportReq.Headers.Add("Depth", "1");
        reportReq.Content = new StringContent(CalendarQueryBody, Encoding.UTF8, "application/xml");

        var reportResp = await http.SendAsync(reportReq, ct);
        reportResp.EnsureSuccessStatusCode();

        var reportXml = await reportResp.Content.ReadAsStringAsync(ct);
        var hrefs     = ParseHrefsFromReport(reportXml);

        if (hrefs.Count == 0) return [];

        var multigetBody = BuildMultigetBody(hrefs);
        var multigetReq  = new HttpRequestMessage(new HttpMethod("REPORT"), url);
        multigetReq.Headers.Add("Depth", "1");
        multigetReq.Content = new StringContent(multigetBody, Encoding.UTF8, "application/xml");

        var multigetResp = await http.SendAsync(multigetReq, ct);
        multigetResp.EnsureSuccessStatusCode();

        var multiXml = await multigetResp.Content.ReadAsStringAsync(ct);
        return ParseMultigetResponse(multiXml);
    }

    // ── Merge ───────────────────────────────────────────────────────────────

    private async Task MergeEventsAsync(
        ZeCalendar calendar,
        Dictionary<string, (string Etag, string ICal, string FullHref)> remoteItems,
        CancellationToken ct)
    {
        var localEvents = await Task.Run(() =>
            _db.CalendarEvents
               .Where(e => e.CalendarId == calendar.Id)
               .ToList(), ct);

        // Lokale Events per normalisiertem Dateinamen indizieren
        var localByFilename = localEvents
            .Where(e => e.CalDavHref != null)
            .ToDictionary(e => NormalizeHref(e.CalDavHref!), e => e);

        var remoteFilenames = remoteItems.Keys.ToHashSet();

        // Gelöschte entfernen
        foreach (var local in localByFilename.Values
            .Where(e => !remoteFilenames.Contains(NormalizeHref(e.CalDavHref!))))
            _db.Remove(local);

        // Neue/geänderte upserten
        foreach (var (filename, (etag, ical, fullHref)) in remoteItems)
        {
            if (localByFilename.TryGetValue(filename, out var existing))
            {
                // ETag identisch → keine Änderung
                if (existing.CalDavEtag == etag) continue;
                ApplyICalToEvent(existing, ical, etag);
                // FullHref aktualisieren falls nötig
                existing.CalDavHref = fullHref;
            }
            else
            {
                var newEvent = new CalendarEvent
                {
                    AccountId  = calendar.AccountId,
                    CalendarId = calendar.Id,
                    CalDavHref = fullHref,
                };
                ApplyICalToEvent(newEvent, ical, etag);
                _db.Add(newEvent);
            }
        }
    }

    private static void ApplyICalToEvent(CalendarEvent ev, string ical, string etag)
    {
        try
        {
            var cal    = IcalCalendar.Load(ical);
            var vEvent = cal.Events.OfType<IcalEvent>().FirstOrDefault();
            if (vEvent == null) return;

            ev.Title        = vEvent.Summary     ?? "(kein Titel)";
            ev.Description  = vEvent.Description;
            ev.Location     = vEvent.Location;
            ev.IsAllDay     = vEvent.IsAllDay;
            ev.ICalRaw      = ical;
            ev.CalDavEtag   = etag;
            ev.UpdatedAtUtc = DateTime.UtcNow;

            ev.StartUtc = vEvent.DtStart.IsUtc
                ? vEvent.DtStart.Value
                : TimeZoneInfo.ConvertTimeToUtc(vEvent.DtStart.Value);

            var dtEnd = vEvent.DtEnd ?? vEvent.DtStart;
            ev.EndUtc = dtEnd.IsUtc
                ? dtEnd.Value
                : TimeZoneInfo.ConvertTimeToUtc(dtEnd.Value);
        }
        catch { /* ungültiges iCal überspringen */ }
    }

    // ── XML ─────────────────────────────────────────────────────────────────

    private const string PropfindBody = """
        <?xml version="1.0" encoding="UTF-8"?>
        <D:propfind xmlns:D="DAV:">
          <D:prop>
            <D:resourcetype/>
            <D:displayname/>
          </D:prop>
        </D:propfind>
        """;

    private const string CalendarQueryBody = """
        <?xml version="1.0" encoding="UTF-8"?>
        <C:calendar-query xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
          <D:prop>
            <D:getetag/>
            <D:href/>
          </D:prop>
          <C:filter>
            <C:comp-filter name="VCALENDAR">
              <C:comp-filter name="VEVENT"/>
            </C:comp-filter>
          </C:filter>
        </C:calendar-query>
        """;

    private static string BuildMultigetBody(IEnumerable<string> hrefs)
    {
        var hrefElements = string.Join("\n", hrefs.Select(h => $"  <D:href>{h}</D:href>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <C:calendar-multiget xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
              <D:prop>
                <D:getetag/>
                <C:calendar-data/>
              </D:prop>
            {hrefElements}
            </C:calendar-multiget>
            """;
    }

    private static List<string> ParseHrefsFromReport(string xml)
    {
        var result = new List<string>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var resp in doc.Descendants(Dav + "response"))
            {
                var href = resp.Element(Dav + "href")?.Value;
                if (!string.IsNullOrWhiteSpace(href)
                    && href.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(href);
                }
            }
        }
        catch { }
        return result;
    }

    private static Dictionary<string, (string Etag, string ICal, string FullHref)> ParseMultigetResponse(string xml)
    {
        // Key = normalisierter Dateiname, Value = (etag, ical, vollständiger href)
        var result = new Dictionary<string, (string, string, string)>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var resp in doc.Descendants(Dav + "response"))
            {
                var href = resp.Element(Dav + "href")?.Value;
                var etag = resp.Descendants(Dav + "getetag").FirstOrDefault()?.Value ?? "";
                var ical = resp.Descendants(CDav + "calendar-data").FirstOrDefault()?.Value ?? "";

                if (href != null
                    && href.EndsWith(".ics", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(ical))
                {
                    var filename = NormalizeHref(href);
                    result[filename] = (etag, ical, href);
                }
            }
        }
        catch { }
        return result;
    }

    // ── HTTP ────────────────────────────────────────────────────────────────

    private static HttpClient BuildHttpClient(string baseUrl, string username, string password)
    {
        var handler = new HttpClientHandler
        {
            PreAuthenticate = false,
            Credentials     = new NetworkCredential(username, password),
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        return client;
    }

    // ── Encryption Stub ──────────────────────────────────────────────────────
    // TODO: Später durch DPAPI/AES ersetzen
    private static string DecryptPassword(string encrypted) => encrypted;
}