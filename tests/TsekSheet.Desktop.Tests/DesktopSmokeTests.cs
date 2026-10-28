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
    private static readonly List<string> Profiles = [];
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
