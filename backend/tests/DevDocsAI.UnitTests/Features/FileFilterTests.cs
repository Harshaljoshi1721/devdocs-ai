using System.Text;
using DevDocsAI.Application.Features.Ingestion;
using DevDocsAI.Domain.Enums;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class FileFilterTests
{
    private readonly ExtensionFileFilter _filter = new();

    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("clip.mp4", true)]
    [InlineData("archive.zip", true)]
    [InlineData("lib.dll", true)]
    [InlineData("doc.pdf", true)]
    [InlineData("font.woff2", true)]
    [InlineData("Program.cs", false)]
    [InlineData("service.rb", false)]
    [InlineData("icon.svg", false)]      // SVG is text/XML, not binary
    [InlineData("Dockerfile", false)]    // no extension, treated as text
    [InlineData("noextension", false)]
    public void IsBinaryExtension_flags_known_binary_types(string fileName, bool expected)
    {
        _filter.IsBinaryExtension(fileName).ShouldBe(expected);
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
    [InlineData("Program.cs", true)]     // ordinary source: accepted
    [InlineData("service.rb", true)]     // not on any old allowlist, now accepted
    [InlineData("query.sql", true)]
    [InlineData("noextension", true)]    // could be text; content sniff decides later
    [InlineData(".env", false)]          // secret
    [InlineData("secrets.pem", false)]   // secret
    [InlineData("image.png", false)]     // binary extension
    [InlineData("app.exe", false)]       // binary extension
    public void IsAllowed_accepts_text_rejects_secret_and_binary(string fileName, bool expected)
    {
        _filter.IsAllowed(fileName).ShouldBe(expected);
    }

    [Fact]
    public void LooksBinary_is_true_when_sample_contains_a_nul_byte()
    {
        byte[] withNul = [0x48, 0x69, 0x00, 0x21];
        _filter.LooksBinary(withNul).ShouldBeTrue();
    }

    [Fact]
    public void LooksBinary_is_false_for_plain_utf8_text()
    {
        var text = Encoding.UTF8.GetBytes("def hello\n  puts 'hi'\nend\n");
        _filter.LooksBinary(text).ShouldBeFalse();
    }

    [Fact]
    public void LooksBinary_is_false_for_empty_sample()
    {
        _filter.LooksBinary([]).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Program.cs", FileType.Code)]
    [InlineData("main.go", FileType.Code)]
    [InlineData("service.rb", FileType.Code)]
    [InlineData("README.md", FileType.Documentation)]
    [InlineData("notes.txt", FileType.Documentation)]
    [InlineData("docker-compose.yml", FileType.Configuration)]
    [InlineData("mystery.xyz", FileType.Other)]
    public void Categorize_maps_extension_to_type(string fileName, FileType expected)
    {
        _filter.Categorize(fileName).ShouldBe(expected);
    }

    [Fact]
    public void Path_like_names_are_classified_by_extension_only()
    {
        // A traversal-looking name is still just categorized by its extension;
        // it is never used to build a storage path (see LocalFileStorage).
        _filter.IsAllowed("../../etc/passwd.cs").ShouldBeTrue();
        _filter.Categorize("../../etc/passwd.cs").ShouldBe(FileType.Code);
    }
}
