using System.Reflection;
using Xunit;

namespace CashFlow.Consolidation.UnitTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void DomainAndApplicationAssembliesAreAvailable()
    {
        var domain = Assembly.Load("CashFlow.Consolidation.Domain");
        var application = Assembly.Load("CashFlow.Consolidation.Application");

        Assert.Equal("CashFlow.Consolidation.Domain", domain.GetName().Name);
        Assert.Equal("CashFlow.Consolidation.Application", application.GetName().Name);
    }
}
