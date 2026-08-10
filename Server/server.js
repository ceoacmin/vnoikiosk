require('dotenv').config();
const express = require('express');
const http = require('http');
const cors = require('cors');
const path = require('path');
const session = require('express-session');
const socketIo = require('socket.io');

const middleware = require('./src/config/middleware');
const authController = require('./src/controllers/authController');
const adminController = require('./src/controllers/adminController');
const examController = require('./src/controllers/examController');
const kioskController = require('./src/controllers/kioskController');
const socketManager = require('./src/sockets/socketManager');

const app = express();
const server = http.createServer(app);
const io = socketIo(server, { cors: { origin: '*' }, maxHttpBufferSize: 1e8, pingTimeout: 60000 });

app.use((req, res, next) => {
    res.removeHeader('X-Powered-By');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('X-Frame-Options', 'SAMEORIGIN');
    res.setHeader('X-XSS-Protection', '1; mode=block');
    next();
});

app.use(cors());
app.use(express.json({ limit: '200mb' }));
app.use(express.urlencoded({ limit: '200mb', extended: true }));
app.use(session({
    secret: process.env.SESSION_SECRET || 'ctns_key_2026',
    resave: false,
    saveUninitialized: false,
    cookie: { maxAge: 1000 * 60 * 60 * 24 }
}));

app.use((req, res, next) => {
    console.log(`[REQ] ${req.method} ${req.url}`);
    next();
});

app.use('/css', express.static(path.join(__dirname, 'public/css')));
app.use('/js', express.static(path.join(__dirname, 'public/js')));
app.use('/uploads', express.static(path.join(__dirname, 'public/uploads')));

app.use('/api/auth', authController);
app.use('/api/kiosk', kioskController);
app.use('/api/webhook', kioskController);
app.use('/api/admin', middleware.isAuthenticated, adminController);
app.use('/api/admin/exams', middleware.isAuthenticated, examController);

app.post('/api/stream/push', (req, res) => res.json({ success: true }));

app.get('/login', (req, res) => res.sendFile(path.join(__dirname, 'public/index.html')));
app.get('/dashboard', (req, res) => {
    if (!req.session.adminId) return res.redirect('/login');
    res.sendFile(path.join(__dirname, 'public/dashboard.html'));
});
app.get('/support', (req, res) => res.sendFile(path.join(__dirname, 'public/support.html')));
app.get('/', (req, res) => res.sendFile(path.join(__dirname, 'public/index.html')));

app.use((err, req, res, next) => {
    console.error('[EXPRESS ERROR]', err);
    res.status(500).json({ success: false, error: "INTERNAL_SERVER_ERROR", message: err.message });
});

socketManager.init(io);

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
    console.log(`[OK] Kiosk Enterprise Server running on port ${PORT}`);
});