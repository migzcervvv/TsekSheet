using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workbooks;
using TsekSheet.Workbooks;

namespace TsekSheet.Desktop;

public partial class MainWindow : Window
{
    private BridgeSession? session;
    private bool initializing, closed, attached;
    private string? selectedPath; // Native-only; never serialized.
    private readonly string assets;
    private readonly string profile;
    private readonly string? browserFolder;
    private readonly IWorkbookEngine workbookEngine = new WorkbookEngine();
    private readonly Func<CancellationToken, Task<string?>>? workbookSelector;
    private readonly Func<bool>? confirmDiscard;
    private readonly Func<CancellationToken, Task<string?>>? saveSelector;
    private bool dialogOpen;

    public MainWindow() : this(null, null, null) { }

    internal MainWindow(string? assetDirectory, string? profileDirectory, string? browserExecutableFolder, Func<CancellationToken, Task<string?>>? workbookSelector = null, Func<bool>? confirmDiscard = null, Func<CancellationToken, Task<string?>>? saveSelector = null)
    {
        assets = assetDirectory ?? Path.Combine(AppContext.BaseDirectory, "frontend");
        profile = profileDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TsekSheet", "WebView2");
        browserFolder = browserExecutableFolder;
        this.workbookSelector = workbookSelector;
        this.confirmDiscard = confirmDiscard;
        this.saveSelector = saveSelector;
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
        Closing += (_, e) =>
        {
            if (session?.HasPendingWorkbookChanges == true && !(confirmDiscard?.Invoke() ?? MessageBox.Show(this, "Discard all in-memory workbook changes and close?", "Unsaved workbook changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)) e.Cancel = true;
        };
        Closed += (_, _) => Shutdown();
    }

    private async void RetryInitialization(object sender, RoutedEventArgs e) => await InitializeAsync();

    private async Task InitializeAsync()
    {
        if (closed || initializing) return;
        initializing = true;
        RetryButton.Visibility = Visibility.Collapsed;
        HostStatus.Text = "Starting the desktop foundation…";
        try
        {
            ValidateAssets(assets);
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString(browserFolder);
            var environment = await CoreWebView2Environment.CreateAsync(browserFolder, profile).WaitAsync(TimeSpan.FromSeconds(20));
            if (closed) return;
            await FrontendWebView.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(20));
            if (closed) return;
            var core = FrontendWebView.CoreWebView2;
            if (!attached)
            {
                core.Settings.AreHostObjectsAllowed = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.SetVirtualHostNameToFolderMapping("tseksheet.local", assets, CoreWebView2HostResourceAccessKind.DenyCors);
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += RestrictResource;
                core.NavigationStarting += NavigationStarting;
                core.NavigationCompleted += NavigationCompleted;
                core.NewWindowRequested += (_, e) => e.Handled = true;
                core.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.Handled = true; };
                core.DownloadStarting += (_, e) => e.Cancel = true;
                core.FrameCreated += (_, e) => e.Frame.NavigationStarting += (_, navigation) => navigation.Cancel = true;
                core.WebMessageReceived += MessageReceived;
                core.ProcessFailed += (_, _) => Fail("The embedded browser stopped. Close and reopen TsekSheet to restart it.", false);
                attached = true;
            }
            core.Navigate(BridgePolicy.Origin + "/index.html");
        }
        catch (WebView2RuntimeNotFoundException) { Fail("WebView2 Runtime is missing. Supply the approved x64 standalone offline runtime installer before launching TsekSheet."); }
        catch (FileNotFoundException ex) { Fail(ex.Message); }
        catch (Exception) { Fail("The embedded browser could not initialize. Check the local WebView2 Runtime and access to your per-user application data, then retry."); }
        finally { initializing = false; }
    }

    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!BridgePolicy.IsTrusted(e.Uri)) { e.Cancel = true; return; }
        if (session?.HasPendingWorkbookChanges == true && MessageBox.Show(this, "Discard all in-memory workbook changes and reload?", "Unsaved workbook changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { e.Cancel = true; return; }
        session?.Dispose();
        selectedPath = null;
        session = new BridgeSession(Send, SelectFileAsync, workbooks: new WorkbookWorkspace(workbookEngine, SelectWorkbookAsync, SelectSaveDestinationAsync));
    }

    internal static void ValidateAssets(string directory)
    {
        const string message = "Bundled frontend assets are missing. Rebuild the desktop application or restore the complete distribution.";
        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        var index = Path.Combine(directory, "index.html");
        if (!File.Exists(index)) throw new FileNotFoundException(message);
        var references = Regex.Matches(File.ReadAllText(index), "(?:src|href)=\"([^\"]+)\"");
        if (references.Count == 0) throw new FileNotFoundException(message);
        foreach (Match reference in references)
        {
            var relative = reference.Groups[1].Value;
            var path = Path.GetFullPath(Path.Combine(directory, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new FileNotFoundException(message);
        }
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (closed) return;
        if (!e.IsSuccess) { Fail("The bundled frontend could not load. Rebuild the application and verify its local assets."); return; }
        StatusPanel.Visibility = Visibility.Collapsed;
    }

    private void RestrictResource(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!BridgePolicy.IsTrusted(e.Request.Uri))
            e.Response = FrontendWebView.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain");
    }

    private async void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try { if (!closed && session != null) await session.HandleAsync(e.Source, e.WebMessageAsJson); }
        catch (Exception) { if (!closed) Fail("The desktop connection failed. Close and reopen TsekSheet to reconnect.", false); }
    }

    private void Send(string json)
    {
        if (closed || Dispatcher.HasShutdownStarted) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => Send(json)); return; }
        FrontendWebView.CoreWebView2?.PostWebMessageAsJson(json);
    }

    private Task<FileSelection> SelectFileAsync(CancellationToken token)
    {
        if (dialogOpen) throw new WorkbookException("BUSY", "A native file dialog is already open.");
        dialogOpen = true;
        try
        {
        token.ThrowIfCancellationRequested();
        var dialog = new OpenFileDialog { Title = "Select a file — demonstration only", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return Task.FromResult(new FileSelection("cancelled"));
        token.ThrowIfCancellationRequested();
        selectedPath = dialog.FileName;
        return Task.FromResult(new FileSelection("selected", Guid.NewGuid().ToString("N"), Path.GetFileName(selectedPath)));
        }
        finally { dialogOpen = false; }
    }

    private Task<string?> SelectWorkbookAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (workbookSelector != null) return workbookSelector(token); // Internal test seam; never a bridge command or frontend path.
        if (dialogOpen) throw new WorkbookException("BUSY", "A native file dialog is already open.");
        dialogOpen = true;
        try
        {
            var dialog = new OpenFileDialog { Title = "Open workbook — read only", Filter = "Excel workbook (*.xlsx)|*.xlsx", CheckFileExists = true, Multiselect = false };
            var selected = dialog.ShowDialog(this) == true ? dialog.FileName : null;
            token.ThrowIfCancellationRequested();
            return Task.FromResult(selected);
        }
        finally { dialogOpen = false; }
    }

    private Task<string?> SelectSaveDestinationAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (saveSelector != null) return saveSelector(token);
        if (dialogOpen) throw new WorkbookException("BUSY", "A native file dialog is already open.");
        dialogOpen = true;
        try
        {
            var dialog = CreateSaveDialog();
            dialog.FileOk += (_, e) =>
            {
                if (!Path.Exists(dialog.FileName)) return;
                e.Cancel = true;
                MessageBox.Show(this, "Choose a new filename. TsekSheet cannot overwrite an existing file.", "Save As", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            var selected = dialog.ShowDialog(this) == true ? dialog.FileName : null;
            token.ThrowIfCancellationRequested();
            return Task.FromResult(selected);
        }
        finally { dialogOpen = false; }
    }

    internal static SaveFileDialog CreateSaveDialog() => new()
    {
        Title = "Save As — original session only; working-copy edits excluded",
        Filter = "Excel workbook (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true,
        CheckPathExists = true, OverwritePrompt = false, CreatePrompt = false, CreateTestFile = false
    };

    private void Fail(string message, bool retry = true)
    {
        session?.Dispose(); selectedPath = null;
        if (closed) return;
        StatusPanel.Visibility = Visibility.Visible;
        HostStatus.Text = message;
        RetryButton.Visibility = retry ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Shutdown()
    {
        closed = true;
        session?.Dispose(); selectedPath = null;
        FrontendWebView.Dispose();
    }
}
