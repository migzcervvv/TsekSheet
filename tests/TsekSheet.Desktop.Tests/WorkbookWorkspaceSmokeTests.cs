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
    [DataRow(1), DataRow(3)]
    public Task WorkbookSelectionOpensQueueAndInsertsOnlyIntoSelectedExcelIdentity(int count) => OnDesktop(async () =>
    {
        var host = new TsekSheet.Excel.ExcelHost();
        var path = await LiquidationFixture.CreateAsync(host);
        var otherPath = Path.Combine(Path.GetDirectoryName(path)!, "other-synthetic.xlsx");
        File.Copy(path, otherPath);
        var other = await host.OpenAsync(otherPath);
        var otherBefore = await host.CaptureAsync(other.Id);
        var sources = Enumerable.Range(0,count).Select(i=>Path.Combine(Path.GetDirectoryName(path)!, $"workspace-receipt-{i}.pdf")).ToArray();
        for(var i=0;i<count;i++)File.WriteAllBytes(sources[i],DocumentRenderingTests.Pdf().Concat(System.Text.Encoding.ASCII.GetBytes($"\n% UX02 receipt {i}\n")).ToArray());
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var opens = 0;
        var window = new MainWindow(null, profile, null, _ => { opens++; return Task.FromResult<string?>(path); },
            workflowProfiles: [LiquidationProfile.Create()], ownedExcelHost: host,
            documentQueueSelector: _ => Task.FromResult<IReadOnlyList<string>>(sources))
            { ShowInTaskbar = false, Width = 1366, Height = 768 };
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
            async Task Layout(string mode)
            {
                foreach(var width in new[]{1366d,768d,390d})
                {
                    window.Width=width;await Task.Delay(100);
                    Assert.AreEqual("true",await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"),$"{mode} overflow at {width}");
                    if(mode=="edit")
                    {
                        Assert.AreEqual(width==1366?"true":"false",await web.ExecuteScriptAsync("document.querySelector('.unified-receipts .source-inspector').getBoundingClientRect().right<=document.querySelector('.unified-receipts .receipt-editor').getBoundingClientRect().left"),"Side-by-side desktop / stacked narrow layout");
                        await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop=0");
                    }
                    else await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Queue row preview\"]').scrollIntoView({block:'start'})");
                    using(var screenshot=File.Create(Path.Combine(AppContext.BaseDirectory,$"ux02-{count}-{mode}-{width}.png")))
                        await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,screenshot);
                    await web.ExecuteScriptAsync($"document.querySelector('#main-workspace').focus();Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='{(mode=="edit"?"Check row":"Add row to Excel")}').focus()");
                    Assert.AreEqual("true",await web.ExecuteScriptAsync("(()=>{const e=document.activeElement,r=e.getBoundingClientRect(),m=document.querySelector('#main-workspace').getBoundingClientRect();return r.top>=m.top-1&&r.bottom<=m.bottom+1;})()"),"Focused primary action must remain reachable");
                    using(var action=File.Create(Path.Combine(AppContext.BaseDirectory,$"ux02-{count}-{mode}-{width}-action.png")))
                        await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,action);
                }
                window.Width=1366;
            }
            await Ready("document.querySelector('[role=status]')?.textContent==='Connected'");
            await Click("Open workbook");await Click("Add receipts");
            await Ready("!!document.querySelector('#queue-itemNo')&&document.querySelector('[aria-label=\"Queue sheet\"]')?.options.length===3");
            await Ready("!!document.querySelector('.rendered-source')&&document.querySelector('.rendered-source').complete");
            var selected=(await host.ListWorkbooksAsync()).Single(b=>b.FullPath==path);
            Assert.AreNotEqual(other.Id,selected.Id);Assert.AreEqual(1,opens);
            Assert.AreEqual("false",await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Queue workbook\"]')"),"No second workbook selector");
            await Input("[aria-label=\"Queue sheet\"]","Person 1",true);await Input("[aria-label=\"Queue section\"]","expenses",true);
            await Click("Use destination for this receipt and new receipts");
            for(var i=0;i<count;i++)
            {
                await Input("#queue-itemNo",$"UX02-{i}");await Input("#queue-particulars","Synthetic workspace receipt");
                if(i==0)await Input("#queue-account","J",true);
                else Assert.AreEqual("\"J\"",await web.ExecuteScriptAsync("document.querySelector('#queue-account').value"),"New receipts inherit account as a draft");
                await Input("#queue-amount","123.50");
                if(i==0)
                {
                    await Click("SettingsAppearance & troubleshooting");await Click("Switch workbook…");
                    await Ready("document.querySelector('[role=alert]')?.textContent.includes('switching is blocked')");
                    await web.ExecuteScriptAsync("document.querySelector('nav button[data-activity=receipts]').click()");
                    Assert.AreEqual("\"UX02-0\"",await web.ExecuteScriptAsync("document.querySelector('#queue-itemNo').value"));Assert.AreEqual(1,opens);
                    await Layout("edit");
                }
                if(i==1)
                {
                    var receiptId=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Review receipt\"]').value"))!;
                    await Click("Skip receipt");await Ready("document.querySelector('[aria-label=\"Queue progress\"]').textContent.includes('1 Skipped')");
                    await Input("[aria-label=\"Review receipt\"]",receiptId,true);
                    await Ready("document.querySelector('#queue-itemNo')?.value==='UX02-1'");
                    Assert.AreEqual("\"123.50\"",await web.ExecuteScriptAsync("document.querySelector('#queue-amount').value"));
                }
                await Ready("!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Check row').disabled");
                await Click("Check row");await Ready("!!document.querySelector('[aria-label=\"Queue row preview\"]')");
                Assert.AreEqual("true",await web.ExecuteScriptAsync($"document.querySelector('[aria-label=\"Queue row preview\"]').textContent.includes('{selected.Id}')"));
                Assert.AreNotEqual($"UX02-{i}",(await host.ReadAsync(selected.Id,"Person 1",new(29+i,2,1,1))).Values[0,0],"Check must not write");
                if(i==0)
                {
                    await Click("Back to edit");await Input("#queue-amount","123.51");
                    Assert.AreEqual("false",await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Queue row preview\"]')"));
                    await Click("Check row");await Ready("!!document.querySelector('[aria-label=\"Queue row preview\"]')");await Layout("check");
                }
                Assert.AreEqual("1",await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('.unified-receipts button.primary')).filter(e=>e.getClientRects().length).length"));
                await Click("Add row to Excel");
                await Ready($"document.querySelectorAll('[aria-label=\"Inserted queue rows\"] li').length==={i+1}&&document.querySelector('.unified-receipts').getAttribute('aria-busy')==='false'");
                var row=await host.ReadAsync(selected.Id,"Person 1",new(29+i,2,1,31));
                Assert.AreEqual($"UX02-{i}",row.Values[0,0]);Assert.AreEqual(i==0?123.51:123.50,row.Values[0,8]);
                Assert.IsTrue((await host.ReadAsync(selected.Id,"Person 1",new(29+i,14,1,1))).Formulas[0,0]?.Length>0);
            }
            Assert.AreEqual(1,opens,"Receipt processing must not reopen the workbook");
            Assert.AreEqual(0,PreservationCheck.Compare(otherBefore,await host.CaptureAsync(other.Id),"Person 1",new(1,2,1,1),new object?[,]{{"Synthetic liquidation 0"}}).Count,"Other workbook preserved by Excel readback");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('.receipt-field input,.receipt-field select')).filter(e=>e.getClientRects().length).every(e=>e.disabled)"),"Inserted receipts are read-only");
        }
        finally
        {
            foreach(var book in await host.ListWorkbooksAsync())await host.CloseAsync(book.Id,discardChanges:true);
            window.Close();await host.DisposeAsync();
        }
    },TimeSpan.FromMinutes(2));
}
