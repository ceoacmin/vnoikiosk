async function generateAccessKey() {
    if(confirm("LƯU Ý: Khởi tạo mã mới sẽ lập tức vô hiệu hóa mã cũ.\nBạn có chắc chắn muốn tạo mã mới?")) {
        const btn = document.getElementById('btn-generate-key');
        if(!btn) return;
        const ogText = btn.innerHTML;
        try {
            btn.innerHTML = 'ĐANG TẠO...';
            btn.disabled = true;
            const data = await apiFetch('/api/admin/masterkey/generate', { method: 'POST' });
            if(data && data.success) {
                const keyEl = document.getElementById('admin-access-key');
                if(keyEl) {
                    keyEl.style.transform = 'scale(0.95)'; keyEl.style.opacity = 0;
                    setTimeout(() => { keyEl.innerText = data.key; keyEl.style.transform = 'scale(1)'; keyEl.style.opacity = 1; }, 250);
                }
                showToast("Đã khởi tạo thành công Mã Truy Cập Bảo Mật mới!", true);
                if(typeof loadData === 'function') loadData();
            } else showToast(data && data.error ? data.error : "Hệ thống lỗi khi khởi tạo mã.", false);
        } catch(e) { showToast("Lỗi kết nối.", false); } finally { btn.innerHTML = ogText; btn.disabled = false; }
    }
}
async function createExam(e) {
    e.preventDefault();
    const btn = e.target.querySelector('button[type="submit"]');
    if(!btn) return;
    const ogText = btn.innerHTML;
    try {
        btn.innerHTML = '...';
        btn.disabled = true;
        const payload = {
            title: document.getElementById('exam-title')?.value || '',
            contest_link: document.getElementById('exam-url')?.value || '',
            access_code: document.getElementById('exam-code')?.value || ''
        };
        const res = await apiFetch('/api/admin/exams', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        if (res && res.success) { 
            document.getElementById('form-create-exam').reset(); 
            showToast("Đã tạo phòng thi thành công!", true);
            switchView('exams');
        } else showToast(res && res.error ? res.error : "Lỗi tạo phòng thi.", false);
    } catch(e) { showToast("Lỗi kết nối.", false); } finally { btn.innerHTML = ogText; btn.disabled = false; }
}
async function deleteExam(id) {
    if(confirm("CẢNH BÁO: Hành động này sẽ xóa vĩnh viễn bài thi khỏi hệ thống.\nTiếp tục?")) {
        const res = await apiFetch('/api/admin/exams/' + id, { method: 'DELETE' });
        if(res && res.success) {
            showToast("Đã xóa vĩnh viễn phòng thi.", true);
            if(typeof loadData === 'function') loadData();
        } else showToast(res && res.error ? res.error : "Lỗi xóa phòng thi.", false);
    }
}
async function changePassword(e) {
    e.preventDefault();
    const btn = document.getElementById('btn-change-pwd');
    if(!btn) return;
    const ogText = btn.innerHTML;
    try {
        const p1 = document.getElementById('set-new-pwd')?.value || '';
        const p2 = document.getElementById('set-confirm-pwd')?.value || '';
        if(p1 !== p2) { showToast('Mật khẩu mới không khớp!', false); return; }
        btn.innerHTML = '...'; btn.disabled = true;
        const payload = { oldPassword: document.getElementById('set-old-pwd')?.value || '', newPassword: p1 };
        const res = await apiFetch('/api/admin/change_password', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        if(res && res.success) {
            showToast('Cập nhật mật khẩu bảo mật thành công!', true);
            document.getElementById('form-change-pwd').reset();
        } else showToast(res && res.error ? res.error : 'Mật khẩu hiện tại không chính xác.', false);
    } catch(e) { showToast('Lỗi kết nối.', false); } finally { btn.innerHTML = ogText; btn.disabled = false; }
}
async function uploadLogo(e) {
    e.preventDefault();
    const btn = document.getElementById('btn-upload-logo');
    if(!btn) return;
    const ogText = btn.innerHTML;
    try {
        const fileInput = document.getElementById('set-logo-file');
        if(!fileInput || fileInput.files.length === 0) { showToast('Vui lòng chọn hình ảnh cần tải lên!', false); return; }
        btn.innerHTML = '...'; btn.disabled = true;
        const formData = new FormData();
        formData.append('logo', fileInput.files[0]);
        const response = await fetch('/api/admin/upload_logo', { method: 'POST', body: formData });
        const data = await response.json();
        if(data && data.success) {
            showToast('Cập nhật Logo hiển thị thành công!', true);
            document.getElementById('form-upload-logo').reset();
            if(typeof loadData === 'function') loadData();
        } else showToast(data && data.error ? data.error : 'Hệ thống từ chối tệp tin này.', false);
    } catch (err) { showToast('Lỗi đường truyền mạng.', false); } finally { btn.innerHTML = ogText; btn.disabled = false; }
}
async function logout() {
    await apiFetch('/api/auth/logout', { method: 'POST' });
    window.location.href = '/login';
}