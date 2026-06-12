using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ZeMail.UI.Services;

public static class AvatarService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly Dictionary<string, Bitmap?> _cache = [];

    private static readonly string[] AvatarColors =
    [
        "#3a3aff", "#ff6060", "#ffaa00", "#40a060",
        "#60aaff", "#a060ff", "#ff60aa", "#00cccc"
    ];

    public static async Task<Bitmap?> ResolveAsync(string fromAddress, string fromName)
    {
        if (string.IsNullOrEmpty(fromAddress)) return null;

        var key = fromAddress.ToLower();
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var bimi = await TryLoadBimiAsync(fromAddress);
        if (bimi is not null) { _cache[key] = bimi; return bimi; }

        var gravatar = await TryLoadGravatarAsync(fromAddress);
        if (gravatar is not null) { _cache[key] = gravatar; return gravatar; }

        var initials = RenderInitials(fromName, fromAddress);
        _cache[key] = initials;
        return initials;
    }

    private static async Task<Bitmap?> TryLoadBimiAsync(string email)
    {
        try
        {
            var domain = email.Split('@')[1];
            var dohUrl = $"https://cloudflare-dns.com/dns-query?name=default._bimi.{domain}&type=TXT";
            var req    = new HttpRequestMessage(HttpMethod.Get, dohUrl);
            req.Headers.Add("Accept", "application/dns-json");

            var resp = await _http.SendAsync(req).WaitAsync(TimeSpan.FromSeconds(3));
            if (!resp.IsSuccessStatusCode) return null;

            var json   = await resp.Content.ReadAsStringAsync();
            var lIndex = json.IndexOf("l=https://", StringComparison.OrdinalIgnoreCase);
            if (lIndex < 0) return null;

            var urlStart = lIndex + 2;
            var urlEnd   = json.IndexOfAny(['"', ';', '\\'], urlStart);
            if (urlEnd < 0) urlEnd = json.Length;

            var svgUrl = json[urlStart..urlEnd]
                .Replace("\\u003D", "=")
                .Trim();

            if (!svgUrl.StartsWith("https://")) return null;

            var bytes = await _http.GetByteArrayAsync(svgUrl).WaitAsync(TimeSpan.FromSeconds(3));
            return LoadBitmapFromBytes(bytes);
        }
        catch { return null; }
    }

    private static async Task<Bitmap?> TryLoadGravatarAsync(string email)
    {
        try
        {
            var hash = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLower()))).ToLower();
            var url  = $"https://www.gravatar.com/avatar/{hash}?s=72&d=404";
            var resp = await _http.GetAsync(url).WaitAsync(TimeSpan.FromSeconds(3));
            if (!resp.IsSuccessStatusCode) return null;

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return LoadBitmapFromBytes(bytes);
        }
        catch { return null; }
    }

    private static Bitmap? LoadBitmapFromBytes(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch { return null; }
    }

    private static Bitmap? RenderInitials(string fromName, string fromAddress)
    {
        try
        {
            var initials = GetInitials(fromName, fromAddress);
            var color    = GetColor(fromAddress);
            const int size = 72;

            var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            using var ctx = bitmap.CreateDrawingContext();

            var brush = new SolidColorBrush(Color.Parse(color));
            ctx.DrawEllipse(brush, null,
                new Point(size / 2.0, size / 2.0),
                size / 2.0, size / 2.0);

            var ft = new FormattedText(
                initials,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial", FontStyle.Normal, FontWeight.Bold),
                26,
                Brushes.White);

            ctx.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            return bitmap;
        }
        catch { return null; }
    }

    private static string GetInitials(string name, string email)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            if (parts.Length == 1 && parts[0].Length >= 1)
                return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
        }
        return email.Length >= 2 ? email[..2].ToUpper() : "?";
    }

    private static string GetColor(string email)
        => AvatarColors[Math.Abs(email.GetHashCode()) % AvatarColors.Length];
}