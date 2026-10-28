using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ApplicationArchitectureTests
{
    [TestMethod]
    public void ApplicationAssembly_DoesNotReferenceDesktopFrameworksOrHost()
    {
        var wpfAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WindowsBase",
            "System.Xaml",
            "System.Printing",
            "ReachFramework",
            "System.Windows.Controls.Ribbon",
            "System.Windows.Presentation"
        };

        // Inspect compiled direct references without loading WPF or starting the host.
        var forbiddenReferences = typeof(ApplicationMetadataService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => wpfAssemblies.Contains(name)
                || name.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("UIAutomation", StringComparison.OrdinalIgnoreCase)
                || IsAssemblyOrChild(name, "Microsoft.Web.WebView2")
                || IsAssemblyOrChild(name, "TsekSheet.Desktop"))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.AreEqual(0, forbiddenReferences.Length,
            $"Application must remain independent of desktop dependencies. Found: {string.Join(", ", forbiddenReferences)}");
    }

    private static bool IsAssemblyOrChild(string name, string root) =>
        name.Equals(root, StringComparison.OrdinalIgnoreCase)
        || name.StartsWith(root + ".", StringComparison.OrdinalIgnoreCase);
}
