using System.Reflection;
using Xunit;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void ApiAndInfrastructureAssembliesAreAvailable()
    {
        var api = Assembly.Load("CashFlow.Consolidation.Api");
        var infrastructure = Assembly.Load("CashFlow.Consolidation.Infrastructure");

        Assert.Equal("CashFlow.Consolidation.Api", api.GetName().Name);
        Assert.Equal("CashFlow.Consolidation.Infrastructure", infrastructure.GetName().Name);
    }
}
