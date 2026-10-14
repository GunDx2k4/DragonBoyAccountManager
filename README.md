# 🐉 DragonBoy Account Manager (DBAM)

> **Tool hỗ trợ game DragonBoy (Chú Bé Rồng Online)**  
> 🎨 **Thiết kế bởi:** `l7ungdz`  
> 🤖 **Thực hiện bởi:** `Gehihi AI`

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows&logoColor=white)](https://github.com/)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)
[![GitHub Release](https://img.shields.io/badge/Release-v1.0.0-green.svg)](https://github.com/)

---

## 📌 Giới thiệu

**DragonBoy Account Manager (DBAM)** là bộ công cụ toàn diện hỗ trợ quản lý tài khoản, điều khiển và tiện ích cho tựa game **DragonBoy (Chú Bé Rồng Online - Ngọc Rồng Online)**.

Dự án được xây dựng trên nền tảng **.NET 10.0** hiện đại nhất, chú trọng vào tính an toàn, hiệu năng cao và **tuyệt đối không can thiệp hay chỉnh sửa tệp gốc của game** (`Assembly-CSharp.dll`). Toàn bộ cơ chế tương tác đều thông qua bộ nhớ tiến trình (Mono Runtime & Win32 API).

---

## ✨ Tính năng nổi bật

### 1. 📋 Quản lý tài khoản thông minh
- **Lưu trữ bảo mật**: Thêm, sửa, xóa tài khoản, ẩn/hiện mật khẩu, chọn máy chủ theo danh sách chuẩn.
- **Thao tác nhanh**: Nhấp đúp chuột (Double-Click) vào tài khoản để mở/đóng game tức thì.
- **Menu ngữ cảnh**: Chuột phải vào tài khoản để tự động đăng nhập, sao chép thông tin hoặc đóng cửa sổ tương ứng.

### 2. 🎮 Điều khiển & Tùy biến cửa sổ
- **Bảng điều khiển phím ảo**: Hỗ trợ cụm phím điều hướng (Lên, Xuống, Trái, Phải, Chọn) tương tác trực tiếp với nhân vật.
- **Điều chỉnh tốc độ (TimeScale)**: Thanh kéo tùy chỉnh tốc độ game từ `0.1x` đến `5.0x` mượt mà.
- **Sắp xếp cửa sổ tự động**: Tự động dàn trang các cửa sổ game theo dạng Grid (lưới), Tile hoặc Cascade; tùy chỉnh kích thước cửa sổ game.

### 3. 🗺️ Radar & Tiện ích bản đồ
- **Giám sát thời gian thực**: Quét và hiển thị chi tiết danh sách **Người chơi**, **Boss**, **Quái vật (Mob)** và **NPC** trên cùng bản đồ.
- **Tương tác thông minh**: Nhấp đúp chuột vào bất kỳ thực thể nào (Player, Boss, Mob, NPC) để nhân vật tự động di chuyển đến và chọn mục tiêu đó.
- **Kiểm tra phiên bản Client**: Tự động quét và đối soát mã băm SHA256 để đảm bảo client game tương thích tốt nhất.

### 4. 🔄 Tự động cập nhật (In-App Auto-Update)
- Tự động kiểm tra bản cập nhật mới từ **GitHub Releases**.
- Cơ chế hoán đổi tệp đang chạy trên Windows không gián đoạn, tự động tải về với thanh tiến trình trực quan và khởi động lại phiên bản mới ngay lập tức.
- Tự động dọn dẹp tệp cũ sau khi nâng cấp thành công.

### 5. 💻 Đa nền tảng giao diện
- **GUI (WPF)**: Giao diện đồ họa mang đậm phong cách pixel cổ điển của DragonBoy kết hợp nét hiện đại, trực quan và dễ sử dụng.
- **CLI (Console)**: Giao diện dòng lệnh chuyên nghiệp với `Spectre.Console`, tối ưu cho quản trị viên và máy chủ không cần màn hình.

---

## 🛠️ Cấu trúc dự án

```text
DBAM/
├── .github/workflows/
│   ├── build.yml               # CI build & kiểm thử tự động
│   └── release.yml             # Tự động đóng gói & phát hành GitHub Release
├── src/
│   ├── DragonBoyAccountManager.Core/   # Thư viện lõi (Mono runtime, Game Engine, Memory, Auto-Update)
│   ├── DragonBoyAccountManager.GUI/    # Giao diện đồ họa WPF (.NET 10 Windows x64)
│   └── DragonBoyAccountManager.CLI/    # Giao diện dòng lệnh Console (Spectre.Console)
├── DBAM.slnx                   # Solution file định dạng mới (.slnx)
└── README.md                   # Tài liệu dự án
```

---

## 🚀 Hướng dẫn cài đặt & Chạy

### Yêu cầu hệ thống
- Hệ điều hành: **Windows 10 / 11 (64-bit)**.
- Khi tải bản phát hành đóng gói sẵn (`Single-File`): **Không cần cài đặt thêm .NET Runtime** (đã tích hợp sẵn).
- Nếu tự biên dịch từ mã nguồn: Cần cài đặt [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

### Tự biên dịch từ mã nguồn

```powershell
# 1. Clone repository
git clone https://github.com/GunDx2k4/DragonBoyAccountManager.git
cd DragonBoyAccountManager

# 2. Khôi phục các gói phụ thuộc
dotnet restore DBAM.slnx

# 3. Biên dịch toàn bộ solution
dotnet build DBAM.slnx -c Release

# 4. Xuất bản tệp thực thi duy nhất (Single-File) cho GUI
dotnet publish src/DragonBoyAccountManager.GUI/DragonBoyAccountManager.GUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./publish/gui
```

---

## 📖 Hướng dẫn sử dụng nhanh

1. **Chọn tệp game**:
   - Ở thanh trạng thái dưới cùng của ứng dụng GUI, bấm **"Chọn file..."** và chọn tệp thực thi (`.exe`) của game Chú Bé Rồng Online.
2. **Thêm tài khoản**:
   - Nhập Tên đăng nhập, Mật khẩu, chọn Máy chủ mong muốn rồi nhấn **LƯU**.
3. **Mở game**:
   - Nhấp đúp chuột vào tài khoản trong danh sách để mở game và điều khiển trực tiếp qua DBAM.
4. **Kiểm tra phiên bản & Cập nhật**:
   - Vào tab **HƯỚNG DẪN / TIỆN ÍCH**, bấm **"Kiểm tra phiên bản"** để kiểm tra client game cũng như cập nhật tool khi có bản phát hành mới.

---

## 👥 Đội ngũ & Đóng góp (Credits)

Dự án này là thành quả hợp tác sáng tạo:

| Vai trò | Thành viên | Ghi chú |
| :--- | :--- | :--- |
| **Thiết kế & Ý tưởng** | **`l7ungdz`** | Định hình tính năng, trải nghiệm người dùng và giao diện |
| **Phát triển kỹ thuật** | **`Gehihi AI`** | Hiện thực hóa kiến trúc, mã nguồn, thuật toán và hệ thống CI/CD |

---

## ⚖️ Giấy phép (License)

Dự án được phân phối dưới giấy phép **[GNU General Public License v3.0 (GPL-3.0)](LICENSE)**. Bạn được tự do sử dụng, chỉnh sửa và phân phối lại mã nguồn với điều kiện giữ nguyên giấy phép và ghi nhận quyền tác giả.
