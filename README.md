# DragonBoy Account Manager (DBAM)

> Bộ công cụ quản lý tài khoản, điều khiển và tiện ích hỗ trợ game DragonBoy (Chú Bé Rồng Online).

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows&logoColor=white)](https://github.com/)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)

---

## Giới thiệu tổng quan

**DragonBoy Account Manager (DBAM)** là ứng dụng mã nguồn mở hỗ trợ quản trị đa tài khoản, điều phối cửa sổ và cung cấp các tiện ích thời gian thực cho tựa game **DragonBoy (Chú Bé Rồng Online)** trên nền tảng Windows.

Dự án tuân thủ nghiêm ngặt nguyên tắc **"Zero Client Patching"** (không can thiệp hoặc chỉnh sửa tệp gốc `Assembly-CSharp.dll` của game). Toàn bộ thao tác đọc trạng thái và điều khiển đều được thực hiện từ tiến trình ngoài (out-of-process) thông qua Win32 API và Mono Runtime C-API.

---

## Tính năng chính

- **Quản lý tài khoản:** Lưu trữ an toàn, thêm/sửa/xóa, chọn máy chủ và thao tác mở/đóng game nhanh theo từng tài khoản.
- **Điều khiển & Tinh chỉnh cửa sổ:** Tích hợp cụm phím điều hướng ảo, tùy chỉnh TimeScale (tốc độ game) và tự động sắp xếp cửa sổ (Grid, Tile, Cascade).
- **Radar thế giới thời gian thực:** Quét danh sách người chơi, Boss, quái vật và NPC trong cùng bản đồ; hỗ trợ tương tác tự động tới mục tiêu.
- **Tự động cập nhật:** Kiểm tra và nâng cấp phiên bản ứng dụng liền mạch trực tiếp từ GitHub Releases.
- **Đa dạng giao diện:**
  - **GUI (WPF):** Giao diện đồ họa thiết kế theo phong cách Retro Pixel, hỗ trợ đầy đủ thao tác trực quan.
  - **CLI (Console):** Giao diện dòng lệnh nhẹ, tối ưu cho môi trường điều hành nhanh hoặc máy chủ.

---

## Cấu trúc dự án

```text
DBAM/
├── docs/                               # Tài liệu kiến trúc giải pháp & cẩm nang mở rộng
├── src/
│   ├── DragonBoyAccountManager.Core/   # Thư viện lõi UI-agnostic: Mono runtime, Win32 P/Invoke, Telemetry, CQRS, Automation, Game Engine
│   ├── DragonBoyAccountManager.GUI/    # Giao diện đồ họa WPF (.NET 10, 3-Tier MVVM, Retro Pixel Design, Auto-Update)
│   └── DragonBoyAccountManager.CLI/    # Giao diện dòng lệnh Console (Spectre.Console)
├── tests/
│   └── DragonBoyAccountManager.Core.Tests/ # Bộ kiểm thử đơn vị (xUnit)
├── DBAM.slnx                           # Solution định dạng hiện đại (.slnx)
└── README.md                           # Tài liệu tổng quan dự án
```

---

## Yêu cầu & Cài đặt

### Yêu cầu hệ thống

- **Hệ điều hành:** Windows 10 / 11 (64-bit).
- **Bản chạy sẵn (Pre-built):** Tải bản phát hành tại mục Releases (gói Single-File tự chứa runtime, không cần cài đặt thêm .NET).
- **Biên dịch từ mã nguồn:** Yêu cầu cài đặt [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

### Biên dịch nhanh

```powershell
# Clone mã nguồn
git clone https://github.com/GunDx2k4/DragonBoyAccountManager.git
cd DragonBoyAccountManager

# Biên dịch toàn bộ solution
dotnet build DBAM.slnx -c Release

# Xuất bản gói GUI dạng tệp thực thi duy nhất
dotnet publish src/DragonBoyAccountManager.GUI/DragonBoyAccountManager.GUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/gui
```

---

## Đội ngũ & Đóng góp

| Vai trò | Thành viên | Nhiệm vụ |
| :--- | :--- | :--- |
| **Thiết kế & Ý tưởng** | **`l7ungdz`** | Định hình tính năng, trải nghiệm người dùng và phong cách giao diện |
| **Phát triển kỹ thuật** | **`Gehihi AI`** | Hiện thực hóa kiến trúc hệ thống, mã nguồn, thuật toán và CI/CD |

---

## Giấy phép

Dự án được phân phối dưới giấy phép **[GNU General Public License v3.0 (GPL-3.0)](LICENSE)**.
