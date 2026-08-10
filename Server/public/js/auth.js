function switchAuth(tab) {
    const loginForm = document.getElementById('login-form');
    const registerForm = document.getElementById('register-form');
    const tabLogin = document.getElementById('tab-login');
    const tabRegister = document.getElementById('tab-register');
    if (tab === 'login') {
        if(loginForm) loginForm.classList.remove('hidden');
        if(registerForm) registerForm.classList.add('hidden');
        if(tabLogin) tabLogin.className = 'flex-1 text-white font-black text-[10px] tracking-widest px-2 py-2 border-b-2 border-sky-400 uppercase transition-all';
        if(tabRegister) tabRegister.className = 'flex-1 text-slate-500 font-bold text-[10px] tracking-widest px-2 py-2 hover:text-white uppercase transition-colors';
    } else {
        if(loginForm) loginForm.classList.add('hidden');
        if(registerForm) registerForm.classList.remove('hidden');
        if(tabLogin) tabLogin.className = 'flex-1 text-slate-500 font-bold text-[10px] tracking-widest px-2 py-2 hover:text-white uppercase transition-colors';
        if(tabRegister) tabRegister.className = 'flex-1 text-white font-black text-[10px] tracking-widest px-2 py-2 border-b-2 border-emerald-400 uppercase transition-all';
    }
}
async function handleLogin(e) {
    e.preventDefault();
    const btn = document.getElementById('btn-login');
    const ogText = btn.innerHTML;
    try {
        btn.innerHTML = 'ĐANG TẢI...';
        btn.disabled = true;
        const u = document.getElementById('l-user')?.value || '';
        const p = document.getElementById('l-pass')?.value || '';
        const res = await apiFetch('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username: u, password: p }) });
        if (res && res.success) {
            showToast('Đăng nhập thành công!', true);
            setTimeout(() => window.location.href = '/dashboard', 800);
        } else {
            showToast('Tài khoản hoặc mật khẩu không chính xác.', false);
            btn.innerHTML = ogText; btn.disabled = false;
        }
    } catch(err) {
        showToast('Lỗi kết nối máy chủ.', false);
        btn.innerHTML = ogText; btn.disabled = false;
    }
}
async function handleRegister(e) {
    e.preventDefault();
    const btn = document.getElementById('btn-register');
    const ogText = btn.innerHTML;
    try {
        const p1 = document.getElementById('r-pass')?.value || '';
        const p2 = document.getElementById('r-repass')?.value || '';
        if(p1 !== p2) { showToast('Mật khẩu nhập lại không khớp!', false); return; }
        btn.innerHTML = 'ĐANG KÍCH HOẠT...';
        btn.disabled = true;
        const payload = { 
            license_key: document.getElementById('r-key')?.value || '', 
            username: document.getElementById('r-user')?.value || '', 
            email: document.getElementById('r-email')?.value || '', 
            phone: document.getElementById('r-phone')?.value || '', 
            password: p1 
        };
        const res = await apiFetch('/api/auth/register', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        if (res && res.success) {
            showToast('Kích hoạt thành công! Vui lòng đăng nhập.', true);
            document.getElementById('register-form').reset();
            setTimeout(() => switchAuth('login'), 1500);
        } else {
            showToast(res && res.error ? res.error : 'Kích hoạt thất bại.', false);
        }
    } catch(err) {
        showToast('Lỗi kết nối máy chủ.', false);
    } finally {
        btn.innerHTML = ogText; btn.disabled = false;
    }
}