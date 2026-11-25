using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GunnyRequestStudio.Core;
using GunnyRequestStudio.Core.Ui;
using GunnyRequestStudio.Pages;

namespace GunnyRequestStudio
{
    public static class Program
    {
        private static List<PageDescriptor> BuildPageDescriptors(StudioContext ctx)
        {
            return new List<PageDescriptor>
            {
                new PageDescriptor("Tổng quan",      "▥", () => new DashboardPage(ctx)),
                new PageDescriptor("Rip Request",    "↓", () => new RipPage(ctx)),
                new PageDescriptor("Import CSDL",    "▤", () => new ImportPage(ctx)),
                new PageDescriptor("Giải mã XML",    "◧", () => new DecodePage(ctx)),
                new PageDescriptor("Bomb / Vũ khí",  "◉", () => new BombPage(ctx)),
                new PageDescriptor("Cài đặt",        "⚙", () => new SettingsPage(ctx))
            };
        }

        private static void RunScreenshotMode(string outDir)
        {
            var logPath = System.IO.Path.Combine(outDir, "_shot-log.txt");
            Action<string> trace = m => { try { System.IO.File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + m + Environment.NewLine); } catch { } };
            try
            {
            System.IO.Directory.CreateDirectory(outDir);
            trace("start");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var ctx = new StudioContext();
            trace("context ok");
            var pages = BuildPageDescriptors(ctx);
            trace("pages built");
            var form = new MainForm(ctx, pages);
            trace("form built");
            form.Show();
            Application.DoEvents();
            trace("form shown");
            for (int i = 0; i < pages.Count; i++)
            {
                trace("page " + i);
                try { form.SelectPagePublic(i); } catch (Exception pe) { trace("page error: " + pe); }
                Application.DoEvents();
                System.Threading.Thread.Sleep(700);
                Application.DoEvents();
                var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                var name = string.Format("{0:00}-{1}.png", i + 1, Sanitize(pages[i].Title));
                var path = System.IO.Path.Combine(outDir, name);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
                trace("shot: " + path);
            }
            form.Close();
            trace("done");
            }
            catch (Exception ex)
            {
                trace("ERROR: " + ex);
            }
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "page";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
                sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
            return sb.ToString().Trim('-');
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // CLI mode: GunnyRequestStudio.exe --cli <command> [...]
            if (args != null && args.Length > 0 &&
                (string.Equals(args[0], "--cli", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "/cli", StringComparison.OrdinalIgnoreCase)))
            {
                var cliArgs = args.Skip(1).ToArray();
                int code = 0;
                try { code = Cli.Run(cliArgs); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("CLI ERROR: " + ex);
                    code = 2;
                }
                Environment.Exit(code);
                return;
            }

            // Screenshot mode: GunnyRequestStudio.exe --shots <thu muc xuat PNG>
            if (args != null && args.Length > 0 && string.Equals(args[0], "--shots", StringComparison.OrdinalIgnoreCase))
            {
                RunScreenshotMode(args.Length > 1 ? args[1] : "shots");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var crashLog = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "grs-crash.log");
            Action<Exception, string> logCrash = (ex, source) =>
            {
                try { System.IO.File.AppendAllText(crashLog, DateTime.Now.ToString("s") + " [" + source + "] " + ex + Environment.NewLine + new string('-', 80) + Environment.NewLine); } catch { }
            };
            Application.ThreadException += (s, e) => { logCrash(e.Exception, "ThreadException"); };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                if (ex != null) { logCrash(ex, "Unhandled"); return; }
                logCrash(new Exception(Convert.ToString(e.ExceptionObject)), "Unhandled");
            };

            var ctx = new StudioContext();
            var pages = BuildPageDescriptors(ctx);
            var form = new MainForm(ctx, pages);
            int startPage = 0;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--page", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(args[i + 1], out startPage);
                }
            }
            if (startPage > 0) form.SelectPagePublic(startPage);
            Application.Run(form);
        }
    }
}



