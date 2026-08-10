let ioInstance;
module.exports = {
    init: (io) => {
        ioInstance = io;
        io.on('connection', (socket) => {
            socket.emit('connected', { status: 'ok' });
        });
    },
    getIO: () => {
        if (!ioInstance) throw new Error("Socket.io uninitialized.");
        return ioInstance;
    }
};