namespace SpaceLens.ViewModels;

public sealed class NavEntry
{
    public NavEntry(string page, string label, string? section = null)
    {
        Page = page;
        Label = label;
        Section = section;
    }

    public string Page { get; }
    public string Label { get; }
    public string? Section { get; }
    public bool HasSection => !string.IsNullOrEmpty(Section);
}
