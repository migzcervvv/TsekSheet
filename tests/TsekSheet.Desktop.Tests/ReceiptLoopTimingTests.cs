using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Wpf;
using TsekSheet.TestFixtures;

namespace TsekSheet.Desktop.Tests;

public sealed partial class DesktopSmokeTests
{
    [TestMethod, TestCategory("WindowsSmoke"), TestCategory("Excel")]
    public Task TwentyReceiptWorkflowTiming() => OnDesktop(async () =>
    {
        var host = new TsekSheet.Excel.ExcelHost();
        var path = await LiquidationFixture.CreateAsync(host);
        var sources = Enumerable.Range(0, 20).Select(i => Path.Combine(Path.GetDirectoryName(path)!, $"timing-{i}.pdf")).ToArray();
        foreach (var (source, i) in sources.Select((s, i) => (s, i))) File.WriteAllBytes(source, DocumentRenderingTests.Pdf().Concat(System.Text.Encoding.ASCII.GetBytes($"\n% receipt {i}\n")).ToArray());
        var nextSource = 0;
        var browser = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(browser);
        var window = new MainWindow(null, browser, null, _ => Task.FromResult<string?>(path),
            documentSelector: _ => Task.FromResult<string?>(sources[nextSource++]), workflowProfiles: [LiquidationProfile.Create()], ownedExcelHost: host)
            { ShowInTaskbar = false, Width = 1440, Height = 900 };
        var measurements = new List<object>();
        double input = 0;
        WebView2 web = null!;
        async Task Script(string script) { var watch = Stopwatch.StartNew(); await web.ExecuteScriptAsync(script); input += watch.Elapsed.TotalMilliseconds; }
        async Task Ready(string predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (web.CoreWebView2 != null && await web.ExecuteScriptAsync(predicate) == "true") return;
                await Task.Delay(5);
            }
            Assert.Fail("Timing state unavailable: " + predicate);
        }
        async Task Click(string text)
        {
            var label = JsonSerializer.Serialize(text);
            await Ready($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={label}&&!b.disabled)");
            await Script($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={label}).click()");
        }
        async Task Stage(int receipt, string stage, Func<Task> action)
        {
            input = 0; var watch = Stopwatch.StartNew(); await action();
            measurements.Add(new { receipt, stage, scriptedInputMs = input, applicationMs = watch.Elapsed.TotalMilliseconds - input });
        }
        try
        {
            window.Show(); web = (WebView2)window.FindName("FrontendWebView");
            await Ready("document.querySelector('[role=status]')?.textContent==='Connected'");
            await Navigate(web, "receipts");
            for (var i = 0; i < 20; i++)
            {
                await Stage(i, "receipt navigation", async () => { await Click("Select receipt PDF or image"); await Click("Start manual receipt review"); await Ready("!!document.querySelector('#receipt-itemNo')&&!document.querySelector('#receipt-itemNo').disabled"); });
                await Stage(i, "field entry / verification", async () =>
                {
                    foreach (var field in new[] { ("itemNo", "Item no.", $"LOOP-{i}"), ("particulars", "Particulars", $"Synthetic expense {i}"), ("account", "Account", LiquidationProfile.Columns[i % 19]), ("amount", "Amount", "123.50") })
                    {
                        var value = JsonSerializer.Serialize(field.Item3); var select = field.Item1 == "account";
                        await Script($"(()=>{{const e=document.querySelector('#receipt-{field.Item1}');Object.getOwnPropertyDescriptor({(select ? "HTMLSelectElement" : "HTMLInputElement")}.prototype,'value').set.call(e,{value});e.dispatchEvent(new Event('{(select ? "change" : "input")}',{{bubbles:true}}));}})()");
                        await Click("Save " + field.Item2); await Click("Verify " + field.Item2);
                        await Ready($"document.querySelector('#receipt-{field.Item1}').closest('.receipt-field').textContent.includes('Human-verified')");
                    }
                });
                await Stage(i, "workbook opening", async () => { await Step(web, 2); await Click("Open workbook in Excel"); await Ready("document.querySelector('[aria-label=\"Excel sheet\"]').options.length===3"); });
                await Stage(i, "context selection", async () =>
                {
                    foreach (var choice in new[] { ("Excel sheet", "Person 1"), ("Excel section", "expenses") })
                        await Script($"(()=>{{const e=document.querySelector('[aria-label=\"{choice.Item1}\"]');e.value='{choice.Item2}';e.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
                });
                await Stage(i, "preview", async () => { await Click("Preview Excel insert"); await Ready("!!document.querySelector('[aria-label=\"Excel insertion preview\"]')"); });
                await Stage(i, "Excel insertion", async () => { await Click("Insert verified row"); await Ready("!!document.querySelector('[aria-label=\"Inserted Excel rows\"]')&&!document.querySelector('.excel-insertion').getAttribute('aria-busy').includes('true')"); });
                await Stage(i, "post-insertion navigation", async () => { await Click("Close receipt and discard review"); await Ready("!document.querySelector('#receipt-itemNo')"); });
            }
            var book = (await host.ListWorkbooksAsync()).Single();
            var read = await host.ReadAsync(book.Id, "Person 1", new(29, 2, 20, 31));
            for (var i = 0; i < 20; i++) { Assert.AreEqual($"LOOP-{i}", read.Values[i, 0]); Assert.AreEqual(123.50, read.Values[i, TsekSheet.Application.Excel.A1Formula.ColumnNumber(LiquidationProfile.Columns[i % 19]) - 2]); }
        }
        finally
        {
            var output = Environment.GetEnvironmentVariable("TSEK_TIMING_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "receipt-loop-timing.json");
            File.WriteAllText(output, JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var book in await host.ListWorkbooksAsync()) await host.CloseAsync(book.Id, discardChanges: true);
            window.Close(); await host.DisposeAsync();
        }
    });
}
