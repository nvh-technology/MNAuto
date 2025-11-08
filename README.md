# MNAuto - Ứng dụng điều khiển trình duyệt hàng loạt

MNAuto là một ứng dụng Windows Forms được phát triển bằng C# và .NET 8, cho phép tạo và quản lý nhiều trình duyệt headless sử dụng Playwright với tích hợp extension Lace wallet.

## Tính năng chính

- **Tạo profile hàng loạt**: Tạo nhiều profile với mật khẩu ngẫu nhiên
- **Profile trình duyệt riêng biệt**: Mỗi profile có thư mục dữ liệu riêng, không chia sẻ dữ liệu
- **Khởi tạo trình duyệt tự động**: Tự động cài đặt và cấu hình Lace wallet
- **Quản lý trình duyệt**: Khởi động, mở, đóng trình duyệt hàng loạt
- **Hệ thống logging**: Theo dõi hoạt động của tất cả profiles
- **Lưu trữ dữ liệu**: Sử dụng SQLite để lưu thông tin profiles

## Cài đặt

### Yêu cầu hệ thống

- Windows 10 hoặc cao hơn
- .NET 8.0 Runtime
- Visual Studio 2022 (để biên dịch)

### Các bước cài đặt

1. Clone hoặc tải về mã nguồn
2. Mở project trong Visual Studio 2022
3. Build và chạy ứng dụng

## Sử dụng

### 1. Tạo Profiles

- Nhập số lượng profile cần tạo vào ô "Số lượng profile"
- Nhấn nút "Tạo Profiles"
- Các profile sẽ được tạo với tên "Profile1", "Profile2",...

### 2. Khởi tạo Profiles

- Chọn các profile cần khởi tạo (đánh dấu checkbox)
- Nhấn nút "Khởi tạo"
- Hệ thống sẽ tự động:
  - Tạo trình duyệt headless
  - Tải extension Lace
  - Tạo wallet mới
  - Lưu recovery phrase và địa chỉ wallet

### 3. Khởi động Profiles

- Chọn các profile đã khởi tạo
- Nhấn nút "Khởi động"
- Trình duyệt sẽ chạy ở chế độ headless với wallet đã sẵn sàng

### 4. Mở trình duyệt cho người dùng

- Chọn các profile cần mở
- Nhấn nút "Mở trình duyệt"
- Trình duyệt sẽ mở ở chế độ bình thường để người dùng thao tác

### 5. Đóng Profiles

- Chọn các profile cần đóng
- Nhấn nút "Đóng"
- Tất cả trình duyệt của các profile được chọn sẽ được đóng

## Cấu trúc dự án

```
MNAuto/
├── Models/
│   └── Profile.cs           # Model dữ liệu profile
├── Services/
│   ├── DatabaseService.cs   # Quản lý SQLite
│   ├── BrowserService.cs    # Quản lý Playwright
│   ├── LoggingService.cs    # Hệ thống logging
│   └── ProfileManagerService.cs # Dịch vụ chính
├── Form1.cs                 # Form chính
├── Form1.Designer.cs        # Thiết kế giao diện
└── lace/                    # Extension Lace wallet
```

## Lưu trữ dữ liệu

- **Database**: SQLite được lưu tại `%APPDATA%\MNAuto\profiles.db`
- **Logs**: File log được lưu tại `%APPDATA%\MNAuto\MNAuto_Log_YYYYMMDD.txt`
- **Profile Data**: Mỗi profile có thư mục dữ liệu riêng biệt tại `%TEMP%\MNAuto_Profile_X`
  - Mỗi profile có dữ liệu trình duyệt hoàn toàn riêng biệt
  - Không chia sẻ cookies, localStorage, extension data giữa các profile
  - Profile1: `%TEMP%\MNAuto_Profile_1`
  - Profile2: `%TEMP%\MNAuto_Profile_2`
  - v.v.

## Lưu ý quan trọng

1. **Extension Lace**: Đảm bảo thư mục `lace` tồn tại cùng cấp với file thực thi
2. **Quyền hệ thống**: Ứng dụng cần quyền truy cập clipboard để hoạt động
3. **Tài nguyên**: Mỗi profile tiêu thụ khoảng 100-200MB RAM khi chạy
4. **Network**: Cần kết nối internet để tải extension và tạo wallet

## Gỡ lỗi

### Các vấn đề thường gặp

1. **Không tìm thấy extension**
   - Kiểm tra đường dẫn thư mục `lace`
   - Đảm bảo file `manifest.json` tồn tại

2. **Lỗi khi tạo wallet**
   - Kiểm tra kết nối internet
   - Xem log chi tiết trong ứng dụng

3. **Trình duyệt không khởi động**
   - Kiểm tra quyền hệ thống
   - Đảm bảo không có firewall chặn

### Log chi tiết

- Mở ứng dụng và xem tab "Log" ở dưới cùng
- Log được lưu theo định dạng: `HH:mm:ss [Profile Name]: [LEVEL] Message`
- Các level log: INFO, WARN, ERROR

## Phát triển

### Công nghệ sử dụng

- **.NET 8.0**: Framework chính
- **Windows Forms**: Giao diện người dùng
- **Playwright**: Tự động hóa trình duyệt
- **SQLite**: Lưu trữ dữ liệu
- **Dapper**: ORM cho SQLite

### Mở rộng tính năng

1. Thêm hỗ trợ các loại wallet khác
2. Tích hợp với các DApp cụ thể
3. Thêm tính năng scheduling
4. Phát triển giao diện web

## Giấy phép

Dự án này được phát triển cho mục đích học tập và nghiên cứu.

## Liên hệ

Nếu có câu hỏi hoặc góp ý, vui lòng liên hệ qua email: [your-email@example.com]