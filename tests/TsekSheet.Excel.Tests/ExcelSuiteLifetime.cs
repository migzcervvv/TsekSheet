using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Excel.Tests;

[TestClass]
public sealed class ExcelSuiteLifetime
{
    // Close the shared test session without waiting for process exit. The diagnostics
    // observer records that exit independently; unit-only runs never create a host.
    [AssemblyCleanup]
    public static Task CloseSharedSessionAsync() => ExcelTestScope.RetireSharedHostAsync();
}
