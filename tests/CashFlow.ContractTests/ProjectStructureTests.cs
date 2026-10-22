using System.Reflection;
using Xunit;

namespace CashFlow.ContractTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void ContractsAssemblyIsIndependentFromBusinessContexts()
    {
        var contracts = Assembly.Load("CashFlow.Contracts");
        var references = contracts.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Equal("CashFlow.Contracts", contracts.GetName().Name);
        Assert.DoesNotContain(references, name => name?.StartsWith("CashFlow.Transactions") is true);
        Assert.DoesNotContain(references, name => name?.StartsWith("CashFlow.Consolidation") is true);
    }
}
