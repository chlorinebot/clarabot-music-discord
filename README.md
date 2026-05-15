# Clara Bot - Mô tả & Hướng dẫn sử dụng

## 1. Giới thiệu tổng quan

**Clara** là một Discord bot được viết bằng ngôn ngữ C# (.NET 10), sử dụng thư viện Discord.NET. Bot được thiết kế với mục tiêu mang đến trải nghiệm giải trí âm nhạc và trò chuyện thông minh cho cộng đồng người dùng Discord.

- **Phiên bản:** 1.0.1
- **Ngôn ngữ lập trình:** C# (.NET 10)
- **Thư viện Discord:** Discord.NET
- **Thư viện âm nhạc:** Lavalink4NET
- **Tác giả:** Kim Tuấn
- **GitHub:** https://github.com/chlorinebot

## 2. Hệ thống lệnh

Tất cả lệnh của Clara đều sử dụng tiền tố `/` (slash command qua prefix `/`).

### 2.1. Lệnh hệ thống & thông tin

| Lệnh | Mô tả |
|------|-------|
| `/heyclara` hoặc `/help` | Hiển thị danh sách tất cả các lệnh hiện có của bot |
| `/infoclara` | Hiển thị thông tin về bot, phiên bản, tác giả, GitHub và liên kết donate |

### 2.2. Lệnh phát nhạc

| Lệnh | Mô tả |
|------|-------|
| `/playclara [link]` hoặc `/p [link]` | Phát nhạc từ YouTube vào kênh voice. Có thể nhập link YouTube hoặc từ khóa tìm kiếm |
| `/pauseclara` | Tạm dừng bài hát đang phát |
| `/resumeclara` | Tiếp tục phát nhạc sau khi tạm dừng |
| `/stopclara` | Dừng phát nhạc hoàn toàn và ngắt kết nối khỏi kênh voice |
| `/prevclara` | Lùi về bài trước trong playlist |
| `/nextclara` | Chuyển sang bài tiếp theo trong playlist |
| `/jumpclara [n]` | Nhảy tới bài số n trong playlist đang phát |
| `/showplaylistclara` | Hiển thị danh sách bài hát trong playlist hiện tại (phân trang, 10 bài/trang) |
| `/loopclara` hoặc `/loop` | Bật/tắt chế độ lặp lại toàn bộ playlist |
| `/infoplayclara` | Hiển thị thông tin chi tiết bài hát đang phát (tiêu đề, tác giả, thời lượng, thời gian đã phát) |

### 2.3. Lệnh kiểm tra hệ thống

| Lệnh | Mô tả |
|------|-------|
| `/pingclara` | Kiểm tra và hiển thị thông tin tài nguyên máy chủ bao gồm: CPU, RAM, độ trễ mạng (ping), tốc độ tải xuống và tốc độ tải lên. Mỗi chỉ số đi kèm đánh giá màu sắc (🟢 Tốt, 🟡 Trung bình, 🟠 Cận tệ, 🔴 Tệ) |

### 2.4. Lệnh Roleplay (Trò chuyện AI)

| Lệnh | Mô tả |
|------|-------|
| `/roleplayclara on` | Bật chế độ roleplay, cho phép trò chuyện với Clara bằng tin nhắn thường trong kênh |
| `/roleplayclara off` | Tắt chế độ roleplay cho kênh hiện tại |

Khi chế độ roleplay được bật, Clara sẽ tự động phản hồi mọi tin nhắn trong kênh (không phải lệnh bot) bằng AI, đóng vai một nhân vật fantasy dễ thương và thân thiện. Clara sử dụng Groq API với model Llama-3.3-70B-Versatile để tạo phản hồi tự nhiên bằng tiếng Việt.

## 3. Kiến trúc kỹ thuật

### 3.1. Cấu trúc module

Clara được tổ chức thành các module riêng biệt, dễ bảo trì:

- **GeneralModule** - Xử lý lệnh hệ thống, trợ giúp và thông tin bot
- **MusicModule** - Xử lý toàn bộ chức năng phát nhạc, playlist và các thao tác điều khiển phát
- **RoleplayModule** - Xử lý trò chuyện AI thông qua Groq API
- **HeartModule** - Xử lý kiểm tra tài nguyên hệ thống và tốc độ mạng
- **CommandHandler** - Quản lý tiền xử lý tin nhắn, điều phối lệnh và xử lý button tương tác

### 3.2. Các tính năng kỹ thuật nổi bật

