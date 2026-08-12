#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VnoiKiosk.Core;
using VnoiKiosk.Models;

namespace VnoiKiosk
{
    public partial class MainWindow : Window
    {
        private string _username = "";
        private string _password = "";
        private string _accessKey = "";
        private int _navigationState = 0;
        private bool _isWebViewInitialized = false;
        private KioskManager _kioskManager;
        private ExamData _currentExam;
        private List<ExamData> _availableExams = new List<ExamData>();
        
        private Window _toolWindow;

        private string _setupHtml = "";
        private string BASE_URL = "http://localhost:3000";
        private bool _isPinging = false;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private void FixWebView2LoaderSearchPath()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string archFolder = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "win-x64",
                    Architecture.X86 => "win-x86",
                    Architecture.Arm64 => "win-arm64",
                    _ => "win-x64"
                };
                string nativeDir = Path.Combine(baseDir, archFolder, "native");
                string rootDllPath = Path.Combine(baseDir, "WebView2Loader.dll");
                string nativeDllPath = Path.Combine(nativeDir, "WebView2Loader.dll");

                if (Directory.Exists(nativeDir))
                {
                    SetDllDirectory(nativeDir);
                }

                if (!File.Exists(rootDllPath) && File.Exists(nativeDllPath))
                {
                    try { File.Copy(nativeDllPath, rootDllPath, false); } catch { }
                }
            }
            catch { }
        }

        public MainWindow()
        {
            InitializeComponent();

            FixWebView2LoaderSearchPath();
            
            try
            {
                string searchDir = AppDomain.CurrentDomain.BaseDirectory;
                string envPath = Path.Combine(searchDir, "domain.env");
                
                while (!File.Exists(envPath) && Directory.GetParent(searchDir) != null)
                {
                    searchDir = Directory.GetParent(searchDir).FullName;
                    envPath = Path.Combine(searchDir, "domain.env");
                }

                if (File.Exists(envPath))
                {
                    string[] lines = File.ReadAllLines(envPath);
                    foreach (string line in lines)
                    {
                        if (line.Trim().StartsWith("BASE_URL="))
                        {
                            BASE_URL = line.Substring(line.IndexOf('=') + 1).Trim().TrimEnd('/');
                        }
                    }
                }
                else
                {
                    string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "domain.env");
                    File.WriteAllText(defaultPath, "BASE_URL=http://localhost:3000");
                }
            }
            catch { }
            
            _kioskManager = new KioskManager();
            
            Loaded += MainWindow_Loaded;
            
            Task.Run(async () => 
            {
                if (_kioskManager != null)
                {
                    await _kioskManager.CheckAndReportCrashAsync(BASE_URL);
                }
            });
            
            ShowPreExamWarning();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
            source?.AddHook(WndProc);
        }

        private DateTime _lastDeviceAlertTime = DateTime.MinValue;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0219 && _navigationState >= 3)
            {
                int eventType = wParam.ToInt32();
                bool isRealDeviceEvent = eventType == 0x8000 || eventType == 0x8004;

                if (isRealDeviceEvent && (DateTime.Now - _lastDeviceAlertTime).TotalSeconds > 3)
                {
                    _lastDeviceAlertTime = DateTime.Now;
                    Dispatcher.InvokeAsync(() => {
                        MessageBox.Show("HỆ THỐNG PHÁT HIỆN: Cắm/Rút thiết bị ngoại vi trái phép.", "Cảnh Báo Khẩn Cấp", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }
            return IntPtr.Zero;
        }

        private async Task StartPingLoop()
        {
            _isPinging = true;
            using (HttpClient client = new HttpClient())
            {
                while (_isPinging)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(_username))
                        {
                            var content = new StringContent(JsonConvert.SerializeObject(new { username = _username, machine_id = Environment.MachineName, status = "ONLINE" }), Encoding.UTF8, "application/json");
                            await client.PostAsync($"{BASE_URL}/api/kiosk/ping", content);
                        }
                    }
                    catch { }
                    await Task.Delay(5000);
                }
            }
        }

        private void StopPingLoop() { _isPinging = false; }

        private void HideCloseButton(DependencyObject parent)
        {
            if (parent == null) return;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button btn)
                {
                    btn.Visibility = Visibility.Collapsed;
                }
                else
                {
                    HideCloseButton(child);
                }
            }
        }

        private void ShowLoadingOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                if (Page2_Loading != null) 
                {
                    Page2_Loading.Visibility = Visibility.Visible;
                    Panel.SetZIndex(Page2_Loading, 99999);
                    HideCloseButton(Page2_Loading); 
                }
            });
        }

        private void HideLoadingOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                if (Page2_Loading != null) Page2_Loading.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowWebView2MissingScreen(Exception ex)
        {
            bool isMissingLoaderDll = ex is DllNotFoundException || (ex.InnerException is DllNotFoundException) || ex.Message.Contains("WebView2Loader.dll");

            string msg;
            if (isMissingLoaderDll)
            {
                msg = "Phần mềm thi bị THIẾU FILE CÀI ĐẶT (WebView2Loader.dll) - đây là lỗi ĐÓNG GÓI/CÀI ĐẶT của chính phần mềm thi, KHÔNG PHẢI do máy thiếu WebView2 Runtime hay thiếu Edge/Chrome/Firefox.\n\n" +
                      "Vui lòng gỡ cài đặt và cài lại phần mềm thi từ bộ cài gốc (không copy tay từng file lẻ). Nếu cài lại vẫn lỗi, hãy chụp dòng chi tiết bên dưới gửi cho người phụ trách kỹ thuật:\n" +
                      ex.GetType().Name + ": " + ex.Message;

                MessageBox.Show(msg, "Thiếu File Cài Đặt", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(0);
                return;
            }

            msg = "Không thể khởi tạo môi trường trình duyệt (Microsoft Edge WebView2 Runtime).\n\n" +
                         "Máy này nhiều khả năng CHƯA CÀI WebView2 Runtime - đây là thành phần bắt buộc của Windows để chạy phần mềm thi (khác với việc có cài Edge/Chrome/Firefox hay không).\n\n" +
                         "Bấm OK, trình duyệt mặc định sẽ tự mở trang tải WebView2 Runtime (miễn phí, chính thức từ Microsoft). Cài xong hãy mở lại phần mềm thi.\n\n" +
                         "Nếu đã cài rồi mà vẫn gặp lỗi này, hãy chụp lại dòng chi tiết bên dưới gửi cho người phụ trách kỹ thuật:\n" +
                         ex.GetType().Name + ": " + ex.Message;

            MessageBox.Show(msg, "Thiếu Thành Phần Bắt Buộc (WebView2 Runtime)", MessageBoxButton.OK, MessageBoxImage.Warning);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    UseShellExecute = true
                });
            }
            catch { }

            Environment.Exit(0);
        }

        private async Task<CoreWebView2Environment> CreateWebView2EnvironmentWithFallback(string userDataPath, CoreWebView2EnvironmentOptions options)
        {
            try
            {
                return await CoreWebView2Environment.CreateAsync(null, userDataPath, options);
            }
            catch (Exception)
            {
                string[] candidatePaths = {
                    @"C:\Program Files (x86)\Microsoft\Edge\Application",
                    @"C:\Program Files\Microsoft\Edge\Application",
                    @"C:\Program Files (x86)\Microsoft\Edge Beta\Application",
                    @"C:\Program Files (x86)\Microsoft\Edge Dev\Application",
                    @"C:\Program Files (x86)\Microsoft\Edge SxS\Application"
                };

                foreach (string basePath in candidatePaths)
                {
                    try
                    {
                        if (!Directory.Exists(basePath)) continue;
                        foreach (string versionDir in Directory.GetDirectories(basePath))
                        {
                            string folderName = Path.GetFileName(versionDir);
                            if (!char.IsDigit(folderName.Length > 0 ? folderName[0] : ' ')) continue;
                            if (!File.Exists(Path.Combine(versionDir, "msedge.exe"))) continue;
                            try
                            {
                                return await CoreWebView2Environment.CreateAsync(versionDir, userDataPath, options);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                throw;
            }
        }

        private async Task EnsureBrowserInitialized()
        {
            if (_isWebViewInitialized) return;

            if (VnoiBrowser != null)
            {
                string userDataPath = Path.Combine(Path.GetTempPath(), "VnoiKioskEnv_" + Guid.NewGuid().ToString("N"));
                var options = new CoreWebView2EnvironmentOptions("--disable-features=CalculateNativeWinOcclusion,IntensiveWakeUpThrottling,ElasticOverscroll");

                CoreWebView2Environment env;
                try
                {
                    env = await CreateWebView2EnvironmentWithFallback(userDataPath, options);
                    await VnoiBrowser.EnsureCoreWebView2Async(env);
                }
                catch (Exception ex)
                {
                    ShowWebView2MissingScreen(ex);
                    return;
                }
                
                if (VnoiBrowser.CoreWebView2 != null)
                {
                    VnoiBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    VnoiBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    VnoiBrowser.CoreWebView2.Settings.IsZoomControlEnabled = false;
                    VnoiBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    VnoiBrowser.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                    
                    VnoiBrowser.CoreWebView2.PermissionRequested += (s, ev) => { ev.State = CoreWebView2PermissionState.Allow; };
                    VnoiBrowser.CoreWebView2.DownloadStarting += (s, ev) => { ev.Cancel = true; ev.Handled = true; };
                    
                    VnoiBrowser.CoreWebView2.WebMessageReceived += Global_WebMessageReceived;
                    VnoiBrowser.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    VnoiBrowser.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged; 
                    VnoiBrowser.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
                    VnoiBrowser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                    VnoiBrowser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                    VnoiBrowser.CoreWebView2.ScriptDialogOpening += (s, ev) => { ev.Accept(); }; 

                    string blockSaveScript = @"
                        window.print = function() {}; 
                        document.addEventListener('keydown', function(e) { 
                            if((e.ctrlKey || e.metaKey) && (e.key.toLowerCase() === 'p' || e.key.toLowerCase() === 's' || e.key.toLowerCase() === 'u')) { 
                                e.preventDefault(); 
                                e.stopPropagation(); 
                            } 
                        }, true); 
                        document.addEventListener('dragover', function(e) { e.preventDefault(); e.stopPropagation(); if(e.dataTransfer) e.dataTransfer.dropEffect = 'none'; }, true); 
                        document.addEventListener('drop', function(e) { e.preventDefault(); e.stopPropagation(); }, true); 
                        document.addEventListener('paste', function(e) {
                            if (e.clipboardData && e.clipboardData.files && e.clipboardData.files.length > 0) {
                                e.preventDefault(); e.stopPropagation();
                            }
                        }, true);
                        try {
                            if (window.showOpenFilePicker) window.showOpenFilePicker = function() { return Promise.reject(new DOMException('Blocked', 'SecurityError')); };
                            if (window.showSaveFilePicker) window.showSaveFilePicker = function() { return Promise.reject(new DOMException('Blocked', 'SecurityError')); };
                            if (window.showDirectoryPicker) window.showDirectoryPicker = function() { return Promise.reject(new DOMException('Blocked', 'SecurityError')); };
                        } catch(e) {}
                        window.addEventListener('contextmenu', function(e) { e.preventDefault(); }, false);
                        window.onbeforeunload = null; 
                        window.addEventListener('beforeunload', function(e) { e.stopImmediatePropagation(); e.stopPropagation(); return undefined; }, true);
                        window.addEventListener('click', function(e) {
                            let t = e.target.closest('input[type=""file""]');
                            if(t) { e.preventDefault(); e.stopPropagation(); }
                        }, true);
                        function kioskDisableFileInputs() {
                            document.querySelectorAll('input[type=""file""]').forEach(function(el){ el.disabled = true; });
                        }
                        kioskDisableFileInputs();
                        const kioskFileObserver = new MutationObserver(function(){ kioskDisableFileInputs(); });
                        kioskFileObserver.observe(document.documentElement, { childList: true, subtree: true });
                        try {
                            const kioskOrigInputClick = HTMLInputElement.prototype.click;
                            HTMLInputElement.prototype.click = function() {
                                if (this.type === 'file') { return; }
                                return kioskOrigInputClick.apply(this, arguments);
                            };
                            const kioskOrigShowPicker = HTMLInputElement.prototype.showPicker;
                            if (kioskOrigShowPicker) {
                                HTMLInputElement.prototype.showPicker = function() {
                                    if (this.type === 'file') { return; }
                                    return kioskOrigShowPicker.apply(this, arguments);
                                };
                            }
                        } catch(e) {}
                    ";

                    await VnoiBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(blockSaveScript);
                    await VnoiBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(WebAutomation.GetGlobalInjectionScript(_username, GetLogoBase64()));

                    _isWebViewInitialized = true;
                }
            }
        }

        private async void ShowPreExamWarning()
        {
            if (_kioskManager != null && _kioskManager.IsRemoteSession())
            {
                MessageBox.Show("PHÁT HIỆN ĐIỀU KHIỂN TỪ XA (RDP)! Không thể chạy phần mềm thi.", "Lỗi Bảo Mật", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
                return;
            }

            if (LoginView != null) LoginView.Visibility = Visibility.Hidden;
            
            await EnsureBrowserInitialized();

            if (VnoiBrowser != null && VnoiBrowser.CoreWebView2 != null)
            {
                VnoiBrowser.Visibility = Visibility.Visible;
                VnoiBrowser.Margin = new Thickness(0);
                VnoiBrowser.Width = double.NaN;
                VnoiBrowser.Height = double.NaN;
                VnoiBrowser.HorizontalAlignment = HorizontalAlignment.Stretch;
                VnoiBrowser.VerticalAlignment = VerticalAlignment.Stretch;
                Panel.SetZIndex(VnoiBrowser, 9999);

                string warningHtml = @"<!DOCTYPE html>
                <html lang='vi'>
                <head>
                    <meta charset='utf-8'>
                    <style>body { font-family: 'Segoe UI', Arial, sans-serif !important; background:#050811; display:flex; justify-content:center; align-items:center; height:100vh; margin:0; overflow:hidden; } button { font-family: 'Segoe UI', Arial, sans-serif !important; }</style>
                </head>
                <body>
                    <div style='background:#1E1B4B; padding:30px 40px; border-radius:16px; border:2px solid #E11D48; max-width:550px; box-shadow: 0 0 40px rgba(225,29,72,0.3); text-align:center; transform: translateY(-10%);'>
                        <h1 style='color:#F43F5E; margin-top:0; font-size:26px; font-weight:900;'>⚠️ CẢNH BÁO HỆ THỐNG</h1>
                        <p style='color:#E2E8F0; font-size:14px; margin-bottom:15px; line-height:1.5;'>Hệ thống Kiosk sẽ <b>TỰ ĐỘNG BUỘC DỪNG (KILL)</b> tất cả các phần mềm có giao diện cửa sổ và phần mềm chạy ngầm.</p>
                        <p style='color:#FCA5A5; font-size:14px; margin-bottom:25px; line-height:1.5; font-weight:bold;'>Vui lòng LƯU LẠI công việc và ĐÓNG TẤT CẢ các chương trình đang mở trước khi bấm xác nhận.</p>
                        <div style='display:flex; gap:15px; justify-content:center;'>
                            <button onclick='window.chrome.webview.postMessage(""EXIT_KIOSK"")' style='flex:1; background:#334155; color:white; padding:14px; border:none; border-radius:8px; font-weight:bold; cursor:pointer; font-size:13px; transition: 0.2s;'>THOÁT KIOSK ĐỂ LƯU</button>
                            <button onclick='window.chrome.webview.postMessage(""ACKNOWLEDGE_WARNING"")' style='flex:1.5; background:linear-gradient(135deg, #E11D48, #9F1239); color:white; padding:14px; border:none; border-radius:8px; font-weight:bold; cursor:pointer; font-size:13px; box-shadow: 0 5px 15px rgba(225,29,72,0.3); transition: 0.2s;'>TÔI ĐÃ LƯU & ĐÓNG</button>
                        </div>
                    </div>
                </body>
                </html>";

                VnoiBrowser.CoreWebView2.NavigateToString(warningHtml);
            }
        }

        private void OpenToolWindow(string title, string url)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                if (_toolWindow != null) 
                { 
                    _toolWindow.Visibility = Visibility.Visible;
                    if (_toolWindow.WindowState == WindowState.Minimized)
                    {
                        _toolWindow.WindowState = WindowState.Normal;
                    }
                    _toolWindow.Activate();
                    _toolWindow.Focus();
                    return; 
                }

                _toolWindow = new Window {
                    Title = title, Width = 1100, Height = 700,
                    WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResizeWithGrip,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                    BorderThickness = new Thickness(2), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"))
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var header = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")), Cursor = Cursors.Hand };
                
                var hGrid = new Grid();
                var titleText = new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0), FontFamily = new FontFamily("Segoe UI") };
                
                var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                
                var minBtn = new Button { Content = "—", Width = 45, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 16 };
                minBtn.Click += (s, ev) => { _toolWindow.Visibility = Visibility.Hidden; };

                var maxBtn = new Button { Content = "🗖", Width = 45, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 16 };
                
                maxBtn.Click += (s, ev) => 
                {
                    if (_toolWindow.WindowState == WindowState.Maximized) { _toolWindow.WindowState = WindowState.Normal; maxBtn.Content = "🗖"; }
                    else { _toolWindow.WindowState = WindowState.Maximized; maxBtn.Content = "🗗"; }
                };
                
                header.MouseLeftButtonDown += (s, ev) => 
                { 
                    if (ev.ClickCount == 2) 
                    {
                        if (_toolWindow.WindowState == WindowState.Maximized) { _toolWindow.WindowState = WindowState.Normal; maxBtn.Content = "🗖"; }
                        else { _toolWindow.WindowState = WindowState.Maximized; maxBtn.Content = "🗗"; }
                    } 
                    else _toolWindow.DragMove(); 
                };

                var closeBtn = new Button { Content = "✖", Width = 45, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 16 };
                closeBtn.Click += (s, ev) => 
                { 
                    var res = MessageBox.Show("Bạn có chắc chắn muốn đóng Compiler này không? Mọi code chưa lưu sẽ bị mất.", "Xác nhận đóng", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        _toolWindow.Close(); 
                    }
                };
                
                btnStack.Children.Add(minBtn);
                btnStack.Children.Add(maxBtn);
                btnStack.Children.Add(closeBtn);

                hGrid.Children.Add(titleText);
                hGrid.Children.Add(btnStack);
                header.Child = hGrid;

                var wv = new WebView2();
                grid.Children.Add(header);
                grid.Children.Add(wv);
                Grid.SetRow(wv, 1);

                _toolWindow.Closed += (s, ev) => 
                {
                    _toolWindow = null;
                };

                _toolWindow.Content = grid;
                _toolWindow.Show();

                var envOptions = new CoreWebView2EnvironmentOptions("--disable-frame-rate-limit --enable-features=MaxFramerateUncapped");
                var env = await CreateWebView2EnvironmentWithFallback(Path.Combine(Path.GetTempPath(), "VnoiTool_" + Guid.NewGuid().ToString("N")), envOptions);
                try
                {
                    await wv.EnsureCoreWebView2Async(env);
                }
                catch (Exception exTool)
                {
                    ShowWebView2MissingScreen(exTool);
                    return;
                }

                try { wv.CoreWebView2.CookieManager.DeleteAllCookies(); } catch { }
                wv.CoreWebView2.DownloadStarting += (s, ev) => { ev.Cancel = true; ev.Handled = true; };

                string toolCleanupScript = @"
                    (function() {
                        try {
                            function hideEl(el) {
                                let target = el.closest('button, a, li') || el;
                                target.style.setProperty('display', 'none', 'important');
                            }
                            function run() {
                                try {
                                    document.querySelectorAll('a[href*=""/login""], a[href*=""/register""], a[href*=""/signup""], a[href*=""/sign-in""], a[href*=""/sign_up""]').forEach(hideEl);
                                    document.querySelectorAll('button, a').forEach(function(el) {
                                        let t = (el.textContent || '').trim().toLowerCase();
                                        if (['login','log in','sign in','sign up','register','đăng nhập','đăng ký','upgrade'].indexOf(t) >= 0) hideEl(el);
                                    });
                                    document.querySelectorAll('iframe[src*=""ads""], iframe[src*=""doubleclick""], [class*=""adsbygoogle""], [id*=""google_ads""], [class*=""advertisement""]').forEach(function(el){ el.style.setProperty('display','none','important'); });

                                    let host = window.location.hostname;
                                    if (host.indexOf('onlinegdb.com') >= 0) {
                                        let askAi = document.querySelector('#control-btn-askai');
                                        if (askAi) hideEl(askAi);
                                        let loginSpan = document.querySelector('#login_logout_span');
                                        if (loginSpan) hideEl(loginSpan);
                                    } else if (host.indexOf('onecompiler.com') >= 0) {
                                        document.querySelectorAll('button, [role=""tab""]').forEach(function(btn) {
                                            let t = (btn.textContent || '').trim();
                                            if (t === 'AI' || /^AI(\s|$)/.test(t)) hideEl(btn);
                                        });
                                    } else if (host.indexOf('onlineide.pro') >= 0) {
                                        document.querySelectorAll('.ri-sparkling-2-line, .pi-sign-in').forEach(function(icon) {
                                            let btn = icon.closest('button');
                                            if (btn) hideEl(btn);
                                        });
                                    }
                                } catch(e) {}
                            }
                            if (!window.__kioskToolCleanupInterval) {
                                window.__kioskToolCleanupInterval = setInterval(run, 200);
                            }
                            run();
                            document.addEventListener('DOMContentLoaded', run);
                            window.addEventListener('load', run);
                        } catch(e) {}

                        try {
                            window.print = function() {};
                            document.addEventListener('keydown', function(e) {
                                if((e.ctrlKey || e.metaKey) && (e.key.toLowerCase() === 'p' || e.key.toLowerCase() === 's' || e.key.toLowerCase() === 'u')) {
                                    e.preventDefault();
                                    e.stopPropagation();
                                }
                            }, true);
                            document.addEventListener('dragover', function(e) { e.preventDefault(); e.stopPropagation(); if(e.dataTransfer) e.dataTransfer.dropEffect = 'none'; }, true);
                            document.addEventListener('drop', function(e) { e.preventDefault(); e.stopPropagation(); }, true);
                            document.addEventListener('paste', function(e) {
                                if (e.clipboardData && e.clipboardData.files && e.clipboardData.files.length > 0) {
                                    e.preventDefault(); e.stopPropagation();
                                }
                            }, true);
                        } catch(e) {}

                        try {
                            if (window.showOpenFilePicker) window.showOpenFilePicker = function() { return Promise.reject(new DOMException('Blocked', 'SecurityError')); };
                            if (window.showSaveFilePicker) window.showSaveFilePicker = function() { return Promise.reject(new DOMException('Blocked', 'SecurityError')); };
                            if (window.showDirectoryPicker) window.showDirectoryPicker = function() { return Promise.reject(new DOMException('Blocked', 'SecurityError')); };
                        } catch(e) {}

                        try {
                            window.addEventListener('click', function(e) {
                                let t = e.target.closest('input[type=""file""]');
                                if(t) { e.preventDefault(); e.stopPropagation(); }
                            }, true);
                            function kioskToolDisableFileInputs() {
                                document.querySelectorAll('input[type=""file""]').forEach(function(el){ el.disabled = true; });
                            }
                            kioskToolDisableFileInputs();
                            const kioskToolFileObserver = new MutationObserver(function(){ kioskToolDisableFileInputs(); });
                            kioskToolFileObserver.observe(document.documentElement, { childList: true, subtree: true });
                        } catch(e) {}

                        try {
                            const kioskToolOrigClick = HTMLInputElement.prototype.click;
                            HTMLInputElement.prototype.click = function() {
                                if (this.type === 'file') { return; }
                                return kioskToolOrigClick.apply(this, arguments);
                            };
                            const kioskToolOrigShowPicker = HTMLInputElement.prototype.showPicker;
                            if (kioskToolOrigShowPicker) {
                                HTMLInputElement.prototype.showPicker = function() {
                                    if (this.type === 'file') { return; }
                                    return kioskToolOrigShowPicker.apply(this, arguments);
                                };
                            }
                        } catch(e) {}
                    })();
                ";
                await wv.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(toolCleanupScript);
                
                wv.CoreWebView2.NavigationStarting += (sender, args) =>
                {
                    string checkUri = args.Uri.ToLower();
                    if (checkUri.Contains("/auth/login") || checkUri.Contains("accounts.google.com") || checkUri.Contains("facebook.com") || checkUri.Contains("github.com"))
                    {
                        args.Cancel = true;
                    }
                };

                wv.CoreWebView2.NewWindowRequested += (sender, args) =>
                {
                    args.Handled = true;
                    wv.CoreWebView2.Navigate(args.Uri);
                };

                wv.CoreWebView2.Navigate(url);
            });
        }

        private void Global_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string actionStr = "";
                try { actionStr = e.TryGetWebMessageAsString(); } catch { actionStr = e.WebMessageAsJson; }

                if (string.IsNullOrEmpty(actionStr)) return;

                if (actionStr.Contains("HIDE_TOOL_WINDOW"))
                {
                    Dispatcher.Invoke(() => {
                        if (_toolWindow != null && _toolWindow.Visibility == Visibility.Visible)
                        {
                            _toolWindow.Visibility = Visibility.Hidden;
                        }
                    });
                }
                else if (actionStr.Contains("ACKNOWLEDGE_WARNING"))
                {
                    if (VnoiBrowser != null)
                    {
                        VnoiBrowser.Visibility = Visibility.Hidden;
                        VnoiBrowser.Margin = new Thickness(-99999, -99999, 0, 0);
                        VnoiBrowser.Width = 1;
                        VnoiBrowser.Height = 1;
                    }
                    if (LoginView != null) LoginView.Visibility = Visibility.Visible;
                }
                else if (actionStr.Contains("EXIT_KIOSK") || actionStr.Contains("CLOSE_APP"))
                {
                    FullExitApp();
                }
                else if (actionStr.Contains("OPEN_TOOL"))
                {
                    try
                    {
                        var obj = JObject.Parse(actionStr);
                        string title = obj["title"]?.ToString() ?? "Cửa Sổ Phụ";
                        string url = obj["url"]?.ToString() ?? "about:blank";
                        OpenToolWindow(title, url);
                    }
                    catch { }
                }
                else if (actionStr.Contains("SAVE_DEVICES:"))
                {
                    string idxStr = actionStr.Substring(actionStr.IndexOf("SAVE_DEVICES:") + 13).Trim('"', '}', ' ', '\\');
                    if (int.TryParse(idxStr, out int index) && index >= 0 && index < _availableExams.Count)
                    {
                        _currentExam = _availableExams[index];
                    }
                    else if (_availableExams.Count > 0)
                    {
                        _currentExam = _availableExams[0];
                    }

                    LocalAuthData authData = _kioskManager?.LoadData() ?? new LocalAuthData();
                    authData.Username = _username; authData.Password = _password; authData.AccessKey = _accessKey;
                    if (_kioskManager != null) _kioskManager.SaveData(authData);
                    
                    if (ConfirmPopup != null) ConfirmPopup.Visibility = Visibility.Collapsed;
                    if (VnoiBrowser != null) { VnoiBrowser.Visibility = Visibility.Hidden; VnoiBrowser.Margin = new Thickness(-99999, -99999, 0, 0); VnoiBrowser.Width = 1; VnoiBrowser.Height = 1; }
                    
                    StartContest();
                }
                else if (actionStr.Contains("CONTEST_READY"))
                {
                    if (_navigationState != 3) 
                    {
                        string currentUrlCheck = VnoiBrowser?.Source?.ToString() ?? "";
                        string contestPathCheck = GetContestPath();
                        bool reallyReady = !string.IsNullOrEmpty(contestPathCheck) && currentUrlCheck.ToLower().Contains(contestPathCheck.ToLower());

                        if (reallyReady)
                        {
                            _navigationState = 3;
                            FinalizeExamEnvironment();
                        }
                    }
                }
                else if (actionStr.Contains("AUTH_ERROR"))
                {
                    ResetToLoginScreen();
                    ShowErrorPopup("Tài khoản, Mật khẩu hoặc Mã truy cập cuộc thi không chính xác!");
                }
                else if (actionStr.Contains("EXIT_CONTEST_SUCCESS")) 
                {
                    StopPingLoop();
                    if (_kioskManager != null) {
                        _kioskManager.ClearBlackBox();
                    }
                    try
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            var content = new StringContent(JsonConvert.SerializeObject(new { username = _username, status = "SUCCESS" }), Encoding.UTF8, "application/json");
                            Task<HttpResponseMessage> task = client.PostAsync($"{BASE_URL}/api/webhook/exit_success", content);
                        }
                    } catch { }
                    ShowExitScreen();
                }
            }
            catch { }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_kioskManager != null) _kioskManager.EnableDRM(this);

            if (_kioskManager != null)
            {
                LocalAuthData authData = _kioskManager.LoadData();
                if (authData != null && !string.IsNullOrEmpty(authData.Username) && !string.IsNullOrEmpty(authData.AccessKey))
                {
                    if (UsernameInput != null) { UsernameInput.Text = authData.Username; UsernameInput.Foreground = Brushes.White; }
                    if (PasswordInputHidden != null) { PasswordInputHidden.Password = authData.Password; PasswordInputHidden.Foreground = Brushes.White; }
                    if (AccessKeyInput != null) { AccessKeyInput.Text = authData.AccessKey; AccessKeyInput.Foreground = Brushes.White; }
                }
            }
        }

        private void BtnToggleEye_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordInputHidden != null && PasswordInputVisible != null && BtnToggleEye != null)
            {
                if (PasswordInputHidden.Visibility == Visibility.Visible)
                {
                    PasswordInputVisible.Text = PasswordInputHidden.Password;
                    PasswordInputHidden.Visibility = Visibility.Collapsed;
                    PasswordInputVisible.Visibility = Visibility.Visible;
                    BtnToggleEye.Content = "🙈";
                }
                else
                {
                    PasswordInputHidden.Password = PasswordInputVisible.Text;
                    PasswordInputVisible.Visibility = Visibility.Collapsed;
                    PasswordInputHidden.Visibility = Visibility.Visible;
                    BtnToggleEye.Content = "👁️";
                }
            }
        }

        private string GetPasswordText()
        {
            if (PasswordInputHidden != null && PasswordInputHidden.Visibility == Visibility.Visible) return PasswordInputHidden.Password;
            if (PasswordInputVisible != null) return PasswordInputVisible.Text;
            return "";
        }

        private void ClearPasswordText()
        {
            if (PasswordInputHidden != null) PasswordInputHidden.Password = "";
            if (PasswordInputVisible != null) PasswordInputVisible.Text = "";
        }

        private string GetContestPath()
        {
            if (_currentExam == null || string.IsNullOrEmpty(_currentExam.ContestLink)) return "";
            try { return new Uri(_currentExam.ContestLink).AbsolutePath.TrimEnd('/'); } catch { return ""; }
        }

        private string GetLogoBase64()
        {
            try 
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "1.png");
                if (!File.Exists(path))
                {
                    string searchDir = AppDomain.CurrentDomain.BaseDirectory;
                    while (Directory.GetParent(searchDir) != null)
                    {
                        searchDir = Directory.GetParent(searchDir).FullName;
                        path = Path.Combine(searchDir, "Assets", "1.png");
                        if (File.Exists(path)) break;
                    }
                }

                if (File.Exists(path))
                {
                    byte[] imgData = File.ReadAllBytes(path);
                    return "data:image/png;base64," + Convert.ToBase64String(imgData);
                }
            } catch { }
            return "";
        }

        private async void StartContest()
        {
            if (LoginView != null) LoginView.Visibility = Visibility.Collapsed;
            if (ContestView != null) ContestView.Visibility = Visibility.Collapsed;
            if (Page1_Dashboard != null) Page1_Dashboard.Visibility = Visibility.Collapsed;
            if (ConfirmPopup != null) ConfirmPopup.Visibility = Visibility.Collapsed;
            
            ShowLoadingOverlay();
            
            if (_currentExam != null && _kioskManager != null) _kioskManager.WriteBlackBox(_username, _currentExam.ContestLink);

            _ = StartPingLoop();

            await Task.Delay(1000);

            if (VnoiBrowser != null && VnoiBrowser.CoreWebView2 != null && _currentExam != null)
            {
                VnoiBrowser.Visibility = Visibility.Hidden;
                VnoiBrowser.Margin = new Thickness(-99999, -99999, 0, 0);
                VnoiBrowser.Width = 1;
                VnoiBrowser.Height = 1;
                Panel.SetZIndex(VnoiBrowser, 0);

                _navigationState = 1;
                Uri uri = new Uri(_currentExam.ContestLink);
                string loginUrl = $"{uri.Scheme}://{uri.Host}/accounts/login/?next={uri.AbsolutePath.TrimEnd('/')}/join";
                VnoiBrowser.CoreWebView2.NavigateToString(@"<html><body><script>window.location.href='" + loginUrl + @"';</script></body></html>");
            }

            _ = Task.Run(async () => 
            {
                await Task.Delay(25000);
                Dispatcher.Invoke(() => 
                {
                    if (_navigationState != 3 && _navigationState != 4)
                    {
                        string currentUrl = VnoiBrowser?.Source?.ToString() ?? "";
                        string contestPath = GetContestPath();
                        bool looksReady = !string.IsNullOrEmpty(contestPath) && currentUrl.ToLower().Contains(contestPath.ToLower());

                        if (looksReady)
                        {
                            _navigationState = 3;
                            FinalizeExamEnvironment();
                        }
                        else
                        {
                            StopPingLoop();
                            ResetToLoginScreen();
                            ShowErrorPopup("Không thể tự động vào phòng thi. Vui lòng kiểm tra kết nối mạng và thử lại.");
                        }
                    }
                });
            });
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            string uri = e.Uri.ToLower();

            if (uri.Contains("accounts.google.com") || uri.Contains("facebook.com") || uri.Contains("github.com"))
            {
                e.Cancel = true;
                return;
            }

            try
            {
                if (uri.Contains("programiz.com") || uri.Contains("onlineide.pro") || uri.Contains("online-ide.com") || uri.Contains("onlinegdb.com") || uri.Contains("cpp.sh"))
                {
                    Uri parsedUri = new Uri(uri);
                    if (parsedUri.AbsolutePath == "/" || string.IsNullOrEmpty(parsedUri.AbsolutePath))
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            catch { }

            if (_navigationState == 3)
            {
                if (e.NavigationKind == CoreWebView2NavigationKind.BackOrForward && (uri.Contains("/login") || uri.Contains("/join")))
                {
                    e.Cancel = true;
                }
            }
        }

        private async void CoreWebView2_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (VnoiBrowser != null && VnoiBrowser.CoreWebView2 != null)
            {
                await WebAutomation.SpamInjectKioskUIAsync(VnoiBrowser.CoreWebView2, _username, GetLogoBase64());
                if (_currentExam != null) VnoiBrowser.CoreWebView2.PostWebMessageAsString("SET_CONTEST_URL:" + _currentExam.ContestLink);
            }
        }

        private async void CoreWebView2_HistoryChanged(object sender, object e)
        {
            if (VnoiBrowser != null && VnoiBrowser.CoreWebView2 != null)
            {
                await WebAutomation.SpamInjectKioskUIAsync(VnoiBrowser.CoreWebView2, _username, GetLogoBase64());
                if (_currentExam != null) VnoiBrowser.CoreWebView2.PostWebMessageAsString("SET_CONTEST_URL:" + _currentExam.ContestLink);
            }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
        }

        private async void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (VnoiBrowser == null || VnoiBrowser.CoreWebView2 == null) return;
            string currentUrl = VnoiBrowser.Source?.ToString() ?? "";
            string contestPath = GetContestPath();

            await VnoiBrowser.CoreWebView2.ExecuteScriptAsync(@"let errorDiv = document.querySelector('.form-errors') || document.querySelector('.alert-danger'); let bodyText = document.body.innerText; if((errorDiv && errorDiv.innerText.includes('Wrong access code')) || bodyText.includes('Tài khoản hoặc mật khẩu không chính xác') || bodyText.includes('vô hiệu hóa') || bodyText.includes('không chính xác')) { window.chrome.webview.postMessage('AUTH_ERROR'); }");

            Uri currentUri;
            try { currentUri = new Uri(currentUrl); } catch { return; }
            string path = currentUri.AbsolutePath.ToLower();

            if (_navigationState == 1 || _navigationState == 2)
            {
                if (path.EndsWith("/login") || path.EndsWith("/login/"))
                {
                    _navigationState = 2;
                    await WebAutomation.ExecuteLoginAsync(VnoiBrowser.CoreWebView2, _username, _password);
                }
                else if (path.Contains("/join") || path.Contains("/participate") || path.Contains("/enter"))
                {
                    _navigationState = 2;
                    if (_currentExam != null) await WebAutomation.ExecuteSubmitAccessCodeAsync(VnoiBrowser.CoreWebView2, _currentExam.AccessCode);
                }
                else if (!string.IsNullOrEmpty(contestPath) && path.Contains(contestPath))
                {
                    if (_navigationState != 3)
                    {
                        _navigationState = 3;
                        FinalizeExamEnvironment();
                    }
                }
            }
        }

        private void FinalizeExamEnvironment()
        {
            Dispatcher.Invoke(() => 
            {
                HideLoadingOverlay();
                
                if (LoginView != null) LoginView.Visibility = Visibility.Collapsed;
                if (Page1_Dashboard != null) Page1_Dashboard.Visibility = Visibility.Collapsed;
                if (Page2_Loading != null) Page2_Loading.Visibility = Visibility.Collapsed;
                if (ConfirmPopup != null) ConfirmPopup.Visibility = Visibility.Collapsed;
                if (ContestView != null) ContestView.Visibility = Visibility.Visible;
                Topmost = true;

                Application.Current.Dispatcher.Invoke(() => { try { Clipboard.Clear(); } catch { } });
                
                if (VnoiBrowser != null)
                {
                    VnoiBrowser.Visibility = Visibility.Visible;
                    VnoiBrowser.Margin = new Thickness(0);
                    VnoiBrowser.Width = double.NaN;
                    VnoiBrowser.Height = double.NaN;
                    VnoiBrowser.HorizontalAlignment = HorizontalAlignment.Stretch;
                    VnoiBrowser.VerticalAlignment = VerticalAlignment.Stretch;
                    Panel.SetZIndex(VnoiBrowser, 10);
                }
            });
        }

        private void ShowExitScreen()
        {
            Dispatcher.Invoke(() => 
            {
                _navigationState = 4;
                HideLoadingOverlay();
                
                if (_toolWindow != null) { _toolWindow.Close(); _toolWindow = null; }

                if (VnoiBrowser != null) 
                { 
                    VnoiBrowser.Visibility = Visibility.Hidden;
                    VnoiBrowser.Margin = new Thickness(-99999, -99999, 0, 0); 
                    VnoiBrowser.Width = 1; 
                    VnoiBrowser.Height = 1; 
                }

                if (LoginView != null) LoginView.Visibility = Visibility.Collapsed;
                if (ContestView != null) ContestView.Visibility = Visibility.Collapsed;
                if (Page1_Dashboard != null) Page1_Dashboard.Visibility = Visibility.Collapsed;
                if (Page2_Loading != null) Page2_Loading.Visibility = Visibility.Collapsed;
                if (ConfirmPopup != null) ConfirmPopup.Visibility = Visibility.Collapsed;

                if (Page4_ExitScreen != null) 
                {
                    Page4_ExitScreen.Visibility = Visibility.Visible;
                    Panel.SetZIndex(Page4_ExitScreen, 99999);
                    AttachExitHandlers(Page4_ExitScreen);
                }
                
                if (VnoiBrowser != null && VnoiBrowser.CoreWebView2 != null)
                {
                    VnoiBrowser.Visibility = Visibility.Visible;
                    VnoiBrowser.Margin = new Thickness(0);
                    VnoiBrowser.Width = double.NaN;
                    VnoiBrowser.Height = double.NaN;
                    Panel.SetZIndex(VnoiBrowser, 99999);
                    
                    string exitHtml = @"<!DOCTYPE html>
                    <html lang='vi'>
                    <head>
                        <meta charset='utf-8'>
                        <style>body { font-family: 'Segoe UI', Arial, sans-serif !important; background: #050811; color: white; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; overflow: hidden; overscroll-behavior: none; } button { font-family: 'Segoe UI', Arial, sans-serif !important; }</style>
                    </head>
                    <body>
                        <button onclick='window.chrome.webview.postMessage(""CLOSE_APP"")' style='position:absolute; top:20px; right:30px; background:transparent; border:none; color:#94A3B8; font-size:32px; font-weight:bold; cursor:pointer; transition:0.2s;' onmouseover=""this.style.color='#EF4444'"" onmouseout=""this.style.color='#94A3B8'"">✖</button>
                        <div style='text-align:center;'>
                            <div style='font-size:50px;margin-bottom:15px;'>🎓</div>
                            <h1 style='color:#10B981;font-size:32px;margin:0;font-weight:900;text-transform:uppercase;'>NỘP BÀI THÀNH CÔNG</h1>
                            <p style='color:#94A3B8;font-size:15px;margin-top:10px;line-height:1.5;'>Hệ thống Kiosk đã ghi nhận bài làm và đồng bộ dữ liệu an toàn.<br/>Bộ nhớ đệm đã được xóa. Bạn có thể thoát phần mềm.</p>
                            <button onclick='window.chrome.webview.postMessage(""CLOSE_APP"")' style='margin-top:30px;background:linear-gradient(135deg, #EF4444, #B91C1C);color:white;border:none;padding:15px 35px;border-radius:10px;font-size:16px;font-weight:bold;cursor:pointer;box-shadow:0 10px 25px rgba(239, 68, 68, 0.4);transition:all 0.3s;display:inline-flex;align-items:center;gap:10px;' onmouseover=""this.style.transform='scale(1.05)'"" onmouseout=""this.style.transform='scale(1)'""><span style='font-size:18px;'>✖</span> THOÁT KIOSK</button>
                        </div>
                    </body>
                    </html>";
                    VnoiBrowser.CoreWebView2.NavigateToString(exitHtml);
                }
            });
        }

        private void AttachExitHandlers(DependencyObject parent)
        {
            if (parent == null) return;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Button btn)
                {
                    btn.Click -= BtnEmergencyExit_Click;
                    btn.Click += BtnEmergencyExit_Click;
                }
                AttachExitHandlers(child);
            }
        }

        private void ShowErrorPopup(string msg)
        {
            HideLoadingOverlay();
            if (Page2_Loading != null) Page2_Loading.Visibility = Visibility.Hidden;
            if (VnoiBrowser != null) 
            { 
                VnoiBrowser.Visibility = Visibility.Hidden;
                VnoiBrowser.Margin = new Thickness(-99999, -99999, 0, 0); 
                VnoiBrowser.Width = 1; 
                VnoiBrowser.Height = 1; 
            }
            if (ErrorPopup != null)
            {
                if (lblErrorMsg != null) lblErrorMsg.Text = msg;
                ErrorPopup.Visibility = Visibility.Visible;
                ErrorPopup.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation { From = 0.0, To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(300)) });
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e) { TextBox txt = (TextBox)sender; if (txt.Text == "Tài khoản VNOI" || txt.Text == "Access Key") { txt.Text = ""; txt.Foreground = Brushes.White; } }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e) { TextBox txt = (TextBox)sender; if (string.IsNullOrWhiteSpace(txt.Text)) { txt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94a3b8")); if (txt == UsernameInput) txt.Text = "Tài khoản VNOI"; else if (txt == AccessKeyInput) txt.Text = "Access Key"; } }

        private async void BtnProceedToContests_Click(object sender, RoutedEventArgs e)
        {
            if (UsernameInput == null || TxtCurrentUser == null || LoginView == null || ContestView == null) return;
            _username = UsernameInput.Text; _password = GetPasswordText(); _accessKey = AccessKeyInput.Text;
            if (_username == "Tài khoản VNOI" || _accessKey == "Access Key" || string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password) || string.IsNullOrWhiteSpace(_accessKey))
            {
                ShowErrorPopup("Vui lòng nhập đầy đủ Tài khoản, Mật khẩu và Access Key!");
                return;
            }
            if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = false; BtnProceedToContests.Content = "ĐANG XÁC THỰC..."; }
            try
            {
                using (HttpClient http = new HttpClient())
                {
                    string localIp = _kioskManager != null ? _kioskManager.GetLocalIPAddress() : "Unknown";
                    var content = new StringContent(JsonConvert.SerializeObject(new VerifyRequest { Username = _username, AccessKey = _accessKey, MachineId = Environment.MachineName, IpAddress = localIp }), Encoding.UTF8, "application/json");
                    var response = await http.PostAsync($"{BASE_URL}/api/kiosk/verify", content);
                    JObject resObj = JObject.Parse(await response.Content.ReadAsStringAsync());

                    if (resObj["success"]?.Value<bool>() ?? false)
                    {
                        JArray examsArray = (JArray)(resObj["exams"] ?? new JArray());
                        _availableExams.Clear();
                        foreach (var exam in examsArray)
                        {
                            _availableExams.Add(new ExamData { Title = exam["title"]?.ToString() ?? "", ContestLink = exam["contest_link"]?.ToString() ?? "", AccessCode = exam["access_code"]?.ToString() ?? "" });
                        }

                        if (_availableExams.Count > 0)
                        {
                            if (_kioskManager != null && _kioskManager.IsVirtualMachine())
                            {
                                ShowErrorPopup("Hệ thống phát hiện phần mềm đang chạy trong MÁY ẢO (VirtualBox/VMware...). Vì lý do bảo mật kỳ thi, vui lòng sử dụng máy tính thật để tham gia.");
                                if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = true; BtnProceedToContests.Content = "XÁC THỰC VÀ KẾT NỐI"; }
                                return;
                            }

                            if (_kioskManager != null)
                            {
                                _kioskManager.MuteSystemAudio();
                                _kioskManager.KillExplorer();
                                _kioskManager.StartAntiCheat();
                                _kioskManager.EnableKioskMode();
                                _kioskManager.LockSecondaryMonitors(this);
                            }

                            if (lblStatus != null) { lblStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981")); lblStatus.Text = "Xác thực thành công!"; }
                            LocalAuthData data = _kioskManager?.LoadData() ?? new LocalAuthData();
                            data.Username = _username; data.Password = _password; data.AccessKey = _accessKey;
                            if (_kioskManager != null) _kioskManager.SaveData(data);

                            if (lblExamTitle != null) lblExamTitle.Text = $"CÓ {_availableExams.Count} BÀI THI ĐANG MỞ";
                            TxtCurrentUser.Text = _username;
                            PlayFadeAnimation(LoginView, ContestView);
                        }
                        else
                        {
                            ShowErrorPopup("Hệ thống chưa có bài thi nào được phát cho Access Key này.");
                            if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = true; BtnProceedToContests.Content = "XÁC THỰC VÀ KẾT NỐI"; }
                        }
                    }
                    else
                    {
                        ShowErrorPopup(resObj["error"]?.ToString() ?? "Lỗi xác thực từ máy chủ.");
                        if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = true; BtnProceedToContests.Content = "XÁC THỰC VÀ KẾT NỐI"; }
                    }
                }
            }
            catch
            {
                ShowErrorPopup("Không thể kết nối đến Máy chủ Server.");
                if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = true; BtnProceedToContests.Content = "XÁC THỰC VÀ KẾT NỐI"; }
            }
        }

        private void BtnSelectContest_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmPopup != null)
            {
                ConfirmPopup.Visibility = Visibility.Visible;
                ConfirmPopup.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation { From = 0.0, To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(300)) });
            }
        }

        private void BtnConfirmNo_Click(object sender, RoutedEventArgs e) { if (ConfirmPopup != null) ConfirmPopup.Visibility = Visibility.Hidden; }

        private async void BtnConfirmYes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ConfirmPopup != null) ConfirmPopup.Visibility = Visibility.Hidden;
                await EnsureBrowserInitialized();

                string examOptionsHtml = "";
                for (int i = 0; i < _availableExams.Count; i++)
                {
                    examOptionsHtml += $"<div class='exam-option' data-value='{i}' onclick='selectExam({i}, this)'>{_availableExams[i].Title}</div>";
                }
                string firstExamTitle = _availableExams.Count > 0 ? _availableExams[0].Title : "";

                string rawHtml = @"<!DOCTYPE html>
                <html lang='vi'>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body { background: radial-gradient(circle at top right, #0F172A 0%, #050811 100%); color: #F8FAFC; font-family: 'Segoe UI', Arial, sans-serif !important; height: 100vh; display: flex; justify-content: center; align-items: center; margin: 0; overflow: hidden; overscroll-behavior: none; }
                        button, select { font-family: 'Segoe UI', Arial, sans-serif !important; }
                        .card { background: rgba(11,17,32,0.85); backdrop-filter: blur(20px); padding: 35px; border-radius: 24px; border: 1px solid #1E293B; box-shadow: 0 30px 60px rgba(0,0,0,0.6); width: 100%; max-width: 400px; display: flex; flex-direction: column; position: relative; overflow: hidden; animation: slideUp 0.6s cubic-bezier(0.16,1,0.3,1); }
                        @keyframes slideUp { 0% { opacity: 0; transform: translateY(40px); } 100% { opacity: 1; transform: translateY(0); } }
                        .card::before { content: ''; position: absolute; top: 0; left: -100%; width: 100%; height: 3px; background: linear-gradient(90deg, transparent, #38BDF8, transparent); animation: scan 3s linear infinite; }
                        @keyframes scan { 100% { left: 100%; } }
                        @keyframes spin { 100% { transform:rotate(360deg); } }
                        button.btn-primary { background: linear-gradient(135deg, #2563EB, #1D4ED8); color: white; padding: 15px; border: none; border-radius: 12px; font-weight: 900; cursor: pointer; width: 100%; font-size: 15px; letter-spacing: 1px; box-shadow: 0 8px 20px rgba(37, 99, 235, 0.4); transition: all 0.3s; margin-top: auto; }
                        button.btn-primary:hover { transform: translateY(-3px); box-shadow: 0 12px 25px rgba(37, 99, 235, 0.6); }
                        .exam-option { padding:12px 14px; color:#E2E8F0; cursor:pointer; font-weight:600; font-size:14px; border-bottom:1px solid #334155; }
                        .exam-option:last-child { border-bottom:none; }
                        .exam-option:hover { background:#334155; }
                        #examTrigger:focus { border-color: #38BDF8 !important; box-shadow: 0 0 0 3px rgba(56,189,248,0.2); }
                    </style>
                </head>
                <body>
                    <div class='card'>
                        <div style='text-align:center; margin-bottom: 25px;'>
                            <div style='background:#1E293B; width:55px; height:55px; border-radius:16px; display:flex; justify-content:center; align-items:center; margin:0 auto 15px auto; font-size:26px; box-shadow: 0 8px 20px rgba(0,0,0,0.4); transform: rotate(-10deg);'>📋</div>
                            <h2 style='margin:0; color:#F8FAFC; font-size:22px; font-weight:900; letter-spacing:0.5px;'>CHỌN BÀI THI</h2>
                        </div>
                        <div style='background:rgba(15,23,42,0.6); padding:18px; border-radius:12px; border:1px solid #334155; margin-bottom: 25px;'>
                            <div id='examDropdown' style='position:relative;'>
                                <div id='examTrigger' tabindex='0' onclick='toggleExamDropdown()' style='width:100%; padding:14px; background:#1E293B; color:white; border:2px solid #475569; border-radius:10px; outline:none; font-size:15px; font-weight:600; cursor:pointer; box-sizing:border-box; display:flex; align-items:center; justify-content:space-between;'>
                                    <span id='examTriggerText'>[FIRST_EXAM_TITLE]</span>
                                    <span style='color:#38BDF8;'>▾</span>
                                </div>
                                <div id='examOptionsList' style='display:none; position:absolute; top:calc(100% + 6px); left:0; right:0; background:#1E293B; border:2px solid #475569; border-radius:10px; overflow-y:auto; max-height:220px; z-index:10; box-shadow:0 15px 35px rgba(0,0,0,0.5);'>[EXAM_OPTIONS]</div>
                                <input type='hidden' id='examSelect' value='0' />
                            </div>
                        </div>
                        <div style='display:flex; gap:15px; align-items:center; background:rgba(16,185,129,0.1); padding:15px; border-radius:12px; border:1px solid rgba(16,185,129,0.2); margin-bottom: 25px;'>
                            <div style='width:22px; height:22px; border:3px solid #10B981; border-top-color:transparent; border-radius:50%; animation:spin 1s linear infinite; flex-shrink:0;'></div>
                            <div>
                                <p style='margin:0; color:#10B981; font-weight:800; font-size:13px; letter-spacing:0.5px;'>HỆ THỐNG SẴN SÀNG</p>
                                <p style='margin:4px 0 0 0; color:#94A3B8; font-size:12px;'>Màn hình sẽ được tự động ghi lại.</p>
                            </div>
                        </div>
                        <button onclick='save(this)' class='btn-primary'>XÁC NHẬN VÀO THI ➔</button>
                    </div>
                    <script>
                        function toggleExamDropdown() {
                            let list = document.getElementById('examOptionsList');
                            list.style.display = (list.style.display === 'none' || !list.style.display) ? 'block' : 'none';
                        }
                        function selectExam(idx, el) {
                            document.getElementById('examSelect').value = idx;
                            document.getElementById('examTriggerText').textContent = el.textContent;
                            document.getElementById('examOptionsList').style.display = 'none';
                        }
                        document.addEventListener('click', function(e) {
                            let dd = document.getElementById('examDropdown');
                            if (dd && !dd.contains(e.target)) {
                                document.getElementById('examOptionsList').style.display = 'none';
                            }
                        });
                        function save(btn) { 
                            try {
                                btn.innerHTML='ĐANG XỬ LÝ...'; btn.style.opacity='0.7'; btn.style.pointerEvents='none'; 
                                window.chrome.webview.postMessage('SAVE_DEVICES:' + document.getElementById('examSelect').value); 
                            } catch(err) { }
                        }
                    </script>
                </body>
                </html>";

                _setupHtml = rawHtml.Replace("[EXAM_OPTIONS]", examOptionsHtml).Replace("[FIRST_EXAM_TITLE]", firstExamTitle);
                
                if (Page1_Dashboard != null) Page1_Dashboard.Visibility = Visibility.Collapsed;
                if (VnoiBrowser != null)
                {
                    VnoiBrowser.Visibility = Visibility.Visible;
                    VnoiBrowser.Margin = new Thickness(0); 
                    VnoiBrowser.Width = double.NaN; 
                    VnoiBrowser.Height = double.NaN; 
                    VnoiBrowser.HorizontalAlignment = HorizontalAlignment.Stretch; 
                    VnoiBrowser.VerticalAlignment = VerticalAlignment.Stretch;
                    Panel.SetZIndex(VnoiBrowser, 9999);
                    
                    if (VnoiBrowser.CoreWebView2 != null) VnoiBrowser.CoreWebView2.NavigateToString(_setupHtml);
                }
            } catch { ResetToLoginScreen(); }
        }

        private void BtnErrorAcknowledge_Click(object sender, RoutedEventArgs e)
        {
            if (ErrorPopup != null) ErrorPopup.Visibility = Visibility.Hidden;
            if (LoginView != null) { LoginView.Visibility = Visibility.Visible; LoginView.Opacity = 1; }
            if (ContestView != null) ContestView.Visibility = Visibility.Hidden;
            ClearPasswordText();
            if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = true; BtnProceedToContests.Content = "XÁC THỰC VÀ KẾT NỐI"; }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Hệ thống SEB Kiosk chuyên biệt cho nền tảng VNOI.\nSản Phẩm tạo bởi đội ngũ CTNS Development\nPhiên bản Enterprise 2026", "Giới thiệu", MessageBoxButton.OK, MessageBoxImage.Information); }
        
        private void BtnEmergencyExit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn thoát phần mềm Kiosk không?", "Xác Nhận Thoát", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) FullExitApp();
        }

        private void ResetToLoginScreen()
        {
            try
            {
                StopPingLoop();
                HideLoadingOverlay();
                if (Page1_Dashboard != null) Page1_Dashboard.Visibility = Visibility.Visible;
                if (Page4_ExitScreen != null) Page4_ExitScreen.Visibility = Visibility.Hidden;
                if (Page2_Loading != null) Page2_Loading.Visibility = Visibility.Hidden;
                
                if (VnoiBrowser != null) 
                { 
                    VnoiBrowser.Visibility = Visibility.Hidden;
                    VnoiBrowser.Margin = new Thickness(-99999, -99999, 0, 0); 
                    VnoiBrowser.Width = 1; 
                    VnoiBrowser.Height = 1; 
                    try { VnoiBrowser.CoreWebView2?.Navigate("about:blank"); } catch { }
                }
                
                if (LoginView != null) { LoginView.Visibility = Visibility.Visible; LoginView.Opacity = 1; }
                if (ContestView != null) ContestView.Visibility = Visibility.Hidden;
                ClearPasswordText();
                if (BtnProceedToContests != null) { BtnProceedToContests.IsEnabled = true; BtnProceedToContests.Content = "XÁC THỰC VÀ KẾT NỐI"; }
            } catch { }
        }

        private void FullExitApp()
        {
            Task.Run(() => 
            {
                try
                {
                    StopPingLoop();
                    if (_kioskManager != null) {
                        _kioskManager.UnlockMouse();
                        _kioskManager.UnlockAllMonitors();
                        _kioskManager.RestoreExplorer();
                        _kioskManager.Dispose();
                    }
                } 
                catch { }
                finally
                {
                    Environment.Exit(0);
                }
            });
        }

        private void PlayFadeAnimation(FrameworkElement fadeOutElement, FrameworkElement fadeInElement)
        {
            DoubleAnimation fadeOutAnim = new DoubleAnimation { From = 1.0, To = 0.0, Duration = new Duration(TimeSpan.FromMilliseconds(300)) };
            DoubleAnimation fadeInAnim = new DoubleAnimation { From = 0.0, To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(300)) };
            fadeOutAnim.Completed += (s, ev) => { fadeOutElement.Visibility = Visibility.Hidden; fadeInElement.Visibility = Visibility.Visible; fadeInElement.BeginAnimation(UIElement.OpacityProperty, fadeInAnim); };
            fadeOutElement.BeginAnimation(UIElement.OpacityProperty, fadeOutAnim);
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPingLoop();
            if (_toolWindow != null) { _toolWindow.Close(); _toolWindow = null; }
            if (_kioskManager != null) {
                _kioskManager.UnlockMouse();
                _kioskManager.UnlockAllMonitors();
                _kioskManager.RestoreExplorer();
                _kioskManager.Dispose();
            }
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}
