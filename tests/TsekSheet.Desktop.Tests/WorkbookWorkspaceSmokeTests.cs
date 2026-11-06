using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Wpf;
using TsekSheet.Application.Excel;
using TsekSheet.TestFixtures;

namespace TsekSheet.Desktop.Tests;

public sealed partial class DesktopSmokeTests
{
    [TestMethod, TestCategory("WindowsSmoke"), TestCategory("Excel")]
    public Task WorkbookSelectionOpensQueueAndInsertsOnlyIntoSelectedExcelIdentity() => OnDesktop(async () =>
    {
        var host = new TsekSheet.Excel.ExcelHost();
        var path = await LiquidationFixture.CreateAsync(host);
        var otherPath = Path.Combine(Path.GetDirectoryName(path)!, "other-synthetic.xlsx");
        File.Copy(path, otherPath);
        var other = await host.OpenAsync(otherPath);
        var otherBefore = await host.CaptureAsync(other.Id);
        var source = Path.Combine(Path.GetDirectoryName(path)!, "workspace-receipt.pdf");
        File.WriteAllBytes(source, DocumentRenderingTests.Pdf());
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var opens = 0;
        var window = new MainWindow(null, profile, null, _ => { opens++; return Task.FromResult<string?>(path); },
            workflowProfiles: [LiquidationProfile.Create()], ownedExcelHost: host,
            documentQueueSelector: _ => Task.FromResult<IReadOnlyList<string>>([source]))
            { ShowInTaskbar = false, Width = 1440, Height = 900 };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            async Task Ready(string predicate) => await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync(predicate) == "true", predicate);
            async Task Click(string label)
            {
                var value = JsonSerializer.Serialize(label);
                await Ready($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={value}&&!b.disabled&&b.getClientRects().length)");
                await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={value}&&!b.disabled&&b.getClientRects().length).click()");
            }
            async Task Input(string selector, string value, bool select = false)
            {
                await Ready($"!!document.querySelector('{selector}')&&!document.querySelector('{selector}').disabled");
                await web.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('{selector}');Object.getOwnPropertyDescriptor({(select ? "HTMLSelectElement" : "HTMLInputElement")}.prototype,'value').set.call(e,{JsonSerializer.Serialize(value)});e.dispatchEvent(new Event('{(select ? "change" : "input")}',{{bubbles:true}}));}})()");
            }
            await Ready("document.querySelector('[role=status]')?.textContent==='Connected'");
            await Click("Open workbook");
            await Ready("document.querySelector('h1')?.textContent==='Process Receipts'&&!!document.querySelector('.fast-receipt-loop')");
            var selected = (await host.ListWorkbooksAsync()).Single(b => b.FullPath == path);
            Assert.AreEqual(1, opens);
            Assert.AreNotEqual(other.Id, selected.Id);
            Assert.AreEqual("false", await web.ExecuteScriptAsync("!!Array.from(document.querySelectorAll('.semantic-notice')).find(n=>n.getClientRects().length&&n.textContent.includes('inspection mode'))"));
            await Click("Add PDFs or images"); await Click("Start receipt queue");
            await Ready("document.querySelector('[aria-label=\"Queue sheet\"]')?.options.length===3");
            await Ready("!!document.querySelector('.rendered-source')&&document.querySelector('.rendered-source').complete&&document.querySelector('.rendered-source').naturalWidth>0");
            Assert.AreEqual(JsonSerializer.Serialize(selected.Id), await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Queue workbook\"]').value"));
            await Input("[aria-label=\"Queue sheet\"]", "Person 1", true);
            await Input("[aria-label=\"Queue section\"]", "expenses", true);
            await Click("Use destination for this receipt and new receipts");
            await Input("#queue-itemNo", "UX01-SYNTHETIC"); await Input("#queue-particulars", "Synthetic workspace receipt");
            await Input("#queue-account", "J", true); await Input("#queue-amount", "123.50");
            await Click("SettingsAppearance & troubleshooting");
            await Click("Switch workbook…");
            await Ready("document.querySelector('[role=alert]')?.textContent.includes('switching is blocked')");
            Assert.AreEqual(1, opens, "An unsafe switch must not open the dialog.");
            await web.ExecuteScriptAsync("document.querySelector('nav button[data-activity=receipts]').click()");
            Assert.AreEqual("\"UX01-SYNTHETIC\"", await web.ExecuteScriptAsync("document.querySelector('#queue-itemNo').value"));
            await Click("Preview row"); await Ready("!!document.querySelector('[aria-label=\"Queue row preview\"]')");
            Assert.AreEqual(selected.Id, (await host.ListWorkbooksAsync()).Single(b => b.FullPath == path).Id);
            Assert.AreNotEqual("UX01-SYNTHETIC", (await host.ReadAsync(selected.Id, "Person 1", new(29, 2, 1, 1))).Values[0, 0], "Preview must not write.");
            foreach (var width in new[] { 1440d, 768d, 390d })
            {
                window.Width = width; await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Workspace overflow at " + width);
                await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Queue row preview\"]').scrollIntoView({block:'start'})");
                using var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, $"ux01-workspace-{width}.png"));
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
            }
            await Click("Add Row — confirm values and destination");
            await Ready("document.querySelectorAll('[aria-label=\"Inserted queue rows\"] li').length===1&&document.querySelector('.fast-receipt-loop').getAttribute('aria-busy')==='false'");
            var row = await host.ReadAsync(selected.Id, "Person 1", new(29, 2, 1, 31));
            Assert.AreEqual("UX01-SYNTHETIC", row.Values[0, 0]); Assert.AreEqual(123.50, row.Values[0, 8]);
            Assert.IsTrue((await host.ReadAsync(selected.Id, "Person 1", new(29, 14, 1, 1))).Formulas[0, 0]?.Length > 0);
            Assert.AreEqual(1, opens, "Receipt processing must not reopen the workbook.");
            Assert.AreEqual(0, PreservationCheck.Compare(otherBefore, await host.CaptureAsync(other.Id), "Person 1", new(1, 2, 1, 1), new object?[,] { { "Synthetic liquidation 0" } }).Count, "The other workbook must remain identical through Excel readback.");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Queue workbook\"]').disabled"));
        }
        finally
        {
            foreach (var book in await host.ListWorkbooksAsync()) await host.CloseAsync(book.Id, discardChanges: true);
            window.Close(); await host.DisposeAsync();
        }
    });
}
