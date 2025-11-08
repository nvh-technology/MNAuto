# Hướng dẫn kiểm thử MNAuto

## Chuẩn bị

1. **Cài đặt Playwright Browsers**
   - Mở terminal trong thư mục MNAuto
   - Chạy lệnh: `dotnet tool install --global Microsoft.Playwright.CLI`
   - Chạy lệnh: `playwright install`
   - Hoặc chạy file `install-playwright.bat` (nếu có)

2. **Kiểm tra extension Lace**
   - Đảm bảo thư mục `lace` tồn tại cùng cấp với file thực thi
   - Kiểm tra file `lace/manifest.json` tồn tại

## Các bước kiểm thử

### 1. Khởi động ứng dụng

1. Mở terminal trong thư mục MNAuto
2. Chạy lệnh: `dotnet run`
3. Ứng dụng sẽ khởi động và hiển thị giao diện chính

### 2. Tạo Profiles

1. Nhập số lượng profile (ví dụ: 2)
2. Nhấn nút "Tạo Profiles"
3. Kiểm tra log hiển thị thông báo tạo thành công
4. Kiểm tra danh sách profiles hiển thị trong bảng

### 3. Khởi tạo Profiles (Quan trọng nhất)

1. Chọn 1 profile trong danh sách (đánh dấu checkbox)
2. Nhấn nút "Khởi tạo"
3. **Quan sát trình duyệt mở ra** (vì đã tắt headless)
4. **Theo dõi log chi tiết** các bước:
   - Tạo thư mục profile riêng tại `%TEMP%\MNAuto_Profile_X`
   - Truy cập trang tạo wallet
   - Click Next
   - Copy recovery phrase
   - Paste recovery phrase
   - Nhập mật khẩu
   - Mở wallet
   - Lấy địa chỉ wallet

### 4. Kiểm tra log chi tiết

Log sẽ hiển thị các bước theo định dạng:
```
HH:mm:ss [Profile Name]: [LEVEL] Message
```

Các log quan trọng cần kiểm tra:
- `BrowserService`: Đường dẫn extension Lace
- `ProfileX`: Bắt đầu khởi tạo wallet
- `ProfileX`: Truy cập trang tạo wallet
- `ProfileX`: Bước 1-7 các bước tạo wallet
- `ProfileX`: Đã tìm thấy địa chỉ wallet

### 5. Kiểm tra database

1. Mở file `%APPDATA%\MNAuto\profiles.db`
2. Kiểm tra bảng Profiles có chứa:
   - ID, Name, Recovery Phrase, Wallet Address, Wallet Password
   - Status = 1 (NotStarted) sau khi khởi tạo xong

### 6. Kiểm tra profile riêng biệt

1. Kiểm tra thư mục `%TEMP%\MNAuto_Profile_X` (X là ID của profile)
2. Mỗi profile sẽ có thư mục dữ liệu riêng biệt:
   - `MNAuto_Profile_1` cho Profile1
   - `MNAuto_Profile_2` cho Profile2
   - v.v.
3. Trong mỗi thư mục profile sẽ có:
   - Extension Data: Dữ liệu của extension Lace
   - Local Storage: Dữ liệu local của trình duyệt
   - Session Data: Thông tin phiên làm việc

### 6. Kiểm tra các chức năng khác

1. **Khởi động profile**:
   - Chọn profile đã khởi tạo
   - Nhấn "Khởi động"
   - Kiểm tra trình duyệt mở với wallet đã có

2. **Mở trình duyệt**:
   - Chọn profile
   - Nhấn "Mở trình duyệt"
   - Kiểm tra trình duyệt mở ở chế độ bình thường

3. **Đóng trình duyệt**:
   - Chọn profile đang chạy
   - Nhấn "Đóng"
   - Kiểm tra trình duyệt được đóng

4. **Xóa profile**:
   - Chọn một hoặc nhiều profile
   - Nhấn "Xóa"
   - Xác nhận trong dialog
   - Kiểm tra profile được xóa khỏi database và danh sách
   - Kiểm tra log hiển thị thông báo xóa thành công

## Xử lý lỗi thường gặp

### 1. Không tìm thấy extension

**Log**: `BrowserService: Đường dẫn extension Lace: [path]`

**Giải pháp**:
- Kiểm tra thư mục `lace` tồn tại
- Sao chép thư mục `lace` vào đúng vị trí
- Kiểm tra file `manifest.json` tồn tại

### 2. Không tìm thấy element

**Log**: `ProfileX: Chưa tìm thấy element địa chỉ wallet, chờ 3 giây`

**Giải pháp**:
- Kiểm tra trang web đã load xong chưa
- Kiểm tra selector có đúng không
- Thời gian chờ có thể cần tăng

### 3. Lỗi clipboard

**Log**: `ProfileX: Lỗi khi lấy recovery phrase`

**Giải pháp**:
- Kiểm tra quyền truy cập clipboard
- Thử chạy ứng dụng với quyền administrator

### 4. Lỗi Playwright

**Log**: `System: Lỗi khi khởi tạo Profile Manager`

**Giải pháp**:
- Cài đặt lại Playwright browsers
- Kiểm tra phiên bản Playwright tương thích

## Tối ưu hóa

1. **Giảm số lượng profile test**: Bắt đầu với 1-2 profile
2. **Tăng thời gian chờ**: Nếu mạng chậm, tăng thời gian chờ
3. **Kiểm tra tài nguyên**: Đảm bảo đủ RAM và CPU

## Log file

- Log được lưu tại: `%APPDATA%\MNAuto\MNAuto_Log_YYYYMMDD.txt`
- Log hiển thị trong ứng dụng (giới hạn 1000 dòng)
- Có thể xóa log bằng nút "Xóa Log"

## Ghi chú quan trọng

- **Đã tắt headless**: Trình duyệt sẽ hiển thị để dễ quan sát
- **SlowMo = 100ms**: Các thao tác sẽ chậm lại để dễ theo dõi
- **Log chi tiết**: Mọi bước đều được ghi lại để debug
- **Extension path**: Kiểm tra kỹ đường dẫn đến extension Lace