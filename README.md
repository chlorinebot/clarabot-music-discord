# Clara Bot

```text
   ___ _                     ___       _     _
  / __\ | __ _ _ __ __ _    / __\ ___ | |_  / \
 / /  | |/ _` | '__/ _` |  /__\/// _ \| __|/  /
/ /___| | (_| | | | (_| | / \/  \ (_) | |_/\_/
\____/|_|\__,_|_|  \__,_| \_____/\___/ \__\/  
```

> Logo được tạo bằng `FiggleFonts.Ogre`, cùng font ASCII mà bot hiển thị khi khởi động.

Clara là Discord bot đa năng viết bằng C# và .NET 10, tập trung vào phát nhạc qua Lavalink, quản lý cộng đồng và trò chuyện roleplay bằng AI. Bot hỗ trợ slash command, prefix command, hàng đợi nhạc, tự phục hồi kết nối và chẩn đoán hệ thống.

## Tổng quan

| Thành phần | Công nghệ |
|---|---|
| Runtime | .NET 10 |
| Discord SDK | Discord.Net 3.19.1 |
| Audio | Lavalink4NET 4.2.1 |
| Lavalink | `127.0.0.1:2333` |
| AI roleplay | Groq API |
| Phiên bản | 1.2.0 |

## Tính năng

- Phát nhạc và playlist YouTube qua Lavalink.
- Hàng đợi, tìm kiếm, lặp, trộn, chuyển bài và điều chỉnh tốc độ phát.
- Slash command được đăng ký tự động và xử lý bằng deferred response.
- Công cụ moderation: kick, ban, unban, role, warn, clear, lock và slowmode.
- Roleplay AI theo từng kênh bằng Groq API.
- Theo dõi CPU, RAM, latency và tốc độ mạng.
- Tự động giám sát, kết nối lại Discord và Lavalink.
- Ghi log theo phiên vào `bin/Debug/net10.0/logs`.
- Chế độ kiểm tra kết nối Discord và danh sách guild độc lập.

## Yêu cầu

- [.NET 10 SDK](https://dotnet.microsoft.com/download) để build từ source.
- Java 17 trở lên để chạy Lavalink.
- Discord bot token.
- Bật `Message Content Intent` và `Server Members Intent` trong Discord Developer Portal.
- Groq API key nếu sử dụng roleplay AI.
- Lavalink và plugin YouTube nếu sử dụng tính năng âm nhạc.

## Cài đặt

```powershell
git clone https://github.com/chlorinebot/Clara_bot.git
cd Clara_bot
dotnet restore
dotnet build --no-restore
```

### Biến môi trường

Tạo file `.env` ở thư mục gốc:

```env
DISCORD_TOKEN=your_discord_bot_token
GROQ_API_KEY=your_groq_api_key
YOUTUBE_API_KEY=your_youtube_api_key
```

| Biến | Bắt buộc | Mục đích |
|---|---:|---|
| `DISCORD_TOKEN` | Có | Xác thực bot với Discord |
| `GROQ_API_KEY` | Không | Bật tính năng roleplay AI |
| `YOUTUBE_API_KEY` | Không | Hỗ trợ chức năng YouTube cần API |

Không commit `.env`, token, API key hoặc webhook URL lên GitHub.

## Cấu hình Lavalink

```text
Clara_bot/
├── Lavalink.jar
├── application.yml
└── plugins/
    └── youtube-plugin-*.jar
```

Mặc định bot kết nối tới `http://127.0.0.1:2333` với password `youshallnotpass`.

Khởi động Lavalink trong terminal riêng:

```powershell
java -jar Lavalink.jar
```

Đợi Lavalink báo sẵn sàng trước khi dùng lệnh âm nhạc. Bot vẫn có thể kết nối Discord khi Lavalink chưa hoạt động và sẽ giám sát node trong nền.

## Khởi động bot

Sau khi đã build:

```powershell
dotnet run --no-build
```

Sau khi thay đổi source code:

```powershell
dotnet build --no-restore
dotnet run --no-build
```

Hoặc chạy binary:

```powershell
.\bin\Debug\net10.0\Clara_bot.exe
```

> Dùng `--no-build` để tránh restore/build không cần thiết mỗi lần khởi động.

## Kiểm tra kết nối

Kiểm tra token và Discord REST API mà không khởi động gateway:

```powershell
dotnet run --no-build -- --check-discord
```

Kiểm tra các guild bot có quyền truy cập:

```powershell
dotnet run --no-build -- --check-guilds
```

## Lệnh

### Thông tin và hệ thống

| Slash command | Prefix command | Mô tả |
|---|---|---|
| `/help` | `/heyclara`, `/help` | Hiển thị trợ giúp |
| `/info` | `/infoclara` | Thông tin bot và phiên bản |
| `/ping` | `/pingclara` | CPU, RAM, latency và tốc độ mạng |

### Âm nhạc

| Slash command | Prefix command | Mô tả |
|---|---|---|
| `/play query:<query>` | `/playclara <query>` | Phát nhạc hoặc playlist |
| `/search query:<query>` | `/searchclara <query>` | Tìm kiếm bài hát |
| `/pause` | `/pauseclara` | Tạm dừng |
| `/resume` | `/resumeclara` | Tiếp tục phát |
| `/stop` | `/stopclara` | Dừng và rời voice |
| `/next` | `/nextclara` | Chuyển bài kế tiếp |
| `/prev` | `/prevclara` | Quay lại bài trước |
| `/jump position:<n>` | `/jumpclara <n>` | Nhảy tới vị trí trong playlist |
| `/playlist` | `/showplaylistclara` | Hiển thị playlist hiện tại |
| `/qremove position:<n>` | `/removeclara <n>` | Xóa bài khỏi hàng đợi |
| `/qclear` | `/clearqueueclara` | Xóa hàng đợi |
| `/qmove from:<n> to:<n>` | `/movequeueclara <from> <to>` | Di chuyển bài trong hàng đợi |
| `/queue` | `/queueclara` | Bật hoặc tắt queue mode |
| `/loop` | `/loopclara` | Bật hoặc tắt lặp playlist |
| `/shuffle` | `/shufclara` | Trộn playlist |
| `/speed` | `/speedclara` | Điều chỉnh tốc độ phát |
| `/infoplay` | `/infoplayclara` | Thông tin bài đang phát |

### Moderation

| Slash command | Mô tả |
|---|---|
| `/kick` | Đuổi thành viên khỏi server |
| `/ban` | Cấm thành viên, hỗ trợ thời hạn và lý do |
| `/unban` | Gỡ cấm thành viên |
| `/role` | Thêm hoặc xóa role |
| `/warn` | Cảnh cáo thành viên |
| `/clear` | Xóa tin nhắn trong kênh |
| `/stopclear` | Dừng tác vụ xóa tin nhắn |
| `/lock` | Khóa kênh |
| `/unlock` | Mở khóa kênh |
| `/vkick` | Ngắt thành viên khỏi voice |
| `/slowmode` | Cấu hình slowmode |

Bot phải có quyền phù hợp và role nằm cao hơn thành viên cần quản lý.

### Roleplay

| Slash command | Prefix command | Mô tả |
|---|---|---|
| `/roleplay state:on` | `/roleplayclara on` | Bật roleplay trong kênh |
| `/roleplay state:off` | `/roleplayclara off` | Tắt roleplay trong kênh |

## Cấu trúc project

```text
Clara_bot/
├── Commands/                 # Command modules và dịch vụ bot
├── plugins/                  # Plugin Lavalink
├── Program.cs                # Bootstrap, gateway và reconnect
├── Clara_bot.csproj          # Cấu hình .NET và package
├── application.yml           # Cấu hình Lavalink
├── Lavalink.jar
└── README.md
```

## Xử lý sự cố

### Bot không kết nối Discord

```powershell
dotnet run --no-build -- --check-discord
```

- Kiểm tra `DISCORD_TOKEN` và privileged intents.
- Đảm bảo không có instance Clara khác đang chạy.
- Xem log mới nhất trong `bin/Debug/net10.0/logs`.
- Project ưu tiên IPv4 vì một số mạng NAT64/IPv6 có thể làm Discord gateway kẹt ở `Connecting`.

### Startup dừng lâu ở bước restore

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --no-build
```

### Bot online nhưng không phát nhạc

- Kiểm tra Java và tiến trình Lavalink.
- Kiểm tra `application.yml`, password và plugin YouTube.
- Xem log Lavalink để phát hiện video giới hạn tuổi, yêu cầu đăng nhập hoặc lỗi cipher.

### Roleplay không phản hồi

- Kiểm tra `GROQ_API_KEY`.
- Bật `Message Content Intent`.
- Bật roleplay trong đúng kênh.

## Bảo mật

- Không commit `.env`, file log, token hoặc API key.
- Không đăng secret vào issue, ảnh chụp màn hình hoặc CI log.
- Nếu secret từng bị push lên GitHub, hãy thu hồi và tạo secret mới.
- Chỉ cấp cho bot các quyền Discord thực sự cần thiết.

## Tác giả

- Kim Tuấn
- GitHub: [chlorinebot](https://github.com/chlorinebot)
- **Donate:** [https://i.pinimg.com/736x/1c/5c/5b/1c5c5beddb559e0f2b85b2f354ef75e1.jpg](https://i.pinimg.com/736x/1c/5c/5b/1c5c5beddb559e0f2b85b2f354ef75e1.jpg)

## License

Dự án được phát hành theo [MIT License](LICENSE).

Copyright © 2026 Kim Tuấn. Người dùng được phép sử dụng, sao chép, chỉnh sửa, hợp nhất, xuất bản, phân phối, cấp phép lại và bán các bản sao của phần mềm theo các điều kiện trong giấy phép.
