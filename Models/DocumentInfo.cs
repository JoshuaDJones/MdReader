namespace MdReader.Models;

public sealed class DocumentInfo
{
    public string Title { get; init; } = string.Empty;

    public string ResourceName { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;
}
