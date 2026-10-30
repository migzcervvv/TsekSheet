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
public sealed class DesktopSmokeTests
{
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
            async Task Click(string label){await Until(async()=>await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={JsonSerializer.Serialize(label)}&&!b.disabled)")=="true","Button unavailable: "+label);_=await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={JsonSerializer.Serialize(label)}).click()");}
            async Task Input(string label,string value)=>_=await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('[aria-label={JsonSerializer.Serialize(label)}]');Object.getOwnPropertyDescriptor(el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype,'value').set.call(el,{JsonSerializer.Serialize(value)});el.dispatchEvent(new Event('input',{{bubbles:true}}));}})()");
            async Task Select(string label,string value)=>_=await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('select[aria-label={JsonSerializer.Serialize(label)}]');el.value={JsonSerializer.Serialize(value)};el.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
            await Until(async()=>web.CoreWebView2!=null&&await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent")=="\"Connected\"","Host did not connect");
            await Click("Inspect workbook read-only");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Read-only section review\"]') && !!document.querySelector('button[aria-label=\"Inspect C1\"]') && !document.querySelector('button[aria-label=\"Inspect C1\"]').disabled")=="true","Review entry missing");
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
            Assert.AreEqual("true",await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As').disabled && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy').disabled && !document.querySelector('.cell-editor input') && document.body.textContent.includes('Structural insertion NOT authorized')"));
            foreach(var width in new[]{390d,768d,1100d}){window.Width=width;await Task.Delay(100);Assert.AreEqual("true",await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"),"Review overflow at "+width);}
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only section review\"]').scrollIntoView({block:'start'})");
            using(var screenshot=File.Create(Path.Combine(AppContext.BaseDirectory,"step56-section-review.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            window.Close();Assert.IsTrue(window.IsVisible);Assert.AreEqual(1,closeWarnings,"Native window close must preserve decisions by default");
            await Click("Close section review");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[role=alertdialog]')")=="true","Discard confirmation missing");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.querySelector('[role=alertdialog]').textContent.includes('2 in-memory review decisions') && document.activeElement.textContent==='Continue Reviewing'"));
            await Click("Continue Reviewing");Assert.AreEqual("true",await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Identity case\"]')"));
            await Click("Select profile and start review");await Click("Continue Reviewing");
            await Click("Inspect workbook read-only");await Click("Continue Reviewing");
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
    public Task ManualPayablesPlanningRequiresSectionChoiceAndNeverEnablesInsertion() => OnDesktop(async () =>
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
            async Task Select(string label, string value) => _ = await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('select[aria-label={JsonSerializer.Serialize(label)}]'); el.value={JsonSerializer.Serialize(value)}; el.dispatchEvent(new Event('change',{{bubbles:true}}));}})()");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host did not connect.");
            await Until(async () => await web.ExecuteScriptAsync("(()=>{const img=document.querySelector('header .brand-logo');return !!img && img.complete && img.naturalWidth>0 && new URL(img.src).origin==='https://tseksheet.local';})()") == "true", "Bundled header logo did not load.");
            await web.ExecuteScriptAsync("window.brandAssetsLoaded=null;Promise.all(['TsekSheet.svg','TsekSheet-No-BG.svg'].map(name=>new Promise(resolve=>{const img=new Image();img.onload=()=>resolve(true);img.onerror=()=>resolve(false);img.src='./branding/'+name;}))).then(results=>window.brandAssetsLoaded=results.every(Boolean))");
            await Until(async () => await web.ExecuteScriptAsync("window.brandAssetsLoaded") == "true", "Bundled brand assets did not load.");
            using (var brandingCapture = File.Create(Path.Combine(AppContext.BaseDirectory, "branding-header.png")))
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, brandingCapture);
            await Click("Open workbook");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('select[aria-label=\"Workflow profile\"] option[value=\"synthetic-payables\"]')") == "true", "Profile catalog did not load.");
            await Select("Workflow profile", "synthetic-payables");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('input[aria-label=\"Payable Vendor name\"]')") == "true", "Manual fields did not render.");
            foreach (var (label, value) in new[] { ("Vendor name", "Acme Trading Incorporated"), ("RFP number", "R3"), ("Amount", "123.45") })
                await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('input[aria-label={JsonSerializer.Serialize("Payable " + label)}]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,{JsonSerializer.Serialize(value)});el.dispatchEvent(new Event('input',{{bubbles:true}}));}})()");
            await Click("Preview payable destination");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('select[aria-label=\"Destination section\"]') && document.body.textContent.includes('Potential duplicates')") == "true", "Multi-section matches or duplicate warning missing.");
            Assert.AreEqual("\"\"", await web.ExecuteScriptAsync("document.querySelector('select[aria-label=\"Destination section\"]').value"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Structural insertion status\"]').textContent.includes('Ambiguous destination')"));
            await Select("Destination section", "internal"); await Click("Preview payable destination");
            await Until(async () => await web.ExecuteScriptAsync("document.body.textContent.includes('insert at base row 9, proposed final row 9')") == "true", "Explicit section choice did not yield the expected vendor boundary.");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Apply row insertion — unavailable').disabled"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Structural insertion status\"]').textContent.includes('Duplicate warning')"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Candidate lifecycle review\"]').textContent.includes('Candidate: not generated') && document.querySelector('[aria-label=\"Candidate lifecycle review\"]').textContent.includes('Structural integration: production disabled')"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.body.textContent.includes('No pending changes')"));
            foreach (var width in new[] { 390d, 768d, 1100d })
            {
                window.Width = width; await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Planning overflow at " + width);
            }
            await web.ExecuteScriptAsync("document.querySelector('.workflow-panel').scrollIntoView({block:'start'})");
            using var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "payables-planning.png"));
            await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
            Assert.AreEqual(hash, Hash(), "Planning altered synthetic source bytes.");
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
            await Click("Inspect workbook read-only");
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
            Assert.AreEqual("true", await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As').disabled && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy').disabled && !document.querySelector('#payables-title') && !!document.querySelector('.section-review') && document.querySelector('header img').naturalWidth>0 && document.body.textContent.includes('No pending changes')"));
            await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Read-only cell details\"]').scrollIntoView({block:'start'})");
            using (var capture = File.Create(Path.Combine(AppContext.BaseDirectory, "step47-interpretation.png"))) await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, capture);
            await Click("Close workbook"); await Until(async () => await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Worksheet viewport\"]')") == "true", "Close failed.");
            await Click("Inspect workbook read-only"); await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('button[aria-label=\"Inspect A1\"]')") == "true", "Reopen failed.");
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
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.body.textContent.includes('Compatibility Report') && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy').disabled && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As').disabled"));
            Assert.AreEqual("true", await web.ExecuteScriptAsync("!document.querySelector('#payables-title') && !!document.querySelector('.section-review') && document.querySelector('header img').naturalWidth>0"));
            foreach (var width in new[] { 390d, 768d, 1100d }) { window.Width = width; await Task.Delay(100); Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth")); }
            await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('summary')).find(s=>s.textContent.startsWith('Compatibility Report')).scrollIntoView({block:'start'})");
            using var capture = File.Create(Path.Combine(AppContext.BaseDirectory, "step46-compatibility.png"));
            await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, capture);
        }
        finally { window.Close(); }
        Assert.AreEqual(hash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fixture.Path)))) ;
    });
    [TestMethod,TestCategory("WindowsSmoke")]
    [DataRow(21)][DataRow(22)]
    public Task ActualCandidateReviewApprovesOnlyQualifiedHeightWithoutApplying(int height)=>OnDesktop(async()=>
    {
        var path=Path.Combine(AppContext.BaseDirectory,"Fixtures",height==21?"review21.xlsx":"review22.xlsx");
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        var browser=Path.Combine(Path.GetTempPath(),"TsekSheet-smoke",Guid.NewGuid().ToString("N"));Profiles.Add(browser);
        var window=new MainWindow(null,browser,null,_=>Task.FromResult<string?>(path)){ShowInTaskbar=false};
        try
        {
            window.Show();var web=(WebView2)window.FindName("FrontendWebView");
            async Task Click(string label){await Until(async()=>await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={JsonSerializer.Serialize(label)}&&!b.disabled)")=="true","Button unavailable: "+label);_=await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={JsonSerializer.Serialize(label)}).click()");}
            await Until(async()=>web.CoreWebView2!=null&&await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent")=="\"Connected\"","Host did not connect.");
            await Click("Open workbook");await Until(async()=>await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy')?.disabled===false")=="true","Copy not ready.");
            await Until(async()=>await web.ExecuteScriptAsync("(()=>{const b=Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy');if(!b||b.disabled||!document.querySelector('[aria-label=\"Worksheet viewport\"]'))return false;b.click();return true;})()") == "true", "Working-copy button/viewport never became ready.");await Until(async()=>await web.ExecuteScriptAsync("document.querySelector('select[aria-label=\"Workbook view\"]')?.value!=='original' && !!document.querySelector('select[aria-label=\"Workflow profile\"] option[value=\"synthetic-payables\"]')")=="true","Copy/profile unavailable.");
            await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=\"Workflow profile\"]');s.value='synthetic-payables';s.dispatchEvent(new Event('change',{bubbles:true}));})()");
            await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('input[aria-label=\"Payable Vendor name\"]')")=="true","Fields unavailable.");
            foreach(var(label,value)in new[]{("Vendor name","Other Tools Ltd"),("RFP number","R2"),("Amount","25")})
                await web.ExecuteScriptAsync($"(()=>{{const el=document.querySelector('input[aria-label={JsonSerializer.Serialize("Payable "+label)}]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,{JsonSerializer.Serialize(value)});el.dispatchEvent(new Event('input',{{bubbles:true}}));}})()");
            await Click("Preview payable destination");await Until(async()=>await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Generate candidate for review')?.disabled===false")=="true","Candidate construction blocked unexpectedly.");
            await Click("Generate candidate for review");await Until(async()=>await web.ExecuteScriptAsync("!!document.querySelector('[aria-label=\"Generated candidate review\"]')")=="true","Actual candidate review did not appear.");
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.body.textContent.includes('preservation oracle accepted') && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Apply row insertion — unavailable').disabled"));
            if(height==21)
            {
                Assert.AreEqual("true",await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Approve reviewed candidate').disabled"));
                await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Generated candidate review\"] input[type=checkbox]').click()");
                Assert.AreEqual("true",await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Approve reviewed candidate').disabled"));
                await web.ExecuteScriptAsync("document.querySelectorAll('[aria-label=\"Generated candidate review\"] input[type=checkbox]')[1].click()");
                await Click("Approve reviewed candidate");await Until(async()=>await web.ExecuteScriptAsync("document.body.textContent.includes('user approved review')")=="true","Explicit approval did not complete.");
            }
            else Assert.AreEqual("true",await web.ExecuteScriptAsync("document.body.textContent.includes('has height 22') && Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Approve reviewed candidate').disabled"));
            Assert.AreEqual("true",await web.ExecuteScriptAsync("document.body.textContent.includes('No pending changes') && document.body.textContent.includes('Working-copy adoption, structural integration and export have not occurred')"));
            foreach(var width in new[]{390d,768d,1100d}){window.Width=width;await Task.Delay(100);Assert.AreEqual("true",await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"));}
            await Click("Cancel candidate review");await Until(async()=>await web.ExecuteScriptAsync("!document.querySelector('[aria-label=\"Generated candidate review\"]')")=="true","Review cancellation did not clear state.");
        }
        finally{window.Close();}
        Assert.AreEqual(hash,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
    });
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
            Assert.AreEqual("Welcome to TsekSheet", JsonSerializer.Deserialize<string>(title));
            using (var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "desktop-smoke.png")))
                await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
            foreach (var width in new[] { 390d, 768d, 1100d })
            {
                window.Width = width;
                await Task.Delay(100);
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Horizontal overflow at width " + width);
            }
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

    [TestMethod]
    public Task SaveDialogDoesNotProbeOrAuthorizeOverwriting() => OnDesktop(() =>
    {
        var dialog = MainWindow.CreateSaveDialog();
        Assert.IsFalse(dialog.CreateTestFile); Assert.IsFalse(dialog.OverwritePrompt); Assert.IsFalse(dialog.CreatePrompt);
        Assert.IsTrue(dialog.CheckPathExists); Assert.AreEqual("xlsx", dialog.DefaultExt); // WPF normalizes away the leading dot.
        return Task.CompletedTask;
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
        var allowClose = false; var closeWarnings = 0;
        var saveFolder = Path.Combine(Path.GetTempPath(), "TsekSheet-save-smoke-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(saveFolder);
        var saveTarget = Path.Combine(saveFolder, "export.xlsx"); var saveSelections = 0;
        var window = new MainWindow(null, profile, null, _ => Task.FromResult<string?>(paths[next++]), () => { closeWarnings++; return allowClose; }, _ => Task.FromResult<string?>(saveSelections++ < 2 ? saveTarget : null)) { ShowInTaskbar = false };
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
                Assert.AreEqual("true", await web.ExecuteScriptAsync("document.body.textContent.includes('Original workbook view. Edit an isolated working copy.')"));
                Assert.AreEqual("true", await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('.formula-text')).some(c=>c.textContent.includes('Lookup'))"));
                if (index == 0)
                {
                    await Until(async () => await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy')?.disabled === false") == "true", "Working-copy control was unavailable.");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Create working copy').click()");
                    await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('button[aria-label=\"Select A5\"]') && document.querySelector('select[aria-label=\"Workbook view\"]')?.disabled === false") == "true", "Working copy did not open.");
                    await web.ExecuteScriptAsync("document.querySelector('button[aria-label=\"Select A5\"]').click()");
                    await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('input[aria-label=\"Cell value\"]')") == "true", "Cell editor did not appear.");
                    await web.ExecuteScriptAsync("(()=>{const el=document.querySelector('input[aria-label=\"Cell value\"]'); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,'Synthetic copy edit'); el.dispatchEvent(new Event('input',{bubbles:true}));})()");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Apply to working copy').click()");
                    await Until(async () => await web.ExecuteScriptAsync("document.querySelector('.cell-window')?.textContent.includes('Synthetic copy edit') && document.body.textContent.includes('Pending changes in working copies')") == "true", "Working-copy edit did not appear.");
                    using (var editScreenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "working-copy-edit.png")))
                        await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, editScreenshot);
                    window.Close(); Assert.IsTrue(window.IsVisible); Assert.AreEqual(1, closeWarnings, "Closing the desktop must warn before discarding pending changes.");
                    await Until(async () => await web.ExecuteScriptAsync("document.querySelector('select[aria-label=\"Workbook view\"]')?.disabled === false") == "true", "Workbook view selector remained disabled.");
                    await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=\"Workbook view\"]'); s.value='original'; s.dispatchEvent(new Event('change',{bubbles:true}));})()");
                    await Until(async () => await web.ExecuteScriptAsync("document.querySelector('.cell-window')?.textContent.includes('Sample-1') && !document.querySelector('.cell-window')?.textContent.includes('Synthetic copy edit')") == "true", "Original session did not remain independent.");
                    await Until(async () => await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As')?.disabled === false") == "true", "Save As was unavailable on the original session.");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As').click()");
                    await Until(async () => await web.ExecuteScriptAsync("document.body.textContent.includes('Created export.xlsx from original session revision 0')") == "true", "Save As did not confirm the real commit.");
                    Assert.AreEqual(hashes[index], Hash(saveTarget), "Export included pending working-copy changes or altered original bytes.");
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("document.body.textContent.includes('Pending changes in working copies')"));
                    using (var preview = File.Create(Path.Combine(AppContext.BaseDirectory, "save-as-success.png"))) await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, preview);
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As').click()");
                    await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[role=alert]')?.textContent.includes('Choose a new filename')") == "true", "Existing destination was not rejected.");
                    Assert.AreEqual(hashes[index], Hash(saveTarget));
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Save As').click()");
                    await Until(async () => await web.ExecuteScriptAsync("document.body.textContent.includes('Save As cancelled. No file was created.')") == "true", "Cancelled selection did not reach React.");
                    await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=\"Workbook view\"]'); s.value=s.options[1].value; s.dispatchEvent(new Event('change',{bubbles:true}));})()");
                    await Until(async () => await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Review Merge')?.disabled === false") == "true", "Merge review was unavailable.");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Review Merge').click()");
                    await Until(async () => await web.ExecuteScriptAsync("document.querySelector('[aria-label=\"Review worksheet merge\"]')?.textContent.includes('1 effective pending cell edits')") == "true", "Merge preview did not identify pending edits.");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Cancel merge').click()");
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('select[aria-label=\"Workbook view\"]').value !== 'original'"));
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Review Merge').click()");
                    await Until(async () => await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Confirm merge in memory')?.disabled === false") == "true", "Merge confirmation was unavailable.");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Confirm merge in memory').click()");
                    await Until(async () => await web.ExecuteScriptAsync("document.querySelector('select[aria-label=\"Workbook view\"]')?.value === 'original' && document.querySelector('.cell-window')?.textContent.includes('Synthetic copy edit')") == "true", "Merge did not refresh the original session view.");
                    Assert.AreEqual(hashes[index], Hash(paths[index]), "In-memory integration changed the source fixture.");
                    Assert.AreEqual(hashes[index], Hash(saveTarget), "Integration automatically saved the earlier export.");
                }
                await Until(async () => await web.ExecuteScriptAsync("document.querySelector('select[aria-label=Worksheet]')?.disabled === false") == "true", "Worksheet selector remained disabled.");
                await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=Worksheet]'); s.value=Array.from(s.options).find(o=>o.textContent==='Lookup (Hidden)').value; s.dispatchEvent(new Event('change',{bubbles:true}));})()");
                await Until(async () => await web.ExecuteScriptAsync("document.querySelector('caption')?.textContent.startsWith('Lookup') && document.querySelector('.cell-window')?.textContent.includes('Synthetic multiplier')") == "true", "Hidden sheet was not inspectable.");
                foreach (var width in new[] { 390d, 768d, 1100d })
                {
                    window.Width = width; await Task.Delay(100);
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Workbook viewer overflow at " + width);
                    if (index == 0)
                    {
                        using var preview = File.Create(Path.Combine(AppContext.BaseDirectory, "workbook-viewer-" + (int)width + ".png"));
                        await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, preview);
                    }
                }
                using (var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "workbook-viewer-" + index + ".png")))
                    await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
                await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Close workbook').click()");
                if (index == 0)
                {
                    await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[role=alertdialog]')") == "true", "Discard warning was not displayed.");
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(b=>b.textContent==='Discard changes').click()");
                }
                await Until(async () => await web.ExecuteScriptAsync("document.body.textContent.includes('Workbook closed. Original file unchanged.')") == "true", "Workbook did not close.");
                Assert.AreEqual(hashes[index], Hash(paths[index]));
            }
        }
        finally { allowClose = true; window.Close(); foreach (var file in Directory.GetFiles(saveFolder)) File.Delete(file); Directory.Delete(saveFolder); }
        for (var i = 0; i < paths.Length; i++) Assert.AreEqual(hashes[i], Hash(paths[i]));
    });
}
