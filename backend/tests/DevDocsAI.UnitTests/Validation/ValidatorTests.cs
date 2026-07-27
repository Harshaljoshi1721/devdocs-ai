using DevDocsAI.Application.Features.Auth;
using DevDocsAI.Application.Features.Projects;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Validation;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("", "Alice", "password123", false)]      // empty email
    [InlineData("not-an-email", "Alice", "password123", false)]
    [InlineData("a@b.com", "", "password123", false)]    // empty name
    [InlineData("a@b.com", "Alice", "short", false)]     // < 8 chars
    [InlineData("a@b.com", "Alice", "password123", true)]
    public void Register_validation(string email, string name, string password, bool expectedValid)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest(email, name, password));
        result.IsValid.ShouldBe(expectedValid);
    }

    [Fact]
    public void Create_project_requires_name()
    {
        var validator = new CreateProjectRequestValidator();
        validator.Validate(new CreateProjectRequest("", null)).IsValid.ShouldBeFalse();
        validator.Validate(new CreateProjectRequest("My Project", "desc")).IsValid.ShouldBeTrue();
    }
}
