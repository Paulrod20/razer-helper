namespace RazerHelper.UI.Sections;

/// <summary>A user-facing message a section wants the host to display.</summary>
internal sealed record SectionStatus(string Message, bool IsError = false);
