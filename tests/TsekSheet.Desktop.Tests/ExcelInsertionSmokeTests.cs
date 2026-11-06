using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Wpf;
using TsekSheet.Application.Workflows;
using TsekSheet.TestFixtures;

namespace TsekSheet.Desktop.Tests;

public sealed partial class DesktopSmokeTests
{
    [TestMethod, TestCategory("WindowsSmoke"), TestCategory("Excel")]
    public Task PrinterSettingsInspectionDoesNotGateHostedExcelInsertion() => OnDesktop(async () =>
    {
#if DEBUG
        Assert.Fail("Excel integration tests must run in Release configuration.");
#endif
        var host = new TsekSheet.Excel.ExcelHost();
        var path = await LiquidationFixture.CreateAsync(host);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
            Assert.IsTrue(zip.Entries.Any(e => e.FullName.StartsWith("xl/printerSettings/", StringComparison.Ordinal)));
        var source = Path.Combine(Path.GetDirectoryName(path)!, "fake-receipt.pdf");
        File.WriteAllBytes(source, DocumentRenderingTests.Pdf());
        var browser = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(browser);
        var profile = LiquidationProfile.Create();
        var window = new MainWindow(null, browser, null, _ => Task.FromResult<string?>(path),
            documentSelector: _ => Task.FromResult<string?>(source), workflowProfiles: [profile], ownedExcelHost: host)
            { ShowInTaskbar = false, Width = 1440, Height = 900 };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host unavailable");
            async Task Click(string text)
            {
                var label = JsonSerializer.Serialize(text);
                await Until(async () => await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={label}&&!b.disabled)") == "true", "Button unavailable: " + text);
                await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={label}).click()");
            }
            await Navigate(web, "workbook"); await Click("Open for read-only inspection");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('.reader-file') && document.body.textContent.includes('Workbook opened in inspection mode')") == "true", "Replica must exercise legacy inspection fallback");
            await Navigate(web, "receipts"); await Click("Select receipt PDF or image"); await Click("Start manual receipt review");
            foreach (var field in new[] { ("itemNo", "Item no.", "NEW-29"), ("particulars", "Particulars", "Fake hosted expense"), ("account", "Account", "J"), ("amount", "Amount", "123.50") })
            {
                await Until(async () => await web.ExecuteScriptAsync($"!!document.querySelector('#receipt-{field.Item1}') && !document.querySelector('#receipt-{field.Item1}').disabled") == "true", "Review field unavailable");
                var value = JsonSerializer.Serialize(field.Item3);
                var type = field.Item1 == "account" ? "HTMLSelectElement" : "HTMLInputElement";
                await web.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('#receipt-{field.Item1}');Object.getOwnPropertyDescriptor({type}.prototype,'value').set.call(e,{value});e.dispatchEvent(new Event('{(field.Item1 == "account" ? "change" : "input")}',{{bubbles:true}}));}})()");
                await Click("Save " + field.Item2); await Click("Verify " + field.Item2);
            }
            await web.ExecuteScriptAsync("document.querySelector('.receipt-fields').scrollIntoView({block:'start'})");
            using (var fields = File.Create(Path.Combine(AppContext.BaseDirectory, "excel-account-review.png")))
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, fields);
            await Step(web, 2); await Click("Open workbook in Excel");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Excel sheet\"]').options.length===3") == "true", "Both matching sheets must be offered");
            Assert.AreEqual("false", await web.ExecuteScriptAsync("Array.from(document.querySelector('[aria-label=\"Excel sheet\"]').options).some(o=>o.textContent==='VENDOR')"));
            foreach (var choice in new[] { ("Excel sheet", "Person 1"), ("Excel section", "expenses") })
                await web.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('[aria-label=\"{choice.Item1}\"]');e.value='{choice.Item2}';e.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
            await Click("Preview Excel insert");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Excel insertion preview\"]') || !!document.querySelector('.excel-insertion [role=alert]')") == "true", "Hosted preview response missing");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Excel insertion preview\"]')?.textContent.includes('Person 1!B29:AF29')"),
                await web.ExecuteScriptAsync("document.querySelector('.excel-insertion').textContent"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Excel insertion preview\"]').textContent.includes('123.50')"));
            foreach (var width in new[] { 1440d, 768d, 390d })
            {
                window.Width = width; await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Excel controls overflow");
                await web.ExecuteScriptAsync("document.querySelector('.excel-insertion').scrollIntoView({block:'start'})");
                using var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, $"excel-insertion-{width}.png"));
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
            }
            await Click("Insert verified row");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Inserted Excel rows\"]')") == "true", "Hosted insert failed");
            var book = (await host.ListWorkbooksAsync()).Single();
            var read = await host.ReadAsync(book.Id, "Person 1", new(29, 10, 1, 23));
            Assert.AreEqual(123.50, read.Values[0, 0]); Assert.AreEqual("=SUM(J29:M29)*0.12", read.Formulas[0, 4]);
        }
        finally
        {
            foreach (var book in await host.ListWorkbooksAsync()) await host.CloseAsync(book.Id, discardChanges: true);
            window.Close(); await host.DisposeAsync();
        }
    });
}
