using System.Reflection;
using Xunit;

namespace CashFlow.Transactions.UnitTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void DomainAndApplicationAssembliesAreAvailable()
    {
        var domain = Assembly.Load("CashFlow.Transactions.Domain");
        var application = Assembly.Load("CashFlow.Transactions.Application");

        Assert.Equal("CashFlow.Transactions.Domain", domain.GetName().Name);
        Assert.Equal("CashFlow.Transactions.Application", application.GetName().Name);
    }
}
