namespace WicklowArchery.Core.Data;

public static class FormUrls
{
    public const string ClubForm = "https://docs.google.com/forms/d/e/1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw/viewform";
    public const string TestForm = "https://docs.google.com/forms/d/e/1FAIpQLSdP3j6gNAZbVACWKqW-evjYMjcMXvA1R2XUjJpJzJmC6M-Inw/viewform";

    private const string ClubFormId = "1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw";

    public static bool IsClubForm(string url) =>
        !string.IsNullOrEmpty(url) && url.Contains(ClubFormId, StringComparison.Ordinal);
}
