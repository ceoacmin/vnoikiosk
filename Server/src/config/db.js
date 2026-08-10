const { Pool } = require('pg');
require('dotenv').config();

const pool = new Pool({
    host: process.env.DB_HOST || 'localhost',
    user: process.env.DB_USER || 'postgres',
    password: process.env.DB_PASS || 'postgres',
    database: process.env.DB_NAME || 'vnoi_kiosk',
    port: process.env.DB_PORT || 5432,
});

pool.connect()
    .then(() => console.log('[OK] Connected to PostgreSQL'))
    .catch(err => console.error('[DB ERROR] Connection failed:', err.message));

pool.on('error', (err) => {
    console.error('[DB ERROR] Unexpected error on idle client', err);
});

module.exports = pool;