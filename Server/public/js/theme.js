setInterval(() => {
    const timeEl = document.getElementById('clock');
    if (timeEl) {
        const now = new Date();
        const options = { 
            timeZone: 'Asia/Ho_Chi_Minh', 
            hour12: false, 
            hour: '2-digit', 
            minute: '2-digit', 
            second: '2-digit' 
        };
        timeEl.innerText = now.toLocaleTimeString('vi-VN', options);
    }
}, 1000);