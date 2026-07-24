# vnoikiosk

Trình Khóa máy tính Kiosk Mode chặn AI cho VNOI (và các trang dùng mã nguồn của DMOJ và VNOI)

Hướng dẫn cài đặt:

-Client:


Bước 1: Tải và cài đặt .NET

Bước 2: Giải nén file zip "Client"

Bước 3: Vào domain.env và điền địa chỉ web máy chủ Server

Bước 4: Trỏ CMD vào thư mục Client và nhập lệnh: "dotnet run"

Bước 5: Sau đó mở thư mục Client/bin/debug/net8.0-windows/ và click mở file exe

(Có thể build file setup chính thức)

-Server:
Bước 1: Tải và cài đặt PostgreSQL và NODEJS

Bước 2: Giải nén folder SERVER và chỉnh sửa file .env thành những thông tin của database

Bước 3: Vào postgre và nhập file SQL

Bước 4: Mở CMD (Hoặc Terminal đối với linux)

Bước 5: ghi: "npm install" và đợi

Bước 6: Sau khi cài đặt thành công thì nhập "node server.js"

Bước 7: Tạo tài khoản và Ghi 1 key trong database
