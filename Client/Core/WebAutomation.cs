#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace VnoiKiosk.Core
{
    public static class WebAutomation
    {
        public static async Task ExecuteLoginAsync(CoreWebView2 webView, string username, string password)
        {
            string js = $@"
                setTimeout(function() {{
                    try {{
                        var usr = document.querySelector('input[name=""username""]') || document.getElementById('id_username');
                        var pwd = document.querySelector('input[name=""password""]') || document.getElementById('id_password');
                        if(usr && pwd) {{
                            var frm = usr.closest('form');
                            if(frm) {{
                                usr.value = '{username.Replace("'", "\\'").Replace("\n", "")}';
                                pwd.value = '{password.Replace("'", "\\'").Replace("\n", "")}';
                                HTMLFormElement.prototype.submit.call(frm);
                            }}
                        }}
                    }} catch(e) {{ }}
                }}, 500);
            ";
            await webView.ExecuteScriptAsync(js);
        }

        public static async Task ExecuteSubmitAccessCodeAsync(CoreWebView2 webView, string code)
        {
            string js = $@"
                setTimeout(function() {{
                    try {{
                        var inp = document.getElementById('id_access_code') || document.querySelector('input[name=""access_code""]');
                        if(inp) {{
                            var frm = inp.closest('form') || document.getElementById('access-code-form');
                            if(frm) {{
                                inp.value = '{code.Replace("'", "\\'").Replace("\n", "")}';
                                HTMLFormElement.prototype.submit.call(frm);
                            }}
                        }}
                    }} catch(e) {{ }}
                }}, 500);
            ";
            await webView.ExecuteScriptAsync(js);
        }

        public static string GetGlobalInjectionScript(string username, string logoBase64)
        {
            string rawJs = @"
                (function() {
                    if (window.kioskGlobalScriptInjected) return;
                    window.kioskGlobalScriptInjected = true;

                    let p = window.location.pathname.toLowerCase();
                    if (p.includes('/login')) {
                        sessionStorage.removeItem('kioskReadySent');
                        sessionStorage.removeItem('kioskStartTime');
                        sessionStorage.removeItem('kioskStopped');
                    }

                    window.chrome.webview.addEventListener('message', function(e) {
                        if (typeof e.data === 'string' && e.data.indexOf('SET_CONTEST_URL:') === 0) {
                            window.kioskContestUrl = e.data.substring('SET_CONTEST_URL:'.length);
                        }
                    });

                    document.addEventListener('click', function(e) {
                        let fileInput = e.target.closest('input[type=""file""]');
                        if (fileInput) {
                            e.preventDefault();
                            e.stopPropagation();
                        }
                    }, true);

                    document.addEventListener('paste', function(e) {
                        if (e.clipboardData && e.clipboardData.files && e.clipboardData.files.length > 0) {
                            e.preventDefault();
                        }
                    }, true);

                    document.addEventListener('keydown', function(e) {
                        if (e.key === 'F12' || (e.ctrlKey && e.shiftKey && (e.key === 'I' || e.key === 'i' || e.key === 'J' || e.key === 'j' || e.key === 'C' || e.key === 'c'))) {
                            e.preventDefault();
                            e.stopPropagation();
                        }
                    }, true);

                    function enforceCSS() {
                        if(document.getElementById('kiosk-core-styles') || window !== window.top) return;
                        const style = document.createElement('style');
                        style.id = 'kiosk-core-styles';
                        style.innerHTML = `
                            html, body { overscroll-behavior: none !important; }
                            body { padding-top: 52px !important; margin-top: 0 !important; }
                            
                            #page-tabs a[href*=""/rankings/""], #page-tabs a[href*=""/submissions/""], #page-tabs a[href*=""/editorial/""] { display: none !important; }
                            #contest-info, div#contest-info { display: none !important; opacity: 0 !important; visibility: hidden !important; pointer-events: none !important; height: 0 !important; width: 0 !important; margin: 0 !important; padding: 0 !important; position: absolute !important; }
                            .navbar, #navbar, header#header { display: none !important; height: 0 !important; min-height: 0 !important; overflow: hidden !important; }
                            
                            #ctns-kiosk-toolbar { all: initial !important; box-sizing: border-box !important; position: fixed !important; top: 0 !important; left: 0 !important; width: 100% !important; height: 52px !important; margin: 0 !important; padding: 0 22px !important; background: linear-gradient(180deg, #111827 0%, #0B1120 100%) !important; z-index: 2147483647 !important; display: flex !important; align-items: center !important; justify-content: space-between !important; border-bottom: 1px solid rgba(56,189,248,0.15) !important; box-shadow: 0 8px 20px rgba(0,0,0,0.45) !important; font-family: 'Segoe UI', Arial, sans-serif !important; }
                            .ctns-toolbar-left { display: flex !important; align-items: center !important; height: 100% !important; }
                            .ctns-btn-back { background: linear-gradient(135deg, #3B82F6, #1D4ED8) !important; color: #fff !important; border: none !important; padding: 8px 18px !important; border-radius: 999px !important; font-weight: 800 !important; cursor: pointer !important; text-decoration: none !important; font-size: 12.5px !important; letter-spacing: 0.4px !important; display: inline-flex !important; align-items: center !important; gap: 8px !important; box-shadow: 0 6px 16px rgba(37,99,235,0.35) !important; transition: transform 0.2s ease, box-shadow 0.2s ease !important; }
                            .ctns-btn-back:hover { transform: translateY(-2px) !important; box-shadow: 0 10px 22px rgba(37,99,235,0.5) !important; color: #fff !important; }
                            .ctns-back-arrow { font-size: 15px !important; line-height: 1 !important; }
                            .ctns-brand { display: flex !important; align-items: center !important; gap: 12px !important; margin-left: 18px !important; padding-left: 18px !important; border-left: 1px solid rgba(148,163,184,0.25) !important; height: 30px !important; }
                            .ctns-logo-mark { width: 32px !important; height: 32px !important; background: transparent !important; display: flex !important; align-items: center !important; justify-content: center !important; flex-shrink: 0 !important; box-shadow: none !important; }
                            .ctns-brand-text { display: flex !important; flex-direction: column !important; justify-content: center !important; line-height: 1 !important; }
                            .ctns-title { color: #F8FAFC !important; font-weight: 800 !important; font-size: 12.5px !important; letter-spacing: 0.6px !important; margin: 0 !important; padding: 0 !important; text-transform: uppercase !important; }
                            .ctns-sub { color: #38BDF8 !important; font-weight: 600 !important; font-size: 10px !important; letter-spacing: 0.8px !important; margin: 4px 0 0 0 !important; padding: 0 !important; }
                            .ctns-clock-box { display: flex !important; align-items: center !important; gap: 9px !important; background: rgba(30,41,59,0.7) !important; border: 1px solid rgba(56,189,248,0.25) !important; border-radius: 999px !important; padding: 7px 16px !important; }
                            .ctns-clock-dot { width: 7px !important; height: 7px !important; border-radius: 50% !important; background: #10B981 !important; box-shadow: 0 0 8px #10B981 !important; animation: ctnsClockPulse 2s ease-in-out infinite !important; }
                            @keyframes ctnsClockPulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.4; } }
                            #kiosk-live-clock { color: #E2E8F0 !important; font-family: Consolas, 'Courier New', monospace !important; font-size: 13.5px !important; font-weight: 700 !important; letter-spacing: 0.5px !important; }
                            
                            #kiosk-gear-container { position: fixed; bottom: 25px; right: 25px; z-index: 2147483647; display: flex; flex-direction: column; align-items: flex-end; font-family: 'Segoe UI', Arial, sans-serif; }
                            #kiosk-gear-icon { font-size: 24px; background: #1E293B; color: #38BDF8; border: 2px solid #38BDF8; border-radius: 50%; width: 55px; height: 55px; display: flex; align-items: center; justify-content: center; cursor: pointer; box-shadow: 0 4px 15px rgba(0,0,0,0.5); user-select: none; transition: transform 0.3s; margin-top: 10px; }
                            #kiosk-gear-icon:hover { transform: rotate(45deg) scale(1.1); }
                            #kiosk-menu { display: none; background: #0F172A; border: 2px solid #334155; border-radius: 12px; padding: 15px; flex-direction: column; gap: 10px; width: 220px; box-shadow: 0 10px 30px rgba(0,0,0,0.7); margin-bottom: 15px; }
                            .kiosk-menu-btn { background: #1E293B; color: #E2E8F0; border: 1px solid #475569; padding: 12px; border-radius: 8px; font-weight: bold; cursor: pointer; text-align: left; display: flex; align-items: center; gap: 10px; font-size: 14px; transition: all 0.2s; font-family: 'Segoe UI', Arial, sans-serif; }
                            .kiosk-menu-btn:hover { background: #38BDF8; color: #0F172A; border-color: #38BDF8; transform: translateX(-5px); }
                            .kiosk-exit-btn { background: #EF4444 !important; color: white !important; border-color: #DC2626 !important; margin-top: 10px; }
                            .kiosk-exit-btn:hover { background: #B91C1C !important; transform: scale(1.02) !important; }
                            #kiosk-secure-badge { position:fixed; bottom:25px; left:25px; z-index:999999; background:rgba(15,23,42,0.9); padding:10px 18px; border-radius:30px; display:flex; align-items:center; border: 1px solid #1E293B; box-shadow: 0 10px 25px rgba(0,0,0,0.5); pointer-events:none; backdrop-filter: blur(10px); font-family: 'Segoe UI', Arial, sans-serif; }
                            .kiosk-red-dot { width:12px; height:12px; background-color:#EF4444; border-radius:50%; animation:redpulse 1.5s infinite; }
                            @keyframes redpulse { 0% { box-shadow: 0 0 0 0 rgba(239,68,68,0.7); } 70% { box-shadow: 0 0 0 6px rgba(239,68,68,0); } 100% { box-shadow: 0 0 0 0 rgba(239,68,68,0); } }
                        `;
                        document.documentElement.appendChild(style);
                    }

                    function injectAntiPhotoWatermark() {
                        if (document.getElementById('kiosk-watermark') || window !== window.top) return;
                        const wm = document.createElement('div');
                        wm.id = 'kiosk-watermark';
                        wm.style.cssText = 'position:fixed;top:0;left:0;width:100vw;height:100vh;pointer-events:none;z-index:2147483646;opacity:0.035;background-image:repeating-linear-gradient(45deg,rgba(0,0,0,1) 0px,rgba(0,0,0,1) 1px,transparent 1px,transparent 3px);display:flex;flex-wrap:wrap;overflow:hidden;justify-content:center;align-items:center;';
                        let content = '';
                        for(let i=0; i<150; i++) {
                            content += '<span style=""color:#000;font-size:24px;font-weight:900;transform:rotate(-35deg);margin:25px;user-select:none;font-family:monospace;"">[USERNAME]</span>';
                        }
                        wm.innerHTML = content;
                        document.body.appendChild(wm);
                    }

                    try {
                        const originalXhrOpen = XMLHttpRequest.prototype.open;
                        XMLHttpRequest.prototype.open = function(method, url) {
                            if (method.toUpperCase() === 'POST' && (url.includes('upload') || url.includes('file'))) {
                                this.abort(); return;
                            }
                            return originalXhrOpen.apply(this, arguments);
                        };
                        const originalFetch = window.fetch;
                        window.fetch = async function() {
                            let url = arguments[0];
                            let options = arguments[1];
                            if (options && options.method && options.method.toUpperCase() === 'POST' && typeof url === 'string' && (url.includes('upload') || url.includes('file'))) {
                                return Promise.reject(new Error(''));
                            }
                            return originalFetch.apply(this, arguments);
                        };
                    } catch(e) { }

                    let origPushState = history.pushState;
                    history.pushState = function(state, title, url) { origPushState.apply(this, arguments); setTimeout(enforceUI, 100); setTimeout(enforceUI, 500); };
                    let origReplaceState = history.replaceState;
                    history.replaceState = function(state, title, url) { origReplaceState.apply(this, arguments); setTimeout(enforceUI, 100); setTimeout(enforceUI, 500); };
                    window.addEventListener('popstate', function() { setTimeout(enforceUI, 100); setTimeout(enforceUI, 500); });

                    function triggerLeaveContest() {
                        try {
                            let path = window.location.pathname.toLowerCase();
                            let isDeepInContest = path.includes('/problem') || path.includes('/ranking') || path.includes('/submissions') || path.includes('/submit') || path.includes('/editorial');

                            if (isDeepInContest) {
                                sessionStorage.setItem('kioskPendingLeave', 'true');
                                window.location.href = window.kioskContestUrl || '/';
                                return;
                            }

                            performLeaveContest();
                        } catch(e) { window.chrome.webview.postMessage(JSON.stringify({action: 'EXIT_CONTEST_SUCCESS'})); }
                    }

                    function performLeaveContest() {
                        try {
                            window.confirm = function() { return true; };
                            sessionStorage.removeItem('kioskPendingLeave');
                            sessionStorage.setItem('kioskStopped', 'true');
                            
                            let overlay = document.createElement('div');
                            overlay.style.cssText = 'position:fixed; top:0; left:0; width:100%; height:100%; background:#0f172a; z-index:2147483647; display:flex; align-items:center; justify-content:center; color:white; font-size:20px; font-weight:bold; flex-direction:column; gap:20px; font-family:""Segoe UI"", Arial, sans-serif;';
                            overlay.innerHTML = '<div style=""width:50px; height:50px; border:5px solid #3b82f6; border-top-color:transparent; border-radius:50%; animation:kioskspin 1s linear infinite;""></div><style>@keyframes kioskspin { 100% { transform:rotate(360deg); } }</style><div style=""letter-spacing:2px; font-size:16px;"">ĐANG ĐỒNG BỘ DỮ LIỆU & NỘP BÀI...</div>';
                            document.body.appendChild(overlay);

                            let formLeave = document.querySelector('form[action*=""/leave""]');
                            if (formLeave) {
                                let formData = new FormData(formLeave);
                                fetch(formLeave.action, { method: formLeave.method || 'POST', body: formData })
                                .finally(() => { window.chrome.webview.postMessage(JSON.stringify({action: 'EXIT_CONTEST_SUCCESS'})); });
                            } else {
                                let realLeaveBtn = document.querySelector('input.leaving-forever, button.leaving-forever, a.leaving-forever, a[href*=""/leave""]');
                                if (realLeaveBtn && realLeaveBtn.tagName.toLowerCase() === 'a') {
                                     fetch(realLeaveBtn.href, { method: 'POST' }).finally(() => { window.chrome.webview.postMessage(JSON.stringify({action: 'EXIT_CONTEST_SUCCESS'})); });
                                } else if (realLeaveBtn) {
                                     realLeaveBtn.click();
                                     setTimeout(() => {
                                         let confirmBtn = document.querySelector('form[action*=""/leave""] button[type=""submit""]');
                                         if(confirmBtn) confirmBtn.click();
                                         setTimeout(() => { window.chrome.webview.postMessage(JSON.stringify({action: 'EXIT_CONTEST_SUCCESS'})); }, 1000);
                                     }, 500);
                                } else {
                                     setTimeout(() => { window.chrome.webview.postMessage(JSON.stringify({action: 'EXIT_CONTEST_SUCCESS'})); }, 1500);
                                }
                            }
                        } catch(e) { window.chrome.webview.postMessage(JSON.stringify({action: 'EXIT_CONTEST_SUCCESS'})); }
                    }

                    document.addEventListener('click', function(e) {
                        try {
                            if (window !== window.top) {
                                let a = e.target.closest('a') || e.target.closest('button');
                                if (a) {
                                    let href = a.href || '';
                                    let text = (a.textContent || '').toLowerCase();
                                    if (href.includes('login') || href.includes('signup') || href.includes('register') || text.includes('log in') || text.includes('sign up') || text.includes('auth')) {
                                        e.preventDefault(); e.stopPropagation();
                                    }
                                }
                            }
                        } catch(err) {}
                    }, true);

                    function replaceNavbar() {
                        if (window !== window.top) return;
                        let header = document.querySelector('.navbar') || document.querySelector('header#header') || document.querySelector('#nav-container');
                        if (header && !header.hasAttribute('data-kiosk-replaced')) {
                            header.setAttribute('data-kiosk-replaced', 'true');
                            header.removeAttribute('style');
                            header.removeAttribute('class');
                            header.id = 'ctns-kiosk-toolbar';

                            let logoData = '[BASE64_LOGO]';
                            let logoHtml = (logoData && logoData.startsWith('data:image'))
                                ? `<img src=""${logoData}"" style=""width:100%; height:100%; object-fit:contain; border-radius:4px;"" />`
                                : `<span style=""font-family: Arial; font-weight: bold; color: white; font-size: 16px;"">V</span>`;

                            header.innerHTML = `
                                <div class=""ctns-toolbar-left"">
                                    <a href=""javascript:window.history.back();"" class=""ctns-btn-back""><span class=""ctns-back-arrow"">←</span><span>QUAY LẠI</span></a>
                                    <div class=""ctns-brand"">
                                        <div class=""ctns-logo-mark"">
                                            ${logoHtml}
                                        </div>
                                        <div class=""ctns-brand-text"">
                                            <p class=""ctns-title"">SAFE EXAM BROWSER</p>
                                            <p class=""ctns-sub"">CTNS Development</p>
                                        </div>
                                    </div>
                                </div>
                                <div class=""ctns-clock-box"">
                                    <span class=""ctns-clock-dot""></span>
                                    <span id=""kiosk-live-clock"">00:00:00</span>
                                </div>
                            `;
                        }
                    }

                    function collapseTopGap() {
                        if (window !== window.top) return;
                        try {
                            let ci = document.querySelector('#contest-info, div#contest-info');
                            if (ci && ci.parentElement) {
                                let p = ci.parentElement;
                                p.style.setProperty('padding-top', '0', 'important');
                                p.style.setProperty('padding-bottom', '0', 'important');
                                p.style.setProperty('margin-top', '0', 'important');
                                p.style.setProperty('min-height', '0', 'important');
                            }
                            let oldNav = document.querySelector('.navbar, #navbar, header#header');
                            if (oldNav && oldNav.parentElement) {
                                oldNav.parentElement.style.setProperty('padding-top', '0', 'important');
                                oldNav.parentElement.style.setProperty('margin-top', '0', 'important');
                            }
                        } catch(e) {}
                    }

                    function enforceScrollLock() {
                        if (window !== window.top) return;
                        try {
                            if (window.__kioskScrollMin === undefined) {
                                let minTop = 0;
                                let walk = function(el, depth) {
                                    if (!el || depth > 6) return null;
                                    if (el.id && el.id.indexOf('kiosk') === 0) return null;
                                    let style = window.getComputedStyle(el);
                                    if (style.display === 'none' || style.visibility === 'hidden' || style.position === 'fixed') return null;
                                    let rect = el.getBoundingClientRect();
                                    if (rect.height > 20 && rect.width > 50 && el.textContent && el.textContent.trim().length > 3) {
                                        return rect.top + window.scrollY;
                                    }
                                    for (let i = 0; i < el.children.length; i++) {
                                        let r = walk(el.children[i], depth + 1);
                                        if (r !== null) return r;
                                    }
                                    return null;
                                };
                                for (let i = 0; i < document.body.children.length; i++) {
                                    let r = walk(document.body.children[i], 0);
                                    if (r !== null) { minTop = Math.max(0, r - 8); break; }
                                }
                                window.__kioskScrollMin = minTop;
                            }
                            if (window.__kioskScrollMin > 0 && window.scrollY < window.__kioskScrollMin) {
                                window.scrollTo(0, window.__kioskScrollMin);
                            }
                        } catch(e) {}
                    }

                    function enforceUI() {
                        try {
                            if (!document.body) return;
                            
                            let hostname = window.location.hostname.toLowerCase();
                            if (!hostname.includes('onecompiler.com') && !hostname.includes('programiz.com') && !hostname.includes('onlinegdb.com') && !hostname.includes('cpp.sh') && !hostname.includes('onlineide.pro') && !hostname.includes('online-ide.com')) {
                                enforceCSS();
                                replaceNavbar();
                                collapseTopGap();
                                enforceScrollLock();
                            }

                            injectAntiPhotoWatermark();

                            if (sessionStorage.getItem('kioskPendingLeave') === 'true') {
                                let lp = window.location.pathname.toLowerCase();
                                let stillDeep = lp.includes('/problem') || lp.includes('/ranking') || lp.includes('/submissions') || lp.includes('/submit') || lp.includes('/editorial');
                                if (!stillDeep) { 
                                    if (document.readyState === 'complete' || document.readyState === 'interactive') {
                                        setTimeout(performLeaveContest, 1000); 
                                    }
                                    return; 
                                }
                            }

                            let path = window.location.pathname.toLowerCase();
                            let isInsideContest = path.includes('/contest/') && !path.includes('/login') && !path.includes('/join') && !path.includes('/participate') && !path.includes('/enter');
                            
                            if (isInsideContest && !sessionStorage.getItem('kioskReadySent')) {
                                sessionStorage.setItem('kioskReadySent', 'true');
                                sessionStorage.setItem('kioskStartTime', Date.now().toString());
                                window.chrome.webview.postMessage(JSON.stringify({action: 'CONTEST_READY'}));
                            }

                            let clockEl = document.getElementById('kiosk-live-clock');
                            let startTimeStr = sessionStorage.getItem('kioskStartTime');
                            if (clockEl && startTimeStr && !sessionStorage.getItem('kioskStopped')) {
                                let diff = Math.floor((Date.now() - parseInt(startTimeStr)) / 1000);
                                let h = String(Math.floor(diff / 3600)).padStart(2, '0');
                                let m = String(Math.floor((diff % 3600) / 60)).padStart(2, '0');
                                let s = String(diff % 60).padStart(2, '0');
                                clockEl.innerText = `${h}:${m}:${s}`;
                            }
                            
                            if (window === window.top && sessionStorage.getItem('kioskReadySent')) {
                                if (!document.getElementById('kiosk-gear-container')) {
                                    let container = document.createElement('div'); container.id = 'kiosk-gear-container';
                                    let menu = document.createElement('div'); menu.id = 'kiosk-menu';
                                    menu.innerHTML = '<div style=""color: white; font-weight: 900; font-size: 13px; text-align: center; margin-bottom: 5px; border-bottom: 1px solid #334155; padding-bottom: 10px;"">🛠️ BỘ CÔNG CỤ</div>';

                                    function createMenuBtn(name, icon, url) {
                                        let btn = document.createElement('button'); btn.className = 'kiosk-menu-btn'; btn.innerHTML = `<span>${icon}</span> <span>${name}</span>`;
                                        btn.onclick = function(e) {
                                            e.stopPropagation(); document.getElementById('kiosk-menu').style.display = 'none';
                                            window.chrome.webview.postMessage(JSON.stringify({action: 'OPEN_TOOL', title: name, url: url}));
                                        };
                                        return btn;
                                    }

                                    menu.appendChild(createMenuBtn('Online IDE C++', '⚡', 'https://www.online-ide.com/online_c++_compiler'));
                                    menu.appendChild(createMenuBtn('OneCompiler C++', '🧠', 'https://onecompiler.com/cpp'));
                                    menu.appendChild(createMenuBtn('OnlineIDE Pro', '🚀', 'https://www.onlineide.pro/playground/cpp'));
                                    menu.appendChild(createMenuBtn('ProgramIZ C++', '💻', 'https://www.programiz.com/cpp-programming/online-compiler/'));
                                    menu.appendChild(createMenuBtn('OnlineGDB C++', '🌍', 'https://www.onlinegdb.com/online_c++_compiler'));
                                    menu.appendChild(createMenuBtn('C++ Shell', '⌨️', 'https://cpp.sh/'));

                                    let exitBtn = document.createElement('button'); exitBtn.className = 'kiosk-menu-btn kiosk-exit-btn'; exitBtn.innerHTML = '<span>✖</span> <span>NỘP BÀI VÀ THOÁT</span>';
                                    exitBtn.onclick = function(e) { e.preventDefault(); e.stopPropagation(); triggerLeaveContest(); };
                                    menu.appendChild(exitBtn); container.appendChild(menu);

                                    let gear = document.createElement('div'); gear.id = 'kiosk-gear-icon'; gear.innerHTML = '⚙️';
                                    gear.onclick = function(e) { e.stopPropagation(); let m = document.getElementById('kiosk-menu'); if(m) m.style.display = m.style.display === 'flex' ? 'none' : 'flex'; };
                                    container.appendChild(gear); document.body.appendChild(container);
                                }

                                if(!document.getElementById('kiosk-secure-badge')) {
                                    let fakeBadge = document.createElement('div'); 
                                    fakeBadge.id = 'kiosk-secure-badge';
                                    fakeBadge.innerHTML = '<div class=""kiosk-red-dot""></div><span style=""color:#EF4444;font-weight:900;font-size:11px;letter-spacing:1px;margin-left:10px;"">CHẾ ĐỘ KIOSK ĐANG HOẠT ĐỘNG</span>'; 
                                    document.body.appendChild(fakeBadge);
                                }
                            }
                        } catch(e) { }
                    }
                    
                    if (!window.kioskInterval) window.kioskInterval = setInterval(enforceUI, 300);

                    setInterval(function() {
                        try {
                            let host = window.location.hostname.toLowerCase();
                            
                            if (host.includes('onlineide.pro')) {
                                let spark = document.querySelector('.ri-sparkling-2-line');
                                if (spark && spark.closest('button')) spark.closest('button').style.setProperty('display', 'none', 'important');
                                
                                let signin = document.querySelector('.pi-sign-in');
                                if (signin && signin.closest('button')) signin.closest('button').style.setProperty('display', 'none', 'important');
                                
                                let authBtns = document.querySelectorAll('button[aria-label=""Sign in""]');
                                authBtns.forEach(b => b.style.setProperty('display', 'none', 'important'));
                            }

                            if (host.includes('onecompiler.com') || host.includes('onlinegdb.com') || host.includes('onlineide.pro') || host.includes('online-ide.com') || host.includes('cpp.sh') || host.includes('programiz.com')) {
                                let btns = document.getElementsByTagName('button');
                                for (let i = 0; i < btns.length; i++) {
                                    if (btns[i].dataset.kH === '1') continue;
                                    let txt = btns[i].innerText ? btns[i].innerText.trim().toLowerCase() : '';
                                    let htm = btns[i].innerHTML || '';
                                    if (txt === 'ai' || txt.includes('ai agent') || txt.includes('upgrade') || txt === 'login' || txt === 'sign up' || htm.includes('lucide-sparkles') || btns[i].id.includes('askai')) {
                                        btns[i].style.cssText = 'display:none !important; opacity:0 !important; pointer-events:none !important; width:0 !important; height:0 !important; overflow:hidden !important; margin:0 !important; padding:0 !important; border:none !important;';
                                        btns[i].dataset.kH = '1';
                                    }
                                }
                                let links = document.getElementsByTagName('a');
                                for (let i = 0; i < links.length; i++) {
                                    if (links[i].dataset.kH === '1') continue;
                                    let txt = links[i].innerText ? links[i].innerText.trim().toLowerCase() : '';
                                    if (txt === 'login' || txt === 'sign up' || links[i].href.includes('/login') || links[i].href.includes('login')) {
                                        links[i].style.cssText = 'display:none !important;';
                                        links[i].dataset.kH = '1';
                                    }
                                }
                                let divs = document.getElementsByTagName('div');
                                for (let i = 0; i < divs.length; i++) {
                                    if (divs[i].dataset.kH === '1') continue;
                                    if (divs[i].id === 'login_logout_span' || (divs[i].className && typeof divs[i].className === 'string' && divs[i].className.includes('login'))) {
                                        divs[i].style.cssText = 'display:none !important;';
                                        divs[i].dataset.kH = '1';
                                    }
                                }
                            }
                        } catch(e) {}
                    }, 2000);

                })();
            ".Replace("[USERNAME]", username.Replace("'", "\\'").Replace("\n", "")).Replace("[BASE64_LOGO]", logoBase64);
            return rawJs;
        }

        public static async Task SpamInjectKioskUIAsync(CoreWebView2 webView, string username, string logoBase64) { try { await webView.ExecuteScriptAsync(GetGlobalInjectionScript(username, logoBase64)); } catch { } }
    }
}