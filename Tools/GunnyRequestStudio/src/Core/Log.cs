using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GunnyRequestStudio.Core
{
    /// <summary>Logger thread-safe: ghi file + phat event cho UI.</summary>
    public class Logger
    {
        private readonly object _lock = new object();
        private readonly string _filePath;
        public event Action<LogEntry> OnLog;
        public List<LogEntry> History { get; private set; }

        public Logger(string filePath = null)
        {
            History = new List<LogEntry>();
            _filePath = filePath;
            if (!string.IsNullOrEmpty(_filePath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }
                catch { }
            }
        }

        public void Info(string message) { Write("INFO", message); }
        public void Okay(string message) { Write("OK", message); }
        public void Warn(string message) { Write("WARN", message); }
        public void Error(string message) { Write("ERR", message); }
        public void Step(string message) { Write("STEP", message); }

        private void Write(string level, string message)
        {
            var e = new LogEntry { Level = level, Message = message ?? string.Empty };
            lock (_lock)
            {
                History.Add(e);
                if (History.Count > 5000) History.RemoveRange(0, 1000);
                if (!string.IsNullOrEmpty(_filePath))
                {
                    try { File.AppendAllText(_filePath, e.ToString() + Environment.NewLine, Encoding.UTF8); } catch { }
                }
            }
            var h = OnLog;
            if (h != null) { try { h(e); } catch { } }
        }
    }
}
