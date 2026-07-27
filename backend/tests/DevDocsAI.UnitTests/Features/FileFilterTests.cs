using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Domain.Enums;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class FileFilterTests
{
    private readonly ExtensionFileFilter _filter = new();

    [Theory]
    [InlineData("Program.cs", true)]
    [InlineData("README.md", true)]
    [InlineData("config.json", true)]
    [InlineData("app.yaml", true)]
    [InlineData("photo.png", false)]
    [InlineData("binary.exe", false)]
    [InlineData("noextension", false)]
    public void IsSupported_matches_allowlist(string fileName, bool expected)
    {
        _filter.IsSupported(fileName).ShouldBe(expected);
    }

    [Theory]
    [InlineData(".env", true)]
    [InlineData(".env.production", true)]
    [InlineData("config/.env.local", true)]
    [InlineData("server.pem", true)]
    [InlineData("private.key", true)]
    [InlineData("cert.crt", true)]
    [InlineData("id_rsa", true)]
    [InlineData("Program.cs", false)]
    [InlineData("appsettings.json", false)]
    public void IsSecret_flags_sensitive_files(string fileName, bool expected)
    {
        _filter.IsSecret(fileName).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Program.cs", true)]
    [InlineData(".env", false)]          // supported-looking? no ext, and secret
    [InlineData("secrets.pem", false)]   // secret extension
    [InlineData("keystore.p12", false)]  // secret, and unsupported ext
    [InlineData("image.png", false)]     // unsupported
    public void IsAllowed_requires_supported_and_not_secret(string fileName, bool expected)
    {
        _filter.IsAllowed(fileName).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Program.cs", FileType.Code)]
    [InlineData("main.go", FileType.Code)]
    [InlineData("README.md", FileType.Documentation)]
    [InlineData("notes.txt", FileType.Documentation)]
    [InlineData("docker-compose.yml", FileType.Configuration)]
    [InlineData("data.bin", FileType.Other)]
    public void Categorize_maps_extension_to_type(string fileName, FileType expected)
    {
        _filter.Categorize(fileName).ShouldBe(expected);
    }

    [Fact]
    public void Path_like_names_are_classified_by_extension_only()
    {
        // A traversal-looking name is still just categorized by its extension;
        // it is never used to build a storage path (see LocalFileStorage).
        _filter.IsSupported("../../etc/passwd.cs").ShouldBeTrue();
        _filter.Categorize("../../etc/passwd.cs").ShouldBe(FileType.Code);
    }
}
