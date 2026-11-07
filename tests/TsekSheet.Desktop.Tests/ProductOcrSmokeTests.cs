using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Wpf;
using TsekSheet.TestFixtures;
namespace TsekSheet.Desktop.Tests;
public sealed partial class DesktopSmokeTests
{
    [TestMethod,TestCategory("WindowsSmoke"),TestCategory("Excel")]
    public Task SyntheticPaddleOcrDraftCanBeCorrectedAndExplicitlyInserted()=>OnDesktop(async()=>
    {
        var config=Environment.GetEnvironmentVariable("TSEK_TEST_OCR_CONFIG");
        if(string.IsNullOrEmpty(config)){Assert.Inconclusive("Local OCR assets not configured.");return;}
        var host=new TsekSheet.Excel.ExcelHost();var path=await LiquidationFixture.CreateAsync(host);
        var folder=Path.Combine(Path.GetDirectoryName(path)!,"ocr-source");TsekSheet.OcrEval.Synthetic.Generate(folder,1);
        var source=Directory.GetFiles(folder,"*clean.png").Single();
        var browser=Path.Combine(Path.GetTempPath(),"TsekSheet-smoke",Guid.NewGuid().ToString("N"));Profiles.Add(browser);
        var window=new MainWindow(null,browser,null,_=>Task.FromResult<string?>(path),workflowProfiles:[LiquidationProfile.Create()],ownedExcelHost:host,
            documentQueueSelector:_=>Task.FromResult<IReadOnlyList<string>>([source]),productOcr:new TsekSheet.OcrEval.ProductOcr(config)){ShowInTaskbar=false,Width=1440,Height=900};
        try
        {
            window.Show();var web=(WebView2)window.FindName("FrontendWebView");
            await Until(async()=>web.CoreWebView2!=null&&await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent")=="\"Connected\"","Host unavailable");
            async Task Click(string text)
            {
                var label=JsonSerializer.Serialize(text);
                await Until(async()=>await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={label}&&!b.disabled)")=="true","Button unavailable: "+text);
                await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={label}).click()");
            }
            await Click("Open workbook");await Click("Add receipts");await Click("Recognize page");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label^=\"Source word \"]') && !!document.querySelector('#queue-amount')?.value")=="true","OCR proposals unavailable");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.querySelector('.receipt-edit-heading').textContent.includes('Unverified drafts')"));
            using(var screenshot=File.Create(Path.Combine(AppContext.BaseDirectory,"T4-ocr-proposals.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            Assert.AreEqual("false",await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Inserted queue rows\"]')"));
            foreach(var field in new[]{("itemNo","OCR-1"),("particulars","Reviewed fictional receipt"),("account","J"),("amount","123.50")})
            {
                var value=JsonSerializer.Serialize(field.Item2);var select=field.Item1=="account";
                await web.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('#queue-{field.Item1}');Object.getOwnPropertyDescriptor({(select?"HTMLSelectElement":"HTMLInputElement")}.prototype,'value').set.call(e,{value});e.dispatchEvent(new Event('{(select?"change":"input")}',{{bubbles:true}}));}})()");
            }

            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Queue sheet\"]').options.length===3")=="true","Sheets unavailable");
            foreach(var choice in new[]{("Queue sheet","Person 1"),("Queue section","expenses")})
                await web.ExecuteScriptAsync($"(()=>{{const e=document.querySelector('[aria-label=\"{choice.Item1}\"]');e.value='{choice.Item2}';e.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
            await Click("Use destination for this receipt and new receipts");await Click("Check row");await Click("Add row to Excel");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Inserted queue rows\"]')")=="true","Explicit insert failed");
            var book=(await host.ListWorkbooksAsync()).Single();var read=await host.ReadAsync(book.Id,"Person 1",new(29,10,1,1));Assert.AreEqual(123.50,read.Values[0,0]);
        }
        finally {foreach(var book in await host.ListWorkbooksAsync())await host.CloseAsync(book.Id,discardChanges:true);window.Close();await host.DisposeAsync();}
    });
}
