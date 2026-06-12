using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace ZeMail.UI.Services;

public static class HtmlSanitizer
{
    private static readonly HashSet<string> BlockedTags =
    [
        "script", "iframe", "object", "embed", "applet",
        "base", "form", "input", "button", "textarea", "select"
    ];

    private static readonly HashSet<string> BlockedAttributes =
    [
        "onclick", "ondblclick", "onmousedown", "onmouseup", "onmouseover",
        "onmousemove", "onmouseout", "onkeydown", "onkeypress", "onkeyup",
        "onload", "onunload", "onabort", "onerror", "onresize", "onscroll",
        "onsubmit", "onreset", "onfocus", "onblur", "onchange", "onselect"
    ];

    private const string CspMeta =
        "<meta http-equiv=\"Content-Security-Policy\" content=\"" +
        "default-src 'none'; " +
        "style-src 'unsafe-inline'; " +
        "img-src https: data: cid:; " +
        "font-src https: data:;\">";

    private const string RenderPatchStyle =
        "<style>" +
        "img { max-width: 100%; height: auto; } " +
        ".ze-avatar { display: inline-block; overflow: hidden; } " +
        ".ze-avatar img { border-radius: 50%; display: block; } " +
        "</style>";

    public static string Sanitize(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var tag in BlockedTags)
        {
            var nodes = doc.DocumentNode.SelectNodes("//" + tag);
            if (nodes is null) continue;
            foreach (var node in nodes.ToList())
                node.Remove();
        }

        var allNodes = doc.DocumentNode.SelectNodes("//*");
        if (allNodes is not null)
        {
            foreach (var node in allNodes)
            {
                var toRemove = node.Attributes
                    .Where(a => BlockedAttributes.Contains(a.Name.ToLower()) ||
                                a.Name.ToLower().StartsWith("on"))
                    .ToList();
                foreach (var attr in toRemove)
                    node.Attributes.Remove(attr);

                foreach (var attrName in new[] { "href", "src", "action" })
                {
                    var a = node.Attributes[attrName];
                    if (a is null) continue;
                    if (a.Value.Trim().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                        node.Attributes.Remove(a);
                }
            }
        }

        // Nur Avatar-Bilder fixen: <img> mit border-radius:50% bekommt einen div-Wrapper
        FixAvatarImages(doc);

        var head = doc.DocumentNode.SelectSingleNode("//head");
        if (head is not null)
        {
            var cspNode = HtmlNode.CreateNode(CspMeta);
            head.PrependChild(cspNode);
        }
        else
        {
            var cspNode = HtmlNode.CreateNode(CspMeta);
            doc.DocumentNode.PrependChild(cspNode);
        }

        return doc.DocumentNode.OuterHtml;
    }

    private static void FixAvatarImages(HtmlDocument doc)
    {
        var images = doc.DocumentNode.SelectNodes("//img");
        if (images is null) return;

        foreach (var img in images.ToList())
        {
            var style = img.Attributes["style"]?.Value ?? string.Empty;
            if (!style.Contains("border-radius", StringComparison.OrdinalIgnoreCase))
                continue;

            // Breite und Höhe aus style oder Attributen ermitteln
            var width  = img.Attributes["width"]?.Value  ?? "72";
            var height = img.Attributes["height"]?.Value ?? "72";

            // div-Wrapper mit overflow:hidden und border-radius
            var wrapper = doc.CreateElement("div");
            wrapper.SetAttributeValue("style",
                $"display:inline-block; overflow:hidden; border-radius:50%; " +
                $"width:{width}px; height:{height}px; line-height:0;");
            wrapper.AddClass("ze-avatar");

            // img aus Parent nehmen, in Wrapper legen, Wrapper an gleiche Stelle
            var parent = img.ParentNode;
            var imgClone = img.Clone();
            wrapper.AppendChild(imgClone);
            parent?.ReplaceChild(wrapper, img);
        }
    }

    public static string SanitizeAndWrap(string html, string darkModeStyle)
    {
        if (html.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            var sanitized = Sanitize(html);

            sanitized = sanitized.Replace(
                "</head>",
                RenderPatchStyle + "</head>",
                StringComparison.OrdinalIgnoreCase);

            if (!sanitized.Contains("<body style=", StringComparison.OrdinalIgnoreCase))
            {
                sanitized = sanitized.Replace(
                    "<body",
                    "<body style=\"" + darkModeStyle + "\"",
                    StringComparison.OrdinalIgnoreCase);
            }

            return sanitized;
        }

        var wrapped =
            "<!DOCTYPE html><html><head>" +
            CspMeta +
            "<meta charset='utf-8'>" +
            RenderPatchStyle +
            "<style>body { " + darkModeStyle + " }</style>" +
            "</head><body>" + html + "</body></html>";

        return Sanitize(wrapped);
    }
}