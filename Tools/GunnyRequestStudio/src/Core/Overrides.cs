using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GunnyRequestStudio.Core
{
    /// <summary>Gia tri dien cho cot NOT NULL khong co trong XML (vi du Ball.Delay=0).</summary>
    public class FillMap : Dictionary<string, Dictionary<string, string>>
    {
        public FillMap() : base(StringComparer.OrdinalIgnoreCase) { }

        public bool TryGetTable(string table, out Dictionary<string, string> cols)
        {
            if (table != null && base.TryGetValue(table, out cols)) return true;
            cols = null;
            return false;
        }
    }

    public class OverridesFile
    {
        public FillMap Fills = new FillMap();
        public Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Bang publisher khong tuong thich server local -> bo qua khi import (an toan Fight/Road).</summary>
        public List<string> SkipTables = new List<string>();
        public string Path;

        public static OverridesFile Load(string path)
        {
            var result = new OverridesFile { Path = path };
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;
            try
            {
                var dto = Json.LoadFile<OverridesDto>(path);
                if (dto != null)
                {
                    foreach (var item in dto.Map ?? new List<OverrideItemDto>())
                    {
                        if (!string.IsNullOrEmpty(item.Xml) && item.Table != null) result.Map[item.Xml] = item.Table;
                    }
                    foreach (var f in dto.Fills ?? new List<FillTableDto>())
                    {
                        if (string.IsNullOrEmpty(f.Table)) continue;
                        var cols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var c in f.Columns ?? new List<FillColDto>())
                        {
                            if (!string.IsNullOrEmpty(c.Name)) cols[c.Name] = c.Value ?? string.Empty;
                        }
                        result.Fills[f.Table] = cols;
                    }
                    foreach (var s in dto.SkipTables ?? new List<string>())
                        if (!string.IsNullOrEmpty(s)) result.SkipTables.Add(s.Trim());
                    return result;
                }
            }
            catch { }
            // fallback: dang "xml=table" moi dong (khong ho tro Fills)
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var s = (line ?? string.Empty).Trim();
                    if (s.Length == 0 || s.StartsWith("#") || s.StartsWith("//")) continue;
                    var idx = s.IndexOf('=');
                    if (idx <= 0) continue;
                    result.Map[s.Substring(0, idx).Trim()] = s.Substring(idx + 1).Trim();
                }
            }
            catch { }
            return result;
        }

        public void Save()
        {
            var dto = new OverridesDto
            {
                Map = Map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                         .Select(k => new OverrideItemDto { Xml = k.Key, Table = k.Value }).ToList(),
                SkipTables = SkipTables.ToList(),
                Fills = Fills.Select(kv => new FillTableDto
                {
                    Table = kv.Key,
                    Columns = kv.Value.Select(c => new FillColDto { Name = c.Key, Value = c.Value }).ToList()
                }).ToList()
            };
            Json.SaveFile(Path, dto);
        }
    }

    [System.Runtime.Serialization.DataContract]
    public class OverridesDto
    {
        [System.Runtime.Serialization.DataMember(Order = 1)] public List<OverrideItemDto> Map = new List<OverrideItemDto>();
        [System.Runtime.Serialization.DataMember(Order = 2)] public List<FillTableDto> Fills = new List<FillTableDto>();
        [System.Runtime.Serialization.DataMember(Order = 3)] public List<string> SkipTables = new List<string>();
    }

    [System.Runtime.Serialization.DataContract]
    public class OverrideItemDto
    {
        [System.Runtime.Serialization.DataMember(Order = 1)] public string Xml;
        [System.Runtime.Serialization.DataMember(Order = 2, EmitDefaultValue = false)] public string Table;
    }

    [System.Runtime.Serialization.DataContract]
    public class FillTableDto
    {
        [System.Runtime.Serialization.DataMember(Order = 1)] public string Table;
        [System.Runtime.Serialization.DataMember(Order = 2)] public List<FillColDto> Columns = new List<FillColDto>();
    }

    [System.Runtime.Serialization.DataContract]
    public class FillColDto
    {
        [System.Runtime.Serialization.DataMember(Order = 1)] public string Name;
        [System.Runtime.Serialization.DataMember(Order = 2)] public string Value;
    }
}
