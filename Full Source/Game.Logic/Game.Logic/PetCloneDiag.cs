using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Game.Logic;

/// <summary>
/// Ghi log chẩn đoán riêng cho cơ chế Phân Thân (Clone Posture) ra
/// &lt;thư mục server&gt;/logs/PetClone.log để test có thể kết luận được
/// phân thân còn hay mất sau turn địch.
/// Không dùng log4net để tránh phụ thuộc cấu hình appender.
/// </summary>
internal static class PetCloneDiag
{
    private static readonly object SyncRoot = new object();

    private static readonly string LogPath = BuildPath();

    public static string FilePath
    {
        get { return LogPath; }
    }

    private static string BuildPath()
    {
        try
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "PetClone.log");
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string message)
    {
        if (LogPath == null)
        {
            return;
        }
        try
        {
            string line = string.Format(
                "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}{3}",
                DateTime.Now,
                Thread.CurrentThread.ManagedThreadId,
                message,
                Environment.NewLine);
            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // chẩn đoán không được phép làm gãy game
        }
    }

    /// <summary>Tên ngắn của living để log dễ đọc: "Nick#id".</summary>
    public static string Name(object living)
    {
        if (living == null)
        {
            return "null";
        }
        try
        {
            Game.Logic.Phy.Object.Player player = living as Game.Logic.Phy.Object.Player;
            if (player != null && player.PlayerDetail != null && player.PlayerDetail.PlayerCharacter != null)
            {
                return string.Concat(player.PlayerDetail.PlayerCharacter.NickName, "#", player.Id);
            }
            Game.Logic.Phy.Object.Living l = living as Game.Logic.Phy.Object.Living;
            if (l != null)
            {
                return string.Concat("living#", l.Id);
            }
        }
        catch
        {
        }
        return living.ToString();
    }
}
