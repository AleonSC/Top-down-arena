#if WINDOW
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// Enemy Forge's own Windows window: a WinForms form with a WebView2 (the Edge engine built into Windows 10/11) showing the
// page. The WebView2 DLLs ride inside the exe (package.sh) and are unpacked to %LOCALAPPDATA%\EnemyForge on first run.
// Run returns false when there's no WebView2 runtime (or not on Windows); the caller then opens a browser window instead.
static class AppWindow
{
    const string Ver = "1.0.4191.47";
    static readonly Color Bg = Color.FromArgb(11, 15, 20);

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    public static bool Run(string url, string selfTest)
    {
        if (Path.DirectorySeparatorChar != '\\') return false;
        string dir;
        try { dir = Unpack(); Forge.Log("unpacked WebView2 to " + dir); } catch (Exception e) { Forge.Log("unpack failed: " + e); return false; }
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string f = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(f) ? Assembly.LoadFrom(f) : null;
        };
        try { return Show(url, dir, selfTest); } catch (Exception e) { Forge.Log("show failed: " + e); return false; }
    }

    // Writes the embedded WebView2 files out once per version.
    static string Unpack()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnemyForge", "wv2-" + Ver);
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("wv2/")) continue;
            string f = Path.Combine(dir, name.Substring(4).Replace('/', Path.DirectorySeparatorChar));
            using (var rs = asm.GetManifestResourceStream(name))
            {
                if (File.Exists(f) && new FileInfo(f).Length == rs.Length) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                using (var fs = File.Create(f)) rs.CopyTo(fs);
            }
        }
        return dir;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]  // so the WebView2 types load after AssemblyResolve is hooked
    static bool Show(string url, string dir, string selfTest)
    {
        string arch = (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "").ToUpperInvariant();
        CoreWebView2Environment.SetLoaderDllFolderPath(Path.Combine(dir, arch == "ARM64" ? "arm64" : arch == "X86" ? "x86" : "x64"));
        try { Forge.Log("WebView2 runtime " + CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch (Exception e) { Forge.Log("no WebView2 runtime: " + e.Message); return false; }  // fall back to the browser

        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { try { SetProcessDPIAware(); } catch { } }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Form form = null;
        bool failed = false;
        Action<string> fail = m =>
        {
            Forge.Log("window error: " + m);
            if (failed) return;
            failed = true;
            if (selfTest != null) { try { File.WriteAllText(selfTest, "FAIL " + m); } catch { } }
            else MessageBox.Show("Enemy Forge couldn't show its window:\n\n" + m + "\n\nIt will open in your browser instead. (Details: %LOCALAPPDATA%\\EnemyForge\\log.txt)", "Enemy Forge");
            try { form?.Close(); } catch { }
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => fail(e.Exception.ToString());
        var area = Screen.PrimaryScreen.WorkingArea;
        form = new Form
        {
            Text = "Enemy Forge",
            BackColor = Bg,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(Math.Min(area.Width, (int)(area.Width * 0.9)), Math.Min(area.Height, (int)(area.Height * 0.9))),
        };
        form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
        form.MinimumSize = new Size(Math.Min(area.Width, 1000), Math.Min(area.Height, 640));
        try { form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var wv = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Bg };
        form.Controls.Add(wv);
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnemyForge", "WebView2");
        form.Load += async (s, e) =>
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await wv.EnsureCoreWebView2Async(env);
                wv.CoreWebView2.Settings.IsStatusBarEnabled = false;
                wv.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                wv.CoreWebView2.DocumentTitleChanged += (o, a) => form.Text = wv.CoreWebView2.DocumentTitle;
                wv.CoreWebView2.ProcessFailed += (o, a) => Forge.Log("WebView2 process failed: " + a.ProcessFailedKind);
                wv.CoreWebView2.NavigationCompleted += (o, a) =>
                {
                    Forge.Log("page loaded: " + a.IsSuccess + " " + a.WebErrorStatus);
                    if (selfTest == null) return;
                    var t = new Timer { Interval = 3000 };
                    t.Tick += async (o2, a2) =>
                    {
                        t.Stop();
                        try
                        {
                            string r = await wv.CoreWebView2.ExecuteScriptAsync("JSON.stringify({title:document.title,editor:document.querySelectorAll('#editor .f').length,enemies:typeof META!=='undefined'?META.enemies.length:-1,errs:window.__errs})");
                            Forge.Log("self-test: " + r);
                            File.WriteAllText(selfTest, "OK " + r);
                        }
                        catch (Exception ex) { fail(ex.ToString()); }
                        form.Close();
                    };
                    t.Start();
                };
                Forge.Log("WebView2 ready, opening " + url);
                wv.Source = new Uri(url);
            }
            catch (Exception ex) { fail(ex.ToString()); }
        };
        Forge.Log("window opening");
        Application.Run(form);
        return !failed;
    }
}
#endif
