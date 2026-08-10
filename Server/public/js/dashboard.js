let prevStats = { students: 0, exams: 0 };
let currentPage = 1;
const studentsPerPage = 25;
let allStudents = [];
const socket = typeof io !== 'undefined' ? io() : null;
let isDeleteMode = false;
let selectedStudents = new Set();
function switchView(viewName) {
    document.querySelectorAll('.view-section').forEach(el => el.classList.add('hidden'));
    const target = document.getElementById('view-' + viewName);
    if(target) target.classList.remove('hidden');
    document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('nav-active'));
    const nav = document.getElementById('nav-' + viewName);
    if(nav) nav.classList.add('nav-active');
    const titles = { 'overview': 'BẢNG THỐNG KÊ TỔNG QUAN', 'access': 'MÃ TRUY CẬP PHÒNG THI', 'addexam': 'TẠO BÀI THI MỚI', 'exams': 'QUẢN LÝ BÀI THI', 'students': 'GIÁM SÁT HỌC SINH', 'settings': 'CÀI ĐẶT BẢO MẬT' };
    const titleEl = document.getElementById('page-title');
    if(titleEl) {
        titleEl.style.opacity = 0;
        titleEl.style.transform = 'translateY(-5px)';
        setTimeout(() => {
            titleEl.innerText = titles[viewName] || 'WORKSPACE';
            titleEl.style.opacity = 1;
            titleEl.style.transform = 'translateY(0)';
        }, 150);
    }
    if(viewName !== 'settings' && viewName !== 'addexam') loadData();
}
function exportExcel() {
    if (allStudents.length === 0) { showToast("Không có dữ liệu để xuất!", false); return; }
    const data = allStudents.map((s, index) => ({
        "STT": index + 1, "Tài Khoản": s.username, "IP Address": s.ip_address, "Hardware ID": s.machine_id,
        "Trạng Thái": s.status === 'CHEAT' ? 'CHEAT (AI)' : (s.status === 'CRASHED' || s.status === 'CRASH' ? 'CRASHED' : (s.status === 'ĐÃ NỘP BÀI' || s.status === 'COMPLETED' ? 'ĐÃ NỘP' : 'ONLINE/OFFLINE')),
        "Số Lần Vi Phạm (AI)": s.join_count, "Lần Ping Cuối": s.last_time
    }));
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, "DanhSachHocSinh");
    XLSX.writeFile(wb, "DanhSachHocSinh_Kiosk.xlsx");
}
function toggleDeleteMode() {
    isDeleteMode = !isDeleteMode;
    selectedStudents.clear();
    const normalActions = document.getElementById('student-normal-actions');
    const deleteActions = document.getElementById('student-delete-actions');
    const thSelect = document.getElementById('th-select');
    const thAccount = document.getElementById('th-account');
    if (isDeleteMode) {
        if(normalActions) normalActions.classList.add('hidden');
        if(deleteActions) deleteActions.classList.remove('hidden');
        if(thSelect) thSelect.classList.remove('hidden');
        if(thAccount) thAccount.classList.replace('pl-6', 'pl-2');
    } else {
        if(normalActions) normalActions.classList.remove('hidden');
        if(deleteActions) deleteActions.classList.add('hidden');
        if(thSelect) thSelect.classList.add('hidden');
        if(thAccount) thAccount.classList.replace('pl-2', 'pl-6');
    }
    renderStudents();
}
function toggleStudentSelection(username) {
    if (selectedStudents.has(username)) selectedStudents.delete(username);
    else selectedStudents.add(username);
}
async function confirmDeleteStudents() {
    if (selectedStudents.size === 0) { showToast("Vui lòng chọn ít nhất 1 học sinh để xóa!", false); return; }
    if (confirm(`Bạn có chắc chắn muốn xóa ${selectedStudents.size} hồ sơ đã chọn?`)) {
        const res = await apiFetch('/api/admin/delete_students', { 
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ usernames: Array.from(selectedStudents) })
        });
        if (res && res.success) {
            showToast("Đã xóa các hồ sơ được chọn.", true);
            toggleDeleteMode();
            loadData();
        } else showToast("Lỗi khi xóa.", false);
    }
}
async function resetTracker() {
    if(!confirm("Bạn có chắc chắn muốn RESET trạng thái AI Tracker của TẤT CẢ học sinh không?")) return;
    const res = await apiFetch('/api/admin/reset_tracker', { method: 'POST' });
    if (res && res.success) { showToast("Đã Reset toàn bộ AI Tracker.", true); if(typeof loadData === 'function') loadData(); } 
    else showToast("Lỗi khi reset.", false);
}
function renderStudents() {
    const tbodyStudent = document.getElementById('student-list');
    if (!tbodyStudent) return;
    tbodyStudent.innerHTML = '';
    const totalPages = Math.ceil(allStudents.length / studentsPerPage) || 1;
    if (currentPage > totalPages) currentPage = totalPages;
    if (currentPage < 1) currentPage = 1;
    const curPageEl = document.getElementById('current-page');
    const totPageEl = document.getElementById('total-pages');
    const inputEl = document.getElementById('page-input');
    if(curPageEl) curPageEl.innerText = currentPage;
    if(totPageEl) totPageEl.innerText = totalPages;
    if(inputEl) inputEl.value = currentPage;
    const start = (currentPage - 1) * studentsPerPage;
    const end = start + studentsPerPage;
    const pageData = allStudents.slice(start, end);
    if (pageData.length === 0) {
        tbodyStudent.innerHTML = `<tr><td colspan="7" class="text-center text-slate-500 py-10 font-bold text-[10px] tracking-widest uppercase">Trống</td></tr>`;
        return;
    }
    const now = Date.now();
    pageData.forEach(s => {
        const pingTime = s.ping_ms || new Date(s.last_ping || s.last_time).getTime();
        const isOnline = (now - pingTime) <= 20000;
        let stText = 'OFFLINE'; let stColor = 'text-slate-500 bg-slate-800/50 border-slate-700';
        if (s.status === 'CHEAT') { stText = 'CHEAT (AI)'; stColor = 'text-red-400 bg-red-500/10 border-red-500/30'; }
        else if (isOnline && s.status !== 'CRASHED' && s.status !== 'ĐÃ NỘP BÀI' && s.status !== 'COMPLETED') { stText = 'ONLINE LIVE'; stColor = 'text-emerald-400 bg-emerald-500/10 border-emerald-500/30'; }
        else if (s.status === 'CRASHED' || s.status === 'CRASH') { stText = 'CRASHED'; stColor = 'text-red-400 bg-red-500/10 border-red-500/30'; }
        else if (s.status === 'ĐÃ NỘP BÀI' || s.status === 'COMPLETED') { stText = 'ĐÃ NỘP'; stColor = 'text-sky-400 bg-sky-500/10 border-sky-500/30'; }
        const timeStr = s.last_time || new Date(pingTime).toLocaleTimeString('vi-VN');
        const trackerStr = s.join_count > 0 ? `${s.join_count}/4 Lần` : `Sạch`;
        const checked = selectedStudents.has(s.username) ? 'checked' : '';
        const checkboxHtml = isDeleteMode ? `<td class="w-10 pl-6 text-center"><input type="checkbox" onchange="toggleStudentSelection('${s.username}')" ${checked} class="w-4 h-4 rounded border-slate-600 bg-slate-800 accent-red-500 cursor-pointer"></td>` : '';
        tbodyStudent.innerHTML += `
            <tr class="${isDeleteMode ? 'hover:bg-red-500/10' : ''}">
                ${checkboxHtml}
                <td class="font-bold text-sky-400 ${isDeleteMode ? 'pl-2' : 'pl-6'}">${s.username}</td>
                <td class="text-center font-mono text-[9px] text-slate-400">${s.ip_address}</td>
                <td class="text-center font-mono text-[9px] text-slate-500 truncate max-w-[100px] mx-auto" title="${s.machine_id}">${s.machine_id}</td>
                <td class="text-center text-slate-400 text-[10px] font-bold">${timeStr}</td>
                <td class="text-center"><span class="font-black text-[9px] px-2.5 py-1 rounded-lg border inline-flex items-center tracking-widest ${stColor}">${stText}</span></td>
                <td class="text-right pr-6"><span class="text-[9px] font-bold text-slate-400 mr-2">${trackerStr}</span></td>
            </tr>
        `;
    });
}
function changePage(dir) { currentPage += dir; renderStudents(); }
function goToPage(val) { currentPage = parseInt(val) || 1; renderStudents(); }
async function loadData() {
    try {
        const res = await apiFetch('/api/admin/data');
        if (!res) return;
        if (res.success) {
            const adminInfo = res.admin || {};
            const examsList = res.exams || [];
            allStudents = res.students || [];
            const mKey = res.master_key || 'CHƯA TẠO';
            const statSt = document.getElementById('stat-students');
            const statEx = document.getElementById('stat-exams');
            if(statSt) animateValue(statSt, prevStats.students, allStudents.length, 800);
            if(statEx) animateValue(statEx, prevStats.exams, examsList.length, 800);
            prevStats = { students: allStudents.length, exams: examsList.length };
            const adminNameEl = document.getElementById('admin-name');
            if(adminNameEl && adminInfo.username) adminNameEl.innerText = adminInfo.username;
            const initial = document.getElementById('avatar-initial');
            const logoImg = document.getElementById('sidebar-logo-img');
            if(initial && logoImg && adminInfo.username) {
                if(adminInfo.avatar_url && adminInfo.avatar_url !== 'default' && adminInfo.avatar_url !== 'default.png') {
                    initial.classList.add('hidden'); logoImg.src = '/uploads/' + adminInfo.avatar_url; logoImg.classList.remove('hidden');
                } else {
                    initial.innerText = adminInfo.username.charAt(0).toUpperCase(); initial.classList.remove('hidden'); logoImg.classList.add('hidden');
                }
            }
            const dashKey = document.getElementById('dash-access-key');
            const admKey = document.getElementById('admin-access-key');
            if(dashKey) dashKey.innerText = mKey;
            if(admKey) admKey.innerText = mKey;
            const tbodyExam = document.getElementById('exam-list');
            if(tbodyExam) {
                tbodyExam.innerHTML = '';
                if(examsList.length === 0) {
                    tbodyExam.innerHTML = `<tr><td colspan="4" class="text-center text-slate-500 py-10 font-bold text-[10px] tracking-widest uppercase">Trống</td></tr>`;
                } else {
                    examsList.forEach(e => {
                        tbodyExam.innerHTML += `
                            <tr>
                                <td class="font-bold text-sky-400 pl-6"><div class="truncate max-w-[200px]" title="${e.title}">${e.title}</div></td>
                                <td class="text-center"><button onclick="showLinkModal('${e.contest_link}')" class="btn-outline !py-1.5 !px-3 !text-[9px] !bg-blue-500/10 !text-blue-400 !border-blue-500/30 mx-auto">XEM</button></td>
                                <td class="text-center"><button onclick="showCodeModal('${e.access_code.replace(/'/g, "\\'")}')" class="btn-outline !py-1.5 !px-3 !text-[9px] !bg-emerald-500/10 !text-emerald-400 !border-emerald-500/30 mx-auto">XEM</button></td>
                                <td class="text-right pr-6"><button onclick="deleteExam(${e.id})" class="px-3 py-1.5 rounded-lg text-[9px] font-black bg-red-500/10 text-red-400 hover:bg-red-500 hover:text-white transition uppercase">XÓA</button></td>
                            </tr>
                        `;
                    });
                }
            }
            if(!isDeleteMode) renderStudents();
        }
    } catch(e) { }
}
if(socket) { socket.on('data_updated', loadData); socket.on('connected', loadData); }
if(document.getElementById('view-overview')) switchView('overview');