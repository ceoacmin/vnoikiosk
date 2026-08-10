# HƯỚNG DẪN TRIỂN KHAI MÁY CHỦ CTNS KIOSK ENTERPRISE

Tài liệu này cung cấp hướng dẫn từng bước để cài đặt, cấu hình và triển khai Hệ thống Máy chủ CTNS Kiosk Enterprise trên nền tảng hệ điều hành **Ubuntu Server** (khuyến nghị bản 20.04 LTS, 22.04 LTS hoặc 24.04 LTS).

Hệ thống sử dụng **Node.js** làm lõi xử lý, **Socket.IO** để giao tiếp thời gian thực với máy trạm và **PostgreSQL** để lưu trữ cơ sở dữ liệu.

---

## MỤC LỤC
1. [Yêu cầu hệ thống](#1-yêu-cầu-hệ-thống)
2. [Cài đặt và Cấu hình PostgreSQL](#2-cài-đặt-và-cấu-hình-postgresql)
3. [Khởi tạo Cơ sở dữ liệu (Import SQL)](#3-khởi-tạo-cơ-sở-dữ-liệu-import-sql)
4. [Cài đặt Node.js và NPM](#4-cài-đặt-nodejs-và-npm)
5. [Cấu hình biến môi trường (.env)](#5-cấu-hình-biến-môi-trường-env)
6. [Khởi chạy máy chủ](#6-khởi-chạy-máy-chủ)
7. [Cấu hình Tường lửa (Firewall)](#7-cấu-hình-tường-lửa-firewall)

---

## 1. YÊU CẦU HỆ THỐNG
* Hệ điều hành: **Ubuntu Linux**
* Quyền truy cập: Có quyền `sudo` hoặc `root`.
* Môi trường mạng: Có kết nối Internet để tải các package cài đặt.

---

## 2. CÀI ĐẶT VÀ CẤU HÌNH POSTGRESQL

### 2.1. Cập nhật hệ thống và Cài đặt PostgreSQL
Mở Terminal và chạy tuần tự các lệnh sau:
```bash
sudo apt update && sudo apt upgrade -y
sudo apt install postgresql postgresql-contrib -y
```

### 2.2. Kiểm tra trạng thái dịch vụ
Đảm bảo PostgreSQL đã được bật và tự động chạy khi khởi động lại máy chủ:
```bash
sudo systemctl start postgresql
sudo systemctl enable postgresql
sudo systemctl status postgresql
```

### 2.3. Cấu hình tài khoản và Cơ sở dữ liệu
Truy cập vào tài khoản mặc định `postgres`:
```bash
sudo -i -u postgres psql
```
Khi giao diện command line chuyển thành `postgres=#`, hãy chạy các lệnh SQL sau để tạo CSDL và cấu hình mật khẩu (mặc định trong hệ thống đang cấu hình pass là `postgres`):

```sql
-- Đổi mật khẩu cho user postgres (để kết nối qua Node.js)
ALTER USER postgres WITH PASSWORD 'postgres';

-- Tạo cơ sở dữ liệu cho dự án Kiosk
CREATE DATABASE vnoi_kiosk;

-- Cấp quyền truy cập
GRANT ALL PRIVILEGES ON DATABASE vnoi_kiosk TO postgres;

-- Thoát
\q
```
*Lưu ý: Bạn có thể đổi mật khẩu `postgres` thành mật khẩu khác an toàn hơn (nhưng chỉ có chữ cái thường, hoa và số), và nhớ phải cập nhật lại tương ứng trong file `.env` (và không thêm dấu "").*

---

## 3. KHỞI TẠO CƠ SỞ DỮ LIỆU (IMPORT SQL)

Tải toàn bộ mã nguồn máy chủ (thư mục chứa file `database.sql`, `server.js`...) lên Ubuntu Server (vào một thư mục ví dụ: `/var/www/kiosk-server`).

Di chuyển vào thư mục chứa mã nguồn:
```bash
cd /var/www/kiosk-server
```

Thực thi file `database.sql` để tạo các bảng (admins, exams, students...) và nạp dữ liệu mẫu (License Key):
```bash
sudo -u postgres psql -d vnoi_kiosk -f database.sql
```
*Nếu thông báo hiển thị `CREATE TABLE` và `INSERT 0 5`, việc khởi tạo đã thành công 100%.*

---

## 4. CÀI ĐẶT NODE.JS VÀ NPM

Hệ thống yêu cầu Node.js phiên bản 18.x hoặc 20.x LTS. Ở đây chúng ta sẽ cài đặt bản 20.x:

```bash
# Thêm repository của NodeSource
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -

# Cài đặt Node.js
sudo apt install -y nodejs
```

Kiểm tra lại phiên bản sau khi cài đặt thành công:
```bash
node -v   # Yêu cầu v18+ hoặc v20+
npm -v    # Yêu cầu v9+ hoặc v10+
```

---

## 5. CẤU HÌNH BIẾN MÔI TRƯỜNG (.env)

Tại thư mục gốc của dự án (nơi chứa file `server.js`), hãy đảm bảo có file `.env`. Nếu chưa có, hãy tạo tệp `.env`:
```bash
nano .env
```

Nhập cấu hình mặc định như sau:
```env
DB_HOST=localhost
DB_USER=postgres
DB_PASS=postgres
DB_NAME=vnoi_kiosk
DB_PORT=5432

# Mã hóa Session bảo mật cho Admin
SESSION_SECRET=ctns_kiosk_enterprise_2026

# Cổng mạng ứng dụng sẽ chạy
PORT=3000
```
Nhấn `Ctrl + O` để lưu, `Enter` để xác nhận và `Ctrl + X` để thoát nano.

---

## 6. KHỞI CHẠY MÁY CHỦ

### 6.1. Cài đặt các thư viện phụ thuộc (Dependencies)
Tại thư mục mã nguồn, cài đặt các module khai báo trong `package.json` (Express, PG, Socket.io, Bcrypt...):
```bash
npm install
```

### 6.2. Chạy thử máy chủ (Chế độ phát triển)
```bash
node server.js
```
Nếu màn hình Terminal log ra thông báo `[OK] Connected to PostgreSQL` và `[OK] Kiosk Enterprise Server running on port 3000`, máy chủ đã hoạt động hoàn hảo. Nhấn `Ctrl + C` để tắt.

### 6.3. Khởi chạy chính thức (Chế độ Production với PM2)
Để máy chủ chạy ngầm liên tục, tự động restart khi sập hoặc khi VPS khởi động lại, chúng ta dùng `pm2`:
```bash
# Cài đặt pm2 toàn cầu
sudo npm install -g pm2

# Khởi chạy server
pm2 start server.js --name "ctns-kiosk"

# Lưu tiến trình để tự chạy cùng hệ thống khi khởi động lại VPS
pm2 save
pm2 startup
```

---

## 7. CẤU HÌNH TƯỜNG LỬA (FIREWALL)

Hệ thống hoạt động mặc định ở cổng `3000`. Máy trạm C# Client và người quản trị sẽ truy cập thông qua cổng này, do đó cần mở khóa cổng trên tường lửa UFW của Ubuntu:

```bash
# Mở port 3000
sudo ufw allow 3000/tcp

# Kiểm tra lại trạng thái các port đã mở
sudo ufw status
```

*Lưu ý: Nếu bạn đang sử dụng dịch vụ Cloud VPS của AWS, Google Cloud, hoặc Azure, bạn cần phải mở thêm port `3000` ở phần "Security Groups" / "Inbound Rules" trên bảng điều khiển của nhà cung cấp Cloud đó.*

---

## 8. HƯỚNG DẪN TRUY CẬP VÀ ĐĂNG KÝ ADMIN LẦN ĐẦU

1. Truy cập vào trang quản trị trên trình duyệt qua địa chỉ: `http://<IP_MAY_CHU_CUA_BAN>:3000/`
2. Chọn tab **KÍCH HOẠT**.
3. Nhập **License Key VIP** mặc định của hệ thống để khởi tạo tài khoản Super Admin (Các License mặc định nằm trong `database.sql`):
   - `CTNS-2026-VIP-KEY-001`
   - `CTNS-2026-VIP-KEY-002`
   - `CTNS-2026-VIP-KEY-003`
4. Khai báo Tên đăng nhập và Mật khẩu tùy ý, hệ thống sẽ mã hóa và lưu vào PostgreSQL.
5. Quay lại tab **ĐĂNG NHẬP** và bắt đầu quản lý phòng thi!
