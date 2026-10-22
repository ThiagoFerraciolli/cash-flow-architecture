using System.Reflection;
using Xunit;

namespace CashFlow.EndToEndTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void BothApiAssembliesAreAvailableWithoutDirectCoupling()
    {
        var transactionsApi = Assembly.Load("CashFlow.Transactions.Api");
        var consolidationApi = Assembly.Load("CashFlow.Consolidation.Api");

        Assert.Equal("CashFlow.Transactions.Api", transactionsApi.GetName().Name);
        Assert.Equal("CashFlow.Consolidation.Api", consolidationApi.GetName().Name);
        Assert.NotEqual(transactionsApi, consolidationApi);
    }
}
