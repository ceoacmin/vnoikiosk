module.exports = {
    isAuthenticated: (req, res, next) => {
        if (req.session && req.session.adminId) {
            return next();
        }
        res.status(401).json({ success: false, message: 'Unauthorized' });
    }
};