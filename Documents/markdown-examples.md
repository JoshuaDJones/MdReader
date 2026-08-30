# Markdown Examples

This document is a quick visual check of common Markdown formatting.

## Text

You can use **bold text**, *italic text*, and [links](https://learn.microsoft.com/dotnet/maui/).

> Block quotes are useful for notes, callouts, and excerpts.

## Lists

- First unordered item
- Second unordered item
  - Nested item

1. First ordered item
2. Second ordered item

## Tasks

- [x] Drop a Markdown file into `Documents`
- [x] Rebuild the app
- [x] Read it offline

## Table

| Platform | Supported |
| --- | --- |
| Windows | Yes |
| Android | Yes |

## Code

Inline code looks like `Documents/my-notes.md`.

```csharp
var documents = typeof(App).Assembly
    .GetManifestResourceNames()
    .Where(name => name.EndsWith(".md"));
```

---

The reader supports standard Markdown plus common extensions such as tables and task lists.
