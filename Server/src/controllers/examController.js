const express = require('express');
const router = express.Router();
const db = require('../config/db');
const socketManager = require('../sockets/socketManager');

router.post('/', async (req, res) => {
    try {
        const { title, contest_link, access_code } = req.body;
        await db.query("INSERT INTO exams (admin_id, title, contest_link, access_code) VALUES ($1, $2, $3, $4)", [req.session.adminId, title, contest_link, access_code]);
        socketManager.getIO().emit('data_updated');
        res.json({ success: true });
    } catch (e) {
        console.error('[CREATE EXAM ERROR]', e.message);
        res.json({ success: false, error: e.message });
    }
});

router.delete('/:id', async (req, res) => {
    try {
        await db.query("DELETE FROM exams WHERE id = $1 AND admin_id = $2", [req.params.id, req.session.adminId]);
        socketManager.getIO().emit('data_updated');
        res.json({ success: true });
    } catch (e) {
        console.error('[DELETE EXAM ERROR]', e.message);
        res.json({ success: false, error: e.message });
    }
});

module.exports = router;