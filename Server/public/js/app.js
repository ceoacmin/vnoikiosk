async function apiFetch(url, options = {}) {
    try {
        const res = await fetch(url, options);
        if (res.status === 401) { window.location.href = '/'; return null; }
        return await res.json();
    } catch (err) { return null; }
}
function showToast(msg, isSuccess) {
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 999999; display: flex; flex-direction: column; gap: 12px; align-items: flex-end;';
        document.body.appendChild(container);
    }
    const toast = document.createElement('div');
    const colorClass = isSuccess ? 'bg-[#0b1120]/95 text-emerald-400 border-emerald-500/30' : 'bg-[#0b1120]/95 text-red-400 border-red-500/30';
    toast.className = `toast-enter flex items-center gap-3 px-5 py-4 rounded-xl border backdrop-blur-xl font-bold text-xs min-w-[280px] max-w-sm tracking-wide ${colorClass}`;
    toast.innerHTML = `<span class="flex-1">${msg}</span>`;
    container.appendChild(toast);
    setTimeout(() => { toast.classList.replace('toast-enter', 'toast-leave'); setTimeout(() => toast.remove(), 300); }, 3500);
}
function animateValue(obj, start, end, duration) {
    if(!obj) return;
    let startTimestamp = null;
    const step = (timestamp) => {
        if (!startTimestamp) startTimestamp = timestamp;
        const progress = Math.min((timestamp - startTimestamp) / duration, 1);
        const easeProgress = 1 - Math.pow(1 - progress, 4);
        obj.innerHTML = Math.floor(easeProgress * (end - start) + start);
        if (progress < 1) window.requestAnimationFrame(step);
    };
    window.requestAnimationFrame(step);
}
function copyDirect(text) {
    if(navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(() => showToast("Đã sao chép nội dung!", true)).catch(() => fallbackCopy(text));
    } else fallbackCopy(text);
}
function fallbackCopy(text) {
    let input = document.createElement('textarea');
    input.value = text;
    document.body.appendChild(input);
    input.select();
    try { document.execCommand('copy'); showToast("Đã sao chép nội dung!", true); } catch(err) { showToast("Không thể sao chép", false); }
    document.body.removeChild(input);
}
window.showModal = function(id) {
    const modal = document.getElementById(id);
    if(modal) modal.classList.remove('hidden');
};
window.hideModal = function(id) {
    const modal = document.getElementById(id);
    if(modal) {
        const content = modal.querySelector('.modal-content');
        if(content) {
            content.style.animation = 'modalScale 0.3s reverse forwards';
            setTimeout(() => { modal.classList.add('hidden'); content.style.animation = ''; }, 300);
        } else modal.classList.add('hidden');
    }
};
window.showLinkModal = function(link) {
    const el = document.getElementById('display-link-text');
    const hrefEl = document.getElementById('display-link-href');
    if(el && hrefEl) { el.innerText = link; hrefEl.href = link; window.showModal('modal-display-link'); }
};
window.showCodeModal = function(code) {
    const el = document.getElementById('display-code-text');
    if(el) { el.innerText = code; window.showModal('modal-display-code'); }
};