using System.Reflection;
using System.Xml.Linq;
using BookExchange.Domain.Shared;

namespace BookExchange.UnitTests.Architecture;

/// <summary>R-5: dependencies point inward (Api → Infrastructure → Application → Domain).</summary>
public sealed class LayeringTests
{
    [Theory]
    [InlineData("BookExchange.Domain", new string[0])]
    [InlineData("BookExchange.Application", new[] { "BookExchange.Domain" })]
    [InlineData("BookExchange.Infrastructure", new[] { "BookExchange.Application" })]
    [InlineData("BookExchange.Api", new[] { "BookExchange.Infrastructure" })]
    public void Project_references_only_the_next_inner_layer(string project, string[] expectedReferences)
    {
        var references = ReadProject(project)
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')));

        references.Should().BeEquivalentTo(expectedReferences);
    }

    [Fact]
    public void Domain_has_no_package_or_framework_dependencies()
    {
        var project = ReadProject("BookExchange.Domain");

        project.Descendants("PackageReference").Should().BeEmpty("Domain is BCL only");
        project.Descendants("FrameworkReference").Should().BeEmpty("Domain is BCL only");
    }

    [Fact]
    public void Domain_assembly_does_not_reference_ef_core_aspnet_or_other_layers()
    {
        var referenced = typeof(DomainException).Assembly.GetReferencedAssemblies().Select(a => a.Name!);

        referenced.Should().NotContain(name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("BookExchange.", StringComparison.Ordinal));
    }

    private static XDocument ReadProject(string name)
    {
        var path = Path.Combine(RepositoryRoot(), "src", name, $"{name}.csproj");
        return XDocument.Load(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BookExchange.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (BookExchange.slnx) not found.");
    }
}
