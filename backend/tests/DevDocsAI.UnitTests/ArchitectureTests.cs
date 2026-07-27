using System.Reflection;
using DevDocsAI.Domain;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests;

/// <summary>
/// Guards the clean-architecture dependency rule: the Domain assembly must not
/// reference ASP.NET Core, EF Core, or any provider/infrastructure package.
/// </summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_has_no_forbidden_dependencies()
    {
        var referenced = typeof(DomainAssemblyReference).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToList();

        string[] forbidden =
        [
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
        ];

        foreach (var name in referenced)
        {
            forbidden.ShouldNotContain(
                f => name.StartsWith(f, StringComparison.Ordinal),
                $"Domain must not reference infrastructure package '{name}'.");
        }
    }
}
