<div align="center">

# 🛡️ VNOI KIOSK ENTERPRISE
**Hệ sinh thái Giám sát, Cách ly và Chống gian lận (Anti-Cheat) chuyên nghiệp dành riêng cho nền tảng VNOI & DMOJ**

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078d7.svg)]()
[![Stack](https://img.shields.io/badge/Stack-.NET%208.0%20%7C%20Node.js%20%7C%20PostgreSQL-success.svg)]()

*“Bảo vệ tư duy thuần túy. Lập trình bằng cái đầu, không phải bằng AI.”*

---

</div>

## 🌟 GIỚI THIỆU TỔNG QUAN ĐỀ ÁN

Trong kỷ nguyên bùng nổ của Trí tuệ Nhân tạo (AI) và các công cụ hỗ trợ lập trình (Copilot, ChatGPT, Gemini...), việc duy trì tính minh bạch và công bằng tuyệt đối trong các kỳ thi học sinh giỏi, Olympic Tin học hay các bài kiểm tra đánh giá năng lực thuật toán trực tuyến là một bài toán vô cùng nan giải. Thí sinh có thể dễ dàng gian lận chỉ bằng một cú chuyển tab hoặc một chiếc USB.

**VNOI Kiosk Enterprise** không đơn thuần là một trình duyệt web khóa màn hình. Đây là một **Hệ sinh thái An ninh Client-Server** khép kín, được thiết kế để can thiệp sâu vào nhân hệ điều hành máy trạm (OS Kernel), cô lập hoàn toàn môi trường thi đấu và cắt đứt mọi nỗ lực giao tiếp với thế giới bên ngoài. 

Hệ thống được tối ưu hóa kiến trúc hạ tầng để hoạt động hoàn hảo với mã nguồn của **DMOJ** và đặc biệt là nền tảng **VNOI (Vietnam Olympiad in Informatics)**.

---

## ⚙️ KIẾN TRÚC & NGUYÊN LÝ HOẠT ĐỘNG CỐT LÕI

Hệ thống vận hành song song giữa hai rào chắn: **Bức tường lửa vật lý tại Client** và **Hệ thống giám sát phân tích hành vi tại Server**.

### 🖥️ 1. PHẦN MỀM MÁY TRẠM (WINDOWS KIOSK CLIENT)
Được xây dựng trên nền tảng **C# .NET 8.0 (WPF) kết hợp lõi WebView2**, Client thực thi các chính sách bảo mật cực đoan ngay khi thí sinh xác nhận bước vào phòng thi:

*   **Cô lập Hệ điều hành (OS Level Lockdown):** 
    *   Đóng băng và "giết" (kill) hoàn toàn tiến trình `explorer.exe` (vô hiệu hóa thanh Taskbar, Start Menu, Desktop).
    *   Khóa cứng các tổ hợp phím thoát hiểm và hệ thống: `Alt+Tab`, `Alt+F4`, `Windows`, `Ctrl+Esc`, `Ctrl+Alt+Del`.
*   **Máy quét tiến trình độc hại (Anti-Cheat Thread):**
    *   Hệ thống sở hữu một luồng quét ngầm liên tục nhận diện và tiêu diệt các phần mềm vi phạm như: Phần mềm quay/chụp màn hình (OBS, Bandicam), Điều khiển từ xa (TeamViewer, UltraViewer, Anydesk), Mạng riêng ảo (VPN/Proxy), và các công cụ dịch ngược (Cheat Engine, Wireshark).
*   **Chống rò rỉ dữ liệu (Data Leak Prevention - DLP):**
    *   **Bảo mật DRM:** Màn hình ứng dụng sẽ tự động bôi đen nếu bị phần mềm thứ ba cố tình ghi hình.
    *   **Kiểm soát Clipboard:** Xóa sạch bộ nhớ tạm ngay khi vào thi và khi nộp bài xong, chặn đứng hành vi copy code từ nhà mang vào phòng thi.
    *   **Vô hiệu hóa Input:** Khóa tính năng kéo-thả (Drag & Drop), tải xuống (Download) và vô hiệu hóa toàn bộ thẻ `<input type="file">` của trình duyệt để chặn upload file lậu.
*   **Quản lý thiết bị phần cứng:** Tự động phát hiện và kích hoạt cảnh báo đỏ toàn màn hình nếu thí sinh cố tình cắm/rút thiết bị ngoại vi (USB) trái phép. Từ chối khởi động nếu phát hiện môi trường Ảo hóa (VMware, VirtualBox) hoặc thiết lập Đa màn hình (Multiple Monitors).
*   **Môi trường Compiler kiểm soát khắt khe:** Thí sinh chỉ được truy cập vào trang làm bài VNOI và danh sách trắng (Whitelist) gồm 4 trình biên dịch duy nhất: *ProgramIZ, CPP Shell, Online-IDE, Ideone*. Toàn bộ Facebook, Google, ChatGPT đều bị chặn từ trứng nước.

### ☁️ 2. HỆ THỐNG MÁY CHỦ TRUNG TÂM (SERVER BACKEND)
Vận hành bằng **Node.js, Express & PostgreSQL**, máy chủ đóng vai trò là "Nhãn quan hệ thống" (The Eye), giám sát hàng ngàn Client đồng thời qua giao thức **WebSocket** theo thời gian thực (Real-time).

*   **Định danh Kép (Dual Identification):** Xóa bỏ rủi ro IP ảo. Hệ thống tự động bắt **Public IP** từ tầng mạng HTTP Header, đối chiếu chéo với **Hardware ID** (Mã phần cứng vật lý của Client) để định danh chính xác duy nhất một cá thể, ngăn chặn việc thi hộ.
*   **Trí tuệ Nhân tạo Giám sát Hành vi (AI Tracker Engine):**
    *   Cơ chế "Nhịp tim" (Ping Mechanism): Client liên tục báo cáo sinh tín hiệu về Server mỗi 5 giây. 
    *   **Thuật toán trừng phạt:** Hệ thống thấu hiểu các sự cố rớt mạng thông thường. Tuy nhiên, nếu học sinh cố tình "offline" ngầm **từ 30 phút trở lên** và sau đó kết nối lại, AI Tracker sẽ tính là 1 lần vi phạm.
    *   Nếu hành vi này lặp lại đến ngưỡng **4 lần**, hệ thống sẽ lập tức gắn cờ **"CHEAT"** (Gian lận) vĩnh viễn trên Dashboard.
*   **High-throughput I/O Logging:** Tối ưu hóa Database bằng cách không ghi đè log liên tục vào SQL. Mọi đếm số vi phạm của AI Tracker được kết xuất ra các file vật lý `.json` độc lập, giúp Server chịu tải hàng ngàn request Ping/giây mà không sập.
*   **Quản trị viên toàn năng (Admin Dashboard):** Giám thị có góc nhìn toàn cảnh về phòng thi (Trạng thái Online Live, Crashed, Đã Nộp Bài), cho phép Reset AI Tracker, xuất báo cáo ra định dạng Excel (XLSX) và quản lý Access Code phân quyền.

---

## ⚖️ RÀNG BUỘC PHÁP LÝ & CẢNH BÁO BẢN QUYỀN (AGPL-3.0)

Dự án này là tâm huyết và chất xám của đội ngũ phát triển, được phân phối nghiêm ngặt dưới giấy phép **GNU Affero General Public License v3.0 (AGPL-3.0)**. Đây là một trong những giấy phép mã nguồn mở có sức răn đe pháp lý mạnh mẽ nhất thế giới.

### Tại sao lại là AGPL-3.0? (Giải quyết "SaaS Loophole")
Với các giấy phép cũ (như MIT hay GPL), một bên thứ ba có thể lấy mã nguồn Server của dự án này, tùy biến lại, và chạy nó trên máy chủ của họ để cung cấp dịch vụ (Software-as-a-Service) mà **không phải** công khai mã nguồn cho người dùng đầu cuối.

Giấy phép AGPL-3.0 được áp dụng để **đóng hoàn toàn lỗ hổng này**. 

### ⚠️ BẠN PHẢI TUÂN THỦ TUYỆT ĐỐI CÁC ĐIỀU KHOẢN SAU:

1.  **Tính lây nhiễm qua Mạng (Network Copyleft):** Nếu bạn (hoặc trường học, tổ chức, doanh nghiệp của bạn) lấy mã nguồn Server này về, **thực hiện bất kỳ sự thay đổi, tinh chỉnh nào** (dù chỉ là đổi tên, đổi logo, hoặc thêm/bớt tính năng), và vận hành nó để người dùng (học sinh/client) kết nối vào qua mạng LAN/Internet... **BẠN BẮT BUỘC PHẢI MỞ TOÀN BỘ MÃ NGUỒN ĐÃ CHỈNH SỬA ĐÓ CHO HỌ.**
2.  **Cung cấp Mã nguồn Phái sinh:** Ngay trên chính giao diện phần mềm Server mà bạn đang cung cấp, phải có nơi (nút bấm, link tải) để bất kỳ người dùng nào đang tương tác với hệ thống cũng có thể tải xuống bản sao nguyên vẹn mã nguồn mà máy chủ đó đang chạy.
3.  **Cấm Thương mại hóa Độc quyền (No Closed-source Commercialization):** Bạn tuyệt đối không được phép sử dụng VNOI Kiosk, đóng gói lại dưới dạng mã nguồn đóng (closed-source), và bán nó cho các trường học/tổ chức khác nhằm mục đích trục lợi cá nhân mà giấu nhẹm mã nguồn. Mọi phiên bản phái sinh của dự án này MÃI MÃI phải mang giấy phép AGPL-3.0.
4.  **Bảo toàn Tác giả gốc:** Mọi thông báo bản quyền, tên tổ chức phát triển ban đầu, và tệp License đính kèm trong mã nguồn phải được giữ lại nguyên vẹn.

> 🚨 **CẢNH BÁO:** Bất kỳ hành vi nào sử dụng mã nguồn này (đặc biệt là Server backend) để vận hành cung cấp dịch vụ, tổ chức thi cử mà cố tình **không công khai mã nguồn sửa đổi** đều cấu thành hành vi vi phạm pháp luật sở hữu trí tuệ quốc tế. Chúng tôi giữ toàn quyền truy cứu các hành vi cố tình lách luật giấy phép AGPL-3.0.

---
**CTNS Development - Nơi sự công bằng của khoa học máy tính bắt đầu.**
