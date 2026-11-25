using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GunnyRequestStudio.Core
{
    /// <summary>
    /// Che do dong lenh (headless) de tu dong hoa: rip, map, import, bomb, test-db.
    /// Dung: GunnyRequestStudio.exe --cli &lt;command&gt; [options]
    /// </summary>
    public static class Cli
    {
        public static int Run(string[] args)
        {
            var ctx = new StudioContext();
            var log = ctx.Log;
            var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
            var opts = ParseOptions(args.Skip(1).ToArray());
            log.OnLog += e => Console.WriteLine(e.ToString());

            try
            {
                switch (cmd)
                {
                    case "help":
                    case "-h":
                    case "--help":
                        PrintHelp();
                        return 0;
                    case "test-db":
                        {
                            string msg;
                            var ok = ctx.TestDb(out msg);
                            Console.WriteLine(msg);
                            return ok ? 0 : 1;
                        }
                    case "names":
                        {
                            var names = ctx.Ripper.DiscoverNames(ctx.Settings);
                            Console.WriteLine("Tim thay " + names.Count + " ten request (Request + mirror).");
                            foreach (var n in names) Console.WriteLine("  " + n);
                            return 0;
                        }
                    case "rip":
                        return CmdRip(ctx, opts);
                    case "inspect":
                        {
                            var folder = opts.ContainsKey("dir") ? opts["dir"] : ctx.Settings.MirrorDir;
                            foreach (var name in (opts.ContainsKey("_sub") ? new[] { opts["_sub"] } : new string[0]))
                            {
                                var path = Path.Combine(folder, name + ".xml");
                                if (!File.Exists(path)) { Console.WriteLine("Khong thay " + path); continue; }
                                var d = XmlCodec.Inspect(path);
                                Console.WriteLine("=== " + name + " root=" + d.RootElement + " wrapper=" + d.Wrapper + " item=" + d.ItemElement + " rows=" + d.ItemCount + " err=" + d.Error);
                                Console.WriteLine("  attrs: " + string.Join(", ", d.Attributes.ToArray()));
                                if (d.Rows.Count > 0)
                                {
                                    Console.WriteLine("  row0: " + string.Join(", ", d.Rows[0].Select(kv => kv.Key + "=" + kv.Value).ToArray()));
                                }
                            }
                            return 0;
                        }
                    case "map":
                        return CmdMap(ctx, opts);
                    case "import":
                        return CmdImport(ctx, opts);
                    case "audit":
                        return CmdAudit(ctx, opts);
                    case "decode":
                        return CmdDecode(ctx, opts);
                    case "bomb":
                        return CmdBomb(ctx, opts);
                    default:
                        Console.Error.WriteLine("Lenh khong hop le: " + cmd);
                        PrintHelp();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex);
                return 2;
            }
        }

        // ------------------------------------------------------------ commands

        private static int CmdRip(StudioContext ctx, Dictionary<string, string> o)
        {
            var s = ctx.Settings;
            var names = ctx.Ripper.DiscoverNames(s);
            if (names.Count == 0) { Console.Error.WriteLine("Khong tim thay ten request nao."); return 1; }
            var opt = new RipOptions
            {
                Overwrite = !o.ContainsKey("keep"),
                PromoteTemplates = !o.ContainsKey("no-promote"),
                PromoteAll = o.ContainsKey("promote-all"),
                DryRun = o.ContainsKey("dry"),
                Concurrency = o.ContainsKey("concurrency") ? int.Parse(o["concurrency"]) : ctx.Settings.Concurrency,
                RefreshFromPublisher = !o.ContainsKey("no-refresh")
            };
            Console.WriteLine("Rip " + names.Count + " request -> " + s.MirrorDir);
            Console.WriteLine("Nguon: " + string.Join(", ", (s.PublisherBases ?? new List<string>()).ToArray()));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = ctx.Ripper.Rip(s, names, opt, null, CancellationToken.None);
            ctx.Ripper.SaveNames(s, names);
            Console.WriteLine(string.Format("RIP: OK={0} unchanged={1} missing={2} failed={3} bytes={4} trong {5}s",
                report.Stats.Ok, report.Stats.Unchanged, report.Stats.Missing, report.Stats.Failed, report.Stats.Bytes, report.Stats.Seconds));

            if (opt.PromoteTemplates && !opt.DryRun)
            {
                // mapper can schema
                try { var _ = ctx.Schema; } catch (Exception ex) { Console.Error.WriteLine("Khong nap duoc schema: " + ex.Message); }
                var promoteReport = ctx.Ripper.Promote(s, report, opt.Overwrite, opt.PromoteAll, null, CancellationToken.None);
                Console.WriteLine("PROMOTE: " + promoteReport.Stats.Promoted + " file vao " + s.RequestDir + " (" + promoteReport.Stats.PromotedUnchanged + " giong het)");
            }
            var reportPath = Path.Combine(ctx.ReportsDir, "request-rip-report.json");
            ctx.Ripper.SaveReport(report, reportPath);
            var mirrorCopy = Path.Combine(s.MirrorDir, "_request-rip-report.json");
            ctx.Ripper.SaveReport(report, mirrorCopy);
            Console.WriteLine("Report: " + reportPath);
            Console.WriteLine("Mirror report: " + mirrorCopy);
            return 0;
        }

        private static int CmdMap(StudioContext ctx, Dictionary<string, string> o)
        {
            ctx.Schema.ToString();
            var docs = ctx.LoadRequestDocs(ctx.Settings.MirrorDir);
            var rows = new List<MappingRow>();
            foreach (var d in docs)
            {
                var m = ctx.Mapper.Resolve(d);
                rows.Add(new MappingRow
                {
                    Xml = d.Name, Items = d.ItemCount, Table = m.TableName, Status = m.Status,
                    Score = m.Score, Attrs = m.XmlAttributeCount,
                    MissingRequired = string.Join(",", m.MissingRequiredColumns.ToArray()),
                    Ignored = string.Join(",", m.IgnoredAttributes.ToArray()),
                    Problem = m.Problem,
                    Candidates = string.Join(" | ", m.Candidates.Select(c => c.TableName + "(" + c.Score.ToString("0.00") + ")").ToArray())
                });
            }
            var grouped = rows.GroupBy(r => r.Status).OrderByDescending(g => g.Count());
            Console.WriteLine("=== MAPPING " + rows.Count + " file XML -> bang CSDL ===");
            foreach (var g in grouped) Console.WriteLine(string.Format("  {0,-12} {1}", g.Key, g.Count()));
            Console.WriteLine();
            foreach (var r in rows.Where(x => x.Status != "none").OrderBy(x => x.Xml, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine(string.Format("  {0,-42} -> {1,-34} [{2}] items={3}", r.Xml, r.Table, r.Status, r.Items));
            Console.WriteLine();
            Console.WriteLine("=== KHONG MAP DUOC (" + rows.Count(x => x.Status == "none" || x.Status == "ambiguous") + ") ===");
            foreach (var r in rows.Where(x => x.Status == "none" || x.Status == "ambiguous").OrderBy(x => x.Xml, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine(string.Format("  {0,-42} items={1,-7} status={2,-11} {3}", r.Xml, r.Items, r.Status, r.Problem ?? r.Candidates));
            Paths.EnsureDir(ctx.ReportsDir);
            var jsonPath = Path.Combine(ctx.ReportsDir, "mapping-report.json");
            Json.SaveFile(jsonPath, new MappingReport
            {
                GeneratedAt = DateTime.Now.ToString("s"),
                Total = rows.Count,
                Mapped = rows.Count(r => r.Status != "none" && r.Status != "ambiguous"),
                Rows = rows
            });
            Console.WriteLine();
            Console.WriteLine("Report: " + jsonPath);
            return 0;
        }

        private static int CmdImport(StudioContext ctx, Dictionary<string, string> o)
        {
            var docs = ctx.LoadRequestDocs(ctx.Settings.MirrorDir);
            var opt = new ImportOptions
            {
                Backup = !o.ContainsKey("no-backup"),
                DryRun = o.ContainsKey("dry"),
                Force = o.ContainsKey("force"),
                BatchSize = o.ContainsKey("batch") ? int.Parse(o["batch"]) : 2000
            };
            if (o.ContainsKey("only"))
            {
                foreach (var n in o["only"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    opt.OnlyXmls.Add(n.Trim());
            }
            if (o.ContainsKey("table"))
            {
                foreach (var n in o["table"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    opt.OnlyTables.Add(n.Trim());
            }
            if (o.ContainsKey("no-preserve")) opt.PreserveLocalColumns = false;
            if (o.ContainsKey("allow-local-loss")) opt.AllowLocalColumnLoss = true;
            if (o.ContainsKey("skip-table"))
            {
                foreach (var n in o["skip-table"].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (!opt.SkipTables.Any(x => string.Equals(x, n.Trim(), StringComparison.OrdinalIgnoreCase))) opt.SkipTables.Add(n.Trim());
            }
            try
            {
                var ov = ctx.Overrides;
                if (ov != null && ov.SkipTables != null)
                    foreach (var n in ov.SkipTables)
                        if (!opt.SkipTables.Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase))) opt.SkipTables.Add(n);
            }
            catch { }
            Console.WriteLine("Import " + docs.Count + " file XML tu " + ctx.Settings.MirrorDir + " (dry=" + opt.DryRun + ", backup=" + opt.Backup
                + ", preserve-local=" + opt.PreserveLocalColumns + ", skip=" + (opt.SkipTables.Count) + " bang)");
            var results = new List<ImportResult>();
            int total = 0;
            foreach (var d in docs)
            {
                var m = ctx.Mapper.Resolve(d);
                if (!DbImporter.IsImportable(m)) continue;
                if (opt.OnlyXmls.Count > 0 && !opt.OnlyXmls.Any(x => string.Equals(x, d.Name, StringComparison.OrdinalIgnoreCase))) continue;
                if (opt.OnlyTables.Count > 0 && !opt.OnlyTables.Any(x => string.Equals(x, m.TableName, StringComparison.OrdinalIgnoreCase))) continue;
                total++;
                var r = ctx.Importer.ImportOne(d, m, opt);
                results.Add(r);
                Console.WriteLine(string.Format("  [{0,-10}] {1,-40} -> {2,-32} rows={3,-6} {4} {5}",
                    r.Status, r.XmlName, r.TableName, r.RowsWritten, r.BackupTable != null ? "bak=" + r.BackupTable : "", r.Message));
            }
            var warnings = new List<string>();
            foreach (var r in results)
            {
                if (r == null || string.IsNullOrEmpty(r.Warnings)) continue;
                foreach (var line in r.Warnings.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    warnings.Add(r.XmlName + " (" + r.TableName + "): " + line.Trim());
            }
            List<string> safety;
            try { safety = ctx.Importer.CheckServerSafety(); } catch (Exception ex) { safety = new List<string> { "khong kiem tra duoc: " + ex.Message }; }
            var report = new ImportReport
            {
                GeneratedAt = DateTime.Now.ToString("s"),
                Connection = StudioContext.MaskConnection(ctx.Settings.DbConnection),
                Entries = results,
                Imported = results.Count(r => r.Status == "imported"),
                Unchanged = results.Count(r => r.Status == "unchanged"),
                Skipped = results.Count(r => r.Status == "skipped"),
                Failed = results.Count(r => r.Status == "failed"),
                Warnings = warnings,
                ServerSafety = safety
            };
            Paths.EnsureDir(ctx.ReportsDir);
            var path = Path.Combine(ctx.ReportsDir, "import-report.json");
            Json.SaveFile(path, report);
            Console.WriteLine();
            Console.WriteLine(string.Format("IMPORT XONG: imported={0} unchanged={1} skipped={2} failed={3} / {4} template",
                report.Imported, report.Unchanged, report.Skipped, report.Failed, total));
            Console.WriteLine("Report: " + path);
            foreach (var f in results.Where(r => r.Status == "failed").Take(20))
                Console.WriteLine("  FAILED " + f.XmlName + " -> " + f.TableName + " : " + f.Message);
            if (report.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== CANH BAO (" + report.Warnings.Count + ") ===");
                foreach (var w in report.Warnings.Take(60)) Console.WriteLine("  " + w);
            }
            if (report.ServerSafety.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== SERVER SAFETY (" + report.ServerSafety.Count + ") ===");
                foreach (var w in report.ServerSafety) Console.WriteLine("  " + w);
            }
            return report.Failed > 0 ? 1 : 0;
        }

        private static int CmdAudit(StudioContext ctx, Dictionary<string, string> o)
        {
            var folder = o.ContainsKey("_sub") ? o["_sub"] : (o.ContainsKey("path") ? o["path"] : ctx.Settings.MirrorDir);
            if (!Directory.Exists(folder))
            {
                Console.Error.WriteLine("Khong thay thu muc: " + folder);
                return 2;
            }
            var docs = new List<XmlDocInfo>();
            foreach (var f in Directory.GetFiles(folder, "*.xml"))
            {
                try { var d = XmlCodec.Inspect(f); d.Name = Path.GetFileNameWithoutExtension(f); docs.Add(d); } catch { }
            }
            if (o.ContainsKey("request"))
            {
                foreach (var f in Directory.GetFiles(ctx.Settings.RequestDir, "*.xml"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (docs.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                    try { var d = XmlCodec.Inspect(f); d.Name = name; docs.Add(d); } catch { }
                }
            }
            Console.WriteLine("Audit kieu du lieu: " + docs.Count + " file XML, " + ctx.Schema.Tables.Count + " bang CSDL...");
            var report = TypeAudit.Run(docs, ctx.Mapper, ctx.Schema, ctx.Log);
            var outPath = o.ContainsKey("out") ? o["out"] : TypeAudit.DefaultReportPath(ctx.ReportsDir);
            try
            {
                Paths.EnsureDir(Path.GetDirectoryName(outPath) ?? ".");
                Json.SaveFile(outPath, report);
            }
            catch (Exception ex) { Console.Error.WriteLine("Khong ghi duoc report: " + ex.Message); }
            Console.WriteLine();
            Console.WriteLine(report.Summary);
            foreach (var i in report.Issues)
            {
                Console.WriteLine(string.Format("  [{0}.{1}] -> {2}.{3} ({4}) max={5} rows={6}", i.Xml, i.Attr, i.Table, i.Column, i.SqlType, i.MaxValue, i.RowsAffected));
                Console.WriteLine("      " + i.Suggestion);
            }
            if (o.ContainsKey("print"))
            {
                foreach (var i in report.Issues) Console.WriteLine(i.Xml + "|" + i.Attr + "|" + i.Table + "|" + i.Column + "|" + i.Suggestion);
            }
            Console.WriteLine();
            Console.WriteLine("Report: " + outPath);
            return report.Issues.Count == 0 ? 0 : 1;
        }

        private static int CmdDecode(StudioContext ctx, Dictionary<string, string> o)
        {
            var svc = new DecodeService(ctx.Log, new TableMapper(ctx.Schema,
                OverridesFile.Load(Paths.OverridesPath()).Map));
            var target = o.ContainsKey("_sub") ? o["_sub"] : (o.ContainsKey("path") ? o["path"] : null);
            if (string.IsNullOrEmpty(target) && o.ContainsKey("request"))
                target = ctx.Settings.RequestDir;
            if (string.IsNullOrEmpty(target))
            {
                target = ctx.Settings.MirrorDir;
                Console.WriteLine("Khong truyen duong dan -> dung mirror: " + target);
            }
            if (!File.Exists(target) && !Directory.Exists(target))
            {
                Console.Error.WriteLine("Khong thay: " + target);
                return 1;
            }
            var outDir = o.ContainsKey("out") ? Paths.Resolve(o["out"]) : null;
            bool overwrite = !o.ContainsKey("keep");
            bool analyze = !o.ContainsKey("no-map");
            bool encode = o.ContainsKey("encode");
            bool print = o.ContainsKey("print");

            if (File.Exists(target))
            {
                if (encode)
                {
                    var enc = svc.EncodeFile(target, outDir == null ? null : Path.Combine(outDir, Path.GetFileName(target)), overwrite);
                    Console.WriteLine(string.Format("ENCODE {0}: {1} -> {2} {3}", enc.Status, Paths.FormatSize(enc.FileSize), Paths.FormatSize(new FileInfo(enc.OutputPath ?? target).Length), enc.Error));
                    return enc.Status == "failed" ? 1 : 0;
                }
                var r = svc.DecodeFile(target, true, analyze);
                Console.WriteLine("=== " + r.Name + " ===");
                Console.WriteLine("  Trang thai    : " + r.Status + (r.Error != null ? " (" + r.Error + ")" : ""));
                Console.WriteLine("  Dinh dang     : " + (r.WasCompressed ? "zlib-compressed XML" : "XML thuong"));
                Console.WriteLine("  Kich thuoc    : file=" + Paths.FormatSize(r.FileSize) + " -> xml=" + Paths.FormatSize(r.DecodedSize));
                if (analyze)
                    Console.WriteLine("  XML           : root=" + r.Root + " wrapper=" + (r.Wrapper ?? "-") + " items=" + r.ItemCount);
                if (analyze)
                    Console.WriteLine("  Bang CSDL     : " + (r.TableName ?? "-") + " [" + (r.MapStatus ?? "-") + "]");
                if (!string.IsNullOrEmpty(outDir))
                {
                    var outPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(target) + ".xml");
                    Paths.EnsureDir(outDir);
                    Paths.WriteAtomic(outPath, Encoding.UTF8.GetBytes(r.XmlText));
                    Console.WriteLine("  Da ghi        : " + outPath);
                }
                if (print)
                {
                    var lines = r.XmlText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    Console.WriteLine();
                    foreach (var line in lines.Take(2000)) Console.WriteLine(line);
                    if (lines.Length > 2000) Console.WriteLine("... (" + lines.Length + " dong, hien thi 2000)");
                }
                return r.Status == "ok" ? 0 : 1;
            }

            // directory
            Console.WriteLine("Thu muc: " + target + "  -> " + (outDir ?? "(chi phan tich)"));
            var cancel = CancellationToken.None;
            List<DecodeResult> results;
            if (encode)
            {
                results = svc.EncodeDirectory(target, outDir ?? target, "*.xml", true, overwrite, x =>
                {
                    if (x.Status != "ok") Console.WriteLine("  " + x.Status + " " + Path.GetFileName(x.Path) + " " + x.Error);
                }, cancel);
            }
            else if (outDir == null)
            {
                results = svc.Scan(target, "*.xml", null, cancel);
                int ok = results.Count(x => x.Status == "ok");
                int packed = results.Count(x => x.WasCompressed);
                Console.WriteLine(string.Format("  co {0} file XML, giai ma duoc {1}, trong do {2} file zlib, {3} file XML thuong", results.Count, ok, packed, ok - packed));
                foreach (var r in results.Where(x => x.Status != "ok").Take(10))
                    Console.WriteLine("  LOI " + Path.GetFileName(r.Path) + ": " + r.Error);
                foreach (var r in results.Take(15))
                    Console.WriteLine(string.Format("  {0,-34} {1,-18} {2} -> {3}", Path.GetFileName(r.Path), r.WasCompressed ? "zlib" : "xml", Paths.FormatSize(r.FileSize), Paths.FormatSize(r.DecodedSize)));
                return 0;
            }
            else
            {
                results = svc.DecodeDirectory(target, outDir, "*.xml", true, overwrite, analyze, null, cancel);
            }
            Paths.EnsureDir(ctx.ReportsDir);
            var reportPath = Path.Combine(ctx.ReportsDir, "decode-report.json");
            Json.SaveFile(reportPath, new DecodeReport
            {
                GeneratedAt = DateTime.Now.ToString("s"),
                Source = target,
                Target = outDir,
                Mode = encode ? "encode" : "decode",
                Total = results.Count,
                Ok = results.Count(x => x.Status == "ok"),
                Skipped = results.Count(x => x.Status == "skipped"),
                Failed = results.Count(x => x.Status == "failed"),
                Entries = results
            });
            Console.WriteLine(string.Format("XONG: ok={0}, bo qua={1}, loi={2} / {3} file", results.Count(x => x.Status == "ok"),
                results.Count(x => x.Status == "skipped"), results.Count(x => x.Status == "failed"), results.Count));
            Console.WriteLine("Report: " + reportPath);
            return results.Count(x => x.Status == "failed") > 0 ? 1 : 0;
        }

        private static int CmdBomb(StudioContext ctx, Dictionary<string, string> o)
        {
            var sub = o.ContainsKey("_sub") ? o["_sub"] : "scan";
            var svcType = Type.GetType("GunnyRequestStudio.Core.BombService, GunnyRequestStudio.Core");
            if (svcType == null)
            {
                Console.Error.WriteLine("Khong tim thay BombService (chua build).");
                return 1;
            }
            return 1; // BombService se duoc tich hop qua CLI rieng cua bomb-dev
        }

        // ------------------------------------------------------------ helpers

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string lastName = null;
            foreach (var a in args)
            {
                if (a.StartsWith("--"))
                {
                    var k = a.Substring(2);
                    var idx = k.IndexOf('=');
                    if (idx > 0)
                    {
                        d[k.Substring(0, idx).ToLowerInvariant()] = k.Substring(idx + 1);
                        lastName = null;
                    }
                    else
                    {
                        lastName = k.ToLowerInvariant();
                        d[lastName] = "1";
                    }
                }
                else if (lastName != null && !a.StartsWith("-"))
                {
                    d[lastName] = a;
                    lastName = null;
                }
                else if (!a.StartsWith("--")) d["_sub"] = a.ToLowerInvariant();
            }
            return d;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"Gunny Request Studio - CLI
  --cli test-db
  --cli names
  --cli rip    [--no-refresh] [--no-promote] [--promote-all] [--dry] [--keep] [--concurrency 8]
  --cli map
  --cli decode [file|folder] [--out DIR] [--encode] [--print] [--keep] [--no-map]
  --cli import [--only a,b,c] [--table T] [--dry] [--force] [--no-backup] [--batch 2000]
               [--no-preserve] [--allow-local-loss] [--skip-table A,B]
  --cli audit  [thu muc mirror] [--out FILE] [--print]     kiem tra kieu du lieu publisher vs CSDL (int->long...)
  --cli bomb   [scan|rip|mask|all]
");
        }

        // ------------------------------------------------------------ DTO report

        [System.Runtime.Serialization.DataContract]
        public class MappingRow
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string Xml;
            [System.Runtime.Serialization.DataMember(Order = 2)] public int Items;
            [System.Runtime.Serialization.DataMember(Order = 3)] public string Table;
            [System.Runtime.Serialization.DataMember(Order = 4)] public string Status;
            [System.Runtime.Serialization.DataMember(Order = 5)] public double Score;
            [System.Runtime.Serialization.DataMember(Order = 6)] public int Attrs;
            [System.Runtime.Serialization.DataMember(Order = 7)] public string MissingRequired;
            [System.Runtime.Serialization.DataMember(Order = 8)] public string Ignored;
            [System.Runtime.Serialization.DataMember(Order = 9)] public string Problem;
            [System.Runtime.Serialization.DataMember(Order = 10)] public string Candidates;
        }

        [System.Runtime.Serialization.DataContract]
        public class DecodeReport
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string GeneratedAt;
            [System.Runtime.Serialization.DataMember(Order = 2)] public string Source;
            [System.Runtime.Serialization.DataMember(Order = 3)] public string Target;
            [System.Runtime.Serialization.DataMember(Order = 4)] public string Mode;
            [System.Runtime.Serialization.DataMember(Order = 5)] public int Total;
            [System.Runtime.Serialization.DataMember(Order = 6)] public int Ok;
            [System.Runtime.Serialization.DataMember(Order = 7)] public int Skipped;
            [System.Runtime.Serialization.DataMember(Order = 8)] public int Failed;
            [System.Runtime.Serialization.DataMember(Order = 9)] public List<DecodeResult> Entries = new List<DecodeResult>();
        }

        [System.Runtime.Serialization.DataContract]
        public class MappingReport
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string GeneratedAt;
            [System.Runtime.Serialization.DataMember(Order = 2)] public int Total;
            [System.Runtime.Serialization.DataMember(Order = 3)] public int Mapped;
            [System.Runtime.Serialization.DataMember(Order = 4)] public List<MappingRow> Rows = new List<MappingRow>();
        }
    }
}


