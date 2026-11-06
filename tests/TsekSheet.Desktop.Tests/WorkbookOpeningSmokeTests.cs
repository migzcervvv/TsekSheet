using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Wpf;
using TsekSheet.Desktop;
using TsekSheet.TestFixtures;

namespace TsekSheet.Desktop.Tests;

public sealed partial class DesktopSmokeTests
{
    [TestMethod, TestCategory("WindowsSmoke")]
    public Task SecondaryInspectionKeepsAuthorizationWithoutChangingActiveWorkspace() => OnDesktop(async () =>
    {
        using var large = new AccountingCompatibilityFixture("cells");
        using var linked = new AccountingCompatibilityFixture("external");
        using var broken = new AccountingCompatibilityFixture("broken");
        using var ordinary = new AccountingCompatibilityFixture("plain");
        var fixtures = new[] { large.Path, linked.Path, broken.Path, ordinary.Path };
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var hashes = fixtures.Select(Hash).ToArray(); var selected = large.Path;
        var profile = Path.Combine(Path.GetTempPath(), "TsekSheet-smoke", Guid.NewGuid().ToString("N")); Profiles.Add(profile);
        var window = new MainWindow(null, profile, null, _ => Task.FromResult<string?>(selected)) { ShowInTaskbar = false, Width = 1366, Height = 768 };
        try
        {
            window.Show(); var web = (WebView2)window.FindName("FrontendWebView");
            await Until(async () => web.CoreWebView2 != null && await web.ExecuteScriptAsync("document.querySelector('[role=status]')?.textContent") == "\"Connected\"", "Host unavailable");
            async Task Click(string label)
            {
                var value = JsonSerializer.Serialize(label);
                await Until(async () => await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).some(b=>b.textContent==={value}&&!b.disabled)") == "true", "Button unavailable: " + label);
                await web.ExecuteScriptAsync($"Array.from(document.querySelectorAll('button')).find(b=>b.textContent==={value}).click()");
            }
            async Task Reject(string command, string payload, string expected)
            {
                var id = "probe-" + Guid.NewGuid().ToString("N");
                await web.ExecuteScriptAsync($"(()=>{{window.openProbe=null;const h=e=>{{if(e.data.requestId==='{id}'){{window.openProbe=e.data;chrome.webview.removeEventListener('message',h);}}}};chrome.webview.addEventListener('message',h);chrome.webview.postMessage({{protocolVersion:1,requestId:'{id}',command:'{command}',payload:{payload}}});}})()");
                await Until(async () => await web.ExecuteScriptAsync("window.openProbe!==null") == "true", "Missing rejection: " + command);
                Assert.AreEqual("false", await web.ExecuteScriptAsync("window.openProbe.ok"));
                Assert.AreEqual(JsonSerializer.Serialize(expected), await web.ExecuteScriptAsync("window.openProbe.error.code"));
            }
            foreach (var fixture in new[] { (large.Path, "EDITABLE_ADMISSION_LIMIT_EXCEEDED"), (linked.Path, "EDITABLE_ADMISSION_EXTERNAL_RESOURCE") })
            {
                selected = fixture.Path;
                await Navigate(web, "workbook"); await Click("Open for read-only inspection");
                var displayName = JsonSerializer.Serialize(Path.GetFileName(fixture.Path));
                await Until(async () => await web.ExecuteScriptAsync($"document.querySelector('.reader-file strong')?.textContent==={displayName} && document.body.textContent.includes('{fixture.Item2}') && !document.querySelector('select[aria-label=Worksheet]').disabled") == "true", "Inspection project was not adopted");
                Assert.AreEqual("true", await web.ExecuteScriptAsync("!!document.querySelector('.reader-file') && document.body.textContent.includes('Workbook opened in inspection mode')"));
                Assert.AreEqual("false", await web.ExecuteScriptAsync("!!document.querySelector('.active-project')"), "Secondary inspection must not establish an Excel workspace.");
                await Navigate(web, "workbook");
                await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('.cell-window.inspection table') && !document.querySelector('select[aria-label=Worksheet]').disabled") == "true", "Inspection worksheet did not render");
                Assert.AreEqual("true", await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).filter(b=>['Create working copy','Save As'].includes(b.textContent)).every(b=>b.disabled)"));
                // Obtain active identity from the real viewport request, not a test-only host API.
                await web.ExecuteScriptAsync("window.openingIdentity=null;window.openingObserver=e=>{if(e.data.ok&&e.data.result?.worksheetId)window.openingIdentity=e.data.result;};chrome.webview.addEventListener('message',window.openingObserver)");
                await web.ExecuteScriptAsync("(()=>{const s=document.querySelector('select[aria-label=Worksheet]');s.value=s.options[1].value;s.dispatchEvent(new Event('change',{bubbles:true}));})()");
                await Until(async () => await web.ExecuteScriptAsync("!!window.openingIdentity && document.querySelector('caption')?.textContent.startsWith('Ledger 2') && !document.querySelector('select[aria-label=Worksheet]').disabled") == "true", "Hidden worksheet navigation failed");
                var identity = JsonSerializer.Deserialize<JsonElement>(await web.ExecuteScriptAsync("({sessionId:window.openingIdentity.sessionId,worksheetId:window.openingIdentity.worksheetId,expectedRevision:0})"));
                await web.ExecuteScriptAsync("chrome.webview.removeEventListener('message',window.openingObserver)");
                await Reject("workbook.createCopy", identity.GetRawText(), "UNSUPPORTED_COMMAND");
                await Reject("workbook.saveAsNew", JsonSerializer.Serialize(new { sessionId = identity.GetProperty("sessionId").GetString(), expectedRevision = 0 }), "UNSUPPORTED_COMMAND");
                foreach (var width in new[] { 1366d, 390d })
                {
                    window.Width = width;
                    await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('details')).filter(d=>d.querySelector('summary')?.textContent==='Details').forEach(d=>d.open=true)");
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("document.documentElement.scrollWidth<=document.documentElement.clientWidth"), "Details overflow");
                    Assert.AreEqual("true", await web.ExecuteScriptAsync("Array.from(document.querySelectorAll('.semantic-notice')).every(n=>n.scrollWidth<=n.clientWidth)"), "Technical findings clipped inside notice");
                    using var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, $"workbook-opening-{fixture.Item2}-{width}.png"));
                    await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, screenshot);
                }
                window.Width = 1366;
            }
            selected = broken.Path; await Click("Open for read-only inspection");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('[role=alert]') && document.body.textContent.includes('INSPECTION_INVALID_XML')") == "true", "Missing structured inspection failure");
            Assert.AreEqual("true", await web.ExecuteScriptAsync("document.querySelector('caption')?.textContent.startsWith('Ledger 2') && !!document.querySelector('.reader-file')"), "Failed switch lost previous project");
            selected = ordinary.Path; await Click("Open for read-only inspection");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('.cell-window:not(.inspection) td') && document.querySelector('caption')?.textContent.startsWith('Ledger 1') && !document.querySelector('select[aria-label=Worksheet]').disabled") == "true", "Replacement read-only project did not finish loading");
            Assert.AreEqual("false", await web.ExecuteScriptAsync("document.body.textContent.includes('EDITABLE_ADMISSION_') || document.body.textContent.includes('Opening failure details')"));
            await Click("Inspect workbook read-only");
            await Until(async () => await web.ExecuteScriptAsync("!!document.querySelector('.cell-window.inspection') && !document.querySelector('select[aria-label=Worksheet]').disabled") == "true", "Explicit inspection failed");
            await Click("Close workbook");
            for (var i = 0; i < fixtures.Length; i++) Assert.AreEqual(hashes[i], Hash(fixtures[i]), "Source changed");
        }
        finally { window.Close(); }
    });
}
