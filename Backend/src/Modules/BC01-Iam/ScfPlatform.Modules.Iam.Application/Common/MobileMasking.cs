namespace ScfPlatform.Modules.Iam.Application.Common;

/// <summary>BC-01-IAM-and-UAM.md §10.4 — "Mobile numbers are masked (<c>+8801******23</c>) in admin/list DTOs."</summary>
public static class MobileMasking
{
    public static string Mask(string e164Mobile)
    {
        if (string.IsNullOrEmpty(e164Mobile) || e164Mobile.Length < 6)
        {
            return e164Mobile;
        }

        var prefix = e164Mobile[..4]; // e.g. "+880"
        var suffix = e164Mobile[^2..];
        var maskedLength = e164Mobile.Length - prefix.Length - suffix.Length;

        return prefix + new string('*', Math.Max(maskedLength, 0)) + suffix;
    }
}
