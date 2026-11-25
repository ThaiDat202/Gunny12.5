using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GunnyRequestStudio.Core
{
    // ============================================================ MODEL / DTO

    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)] public string RequestDir = @"C:\Gunny\Gunny12.6\WebSite\Request";
        [DataMember(Order = 2)] public string MirrorDir = @"C:\Gunny\Gunny12.6\WebSite\Request\vng-quest";
        [DataMember(Order = 3)] public List<string> PublisherBases = new List<string> { "https://quest.gn.zing.vn/" };
        [DataMember(Order = 4)] public string DbConnection = "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank_All;User ID=sa;Password=123456;Connect Timeout=15";
        [DataMember(Order = 5)] public string ServiceRoot = @"C:\Gunny\Gunny12.6\Service\Service";
        [DataMember(Order = 6)] public string FullSourceRoot = @"C:\Gunny\Gunny12.6\Full Source";
        [DataMember(Order = 7)] public string WebSiteRoot = @"C:\Gunny\Gunny12.6\WebSite";
        [DataMember(Order = 8)] public List<string> ResBases = new List<string> { "https://res.gn.zing.vn/", "https://gunny.vcdn.vn/" };
        [DataMember(Order = 9)] public string ClientConfigPath = @"C:\Gunny\Gunny12.6\WebSite\config.xml";
        [DataMember(Order = 10)] public int Concurrency = 8;
        [DataMember(Order = 11)] public bool BackupBeforeReplace = true;
        [DataMember(Order = 12)] public bool PromoteTemplatesToRequest = true;
        [DataMember(Order = 13)] public string BombAssetDir = @"C:\Gunny\Gunny12.6\WebSite\image\bomb";
        [DataMember(Order = 14)] public string ReportsDir = "reports";

        public static AppSettings CreateDefault() { return new AppSettings(); }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                RequestDir = RequestDir,
                MirrorDir = MirrorDir,
                PublisherBases = new List<string>(PublisherBases ?? new List<string>()),
                DbConnection = DbConnection,
                ServiceRoot = ServiceRoot,
                FullSourceRoot = FullSourceRoot,
                WebSiteRoot = WebSiteRoot,
                ResBases = new List<string>(ResBases ?? new List<string>()),
                ClientConfigPath = ClientConfigPath,
                Concurrency = Concurrency,
                BackupBeforeReplace = BackupBeforeReplace,
                PromoteTemplatesToRequest = PromoteTemplatesToRequest,
                BombAssetDir = BombAssetDir,
                ReportsDir = ReportsDir
            };
        }
    }

    public enum EntryStatus { Ok, Unchanged, Missing, Failed, Skipped, Downloaded }

    [DataContract]
    public class RipEntry
    {
        [DataMember(Order = 1)] public string Name;
        [DataMember(Order = 2)] public string Url;
        [DataMember(Order = 3)] public string Status;      // ok | unchanged | missing | failed
        [DataMember(Order = 4)] public long Size;
        [DataMember(Order = 5)] public string Sha256;
        [DataMember(Order = 6)] public string Error;
        [DataMember(Order = 7)] public string LocalPath;
        [DataMember(Order = 8)] public string TableName;   // if mapped to a DB table
        [DataMember(Order = 9)] public string MapStatus;   // exact/auto/override/ambiguous/none
    }

    [DataContract]
    public class RipStats
    {
        [DataMember(Order = 1)] public int Total;
        [DataMember(Order = 2)] public int Ok;
        [DataMember(Order = 3)] public int Unchanged;
        [DataMember(Order = 4)] public int Missing;
        [DataMember(Order = 5)] public int Failed;
        [DataMember(Order = 6)] public long Bytes;
        [DataMember(Order = 7)] public double Seconds;
        [DataMember(Order = 8)] public int Promoted;
        [DataMember(Order = 9)] public int PromotedUnchanged;
    }

    [DataContract]
    public class RipReport
    {
        [DataMember(Order = 1)] public string GeneratedAt;
        [DataMember(Order = 2)] public List<string> Sources;
        [DataMember(Order = 3)] public string TargetDir;
        [DataMember(Order = 4)] public RipStats Stats = new RipStats();
        [DataMember(Order = 5)] public List<RipEntry> Entries = new List<RipEntry>();
        [DataMember(Order = 6)] public string Note;
    }

    [DataContract]
    public class XmlDocInfo
    {
        public string FilePath;
        public string Name;            // without .xml
        public string RootElement;
        public string Wrapper;         // ItemTemplate | Store | RuneTemplate | (null)
        public string ItemElement;     // Item | item
        public List<string> Attributes = new List<string>();
        public int ItemCount;
        public bool IsCompressed;
        public long SizeBytes;
        public string Error;
        public List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();
    }

    public class DbColumn
    {
        public string Name;
        public string DataType;
        public int MaxLength;
        public bool IsNullable;
        public bool IsIdentity;
        public bool IsComputed;
        public bool HasDefault;
    }

    public class DbTable
    {
        public string Schema = "dbo";
        public string Name;
        public List<DbColumn> Columns = new List<DbColumn>();
        public List<string> PrimaryKey = new List<string>();
        public long RowCount = -1;

        public DbColumn FindColumn(string name)
        {
            foreach (var c in Columns)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        /// <summary>Tim cot theo ten chinh xac, sau do fuzzy (bo underscore, dong nghia defense/defence...).</summary>
        public DbColumn FindColumnFuzzy(string name)
        {
            var exact = FindColumn(name);
            if (exact != null) return exact;
            var target = Canon(name);
            foreach (var c in Columns)
            {
                if (Canon(c.Name) == target) return c;
            }
            return null;
        }

        public static string Canon(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            var n = sb.ToString();
            n = n.Replace("defense", "defence")
                 .Replace("defenced", "defence")
                 .Replace("defend", "defence")
                 .Replace("atck", "attack")
                 .Replace("defy", "defence")
                 .Replace("lucky", "luck")
                 .Replace("agility", "agile")
                 .Replace("probability", "rate")
                 .Replace("probablity", "rate");
            return n;
        }
    }

    public class DbSchema
    {
        public List<DbTable> Tables = new List<DbTable>();

        public DbTable FindTable(string name)
        {
            foreach (var t in Tables)
            {
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }
    }

    public class MappingCandidate
    {
        public string TableName;
        public double Score;
        public int ExtraColumns;
        public bool NameMatch;
        public bool ExactName;
    }

    public class XmlTableMapping
    {
        public string XmlName;
        public string TableName;
        public string Status;          // exact | auto | override | ambiguous | none
        public double Score;
        public int XmlAttributeCount;
        public int TableColumnCount;
        public List<string> IgnoredAttributes = new List<string>();
        public List<string> MissingRequiredColumns = new List<string>();  // NOT NULL without default
        public List<MappingCandidate> Candidates = new List<MappingCandidate>();
        public string Problem;
    }

    [DataContract]
    public class ImportResult
    {
        [DataMember(Order = 1)] public string XmlName;
        [DataMember(Order = 2)] public string TableName;
        [DataMember(Order = 3)] public string Status;   // imported | unchanged | skipped | failed
        [DataMember(Order = 4)] public int RowsRead;
        [DataMember(Order = 5)] public int RowsWritten;
        [DataMember(Order = 6)] public int RowsBefore;
        [DataMember(Order = 7)] public string BackupTable;
        [DataMember(Order = 8)] public string Message;
        [DataMember(Order = 9)] public long ElapsedMs;
        [DataMember(Order = 10, EmitDefaultValue = false)] public string Warnings;
        [DataMember(Order = 11)] public int LocalColumnsPreserved;
    }

    [DataContract]
    public class ImportReport
    {
        [DataMember(Order = 1)] public string GeneratedAt;
        [DataMember(Order = 2)] public string Connection;
        [DataMember(Order = 3)] public List<ImportResult> Entries = new List<ImportResult>();
        [DataMember(Order = 4)] public int Imported;
        [DataMember(Order = 5)] public int Unchanged;
        [DataMember(Order = 6)] public int Skipped;
        [DataMember(Order = 7)] public int Failed;
        [DataMember(Order = 8)] public string Note;
        [DataMember(Order = 9)] public List<string> Warnings = new List<string>();
        [DataMember(Order = 10)] public List<string> ServerSafety = new List<string>();
    }

    [DataContract]
    public class BombIdInfo
    {
        [DataMember(Order = 1)] public int Id;
        [DataMember(Order = 2)] public bool ServerMask;     // .bomb co trong Service Bomb
        [DataMember(Order = 3)] public bool CraterPng;      // crater.png (asset client)
        [DataMember(Order = 4)] public bool CraterBrink;    // craterBrink.png
        [DataMember(Order = 5)] public bool Bullet;         // bullet<id>.swf  (UI vu khi trong tran)
        [DataMember(Order = 6)] public bool BlastOut;       // blastOut<id>.swf
        [DataMember(Order = 7)] public bool LocalCrater;    // da mirror ve WebSite\image\bomb
        [DataMember(Order = 8)] public bool LocalBullet;
        [DataMember(Order = 9)] public bool LocalBlastOut;
        [DataMember(Order = 10)] public bool LocalBrink;
        [DataMember(Order = 11)] public string Resolution;  // ok | generated | no-source | ...
        [DataMember(Order = 12)] public string Note;
    }

    [DataContract]
    public class BombReport
    {
        [DataMember(Order = 1)] public string GeneratedAt;
        [DataMember(Order = 2)] public int ReferencedIds;
        [DataMember(Order = 3)] public int WithMask;
        [DataMember(Order = 4)] public int MissingMask;
        [DataMember(Order = 5)] public int GeneratedMasks;
        [DataMember(Order = 6)] public int Deployed;
        [DataMember(Order = 7)] public int AssetsDownloaded;
        [DataMember(Order = 8)] public int AssetsUnavailable;
        [DataMember(Order = 9)] public List<BombIdInfo> Ids = new List<BombIdInfo>();
        [DataMember(Order = 10)] public List<int> UnresolvedIds = new List<int>();
        [DataMember(Order = 11)] public string Note;
    }

    [DataContract]
    public class ImportManifest
    {
        [DataMember(Order = 1)] public string GeneratedAt;
        [DataMember(Order = 2)] public List<ImportHash> Entries = new List<ImportHash>();
    }

    [DataContract]
    public class ImportHash
    {
        [DataMember(Order = 1)] public string XmlName;
        [DataMember(Order = 2)] public string TableName;
        [DataMember(Order = 3)] public string Sha256;
        [DataMember(Order = 4)] public int Rows;
        [DataMember(Order = 5)] public string ImportedAt;
    }

    public class LogEntry
    {
        public DateTime Time = DateTime.Now;
        public string Level = "INFO";
        public string Message;
        public override string ToString()
        {
            return string.Format("{0:HH:mm:ss} [{1}] {2}", Time, Level, Message);
        }
    }
}



