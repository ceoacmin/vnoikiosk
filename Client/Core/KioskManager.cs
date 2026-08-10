using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using VnoiKiosk.Models;

namespace VnoiKiosk.Core
{
    public sealed class KioskManager : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        
        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_F4 = 0x73;
        private const int VK_F12 = 0x7B;
        private const int VK_MENU = 0x12; 
        private const int VK_CONTROL = 0x11;
        private const int VK_SNAPSHOT = 0x2C;
        private const int VK_D = 0x44;

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        private static readonly byte[] K = Encoding.UTF8.GetBytes("CEOACMIN2026!VNOI#Secure@KeyKsk!");
        private static readonly byte[] I = Encoding.UTF8.GetBytes("2026@Kiosk#VNOI!");

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool _isDisposed;
        private Thread? _antiCheatThread;
        private bool _isAntiCheatRunning = false;
        private readonly Dictionary<string, string> _originalAccessibilityFlags = new Dictionary<string, string>();
        private readonly List<Window> _monitorLockWindows = new List<Window>();

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int SetMute(bool bMute, Guid pguidEventContext);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [DllImport("user32.dll")]
        private static extern bool ClipCursor(IntPtr lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetKeyState(int keyCode);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        public static extern uint SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public KioskManager()
        {
            _proc = HookCallback;
        }

        public void MuteSystemAudio()
        {
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device);
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(iid, 23, IntPtr.Zero, out IAudioEndpointVolume volume);
                volume.SetMute(true, Guid.Empty);
            }
            catch { }
        }

        public void KillExplorer()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    p.Kill();
                }
            }
            catch { }
        }

        public void RestoreExplorer()
        {
            try
            {
                if (Process.GetProcessesByName("explorer").Length == 0)
                {
                    Process.Start("explorer.exe");
                }
            }
            catch { }
        }

        public bool IsRemoteSession()
        {
            try { return GetSystemMetrics(0x1000) != 0; } catch { return false; }
        }

        public bool IsVirtualMachine()
        {
            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (key != null)
                    {
                        string manufacturer = (key.GetValue("SystemManufacturer") as string ?? "").ToLower();
                        string product = (key.GetValue("SystemProductName") as string ?? "").ToLower();
                        string family = (key.GetValue("SystemFamily") as string ?? "").ToLower();
                        string[] vmSignatures = { "vmware", "virtualbox", "innotek", "qemu", "xen", "kvm", "parallels", "virtual machine" };
                        foreach (string sig in vmSignatures)
                        {
                            if (manufacturer.Contains(sig) || product.Contains(sig) || family.Contains(sig)) return true;
                        }
                    }
                }
            }
            catch { }

            try
            {
                string[] vmMacPrefixes = { "00:05:69", "00:0C:29", "00:1C:14", "00:50:56", "08:00:27", "00:15:5D", "00:16:3E", "00:1C:42" };
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string mac = nic.GetPhysicalAddress().ToString();
                    if (mac.Length < 6) continue;
                    string formatted = string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2)));
                    foreach (string prefix in vmMacPrefixes)
                    {
                        if (formatted.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public void UnlockMouse()
        {
            try { ClipCursor(IntPtr.Zero); } catch { }
        }

        public void LockSecondaryMonitors(Window mainWindow)
        {
            try
            {
                UnlockAllMonitors();

                List<(IntPtr handle, RECT rect)> monitors = new List<(IntPtr, RECT)>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data) =>
                {
                    monitors.Add((hMon, rect));
                    return true;
                }, IntPtr.Zero);

                if (monitors.Count <= 1) return;

                IntPtr mainHandle = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
                IntPtr mainMonitor = MonitorFromWindow(mainHandle, MONITOR_DEFAULTTONEAREST);

                foreach (var mon in monitors)
                {
                    if (mon.handle == mainMonitor) continue;
                    CreateMonitorLockWindow(mon.rect, mainWindow);
                }
            }
            catch { }
        }

        public void UnlockAllMonitors()
        {
            foreach (Window w in _monitorLockWindows)
            {
                try { w.Close(); } catch { }
            }
            _monitorLockWindows.Clear();
        }

        private bool RectEquals(RECT a, RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        private void CreateMonitorLockWindow(RECT rect, Window mainWindow)
        {
            Window lockWin = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Background = new SolidColorBrush(Color.FromRgb(5, 8, 17)),
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Width = 400,
                Height = 300
            };

            lockWin.SourceInitialized += (s, e) =>
            {
                try
                {
                    IntPtr h = new System.Windows.Interop.WindowInteropHelper(lockWin).Handle;
                    int exStyle = GetWindowLong(h, GWL_EXSTYLE);
                    SetWindowLong(h, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
                }
                catch { }
            };

            StackPanel stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            TextBlock icon = new TextBlock { Text = "🔒", FontSize = 54, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };

            TextBlock text = new TextBlock
            {
                Text = "Đã Khóa Màn Hình Phụ Để Chặn Gian Lận",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            };

            Button swapBtn = new Button
            {
                Content = "⇄   ĐỔI MÀN HÌNH CHÍNH SANG ĐÂY",
                Margin = new Thickness(0, 35, 0, 0),
                Padding = new Thickness(22, 12, 22, 12),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            swapBtn.Click += (s, e) => SwapPrimaryMonitor(mainWindow, rect);

            stack.Children.Add(icon);
            stack.Children.Add(text);
            stack.Children.Add(swapBtn);
            lockWin.Content = stack;

            lockWin.Show();
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(lockWin).Handle;
            SetWindowPos(handle, HWND_TOPMOST, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, SWP_SHOWWINDOW);

            _monitorLockWindows.Add(lockWin);
        }

        private void SwapPrimaryMonitor(Window mainWindow, RECT newMainRect)
        {
            try
            {
                UnlockAllMonitors();
                PositionWindowOnMonitor(mainWindow, newMainRect);

                List<RECT> monitors = new List<RECT>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data) =>
                {
                    monitors.Add(rect);
                    return true;
                }, IntPtr.Zero);

                foreach (RECT rect in monitors)
                {
                    if (RectEquals(rect, newMainRect)) continue;
                    CreateMonitorLockWindow(rect, mainWindow);
                }
            }
            catch { }
        }

        private void PositionWindowOnMonitor(Window window, RECT rect)
        {
            try
            {
                window.WindowState = WindowState.Normal;
                IntPtr handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                SetWindowPos(handle, IntPtr.Zero, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, SWP_NOZORDER | SWP_SHOWWINDOW);
                window.WindowState = WindowState.Maximized;
            }
            catch { }
        }

        public void EnableDRM(Window window)
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                SetWindowDisplayAffinity(helper.Handle, 0x00000001);
            }
            catch { }
        }

        private void DisableSystemKeysViaRegistry()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key?.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                }
                using (RegistryKey? key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                {
                    key?.SetValue("DisableCMD", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private void DisableAccessibilityHotkeys()
        {
            DisableHotkeyFlag(@"Control Panel\Accessibility\StickyKeys");
            DisableHotkeyFlag(@"Control Panel\Accessibility\Keyboard Response");
            DisableHotkeyFlag(@"Control Panel\Accessibility\ToggleKeys");
        }

        private void DisableHotkeyFlag(string subKeyPath)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKeyPath, true))
                {
                    if (key == null) return;
                    string? current = key.GetValue("Flags") as string;
                    if (string.IsNullOrEmpty(current)) return;
                    if (!_originalAccessibilityFlags.ContainsKey(subKeyPath))
                    {
                        _originalAccessibilityFlags[subKeyPath] = current;
                    }
                    if (int.TryParse(current, out int flags))
                    {
                        int newFlags = flags & ~0x4;
                        key.SetValue("Flags", newFlags.ToString(), RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }

        private void RestoreAccessibilityHotkeys()
        {
            foreach (var kv in _originalAccessibilityFlags)
            {
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(kv.Key, true))
                    {
                        key?.SetValue("Flags", kv.Value, RegistryValueKind.String);
                    }
                }
                catch { }
            }
            _originalAccessibilityFlags.Clear();
        }

        private void RestoreSystemKeysViaRegistry()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", true))
                {
                    key?.DeleteValue("DisableTaskMgr", false);
                }
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\System", true))
                {
                    key?.DeleteValue("DisableCMD", false);
                }
            }
            catch { }
        }

        private string GetBlackBoxPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VnoiKioskData");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "sys_vnoi_crash_log.sys");
        }

        public void WriteBlackBox(string username, string contestLink)
        {
            try
            {
                var data = new { Username = username, Contest = contestLink, Time = DateTime.Now, Status = "RUNNING" };
                string path = GetBlackBoxPath();
                File.WriteAllText(path, Encrypt(JsonConvert.SerializeObject(data)));
                File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);
            }
            catch { }
        }

        public void ClearBlackBox()
        {
            try
            {
                string path = GetBlackBoxPath();
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch { }
        }

        public async Task CheckAndReportCrashAsync(string baseUrl)
        {
            try
            {
                string path = GetBlackBoxPath();
                if (File.Exists(path))
                {
                    string json = Decrypt(File.ReadAllText(path));
                    using (HttpClient client = new HttpClient())
                    {
                        var contentPost = new StringContent(json, Encoding.UTF8, "application/json");
                        await client.PostAsync($"{baseUrl}/api/webhook/crash_report", contentPost);
                    }
                    ClearBlackBox();
                }
            }
            catch { }
        }

        public void StartAntiCheat()
        {
            if (_isAntiCheatRunning) return;
            _isAntiCheatRunning = true;
            _antiCheatThread = new Thread(() =>
            {
                string[] whitelist = { 
                    "svchost", "System", "Idle", "winlogon", "csrss", 
                    "services", "lsass", "smss", "VnoiKiosk", "cmd", "conhost", "SearchApp", "TextInputHost", "ctfmon", "sihost", "dwm", "fontdrvhost",
                    "msedgewebview2", "msedge", "WebView2Loader", "WWAHost", "RuntimeBroker", "backgroundTaskHost", "ApplicationFrameHost", "SecurityHealthSystray", "SecurityHealthService"
                };

                string[] blacklist = { "taskmgr", "processhacker", "wireshark", "cheatengine", "obs64", "obs32", "bdcam", "teamviewer", "anydesk", "discord", "zalo", "skype", "tailscale", "openvpn", "v2ray", "shadowsocks", "ngrok", "wireguard", "explorer", "ida64", "x64dbg", "ollydbg", "dnspy", "fiddler", "burp" };

                int currentId = Process.GetCurrentProcess().Id;
                int currentSessionId = Process.GetCurrentProcess().SessionId;

                while (_isAntiCheatRunning)
                {
                    try
                    {
                        Process[] processes = Process.GetProcesses();

                        foreach (Process p in processes)
                        {
                            try
                            {
                                if (p.Id == currentId) continue;
                                if (p.SessionId != currentSessionId) continue;

                                string pname = p.ProcessName;

                                bool isSafe = false;
                                foreach (string safe in whitelist)
                                {
                                    if (pname.IndexOf(safe, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        isSafe = true;
                                        break;
                                    }
                                }

                                if (isSafe) continue;

                                bool isBlacklisted = false;
                                foreach (string bad in blacklist)
                                {
                                    if (pname.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        isBlacklisted = true;
                                        break;
                                    }
                                }

                                if (isBlacklisted || p.MainWindowHandle != IntPtr.Zero)
                                {
                                    try { p.Kill(); } catch { }
                                }
                            }
                            catch { }
                        }
                    } catch { }
                    Thread.Sleep(1000);
                }
            });
            _antiCheatThread.IsBackground = true;
            _antiCheatThread.Start();
        }

        public void StopAntiCheat()
        {
            _isAntiCheatRunning = false;
            if (_antiCheatThread != null && _antiCheatThread.IsAlive)
            {
                try { _antiCheatThread.Join(1000); } catch { }
            }
        }

        public void EnableKioskMode() 
        { 
            try 
            { 
                Application.Current.Dispatcher.Invoke(() => { try { Clipboard.Clear(); } catch { } });
#if !DEBUG
                DisableSystemKeysViaRegistry();
#endif
                DisableAccessibilityHotkeys();
                PreventSystemSleep(); 
                IntPtr taskbar = FindWindow("Shell_TrayWnd", "");
                if (taskbar != IntPtr.Zero) ShowWindow(taskbar, 0);
                
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule!)
                {
                    _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            } 
            catch { } 
        }
        
        public void DisableKioskMode() 
        { 
            try 
            { 
                Application.Current.Dispatcher.Invoke(() => { try { Clipboard.Clear(); } catch { } });
                RestoreSystemKeysViaRegistry();
                RestoreAccessibilityHotkeys();
                RestoreSystemSleep();
                IntPtr taskbar = FindWindow("Shell_TrayWnd", "");
                if (taskbar != IntPtr.Zero) ShowWindow(taskbar, 5);
                
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }
            } 
            catch { } 
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) 
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool alt = (GetKeyState(VK_MENU) & 0x8000) != 0;
                bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
                bool win = (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;

                if (vkCode == VK_LWIN || vkCode == VK_RWIN || vkCode == VK_F12 ||
                    (alt && vkCode == VK_TAB) ||
                    (alt && vkCode == VK_F4) ||
                    (alt && vkCode == VK_ESCAPE) ||
                    (ctrl && vkCode == VK_ESCAPE) ||
                    (win && vkCode == VK_D) ||
                    (win && vkCode == VK_TAB) ||
                    (win && ctrl && vkCode == VK_D) ||
                    vkCode == VK_SNAPSHOT)
                {
                    return (IntPtr)1; 
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam); 
        }

        private void PreventSystemSleep() { SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED); }
        private void RestoreSystemSleep() { SetThreadExecutionState(ES_CONTINUOUS); }

        public void SaveData(LocalAuthData data)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sys_vnoi_cfg.dat");
                File.WriteAllText(path, Encrypt(JsonConvert.SerializeObject(data)));
            }
            catch { }
        }

        public LocalAuthData? LoadData()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sys_vnoi_cfg.dat");
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<LocalAuthData>(Decrypt(File.ReadAllText(path))); } catch { return null; }
        }

        public string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList) if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    string ipStr = ip.ToString();
                    if (!ipStr.StartsWith("169.254.") && !ipStr.StartsWith("10.")) return ipStr; 
                }
            }
            catch { }
            return "Unknown";
        }

        private string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = K; aes.IV = I;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs)) sw.Write(plainText);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private string Decrypt(string cipherText)
        {
            byte[] buffer = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = K; aes.IV = I;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream(buffer))
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs)) return sr.ReadToEnd();
            }
        }

        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        private void Dispose(bool disposing) { if (!_isDisposed) { StopAntiCheat(); DisableKioskMode(); UnlockMouse(); RestoreExplorer(); _isDisposed = true; } }
        ~KioskManager() { Dispose(false); }
    }
}