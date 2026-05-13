using System.Reflection;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class AssemblyLoadTests
{
    [Theory]
    [InlineData("AdvancedRag.App")]
    [InlineData("AdvancedRag.Domain")]
    public void FoundationAssembly_Loads(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        assembly.GetName().Name.Should().Be(assemblyName);
    }
}
