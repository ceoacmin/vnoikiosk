const express = require('express');
const router = express.Router();
const db = require('../config/db');
const bcrypt = require('bcrypt');

router.post('/register', async (req, res) => {
    const { username, email, phone, password, license_key } = req.body;
    try {
        const { rows: keys } = await db.query("SELECT id FROM admin_license_keys WHERE key_code = $1 AND is_used = false", [license_key]);
        if (keys.length === 0) return res.json({ success: false, error: 'License Key không hợp lệ hoặc đã được sử dụng.' });
        
        const hash = await bcrypt.hash(password, 12);
        await db.query("INSERT INTO admins (username, email, phone, password_hash) VALUES ($1, $2, $3, $4)", [username, email, phone, hash]);
        await db.query("UPDATE admin_license_keys SET is_used = true WHERE id = $1", [keys[0].id]);
        
        console.log(`[REGISTER] Success: ${username}`);
        res.json({ success: true });
    } catch (e) {
        console.error('[REGISTER ERROR]', e.message);
        res.json({ success: false, error: e.message.includes('unique constraint') ? 'Tài khoản hoặc Email đã tồn tại.' : e.message });
    }
});

router.post('/login', async (req, res) => {
    const { username, password } = req.body;
    try {
        const { rows } = await db.query("SELECT * FROM admins WHERE username = $1", [username]);
        if (rows.length > 0 && await bcrypt.compare(password, rows[0].password_hash)) {
            req.session.adminId = rows[0].id;
            console.log(`[LOGIN] User ${username} logged in.`);
            res.json({ success: true });
        } else {
            console.log(`[LOGIN] Failed attempt for ${username}`);
            res.json({ success: false, error: 'Sai tài khoản hoặc mật khẩu' });
        }
    } catch (e) {
        console.error('[LOGIN ERROR]', e.message);
        res.json({ success: false, error: e.message });
    }
});

router.post('/logout', (req, res) => {
    req.session.destroy();
    res.json({ success: true });
});

module.exports = router;