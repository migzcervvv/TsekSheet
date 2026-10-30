using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Workbooks.Tests;

[TestClass, DoNotParallelize]
public sealed class NativeAccountingDisplayTests
{
    public TestContext TestContext { get; set; } = null!;
    [TestMethod]
    public void CompareInspectorAgainstRetainedActualExcelText()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "ReferenceCorpus", "AccountingDisplay");
        using var capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "capture.json")));
        int matched = 0, unsupported = 0, different = 0;
        foreach (var book in capture.RootElement.GetProperty("workbooks").EnumerateArray())
        {
            var path = Path.Combine(root, book.GetProperty("file").GetString()!);
            string Hash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            var hash = Hash(); Assert.AreEqual(book.GetProperty("outputSha256").GetString(), hash);
            using (var session = new WorkbookInspection().Open(path))
            {
                foreach (var row in book.GetProperty("rows").EnumerateArray())
                {
                    var n = row.GetProperty("row").GetInt32(); var native = row.GetProperty("text").GetString();
                    Assert.AreEqual(row.GetProperty("expected").GetString(), native, "Independent native literal row " + n);
                    var c = session.ReadViewport(session.Info.Worksheets[0].Id, n, 1, 1, 1).Cells[0];
                    var i = c.Interpretation!;
                    if (i.Confidence == "Unsupported") { unsupported++; TestContext.WriteLine($"Unsupported row {n}: {c.Format.NumberFormat} / {i.Warning}"); }
                    else if (i.DisplayValue == native) matched++;
                    else { different++; TestContext.WriteLine($"Divergence synthetic row {n}: native [{native}], inspection [{i.DisplayValue}]"); }
                }
            }
            Assert.AreEqual(hash, Hash());
        }
        TestContext.WriteLine($"Matched {matched}; unsupported {unsupported}; different {different}");
        Assert.AreEqual(26, matched); Assert.AreEqual(4, unsupported); Assert.AreEqual(0, different);
    }
}
