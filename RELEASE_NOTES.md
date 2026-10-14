# DragonBoy Account Manager (DBAM) - Release Notes

## Phiên bản v1.0.0 (Official Release)

Phát hành chính thức phiên bản đầu tiên của **DragonBoy Account Manager (DBAM)** - Công cụ quản lý và tự động hóa đa tài khoản Ngọc Rồng Online (Dragon Boy).

### Tính năng nổi bật

#### 1. Quản lý tài khoản toàn diện
- Quản lý danh sách tài khoản theo server, nhóm và trạng thái.
- Đăng nhập tự động, ghi nhớ thông tin và phiên đăng nhập.
- Tab Tài khoản & Tiện ích đồng bộ đầy đủ các tác vụ nhanh:
  - Nút chuyển khu nhanh: `<` (Khu trước) và `>` (Khu sau).
  - Phím tắt hành động nhanh: **Capsule [C]**, **Tự động luyện tập [X]**.
  - **Skill Hotbar**: Hiển thị bảng kỹ năng trực quan, kích hoạt skill trực tiếp từ giao diện quản lý tài khoản.

#### 2. Kiểm tra tính toàn vẹn Game Client (Version & SHA256)
- Tự động nhận diện thư mục game và file `Assembly-CSharp.dll` trong `Data\Managed`.
- Đối chiếu độc lập cả **Phiên bản (Version)** và **Mã băm SHA256**:
  - Nhận diện bản gốc chính thức, bản mod hoặc phiên bản cũ cần cập nhật.
  - Tự động quét `FileVersionInfo` và cấu trúc tên thư mục/tệp để nhận diện phiên bản.
  - Cảnh báo trực quan qua badge màu sắc và tooltip chi tiết (đường dẫn, kích thước, SHA256 thực tế & mong muốn).

#### 3. Hệ thống Cập nhật Tự động (Auto-Updater Kép)
- Kiểm tra cập nhật ứng dụng DBAM trực tiếp từ GitHub Releases.
- Cơ chế đối chiếu kép: Kiểm tra cả **Số phiên bản (Version)** và **Mã SHA (Git Commit SHA & SHA256)**:
  - Hỗ trợ phát hiện các bản vá hotfix / commit mới ngay cả khi cùng phiên bản.
  - Tự động tải và xác thực tính toàn vẹn của file thực thi trước khi cập nhật.

#### 4. Đóng gói & Phát hành Single-File Executable
- Cung cấp 2 phiên bản độc lập:
  - **DragonBoyAccountManager-GUI.exe**: Giao diện đồ họa hiện đại WPF (Material Design).
  - **DragonBoyAccountManager-CLI.exe**: Giao diện dòng lệnh console gọn nhẹ.
- Đi kèm file kiểm tra tính toàn vẹn: `checksums.txt`, `DragonBoyAccountManager-GUI.exe.sha256`, `DragonBoyAccountManager-CLI.exe.sha256`.
