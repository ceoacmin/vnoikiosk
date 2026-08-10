const express = require('express');
const router = express.Router();
const db = require('../config/db');
const bcrypt = require('bcrypt');
const multer = require('multer');
const path = require('path');
const fs = require('fs');
const socketManager = require('../sockets/socketManager');
const storage = multer.diskStorage({
    destination: (req, file, cb) => {
        const dir = path.join(__dirname, '../../public/uploads');
        if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
        cb(null, dir);
    },
    filename: (req, file, cb) => cb(null, 'logo_' + req.session.adminId + '_' + Date.now() + path.extname(file.originalname))
});
const upload = multer({ storage: storage }).single('logo');
router.get('/data', async (req, res) => {
    try {
        const adminId = req.session.adminId;
        const { rows: admin } = await db.query("SELECT username, email, avatar_url FROM admins WHERE id = $1", [adminId]);
        const { rows: keys } = await db.query("SELECT key_code FROM admin_access_keys WHERE admin_id = $1", [adminId]);
        const { rows: exams } = await db.query("SELECT * FROM exams WHERE admin_id = $1 ORDER BY id DESC", [adminId]);
        const { rows: students } = await db.query("SELECT username, ip_address, machine_id, status, last_ping FROM students WHERE admin_id = $1 ORDER BY last_ping DESC", [adminId]);
        const studentsFormatted = students.map(s => {
            let count = 0;
            try {
                const logFile = path.join(__dirname, '../../logs', `${s.username}.json`);
                if (fs.existsSync(logFile)) count = JSON.parse(fs.readFileSync(logFile, 'utf8')).count;
            } catch(e) {}
            return {
                username: s.username, ip_address: s.ip_address, machine_id: s.machine_id,
                status: s.status, join_count: count, last_time: new Date(s.last_ping).toLocaleTimeString('vi-VN'),
                ping_ms: new Date(s.last_ping).getTime()
            };
        });
        res.json({ success: true, admin: admin[0], master_key: keys.length > 0 ? keys[0].key_code : 'CHƯA TẠO', exams: exams, students: studentsFormatted });
    } catch (e) { res.status(500).json({ success: false, error: e.message }); }
});
router.post('/reset_tracker', async (req, res) => {
    try {
        const { rows } = await db.query("SELECT username FROM students WHERE admin_id = $1", [req.session.adminId]);
        for (let student of rows) {
            const logFile = path.join(__dirname, '../../logs', `${student.username}.json`);
            if (fs.existsSync(logFile)) fs.unlinkSync(logFile);
        }
        await db.query("UPDATE students SET status = 'ONLINE' WHERE admin_id = $1", [req.session.adminId]);
        socketManager.getIO().emit('data_updated');
        res.json({ success: true });
    } catch (e) { res.json({ success: false, error: e.message }); }
});
router.post('/delete_students', async (req, res) => {
    try {
        const { usernames } = req.body;
        if (Array.isArray(usernames) && usernames.length > 0) {
            const placeholders = usernames.map((_, i) => `$${i + 2}`).join(', ');
            await db.query(`DELETE FROM students WHERE admin_id = $1 AND username IN (${placeholders})`, [req.session.adminId, ...usernames]);
            for (let uname of usernames) {
                const logFile = path.join(__dirname, '../../logs', `${uname}.json`);
                if (fs.existsSync(logFile)) fs.unlinkSync(logFile);
            }
            socketManager.getIO().emit('data_updated');
        }
        res.json({ success: true });
    } catch (e) { res.json({ success: false, error: e.message }); }
});
router.post('/masterkey/generate', async (req, res) => {
    try {
        const adminId = req.session.adminId;
        const newCode = 'CTNS-' + Math.random().toString(36).substring(2, 10).toUpperCase() + '-' + Math.random().toString(36).substring(2, 8).toUpperCase();
        await db.query("INSERT INTO admin_access_keys (admin_id, key_code) VALUES ($1, $2) ON CONFLICT (admin_id) DO UPDATE SET key_code = EXCLUDED.key_code, updated_at = NOW()", [adminId, newCode]);
        socketManager.getIO().emit('data_updated');
        res.json({ success: true, key: newCode });
    } catch (e) { res.json({ success: false, error: e.message }); }
});
router.post('/change_password', async (req, res) => {
    try {
        const { oldPassword, newPassword } = req.body;
        const { rows } = await db.query("SELECT password_hash FROM admins WHERE id = $1", [req.session.adminId]);
        if (rows.length > 0 && await bcrypt.compare(oldPassword, rows[0].password_hash)) {
            const hash = await bcrypt.hash(newPassword, 12);
            await db.query("UPDATE admins SET password_hash=$1 WHERE id=$2", [hash, req.session.adminId]);
            res.json({ success: true });
        } else res.json({ success: false, error: 'Mật khẩu cũ không chính xác' });
    } catch (e) { res.json({ success: false, error: e.message }); }
});
router.post('/upload_logo', (req, res) => {
    upload(req, res, async (err) => {
        if (err || !req.file) return res.json({ success: false, error: 'Lỗi tải tệp' });
        try {
            await db.query("UPDATE admins SET avatar_url=$1 WHERE id=$2", [req.file.filename, req.session.adminId]);
            socketManager.getIO().emit('data_updated');
            res.json({ success: true, avatar: req.file.filename });
        } catch(e) { res.json({ success: false, error: e.message }); }
    });
});
module.exports = router;