using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class LifecycleWorkflowDiagnostics
{
    // Keep all three functional repetitions; only the dedicated lifecycle test
    // waits for process exit or asserts the D11 liveness deadline.
    [TestMethod] public Task OriginalT2Run1() => Run();
    [TestMethod] public Task OriginalT2Run2() => Run();
    [TestMethod] public Task OriginalT2Run3() => Run();

    private static Task Run()
    {
#if DEBUG
        Assert.Fail("Excel diagnostics require Release.");
#endif
        return new SectionInsertTests().FiveInsertsPreserveNeighboursFillFormulasAndExtendOnlyTheSectionSubtotal("columns", false);
    }
}