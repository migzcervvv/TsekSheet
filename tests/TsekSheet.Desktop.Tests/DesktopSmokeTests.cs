using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Wpf;

namespace TsekSheet.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed partial class DesktopSmokeTests
{
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task ActualSourceRenderingAndPrimaryHoverWorkInBothThemes() => OnDesktop(async () =>
    {
        using var fixture=new TsekSheet.TestFixtures.PayablesFixture();
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");var bytes=DocumentRenderingTests.Pdf();File.WriteAllBytes(path,bytes);
        var profile=Path.Combine(Path.GetTempPath(),"TsekSheet-smoke",Guid.NewGuid().ToString("N"));Profiles.Add(profile);
        var window=new MainWindow(null,profile,null,_=>Task.FromResult<string?>(fixture.Path),documentSelector:_=>Task.FromResult<string?>(path)){ShowInTaskbar=false};
        try
        {
            window.Show();var web=(WebView2)window.FindName("FrontendWebView");
            await Until(async()=>web.CoreWebView2!=null&&await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent")=="\"Connected\"","Host unavailable");
            await web.ExecuteScriptAsync("document.querySelector('.project-select').click()");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('.active-project') && !document.querySelector('select[aria-label=Worksheet]').disabled")=="true","Workbook unavailable");
            foreach(var theme in new[]{"light","dark"})
            {
                await Navigate(web,"settings");
                await web.ExecuteScriptAsync($"(()=>{{const s=document.querySelector('[aria-label=Appearance]');s.value='{theme}';s.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
                await Navigate(web,"overview");
                var position=await web.ExecuteScriptAsync("(()=>{const b=Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Open workbook');b.scrollIntoView({block:'center'});const r=b.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2};})()");
                var coordinates=JsonSerializer.Deserialize<JsonElement>(position);
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseMoved",x=coordinates.GetProperty("x").GetDouble(),y=coordinates.GetProperty("y").GetDouble()}));
                Assert.AreEqual("true",await web.ExecuteScriptAsync("(()=>{const b=Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Open workbook'),s=getComputedStyle(b);const l=c=>c.match(/[\\d.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4).reduce((a,v,i)=>a+v*[.2126,.7152,.0722][i],0);const a=l(s.color),z=l(s.backgroundColor);return b.matches(':hover')&&(Math.max(a,z)+.05)/(Math.min(a,z)+.05)>=4.5;})()"),"Primary hover contrast failed in "+theme);
                using var hover=File.Create(Path.Combine(AppContext.BaseDirectory,$"primary-hover-{theme}.png"));
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,hover);
            }
            await Navigate(web,"receipts");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Select receipt PDF or image').click()");
            await Until(async()=>await web.ExecuteScriptAsync("(()=>{const i=document.querySelector('.rendered-source');return !!i&&i.complete&&i.naturalWidth===800;})()")=="true","Actual PDF page did not render");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Next source page').click()");
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('.rendered-source')?.alt==='Rendered source page 2' && document.querySelector('.rendered-source').complete")=="true","PDF navigation failed");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Local source preview\"]').textContent.includes('Page 2 of 2') && !document.body.textContent.includes('Human-verified manual entry') && document.querySelector('.rendered-source').src.startsWith('blob:')"));
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Enlarge source page').click()");
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('.rendered-source').getBoundingClientRect().width>400")=="true","Enlarged page not readable");
            using(var rendered=File.Create(Path.Combine(AppContext.BaseDirectory,"actual-source-pdf.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,rendered);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        finally{window.Close();File.Delete(path);}
    });
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task OverflowingTaskCanvasesKeepFinalActionsReachable() => OnDesktop(async () =>
    {
        using var fixture = new TsekSheet.TestFixtures.PayablesFixture();
        var source=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");
        File.WriteAllText(source,"%PDF-1.7\n% synthetic scroll test only\n");
        var profile=Path.Combine(Path.GetTempPath(),"TsekSheet-smoke",Guid.NewGuid().ToString("N"));Profiles.Add(profile);
        var window=new MainWindow(null,profile,null,_=>Task.FromResult<string?>(fixture.Path),documentSelector:_=>Task.FromResult<string?>(source)){ShowInTaskbar=false};
        try
        {
            window.Show();var web=(WebView2)window.FindName("FrontendWebView");
            await Until(async()=>web.CoreWebView2!=null&&await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent")=="\"Connected\"","Host unavailable");
            await web.ExecuteScriptAsync("document.querySelector('.project-select').click()");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('.active-project') && !document.querySelector('select[aria-label=Worksheet]').disabled")=="true","Workbook unavailable");
            await Navigate(web,"receipts");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Select receipt PDF or image').click()");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Receipt profile\"]')")=="true","Source unavailable");
            await Until(async()=>await web.ExecuteScriptAsync("!Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Start manual receipt review').disabled")=="true","Start unavailable");
            await web.ExecuteScriptAsync("(()=>{const p=document.querySelector('[aria-label=\"Receipt profile\"]');p.value='synthetic-payables';p.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Start manual receipt review').click()");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('#receipt-vendor') && !document.querySelector('#receipt-vendor').disabled")=="true","Review unavailable");
            foreach(var size in new[]{(1440d,900d),(1366d,768d),(1280d,720d),(1024d,768d),(390d,768d),(1280d,600d)})
            foreach(var zoom in new[]{1d,1.25d})
            {
                window.Width=size.Item1;window.Height=size.Item2;web.ZoomFactor=zoom;
                foreach(var task in new[]{"overview","receipts","workbook","diagnostics"})
                {
                    await Navigate(web,task);
                    await web.ExecuteScriptAsync("document.querySelectorAll('#main-workspace details').forEach(d=>{if(d.getClientRects().length)d.open=true;})");
                    var result=await web.ExecuteScriptAsync("(()=>{const m=document.querySelector('#main-workspace');m.scrollTop=m.scrollHeight;const a=Array.from(m.querySelectorAll('button,summary,input,select')).filter(e=>e.getClientRects().length&&!e.disabled).at(-1);a?.focus();const r=a?.getBoundingClientRect(),v=m.getBoundingClientRect();return getComputedStyle(m).overflowY==='auto'&&(!r||(r.top>=v.top-1&&r.bottom<=v.bottom+1))&&document.documentElement.scrollWidth<=document.documentElement.clientWidth;})()");
                    Assert.AreEqual("true",result,$"Last action clipped: {task} {size} zoom {zoom}");
                }
            }
            web.ZoomFactor=1;window.Width=1280;window.Height=600;
            await Navigate(web,"receipts");
            await web.ExecuteScriptAsync("const m=document.querySelector('#main-workspace');m.scrollTop=0;m.focus()");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"PageDown\",\"code\":\"PageDown\",\"windowsVirtualKeyCode\":34}");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"PageDown\",\"code\":\"PageDown\",\"windowsVirtualKeyCode\":34}");
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop>0")=="true","PageDown did not scroll canvas");
            await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop=0");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent","{\"type\":\"mouseWheel\",\"x\":900,\"y\":250,\"deltaY\":430,\"deltaX\":0}");
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop>0")=="true","Wheel did not scroll canvas");
            await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop=0");
            var bounds=JsonSerializer.Deserialize<JsonElement>(await web.ExecuteScriptAsync("(()=>{const r=document.querySelector('#main-workspace').getBoundingClientRect();return {x:r.right-6,top:r.top+25,bottom:r.bottom-15};})()"));
            var x=bounds.GetProperty("x").GetDouble();var top=bounds.GetProperty("top").GetDouble();var bottom=bounds.GetProperty("bottom").GetDouble();
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mousePressed",x,y=top,button="left",buttons=1,clickCount=1}));
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseMoved",x,y=bottom,button="left",buttons=1}));
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseReleased",x,y=bottom,button="left",buttons=0,clickCount=1}));
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop>0")=="true","Scrollbar drag did not scroll canvas");
            await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop=0;document.querySelector('#main-workspace').focus()");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"End\",\"code\":\"End\",\"windowsVirtualKeyCode\":35}");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"End\",\"code\":\"End\",\"windowsVirtualKeyCode\":35}");
            await Until(async()=>await web.ExecuteScriptAsync("(()=>{const m=document.querySelector('#main-workspace');return m.scrollTop+m.clientHeight>=m.scrollHeight-2;})()")=="true","End key did not reach canvas bottom");
            using var screenshot=File.Create(Path.Combine(AppContext.BaseDirectory,"scroll-1280-600.png"));
            await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,screenshot);
        }
        finally{window.Close();File.Delete(source);}
    });
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task ReceiptManualReviewUsesActualHostedBridgeAndReadOnlyPlacement() => OnDesktop(async () =>
    {
        using var fixture = new TsekSheet.TestFixtures.PayablesFixture();
        var source = Path.Combine(Path.GetTempPath(), "Synthetic-reimbursement-supporting-document-long-source-filename-for-laptop-layout-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(source, "%PDF-1.7\n% artificial registration input; no rendered contents\n");
        string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        var selectedWorkbook=Path.Combine(Path.GetTempPath(),"Synthetic-reimbursement-workbook-with-a-long-project-name-for-laptop-layout-"+Guid.NewGuid().ToString("N")+".xlsx");
        File.Copy(fixture.Path,selectedWorkbook);
        var sourceHash = Hash(source); var workbookHash = Hash(fixture.Path);
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var window = new MainWindow(null, profile, null, _ => Task.FromResult<string?>(selectedWorkbook), documentSelector: _ => Task.FromResult<string?>(source)) { ShowInTaskbar = false };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            async Task Click(string label)
            {
                await Until(async () => await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={JsonSerializer.Serialize(label)}&&!b.disabled)") == "true", "Button unavailable: " + label);
                await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={JsonSerializer.Serialize(label)}).click()");
            }
            async Task Input(string key, string value)
            {
                await Navigate(web, "receipts");
                await Step(web,1);
                await Until(async () => await web.ExecuteScriptAsync($"!!document.querySelector('#receipt-{key}') && !document.querySelector('#receipt-{key}').disabled") == "true", "Field unavailable: " + key);
                await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('#receipt-{key}');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,{JsonSerializer.Serialize(value)});el.dispatchEvent(new Event('input',{{bubbles:true}}));}})()");
            }
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host did not connect");
            await Navigate(web, "workbook"); await Click("Open workbook"); await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('.active-project')")=="true","Project missing"); await Navigate(web, "receipts");
            await Click("Select receipt PDF or image");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Receipt profile\"]')") == "true", "Receipt profile missing");
            await web.ExecuteScriptAsync("(()=>{const el=document.querySelector('[aria-label=\"Receipt profile\"]');el.value='synthetic-payables';el.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Click("Start manual receipt review");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('#receipt-vendor')") == "true", "Manual fields missing");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Receipt review\"]').textContent.includes('Offline OCR is unavailable') && !document.querySelector('[aria-label=\"Receipt review\"]').textContent.includes('Human-verified manual entry')"));
            await Input("vendor", "Acme Trading Incorporated"); await Click("Save Vendor name"); await Click("Verify Vendor name");
            await Input("rfp", "R3"); await Click("Save RFP number"); await Click("Verify RFP number");
            await Input("amount", "125.50"); await Click("Save Amount"); await Click("Verify Amount");
            await Navigate(web, "receipts");
            await Step(web,2);
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('select[aria-label=Worksheet]') && !document.querySelector('.reader').hidden") == "true", "Destination context missing");
            await Navigate(web, "placement"); await Click("Preview verified receipt placement");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Receipt placement preview\"]')") == "true", "Verified preview missing");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Receipt placement preview\"]').textContent.includes('Potential duplicate at row 8')"));
            await web.ExecuteScriptAsync("(()=>{const el=document.querySelector('[aria-label=\"Receipt destination section\"]');el.value='external';el.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Navigate(web, "placement"); await Click("Preview verified receipt placement");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Receipt placement preview\"]')?.textContent.includes('Proposed row: 18')") == "true", "Section placement missing");
            foreach (var width in new[] { 390d, 768d, 1100d })
            {
                window.Width = width; await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Receipt overflow at " + width);
                await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Receipt review\"]').scrollIntoView({block:'start'})");
                using var image = File.Create(Path.Combine(AppContext.BaseDirectory, $"receipt-review-{width}.png"));
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, image);
            }
            await Navigate(web, "receipts");
            await web.ExecuteScriptAsync("document.querySelector('#receipt-vendor').focus()");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyDown\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.activeElement.tagName==='BUTTON' && getComputedStyle(document.activeElement).outlineStyle==='solid'"), "Keyboard focus ring missing");
            foreach (var theme in new[] { "light", "dark" })
            {
                await Navigate(web,"diagnostics");
                await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('[aria-label=Appearance]');el.value='{theme}';el.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
                Assert.AreEqual(JsonSerializer.Serialize(theme), await web.ExecuteScriptAsync("getComputedStyle(document.querySelector('.shell')).colorScheme"));
                await Navigate(web,"receipts");
                await Step(web,1);
                foreach (var width in new[] { 390d, 1024d, 1280d, 1366d, 1440d })
                {
                    window.Width=width; window.Height=width==1440?900:width==1280?720:768; await Task.Delay(100);
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"));
                    await web.ExecuteScriptAsync("document.querySelector('#main-workspace').scrollTop=0");
                    using var screenshot=File.Create(Path.Combine(AppContext.BaseDirectory, $"redesign-receipts-{theme}-{width}.png"));
                    await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,screenshot);
                    if (width == 390)
                    {
                        await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Toggle navigation\"]').focus();document.querySelector('[aria-label=\"Toggle navigation\"]').click()");
                        Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('.sidebar').getAttribute('data-open')==='true' && getComputedStyle(document.querySelector('.sidebar')).display!=='none'"));
                        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyDown\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
                        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
                        await Until(async () => await web.ExecuteScriptAsync("document.querySelector('.sidebar').getAttribute('data-open')==='false' && document.activeElement.getAttribute('aria-label')==='Toggle navigation'") == "true", "Drawer Escape/focus recovery failed");
                    }
                }
            }
            await Navigate(web, "workbook");
            foreach(var size in new[]{(1366d,768d),(1280d,720d),(1440d,900d),(1024d,768d),(390d,768d)})
            {
                window.Width=size.Item1;window.Height=size.Item2;await Task.Delay(100);
                Assert.AreEqual("true",await web.ExecuteScriptAsync("(()=>{const g=document.querySelector('[aria-label=\"Worksheet viewport\"]').getBoundingClientRect(),b=document.querySelector('.worksheet-body').getBoundingClientRect();return document.documentElement.scrollWidth<=document.documentElement.clientWidth && g.height>=120 && g.bottom<=b.bottom+1;})()"),"Laptop worksheet layout failed at "+size+await web.ExecuteScriptAsync("JSON.stringify({page:document.documentElement.scrollWidth,client:document.documentElement.clientWidth,height:document.querySelector('[aria-label=\"Worksheet viewport\"]').getBoundingClientRect().height})"));
                using var shot=File.Create(Path.Combine(AppContext.BaseDirectory,$"workbook-layout-{size.Item1}.png"));
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,shot);
            }
            window.Width=1366;window.Height=768;web.ZoomFactor=1.25;await Task.Delay(100);
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"),"125% WebView2 zoom overflow");
            using(var scaled=File.Create(Path.Combine(AppContext.BaseDirectory,"workbook-layout-zoom125.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,scaled);
            web.ZoomFactor=1;
            using(var workbookImage=File.Create(Path.Combine(AppContext.BaseDirectory,"redesign-workbook-dark.png"))) await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,workbookImage);
            await Input("amount", "126.50");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Receipt placement preview\"]') && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Verify Amount').disabled"));
            await Navigate(web, "workbook");
            await web.ExecuteScriptAsync("document.querySelector('.project-select').click()");
            await Until(async()=>await web.ExecuteScriptAsync("document.body.textContent.includes('Close the current receipt before switching workbooks')")=="true","Receipt project guard failed");
            await Navigate(web, "receipts");
            Assert.AreEqual("\"126.50\"", await web.ExecuteScriptAsync("document.querySelector('#receipt-amount').value"), "Task switch lost unsaved draft");
            await Navigate(web,"receipts"); await Step(web,1);
            await Click("Save Amount");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('#receipt-amount').closest('.receipt-field').textContent.includes('Corrected — not verified')") == "true", "Correction retained verification");
            await Click("Verify Amount");
            await Until(async () => await web.ExecuteScriptAsync("!document.querySelector('#receipt-amount').disabled && document.querySelector('#receipt-amount').closest('.receipt-field').textContent.includes('Human-verified manual entry')") == "true", "Verification did not finish");
            Assert.AreEqual(sourceHash, Hash(source)); Assert.AreEqual(workbookHash, Hash(fixture.Path));
            File.AppendAllText(source, "% deliberate artificial source change\n");
            await Navigate(web, "placement"); await Click("Preview verified receipt placement");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Receipt review\"]').textContent.includes('Refresh receipt state before continuing')") == "true", "Source invalidation not surfaced");
            await Click("Refresh receipt state");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Receipt review\"]').textContent.includes('Source changed or became unavailable')") == "true", "Invalidated review not displayed");
            await Click("Close receipt and discard review");
            await Until(async () => await web.ExecuteScriptAsync("!document.querySelector('#receipt-vendor')") == "true", "Receipt cleanup failed");
            Assert.AreEqual(workbookHash, Hash(fixture.Path)); Assert.AreEqual(workbookHash,Hash(selectedWorkbook));
        }
        finally { window.Close(); File.Delete(source); File.Delete(selectedWorkbook); }
    });

    [TestMethod]
    public void NativeSectionProfileReaderIsLocalBoundedAndStrict()
    {
        var folder=Path.Combine(Path.GetTempPath(),"TsekSheet-profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"artificial.json");
        try
        {
            var text=TsekSheet.TestFixtures.SectionReviewFixture.ProfileJson;File.WriteAllText(path,text);Assert.AreEqual(text,MainWindow.ReadSectionProfile(path));
            File.WriteAllText(path,text[..^1]+",\"approved\":true}");Assert.ThrowsExactly<TsekSheet.Application.Workbooks.WorkbookException>(()=>MainWindow.ReadSectionProfile(path));
            File.WriteAllText(path,new string(' ',65537));Assert.ThrowsExactly<TsekSheet.Application.Workbooks.WorkbookException>(()=>MainWindow.ReadSectionProfile(path));
            Assert.ThrowsExactly<TsekSheet.Application.Workbooks.WorkbookException>(()=>MainWindow.ReadSectionProfile(@"\\server\share\profile.json"));
        }
        finally{File.Delete(path);Directory.Delete(folder);}
    }
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task SectionReviewUsesActualReactBridgeSupportsNavigationRetractionAndCleanup() => OnDesktop(async () =>
    {
        using var fixture = new TsekSheet.TestFixtures.SectionReviewFixture();
        string Hash()=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path)));var hash=Hash();
        var profile=Path.Combine(Path.GetTempPath(),"TsekSheet-smoke",Guid.NewGuid().ToString("N"));Profiles.Add(profile);
        bool allowClose=false;int closeWarnings=0;
        var window=new MainWindow(null,profile,null,_=>Task.FromResult<string?>(fixture.Path),confirmDiscard:()=>{closeWarnings++;return allowClose;},sectionProfileSelector:_=>Task.FromResult<string?>(TsekSheet.TestFixtures.SectionReviewFixture.ProfileJson)){ShowInTaskbar=false};
        try
        {
            window.Show();var web=(WebView2)window.FindName("FrontendWebView");
            async Task Click(string label){if(label=="Select profile and start review")await web.ExecuteScriptAsync("document.querySelector('.worksheet-tools').open=true");await Until(async()=>await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={JsonSerializer.Serialize(label)}&&!b.disabled)")=="true","Button unavailable: "+label);_=await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={JsonSerializer.Serialize(label)}).click()");}
            async Task Input(string label,string value)=>_=await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('[aria-label={JsonSerializer.Serialize(label)}]');Object.getOwnPropertyDescriptor(el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(el,{JsonSerializer.Serialize(value)});el.dispatchEvent(new Event('input',{{bubbles:true}}));}})()");
            async Task Select(string label,string value)=>_=await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('select[aria-label={JsonSerializer.Serialize(label)}]');el.value={JsonSerializer.Serialize(value)};el.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
            await Until(async()=>web.CoreWebView2!=null&&await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent")=="\"Connected\"","Host did not connect");
            await Navigate(web, "workbook"); await Click("Inspect workbook read-only");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Read-only section review\"]') && !!document.querySelector('button[aria-label=\"Inspect C1\"]') && !document.querySelector('button[aria-label=\"Inspect C1\"]').disabled")=="true","Review entry missing");
            await Input("Go To Cell","C25");await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Go To Cell\"]').form.requestSubmit()");
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]')?.textContent.includes('Inspect C25')")=="true","Direct cell navigation failed");
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Worksheet viewport\"]').dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}))");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').textContent.includes('Inspect C26')"));
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Worksheet viewport\"]').scrollTo(0,0)");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('button[aria-label=\"Inspect A1\"]')&&!document.querySelector('button[aria-label=\"Inspect A1\"]').disabled")=="true","Scrollbar navigation failed");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("(()=>{const v=document.querySelector('[aria-label=\"Worksheet viewport\"]');return getComputedStyle(v).overflowX==='scroll'&&getComputedStyle(v).overflowY==='scroll'&&v.scrollHeight>v.clientHeight;})()"));
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Worksheet viewport\"]').scrollIntoView({block:'start'})");
            using(var navigationScreenshot=File.Create(Path.Combine(AppContext.BaseDirectory,"step56-navigation.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,navigationScreenshot);
            await Click("Select profile and start review");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Identity case\"]')")=="true","Review profile did not load");
            await Click("Missing Vendors");await Click("Review identity row 5");await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]')?.textContent.includes('Inspect A5')")=="true","Row navigation failed");
            await Input("Identity evidence row","4");await Select("Evidence column","3");await Input("Review reason","Artificial vendor evidence checked");await Click("Confirm read-only assertion");
            await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Identity case\"]').textContent.includes('Reviewer-confirmed identity')")=="true","Supplemental identity not displayed");
            await Click("Review History");await Click("Retract assertion row 5 identity");await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Identity case\"]').textContent.includes('Missing literal vendor identity')")=="true","Retraction did not restore missing identity");
            await Click("Sections & Subtotals");await Click("Boundary north row 20");await Until(async()=>await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Review reason\"]').disabled")=="true","Navigation remained busy");await Input("Review reason","Artificial final boundary reviewed");await Click("Confirm read-only assertion");
            await Until(async()=>await web.ExecuteScriptAsync("document.body.textContent.includes('end: 20')")=="true","Boundary assertion not reflected");
            await Click("Potential Duplicates");await Click("Inspect duplicate row 4 · section tools");await Until(async()=>await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Review reason\"]').disabled")=="true","Duplicate navigation remained busy");await Input("Review reason","Warning inspected without uniqueness assertion");await Click("Confirm read-only assertion");
            await Until(async()=>await web.ExecuteScriptAsync("document.body.textContent.includes('Warning reviewed; records retained')")=="true","Duplicate acknowledgment missing");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("!Array.from(document.querySelectorAll('button')).some(b=>['Save As','Create working copy'].includes(b.textContent)) && !document.querySelector('.cell-editor input') && document.body.textContent.includes('Structural insertion NOT authorized')"));
            foreach(var width in new[]{390d,768d,1100d}){window.Width=width;await Task.Delay(100);Assert.AreEqual("true",await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"),"Review overflow at "+width);}
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only section review\"]').scrollIntoView({block:'start'})");
            using(var screenshot=File.Create(Path.Combine(AppContext.BaseDirectory,"step56-section-review.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            window.Close();Assert.IsTrue(window.IsVisible);Assert.AreEqual(1,closeWarnings,"Native window close must preserve decisions by default");
            await Click("Close section review");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[role=alertdialog]')")=="true","Discard confirmation missing");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.querySelector('[role=alertdialog]').textContent.includes('2 in-memory review decisions') && document.activeElement.textContent==='Continue Reviewing'"));
            await Click("Continue Reviewing");Assert.AreEqual("true",await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Identity case\"]')"));
            await Click("Select profile and start review");await Click("Continue Reviewing");
            await Navigate(web, "workbook"); await Click("Inspect workbook read-only");await Click("Continue Reviewing");
            await Click("Close workbook");await Click("Continue Reviewing");
            var otherSheet=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.querySelector('select[aria-label=Worksheet]').options[1].value"))!;
            await Select("Worksheet",otherSheet);await Click("Continue Reviewing");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Identity case\"]')"));
            await Click("Close section review");await Click("Discard & Close");await Until(async()=>await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Identity case\"]')")=="true","Review cleanup failed");
            await Click("Select profile and start review");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Identity case\"]') && document.body.textContent.includes('review revision 0')")=="true","Fresh review retained assertions");
            await Click("Unassigned Rows");await Input("Review reason","Artificial investigation before sheet switch");await Click("Confirm read-only assertion");
            await Until(async()=>await web.ExecuteScriptAsync("document.body.textContent.includes('review revision 1')")=="true","Investigation not recorded");
            await Select("Worksheet",otherSheet);await Click("Discard & Close");
            await Until(async()=>await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Identity case\"]')")=="true","Sheet switch retained private review");
        }
        finally{allowClose=true;window.Close();}Assert.AreEqual(hash,Hash());
    });
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task WorkbookInspectionKeepsBrandingAndHasNoLegacyWriteUi() => OnDesktop(async () =>
    {
        using var fixture = new TsekSheet.TestFixtures.PayablesFixture();
        string Hash() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path)));
        var hash = Hash();
        var browserProfile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(browserProfile);
        // Actual WPF/WebView2/React/C# pipeline; the native picker is deliberately replaced by a selector seam.
        var window = new MainWindow(null, browserProfile, null, _ => Task.FromResult<string?>(fixture.Path)) { ShowInTaskbar = false };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            async Task Click(string label)
            {
                await Until(async () => await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={JsonSerializer.Serialize(label)}&&!b.disabled)") == "true", "Button unavailable: " + label);
                _ = await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={JsonSerializer.Serialize(label)}).click()");
            }
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host did not connect.");
            await Until(async () => await web.ExecuteScriptAsync("(()=>{const img=document.querySelector('header .brand-logo');return !!img && img.complete && img.naturalWidth>0 && new URL(img.src).origin==='https://tseksheet.local';})()") == "true", "Bundled header logo did not load.");
            await web.ExecuteScriptAsync("window.brandAssetsLoaded=null;Promise.all(['TsekSheet.svg','TsekSheet-No-BG.svg'].map(name=>new Promise(resolve=>{const img=new Image();img.onload=()=>resolve(true);img.onerror=()=>resolve(false);img.src='./branding/'+name;}))).then(results=>window.brandAssetsLoaded=results.every(Boolean))");
            await Until(async () => await web.ExecuteScriptAsync("window.brandAssetsLoaded") == "true", "Bundled brand assets did not load.");
            using (var brandingCapture = File.Create(Path.Combine(AppContext.BaseDirectory, "branding-header.png")))
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, brandingCapture);
            await Navigate(web, "workbook"); await Click("Open workbook");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('.cell-window td')") == "true", "Read-only workbook did not render.");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("!document.querySelector('#payables-title') && !Array.from(document.querySelectorAll('button')).some(b=>['Create working copy','Save As','Review Merge'].includes(b.textContent))"));
            foreach (var width in new[] { 1366d, 390d })
            {
                window.Width = width; await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Inspection overflow at " + width);
            }
            Assert.AreEqual(hash, Hash(), "Inspection altered synthetic source bytes.");
        }
        finally { window.Close(); }
        Assert.AreEqual(hash, Hash());
    });

    private static readonly List<string> Profiles = [];
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task InspectionDatesSharedFormulasAndDistantNavigationStayReadOnly() => OnDesktop(async () =>
    {
        using var fixture = new TsekSheet.TestFixtures.InspectionFixture();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path)));
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var window = new MainWindow(null, profile, null, _ => Task.FromResult<string?>(fixture.Path)) { ShowInTaskbar = false };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            async Task Click(string label) => _ = await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={JsonSerializer.Serialize(label)}).click()");
            async Task Cell(string address) => _ = await web.ExecuteScriptAsync($"document.querySelector('button[aria-label=\"Inspect {address}\"]').click()");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host not connected.");
            await Navigate(web, "workbook"); await Click("Inspect workbook read-only");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('button[aria-label=\"Inspect A1\"]')?.textContent") == "\"1900-03-01\"", "Date display missing.");
            await Cell("A1"); Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').textContent.includes('Raw value: 61')"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').textContent.includes('Source format ID: 14') && document.querySelector('[aria-label=\"Read-only cell details\"]').textContent.includes('Display classification: Qualified display subset')"));
            await Cell("B2"); Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').textContent.includes('A2+$A$1') && document.body.textContent.includes('Stored cached value (may be stale): 123')"));
            await Cell("C1"); Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').textContent.includes('Unsupported')"));
            await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=Worksheet]');s.value=s.options[1].value;s.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('caption')?.textContent.includes('Hidden context') && !document.querySelector('input[aria-label=\"Inspection start row\"]').disabled") == "true", "Hidden sheet not ready.");
            await web.ExecuteScriptAsync("(()=>{const el=document.querySelector('input[aria-label=\"Inspection start row\"]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,'900000');el.dispatchEvent(new Event('input',{bubbles:true}));})()");
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('button[aria-label=\"Inspect A900000\"]')?.textContent") == "\"1900-03-01\"", "Distant row missing.");
            await Cell("A900000");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("!Array.from(document.querySelectorAll('button')).some(b=>['Save As','Create working copy'].includes(b.textContent)) && !document.querySelector('#payables-title') && !!document.querySelector('.section-review') && document.querySelector('header img').naturalWidth>0 && document.body.textContent.includes('Read-only inspection')"));
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').scrollIntoView({block:'start'})");
            using (var capture = File.Create(Path.Combine(AppContext.BaseDirectory, "step47-interpretation.png"))) await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, capture);
            await Click("Close workbook"); await Until(async () => await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Worksheet viewport\"]')") == "true", "Close failed.");
            await Navigate(web, "workbook"); await Click("Inspect workbook read-only"); await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('button[aria-label=\"Inspect A1\"]')") == "true", "Reopen failed.");
        }
        finally { window.Close(); }
        Assert.AreEqual(hash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path))));
    });
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task ReadOnlyCompatibilityReportLoadsAndCannotCreateCopies() => OnDesktop(async () =>
    {
        using var fixture = new TsekSheet.TestFixtures.PayablesFixture();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path)));
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var window = new MainWindow(null, profile, null, _ => Task.FromResult<string?>(fixture.Path)) { ShowInTaskbar = false };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host not connected.");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Inspect workbook read-only').click()");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Selected worksheet diagnostics\"]')") == "true", "Inspection viewport did not load.");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.body.textContent.includes('Compatibility Report') && !Array.from(document.querySelectorAll('button')).some(b=>['Save As','Create working copy'].includes(b.textContent))"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("!document.querySelector('#payables-title') && !!document.querySelector('.section-review') && document.querySelector('header img').naturalWidth>0"));
            foreach (var width in new[] { 390d, 768d, 1100d }) { window.Width = width; await Task.Delay(100); Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth")); }
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('summary')).find(s=>s.textContent.startsWith('Compatibility Report')).scrollIntoView({block:'start'})");
            using var capture = File.Create(Path.Combine(AppContext.BaseDirectory, "step46-compatibility.png"));
            await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, capture);
        }
        finally { window.Close(); }
        Assert.AreEqual(hash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path)))) ;
    });
    private static async Task Step(WebView2 web, int step)
    {
        await Until(async()=>await web.ExecuteScriptAsync($"!!document.querySelector('[data-step=\"{step}\"]') && !document.querySelector('[data-step=\"{step}\"]').disabled")=="true","Receipt step unavailable");
        await web.ExecuteScriptAsync($"document.querySelector('[data-step=\"{step}\"]').click()");
        await Until(async()=>await web.ExecuteScriptAsync($"document.querySelector('[data-step=\"{step}\"]').getAttribute('aria-current')==='step'")=="true","Receipt step did not change");
    }
    private static async Task Navigate(WebView2 web, string activity)
    {
        var target = activity switch { "workbook"=>"worksheets", "placement"=>"receipts", "diagnostics"=>"settings", _=>activity };
        await web.ExecuteScriptAsync($"document.querySelector('nav button[data-activity={target}]').click()");
        await Until(async () => await web.ExecuteScriptAsync($"document.querySelector('nav button[data-activity={target}]').getAttribute('aria-current')==='page'") == "true", "Task navigation failed");
        if(target=="receipts" && activity!="placement" && await web.ExecuteScriptAsync("!!document.querySelector('#receipt-vendor')")=="true") await Step(web,1);
        if(target=="settings") await web.ExecuteScriptAsync("document.querySelector('.troubleshooting').open=true");
        if(activity=="placement") await Step(web,3);
    }
    private static async Task OnDesktop(Func<Task> test)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async () =>
            {
                try { await test(); finished.TrySetResult(); }
                catch (Exception ex) { finished.TrySetException(ex); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { await finished.Task.WaitAsync(TimeSpan.FromSeconds(60)); }
        finally
        {
            thread.Join(TimeSpan.FromSeconds(2));
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TsekSheet-smoke")) + Path.DirectorySeparatorChar;
            foreach (var profile in Profiles.ToArray())
            {
                var fullPath = Path.GetFullPath(profile);
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(fullPath), "N", out _)) continue;
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        if (Directory.Exists(fullPath) && new DirectoryInfo(fullPath).LinkTarget == null) Directory.Delete(fullPath, true);
                        break;
                    }
                    catch (IOException) { await Task.Delay(100); }
                    catch (UnauthorizedAccessException) { await Task.Delay(100); }
                }
            }
            Profiles.Clear();
        }
    }

    private static MainWindow Window(string? assets = null, string? browser = null)
    {
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N"));
        Profiles.Add(profile);
        return new MainWindow(assets, profile, browser) { ShowInTaskbar = false };
    }

    private static async Task Until(Func<Task<bool>> predicate, string failure)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline) { if (await predicate()) return; await Task.Delay(50); }
        Assert.Fail(failure);
    }

    [TestMethod, TestCategory("WindowsSmoke")]
    public Task ReactLoadsAndActualNativeMessagingProgressAndCancellationWork() => OnDesktop(async () =>
    {
        var window = Window();
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "React did not connect to the actual C# host.");
            Assert.AreEqual("https://tseksheet.local/index.html", web.Source.AbsoluteUri);
            var title = await web.ExecuteScriptAsync("document.querySelector('h1').textContent");
            Assert.AreEqual("Workbook overview", JsonSerializer.Deserialize<string>(title));
            using (var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "desktop-smoke.png")))
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
            foreach (var width in new[] { 390d, 768d, 1100d })
            {
                window.Width = width;
                await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Horizontal overflow at width " + width);
            }
            await Navigate(web, "diagnostics");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Run diagnostic').click()");
            await Until(async () => await web.ExecuteScriptAsync("Number(document.querySelector('progress').value)>0") == "true", "Native progress did not reach React.");
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Cancel').click()");
            await Until(async () => await web.ExecuteScriptAsync("document.body.textContent.includes('Cancelled')") == "true", "Native cancellation did not reach React.");
            await web.ExecuteScriptAsync("window.chrome.webview.postMessage({protocolVersion:1,requestId:'smoke-unknown',command:'workbook.save',payload:{}})");
            // Foreign resources must be blocked by CSP before leaving the local application.
            await web.ExecuteScriptAsync("window.resourceProbe='pending'; fetch('https://example.com/').then(()=>window.resourceProbe='unexpected').catch(()=>window.resourceProbe='blocked')");
            await Until(async () => await web.ExecuteScriptAsync("window.resourceProbe") == "\"blocked\"", "Remote request was not blocked.");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.emulateNetworkConditions", "{\"offline\":true,\"latency\":0,\"downloadThroughput\":0,\"uploadThroughput\":0}");
            web.Reload();
            await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Offline reload did not create a new connection.");
        }
        finally { window.Close(); }
    });


    [TestMethod, TestCategory("WindowsSmoke")]
    public Task MissingAssetsProduceNativeErrorWithoutBrowserInitialization() => OnDesktop(async () =>
    {
        var window = Window(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            window.Show();
            await Until(() => Task.FromResult(((TextBlock)window.FindName("HostStatus")).Text.Contains("assets are missing")), "Missing-asset error was not presented.");
            Assert.IsNull(((WebView2)window.FindName("FrontendWebView")).CoreWebView2);
        }
        finally { window.Close(); }
    });

    [TestMethod, TestCategory("WindowsSmoke")]
    public void IncompleteDistributionIsRejectedBeforeInitialization()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TsekSheet-missing-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "index.html"), "<script src=\"./assets/missing.js\"></script>");
            Assert.ThrowsExactly<FileNotFoundException>(() => MainWindow.ValidateAssets(directory));
        }
        finally
        {
            // Only files created by this test, without recursive path traversal.
            File.Delete(Path.Combine(directory, "index.html"));
            Directory.Delete(directory);
        }
    }

    [TestMethod, TestCategory("WindowsSmoke")]
    public Task UnavailableRuntimeProducesNativeError() => OnDesktop(async () =>
    {
        var window = Window(browser: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        try
        {
            window.Show();
            await Until(() => Task.FromResult(((TextBlock)window.FindName("HostStatus")).Text.Contains("Runtime is missing")), "Runtime failure was not presented.");
        }
        finally { window.Close(); }
    });

    [TestMethod, TestCategory("WindowsSmoke")]
    public Task RendererFailureProducesNativeError() => OnDesktop(async () =>
    {
        var window = Window();
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host did not connect before renderer fault test.");
            // Deliberately terminate only this isolated test renderer, never a user's browser.
            _ = web.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash", "{}").ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            await Until(() => Task.FromResult(((TextBlock)window.FindName("HostStatus")).Text.Contains("embedded browser stopped")), "Renderer failure was not presented.");
        }
        finally { window.Close(); }
    });

    [TestMethod, TestCategory("WindowsSmoke")]
    public Task ClosingDuringInitializationDoesNotLeaveAWindow() => OnDesktop(async () =>
    {
        var window = Window(); window.Show(); window.Close(); await Task.Delay(250);
        Assert.IsFalse(window.IsVisible);
    });

    [TestMethod, TestCategory("WindowsSmoke")]
    public Task ReadOnlyWorkbookViewerOpensBothFixturesSwitchesSheetsAndPreservesSources() => OnDesktop(async () =>
    {
        var paths = new[] { "reimbursement", "laboratory" }.Select(n => Path.Combine(AppContext.BaseDirectory, "Fixtures", n + "-synthetic.xlsx")).ToArray();
        string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        var hashes = paths.Select(Hash).ToArray(); var next = 0;
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var window = new MainWindow(null, profile, null, _ => Task.FromResult<string?>(paths[next++])) { ShowInTaskbar = false };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Workbook viewer did not connect.");
            for (var index = 0; index < paths.Length; index++)
            {
                await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Open workbook').click()");
                var file = Path.GetFileName(paths[index]);
                await Until(async () => await web.ExecuteScriptAsync($"document.body.textContent.includes({JsonSerializer.Serialize(file)}) && !!document.querySelector('.cell-window td')") == "true", "Workbook did not render: " + file);
                Assert.AreEqual("4", await web.ExecuteScriptAsync("document.querySelector('select[aria-label=Worksheet]').options.length"));
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.body.textContent.includes('Read-only workbook view. Use Insert into Excel')"));
                Assert.AreEqual("true", await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('.formula-text')).some(c=>c.textContent.includes('Lookup'))"));
                Assert.AreEqual("true", await web.ExecuteScriptAsync("!Array.from(document.querySelectorAll('button')).some(b=>['Create working copy','Save As','Review Merge'].includes(b.textContent))"));
                await Until(async () => await web.ExecuteScriptAsync("!document.querySelector('select[aria-label=Worksheet]').disabled") == "true", "Worksheet selector remained disabled.");
                await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=Worksheet]'); s.value=Array.from(s.options).find(o=>o.textContent==='Lookup (Hidden)').value; s.dispatchEvent(new Event('change',{bubbles:true}));})()");
                await Until(async () => await web.ExecuteScriptAsync("document.querySelector('caption')?.textContent.startsWith('Lookup') && document.querySelector('.cell-window')?.textContent.includes('Synthetic multiplier')") == "true", "Hidden sheet was not inspectable.");
                foreach (var width in new[] { 390d, 768d, 1100d })
                {
                    window.Width = width; await Task.Delay(100);
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Workbook viewer overflow at " + width);
                }
                using (var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "workbook-viewer-" + index + ".png")))
                    await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
                await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Close workbook').click()");
                await Until(async () => await web.ExecuteScriptAsync("document.body.textContent.includes('Workbook closed. Original file unchanged.')") == "true", "Workbook did not close.");
                Assert.AreEqual(hashes[index], Hash(paths[index]));
            }
        }
        finally { window.Close(); }
        for (var i = 0; i < paths.Length; i++) Assert.AreEqual(hashes[i], Hash(paths[i]));
    });
}
