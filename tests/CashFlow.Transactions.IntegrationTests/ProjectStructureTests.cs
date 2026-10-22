using System.Reflection;
using Xunit;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void ApiAndInfrastructureAssembliesAreAvailable()
    {
        var api = Assembly.Load("CashFlow.Transactions.Api");
        var infrastructure = Assembly.Load("CashFlow.Transactions.Infrastructure");

        Assert.Equal("CashFlow.Transactions.Api", api.GetName().Name);
        Assert.Equal("CashFlow.Transactions.Infrastructure", infrastructure.GetName().Name);
    }
}
