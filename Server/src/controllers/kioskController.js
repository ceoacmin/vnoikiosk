const express = require('express');
const router = express.Router();
const db = require('../config/db');
const socketManager = require('../sockets/socketManager');
const fs = require('fs');
const path = require('path');
const logDir = path.join(__dirname, '../../logs');
if (!fs.existsSync(logDir)) fs.mkdirSync(logDir, { recursive: true });
router.post('/verify', async (req, res) => {
    try {
        const username = (req.body.username || req.body.Username || req.body.userName || '').toString().trim();
        const accessKey = (req.body.access_key || req.body.AccessKey || req.body.accessKey || '').toString().trim();
        const machineId = (req.body.machine_id || req.body.MachineId || req.body.machineId || '').toString().trim();
        let ip = req.headers['x-forwarded-for'] || (req.socket ? req.socket.remoteAddress : '') || req.ip || (req.body.IpAddress || '').toString().trim();
        if (typeof ip === 'string') {
            ip = ip.split(',')[0].trim();
            if (ip.startsWith('::ffff:')) ip = ip.substring(7);
        } else ip = 'Unknown';
        const { rows: adminKeys } = await db.query("SELECT admin_id FROM admin_access_keys WHERE key_code = $1", [accessKey]);
        let adminId = null, exams = [];
        if (adminKeys.length > 0) {
            adminId = adminKeys[0].admin_id;
            const { rows } = await db.query("SELECT title, contest_link, access_code FROM exams WHERE admin_id = $1 AND is_active = true", [adminId]);
            exams = rows;
        } else {
            const { rows } = await db.query("SELECT title, contest_link, access_code, admin_id FROM exams WHERE access_code = $1 AND is_active = true", [accessKey]);
            exams = rows;
            if (rows.length > 0) adminId = rows[0].admin_id;
        }
        if (adminId !== null) {
            const logFile = path.join(logDir, `${username}.json`);
            let tracker = { count: 0, lastTime: 0, isCheat: false };
            if (fs.existsSync(logFile)) tracker = JSON.parse(fs.readFileSync(logFile, 'utf8'));
            const now = Date.now();
            if (tracker.lastTime > 0) {
                const diffMins = (now - tracker.lastTime) / 60000;
                if (diffMins <= 30) tracker.count += 1;
                else tracker.count = 1;
            } else tracker.count = 1;
            tracker.lastTime = now;
            if (tracker.count >= 4) tracker.isCheat = true;
            fs.writeFileSync(logFile, JSON.stringify(tracker));
            const newStatus = tracker.isCheat ? 'CHEAT' : 'ONLINE';
            await db.query("INSERT INTO students (username, admin_id, ip_address, machine_id, status, last_ping) VALUES ($1, $2, $3, $4, $5, NOW()) ON CONFLICT (username) DO UPDATE SET ip_address = EXCLUDED.ip_address, machine_id = EXCLUDED.machine_id, status = $5, last_ping = NOW()", [username, adminId, ip, machineId, newStatus]);
            socketManager.getIO().emit('data_updated');
            res.json({ success: true, exams });
        } else res.json({ success: false, error: 'Mã Access Key không tồn tại hoặc đã đóng.' });
    } catch (e) { res.json({ success: false, error: e.message }); }
});
router.post('/ping', async (req, res) => {
    try {
        const machineId = (req.body.machine_id || req.body.MachineId || req.body.machineId || '').toString().trim();
        const status = (req.body.status || req.body.Status || 'ONLINE').toString().trim();
        const { rows } = await db.query("SELECT status FROM students WHERE machine_id=$1", [machineId]);
        if (rows.length > 0) {
            if (rows[0].status !== 'CHEAT') await db.query("UPDATE students SET status=$1, last_ping=NOW() WHERE machine_id=$2", [status, machineId]);
            else await db.query("UPDATE students SET last_ping=NOW() WHERE machine_id=$1", [machineId]);
        }
        socketManager.getIO().emit('data_updated');
        res.json({ success: true });
    } catch (e) { res.json({ success: false }); }
});
router.post('/exit_success', async (req, res) => {
    try {
        const username = (req.body.username || '').toString().trim();
        await db.query("UPDATE students SET status='ĐÃ NỘP BÀI', last_ping=NOW() WHERE username=$1", [username]);
        socketManager.getIO().emit('data_updated');
        res.json({ success: true });
    } catch (e) { res.json({ success: false }); }
});
router.post('/crash_report', async (req, res) => {
    try {
        const username = (req.body.username || '').toString().trim();
        await db.query("UPDATE students SET status='CRASHED' WHERE username=$1", [username]);
        socketManager.getIO().emit('data_updated');
        res.json({ success: true });
    } catch (e) { res.json({ success: false }); }
});
module.exports = router;