- **Hệ thống Lavalink:** Clara kết nối tới server Lavalink (mặc định `http://127.0.0.1:2333`) để xử lý phát nhạc, đảm bảo hiệu suất cao và không gây lag cho máy chủ.
- **Tự động kết nối lại:** Bot có cơ chế tự động thử kết nối lại Discord (tối đa 500 lần retry) khi bị mất kết nối, đảm bảo uptime tối đa.
- **Xử lý playlist thông minh:** Hỗ trợ phát playlist YouTube với hàng đợi, phân trang, nhảy bài, lặp lại và tự động bỏ qua các bài bị giới hạn đăng nhập/độ tuổi.
- **Auto-disconnect:** Bot tự động ngắt kết nối khỏi voice khi playlist kết thúc hoặc gặp lỗi nghiêm trọng.
- **Rate limiting nhẹ:** Mỗi kênh roleplay có semaphore để tránh spam, đảm bảo chỉ một yêu cầu AI được xử lý tại một thời điểm.
- **Đánh giá tài nguyên thông minh:** Lệnh `/pingclara` đo CPU bằng Windows API, RAM bằng `GlobalMemoryStatusEx`, tốc độ mạng qua Cloudflare và HTTPBin.
- **Phân chia tin nhắn dài:** Nếu phản hồi AI quá dài (vượt giới hạn 1900 ký tự của Discord), tin nhắn sẽ được tự động chia nhỏ theo dòng.

## 4. Yêu cầu hệ thống

### 4.1. Phụ thuộc bắt buộc

- **.NET 10 Runtime** hoặc cao hơn để chạy bản đã build trong thư mục `bin`
- **Lavalink Server** chạy tại `http://127.0.0.1:2333` với password `youshallnotpass` (cấu hình mặc định)
- **Discord Bot Token** - được đọc từ biến môi trường `DISCORD_TOKEN`
- **Groq API Key** - cần thiết cho chức năng roleplay AI, đọc từ biến môi trường `GROQ_API_KEY` hoặc file `.env`

### 4.2. Gateway Intents được sử dụng

Clara yêu cầu các Discord Gateway Intents sau để hoạt động:

- `Guilds` - Truy cập thông tin server
- `GuildMessages` - Đọc tin nhắn trong server
- `MessageContent` - Đọc nội dung tin nhắn (cần thiết cho roleplay)
- `GuildVoiceStates` - Quản lý kênh voice

> **Lưu ý:** Intent `MessageContent` cần được bật trong Discord Developer Portal để bot có thể đọc nội dung tin nhắn.

## 5. Cấu hình & Khởi chạy

### 5.1. Chuẩn bị bản build

Người dùng cuối **không cần chạy từ source code**. Chỉ cần tải thư mục bản build đã biên dịch sẵn, ví dụ:

```text
bin\Debug\net10.0\
```

Trong thư mục này cần giữ nguyên các file đi kèm như:

- `Clara_bot.exe`
- `Clara_bot.dll`
- `Clara_bot.runtimeconfig.json`
- `Clara_bot.deps.json`
- các file `.dll` phụ thuộc khác

> **Lưu ý:** Không chạy riêng từng file `.dll`. Hãy chạy trực tiếp `Clara_bot.exe` và giữ toàn bộ các file build trong cùng một thư mục.

### 5.2. Cấu hình biến môi trường

Các thông tin nhạy cảm (token, API key) được quản lý thông qua biến môi trường hoặc file `.env`:

| Biến | Mô tả | Bắt buộc |
|------|--------|----------|
| `DISCORD_TOKEN` | Token Discord Bot để xác thực với Discord API | Có |
| `GROQ_API_KEY` | API key của Groq để sử dụng AI chat (roleplay) | Có (cho roleplay) |

Cách đơn giản nhất là tạo file `.env` **trong cùng thư mục chạy bot**.

Ví dụ nếu bạn chạy bot từ:

```text
bin\Debug\net10.0\
```

thì hãy tạo file:

```text
bin\Debug\net10.0\.env
```

Nội dung mẫu của file `.env`:

```env
DISCORD_TOKEN=your_discord_bot_token_here
GROQ_API_KEY=your_groq_api_key_here
```

Ý nghĩa từng biến:

- `DISCORD_TOKEN`: token của bot Discord, bắt buộc để bot đăng nhập và hoạt động
- `GROQ_API_KEY`: API key của Groq, cần nếu bạn muốn dùng tính năng roleplay AI

Lưu ý khi tạo file `.env`:

- Không thêm dấu cách trước hoặc sau dấu `=`
- Không đặt thêm dấu nháy nếu không cần
- Lưu file đúng tên là `.env`
- Nên đặt file `.env` cùng thư mục với `Clara_bot.exe` hoặc `Clara_bot.dll` để bot đọc dễ nhất
- Nếu không có `GROQ_API_KEY`, bot vẫn có thể chạy nhưng tính năng roleplay sẽ báo thiếu key

### 5.3. Cài đặt plugin YouTube cho Lavalink

Bot cần plugin `youtube-source` để phát nhạc từ YouTube. Bạn cần:

**1. Tải plugin**
- Truy cập: https://github.com/lavalink-devs/youtube-source/releases
- Tải file `youtube-plugin-x.x.x.jar` (ví dụ: `youtube-plugin-1.18.1.jar`)

