using System.Reflection;
using System.Text.RegularExpressions;
using Markdig;
using MdReader.Models;

namespace MdReader.Services;

public sealed partial class DocumentLibrary
{
    private const string DocumentResourcePrefix = "MdReader.Documents/";
    private const string DefaultDescription = "Bundled Markdown document.";

    private readonly Assembly _assembly = typeof(DocumentLibrary).Assembly;
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public async Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync()
    {
        var resourceNames = _assembly
            .GetManifestResourceNames()
            .Where(name =>
                name.StartsWith(DocumentResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var documents = new List<DocumentInfo>(resourceNames.Length);
        foreach (var resourceName in resourceNames)
        {
            var markdown = await ReadEmbeddedTextAsync(resourceName);
            var relativePath = resourceName[DocumentResourcePrefix.Length..].Replace('\\', '/');

            documents.Add(new DocumentInfo
            {
                Title = ExtractTitle(markdown, relativePath),
                ResourceName = resourceName,
                RelativePath = relativePath,
                Description = ExtractDescription(markdown),
                Category = ExtractCategory(relativePath)
            });
        }

        return documents
            .OrderBy(document => document.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public Task<string> GetMarkdownAsync(DocumentInfo document) =>
        ReadEmbeddedTextAsync(document.ResourceName);

    public string RenderHtml(string markdown, string title, bool useDarkTheme, int fontSize)
    {
        var body = Markdown.ToHtml(markdown, _markdownPipeline);
        var background = useDarkTheme ? "#171A19" : "#FBFAF7";
        var foreground = useDarkTheme ? "#E8ECE9" : "#242A27";
        var muted = useDarkTheme ? "#AAB6B0" : "#617068";
        var accent = useDarkTheme ? "#9AC8B1" : "#315C4A";
        var panel = useDarkTheme ? "#242A27" : "#F0EEE7";
        var border = useDarkTheme ? "#3A4540" : "#D8D7D0";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{System.Net.WebUtility.HtmlEncode(title)}}</title>
              <style>
                :root { color-scheme: {{(useDarkTheme ? "dark" : "light")}}; }
                * { box-sizing: border-box; }
                html { background: {{background}}; }
                body {
                  max-width: 820px;
                  margin: 0 auto;
                  padding: 28px 24px 72px;
                  background: {{background}};
                  color: {{foreground}};
                  font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
                  font-size: {{fontSize}}px;
                  line-height: 1.72;
                  overflow-wrap: break-word;
                }
                h1, h2, h3, h4, h5, h6 {
                  color: {{foreground}};
                  line-height: 1.25;
                  margin: 1.65em 0 .55em;
                }
                h1 { font-size: 2em; margin-top: .25em; letter-spacing: -.025em; }
                h2 { font-size: 1.5em; padding-bottom: .3em; border-bottom: 1px solid {{border}}; }
                h3 { font-size: 1.22em; }
                p, ul, ol, pre, blockquote, table { margin: 0 0 1.15em; }
                a { color: {{accent}}; text-decoration-thickness: .08em; text-underline-offset: .15em; }
                blockquote {
                  margin-left: 0;
                  padding: .2em 1em;
                  color: {{muted}};
                  border-left: 4px solid {{accent}};
                }
                code {
                  padding: .15em .35em;
                  border-radius: 5px;
                  background: {{panel}};
                  font-family: "Cascadia Mono", "SFMono-Regular", Consolas, monospace;
                  font-size: .9em;
                }
                pre {
                  overflow-x: auto;
                  padding: 1em;
                  border: 1px solid {{border}};
                  border-radius: 10px;
                  background: {{panel}};
                }
                pre code { padding: 0; background: transparent; }
                table { width: 100%; border-collapse: collapse; display: block; overflow-x: auto; }
                th, td { padding: .55em .75em; border: 1px solid {{border}}; text-align: left; }
                th { background: {{panel}}; }
                img { max-width: 100%; height: auto; border-radius: 8px; }
                hr { margin: 2em 0; border: 0; border-top: 1px solid {{border}}; }
                input[type="checkbox"] { width: 1.05em; height: 1.05em; }
              </style>
            </head>
            <body>{{body}}</body>
            </html>
            """;
    }

    private async Task<string> ReadEmbeddedTextAsync(string resourceName)
    {
        await using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"The embedded document '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string ExtractTitle(string markdown, string relativePath)
    {
        var heading = H1HeadingRegex().Match(markdown);
        if (heading.Success)
        {
            return CleanInlineMarkdown(heading.Groups[1].Value);
        }

        var fileName = relativePath.Split('/').Last();
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return ToDisplayName(nameWithoutExtension);
    }

    private static string ExtractDescription(string markdown)
    {
        var paragraphs = BlankLineRegex().Split(markdown.Replace("\r\n", "\n"));
        foreach (var paragraph in paragraphs)
        {
            var candidate = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(candidate) ||
                candidate.StartsWith('#') ||
                candidate.StartsWith('>') ||
                candidate.StartsWith("```") ||
                candidate.StartsWith("~~~") ||
                candidate.StartsWith("- ") ||
                candidate.StartsWith("* ") ||
                OrderedListRegex().IsMatch(candidate))
            {
                continue;
            }

            candidate = WhitespaceRegex().Replace(candidate, " ");
            candidate = CleanInlineMarkdown(candidate);
            return candidate.Length <= 180 ? candidate : $"{candidate[..177]}...";
        }

        return DefaultDescription;
    }

    private static string ExtractCategory(string relativePath)
    {
        var directoryEnd = relativePath.LastIndexOf('/');
        if (directoryEnd < 0)
        {
            return "Document";
        }

        var directory = relativePath[..directoryEnd].Split('/').Last();
        return ToDisplayName(directory);
    }

    private static string ToDisplayName(string value)
    {
        var words = WhitespaceRegex().Replace(value.Replace('-', ' ').Replace('_', ' '), " ").Trim();
        return string.IsNullOrWhiteSpace(words)
            ? "Untitled"
            : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words);
    }

    private static string CleanInlineMarkdown(string value)
    {
        var withoutImages = MarkdownImageRegex().Replace(value, "$1");
        var withoutLinks = MarkdownLinkRegex().Replace(withoutImages, "$1");
        return InlineMarkerRegex().Replace(withoutLinks, string.Empty).Trim();
    }

    [GeneratedRegex(@"^\s*#\s+(.+?)\s*#*\s*$", RegexOptions.Multiline)]
    private static partial Regex H1HeadingRegex();

    [GeneratedRegex(@"\n\s*\n+")]
    private static partial Regex BlankLineRegex();

    [GeneratedRegex(@"^\d+[.)]\s")]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"[*_`~]")]
    private static partial Regex InlineMarkerRegex();
}
