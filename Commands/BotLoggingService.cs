using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;

namespace Clara_bot.Commands
{
    public class BotLoggingService
    {
        private readonly string _logsDirectory;
        private readonly string _currentLogFile;
        private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private readonly Timer _flushTimer;
        private bool _isRunning = true;
        
        public BotLoggingService()
        {
            // Tạo thư mục logs nếu chưa tồn tại
            _logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
            
            // Tạo file log với thời gian bắt đầu chạy
            string startTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentLogFile = Path.Combine(_logsDirectory, $"log_{startTime}.txt");
            
            // Tạo file log trống và ghi log khởi động
            try
            {
                File.WriteAllText(_currentLogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 🚀 Bot logging service đã được khởi động." + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Không thể tạo file log: {ex.Message}");
            }
            
            // Set up timer để flush logs định kỳ (mỗi 5 giây)
            _flushTimer = new Timer(FlushLogs, null!, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }
        
        public void Log(string message)
        {
            if (!_isRunning) return;
            
            // Thêm timestamp vào message
            string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            
            // Luôn ghi ra console
            Console.WriteLine(timestampedMessage);
            
            // Chỉ thêm vào queue để timer xử lý, không ghi trực tiếp vào file ở đây
            _logQueue.Enqueue(timestampedMessage);
        }
        
        private void FlushLogs(object? state)
        {
            if (_logQueue.IsEmpty || !_isRunning) return;
            
            // Lấy tất cả logs từ queue và xóa chúng
            var logsToWrite = new List<string>();
            while (_logQueue.TryDequeue(out var logMessage))
            {
                logsToWrite.Add(logMessage);
            }
            
            if (logsToWrite.Count == 0) return;
            
            try
            {
                // Ghi tất cả logs cùng lúc để giảm số lần mở file
                using (var writer = File.AppendText(_currentLogFile))
                {
                    foreach (var logMessage in logsToWrite)
                    {
                        writer.WriteLine(logMessage);
                    }
                    writer.Flush(); // Đảm bảo dữ liệu được ghi ngay lập tức
                }
            }
            catch (Exception ex)
            {
                // Nếu không ghi được file, vẫn ghi ra console
                Console.WriteLine($"❌ Lỗi khi ghi log file: {ex.Message}");
            }
        }
        
        public void StopAndSaveFinalLogs()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            
            // Dừng timer
            _flushTimer?.Dispose();
            
            // Ghi tất cả logs còn lại trong queue
            FlushLogs(null);
            
            Log("🏁 Bot logging service đã dừng. Tất cả logs đã được lưu.");
        }
        
        public string GetCurrentLogFile()
        {
            return _currentLogFile;
        }
        
        public void LogToFile(string message)
        {
            if (!_isRunning) return;
            
            string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            
            try
            {
                // Ghi trực tiếp vào file với lock để tránh xung đột
                lock (_currentLogFile)
                {
                    File.AppendAllText(_currentLogFile, timestampedMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi ghi log file: {ex.Message}");
            }
        }
        
        public void LogImmediate(string message)
        {
            if (!_isRunning) return;
            
            string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            
            // Luôn ghi ra console
            Console.WriteLine(timestampedMessage);
            
            // Ghi ngay lập tức vào file
            try
            {
                lock (_currentLogFile)
                {
                    File.AppendAllText(_currentLogFile, timestampedMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi ghi log file: {ex.Message}");
            }
        }
        
        public void LogStatus()
        {
            try
            {
                if (File.Exists(_currentLogFile))
                {
                    var fileInfo = new FileInfo(_currentLogFile);
                    Console.WriteLine($"📁 File log: {_currentLogFile}");
                    Console.WriteLine($"📊 Kích thước: {fileInfo.Length} bytes");
                    Console.WriteLine($"🕐 Thời gian tạo: {fileInfo.CreationTime}");
                    Console.WriteLine($"🔄 Thay đổi lần cuối: {fileInfo.LastWriteTime}");
                    Console.WriteLine($"📝 Số log trong queue: {_logQueue.Count}");
                }
                else
                {
                    Console.WriteLine($"❌ File log không tồn tại: {_currentLogFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi kiểm tra file log: {ex.Message}");
            }
        }
    }
}