**2. Cài đặt**
- Tạo thư mục `plugins\` nếu chưa có
- Copy file `.jar` vừa tải vào thư mục `plugins\`
- Cấu trúc thư mục:
```
Clara_bot/
├── Lavalink.jar
├── application.yml
├── plugins/
│   └── youtube-plugin-x.xx.x.jar
└── bin/...
```

### 5.4. Khởi động Lavalink

Sau khi cài plugin, khởi động Lavalink bằng các file cấu hình đi kèm:

- `Lavalink.jar`
- `application.yml`
- thư mục `plugins\` (đã có plugin YouTube)

Chạy Lavalink trong thư mục chứa các file trên:

```bash
java -jar Lavalink.jar
```

Khi thấy log dạng:

```text
Lavalink is ready to accept connections.
```

thì mới tiếp tục mở bot.

### 5.4. Mở bot từ bản build

Sau khi Lavalink đã chạy, mở bot bằng file:

```text
Clara_bot.exe
```

Nếu bạn đang dùng Command Prompt hoặc PowerShell và đứng trong thư mục build, có thể chạy:

```powershell
.\Clara_bot.exe
```

Hoặc nếu muốn chạy bằng file `dll`, dùng lệnh:

```powershell
dotnet Clara_bot.dll
```

> **Lưu ý:** Với bản build sẵn, lệnh đúng là `dotnet Clara_bot.dll`, không phải `dotnet run Clara_bot.dll`.

> **Không cần** mở project bằng Visual Studio, **không cần** source code, và **không cần** cài `.NET SDK` nếu bạn chỉ sử dụng bản build sẵn. Chỉ cần `.NET Runtime` là đủ.

## 6. Cách sử dụng chi tiết

### 6.1. Phát nhạc đơn lẻ

```
/playclara https://www.youtube.com/watch?v=xxx
```
hoặc tìm kiếm bằng từ khóa:
```
/playclara tên bài hát
```

### 6.2. Phát playlist YouTube

```
/playclara https://www.youtube.com/playlist?list=xxx
```
Bot sẽ tự động tải toàn bộ playlist và phát lần lượt từng bài.

### 6.3. Điều khiển playlist

- Xem playlist: `/showplaylistclara` (có nút phân trang ⬅️ ➡️)
- Nhảy tới bài số 5: `/jumpclara 5`
- Chuyển bài tiếp: `/nextclara`
- Lùi bài trước: `/prevclara`
- Bật lặp: `/loopclara` (bật/tắt)

### 6.4. Trò chuyện với Clara

1. Bật roleplay: `/roleplayclara on`
2. Gửi tin nhắn bình thường (không có `/`) trong cùng kênh
3. Clara sẽ tự động trả lời với personality fantasy dễ thương
4. Tắt khi không cần: `/roleplayclara off`

### 6.5. Kiểm tra hệ thống

```
/pingclara
```
Bot sẽ hiển thị dashboard với các chỉ số: CPU, RAM, độ trễ ping, tốc độ tải xuống (Download), tốc độ tải lên (Upload).

## 7. Công nghệ sử dụng

| Thư viện/Công nghệ | Mục đích |
|---------------------|-----------|
| **Discord.NET** | Tương tác với Discord API |
| **Lavalink4NET** | Kết nối Lavalink để phát nhạc chất lượng cao |
| **Groq API (Llama-3.3-70B)** | Xử lý AI cho chế độ roleplay |
| **Figgle** | Hiển thị ASCII art banner khi khởi động |
| **Windows API (P/Invoke)** | Đo CPU sử dụng `GetSystemTimes` |
| **Cloudflare Speed Test** | Đo tốc độ tải xuống |
| **HTTPBin** | Đo tốc độ tải lên |
| **.NET 10** | Nền tảng runtime |

## 8. Xử lý lỗi thường gặp

### Lavalink chưa khởi động
- **Triệu chứng:** Bot online nhưng không phát được nhạc
- **Giải pháp:** Khởi động Lavalink server tại `http://127.0.0.1:2333` với password `youshallnotpass`

### YouTube yêu cầu đăng nhập/giới hạn tuổi
- **Triệu chứng:** Không phát được một số bài nhất định
- **Giải pháp:** Cấu hình youtube-plugin của Lavalink với cookies hoặc poToken + visitorData, sau đó restart Lavalink

### Roleplay không hoạt động
- **Triệu chứng:** Clara không trả lời tin nhắn thường
- **Giải pháp:** Kiểm tra biến môi trường `GROQ_API_KEY` đã được thiết lập đúng chưa

### Bot bị mất kết nối
- Bot sẽ tự động thử kết nối lại. Nếu không khôi phục sau nhiều lần thử, hãy kiểm tra token Discord và kết nối mạng.

## 9. Thông tin liên hệ & ủng hộ

- **GitHub:** https://github.com/chlorinebot
- **Tác giả:** Kim Tuấn
- **Donate:** https://i.pinimg.com/736x/1c/5c/5b/1c5c5beddb559e0f2b85b2f354ef75e1.jpg

---

> **Lưu ý bảo mật:** Không bao giờ chia sẻ Discord Token, Groq API Key hay bất kỳ thông tin nhạy cảm nào ra bên ngoài. Tất cả các API key và token nên được lưu trữ trong biến môi trường hoặc file `.env` (đã được thêm vào `.gitignore`).
