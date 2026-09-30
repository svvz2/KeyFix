namespace KeyFix.Core;

public static class ReleaseVersion
{
    public static bool IsNewer(string? candidate, string? current)
    {
        return TryParse(candidate, out var candidateVersion) &&
               TryParse(current, out var currentVersion) &&
               candidateVersion > currentVersion;
    }

    public static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['-', '+']);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out version!);
    }
}
