using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace Game.Server.GameObjects;

public sealed class JewelCultureInfo
{
    public int UserID;
    public int Place;
    public int MachiningExp;
    public int QuenchingExp;
    public string SkillStr = "";
}

/// <summary>
/// Lưu trữ + tính toán Gia Công Trang Sức (Tăng bậc / Tẩy luyện).
/// Bảng: Sys_User_JewelCulture(UserID, Place, MachiningExp, QuenchingExp, SkillStr)
/// </summary>
public static class JewelCultureMgr
{
    // Từ TS_Jewel_Addition.xml: Exp,Attack,Defend,Agility,Luck (cùng giá trị 4 chỉ số)
    private static readonly int[][] MachiningLevels =
    {
        new[] {0,0}, new[] {2100,2}, new[] {4500,4}, new[] {7400,6}, new[] {10700,8},
        new[] {14500,10}, new[] {18900,12}, new[] {23900,14}, new[] {29500,16}, new[] {35800,18},
        new[] {42800,20}, new[] {50600,23}, new[] {59300,26}, new[] {68800,29}, new[] {79300,32},
        new[] {90700,35}, new[] {103100,38}, new[] {116600,41}, new[] {131200,44}, new[] {147000,47},
        new[] {163900,50}, new[] {182100,54}, new[] {201600,58}, new[] {222400,62}, new[] {244600,66},
        new[] {268200,70}, new[] {293300,75}, new[] {319900,80}, new[] {348100,85}, new[] {377900,90},
        new[] {409300,95}, new[] {442500,101}, new[] {477400,107}, new[] {514100,113}, new[] {552600,119},
        new[] {593000,125}, new[] {635400,132}, new[] {679700,139}, new[] {726100,146}, new[] {774500,153},
        new[] {825000,160}
    };

    // JewelSystemSkillOpenPos = 1_2_10,2_4_1000,3_6_2000,4_8_4000,5_10_8000
    private static readonly int[] QuenchingNeedExp = { 10, 1000, 2000, 4000, 8000 };
    // Mốc trang sức +2/+4/+6/+8/+10 để mở từng dòng
    private static readonly int[] JewelrySlotLevels = { 2, 4, 6, 8, 10 };

    private static string ConnectionString
    {
        get
        {
            try
            {
                var cs = ConfigurationManager.ConnectionStrings["PlayerData"];
                if (cs != null && !string.IsNullOrWhiteSpace(cs.ConnectionString)) return cs.ConnectionString;
            }
            catch { }
            return "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank;Persist Security Info=True;User ID=sa;Password=123456;";
        }
    }

    public static Dictionary<int, JewelCultureInfo> Load(int userId)
    {
        var result = new Dictionary<int, JewelCultureInfo>();
        try
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Place,MachiningExp,QuenchingExp,SkillStr FROM Sys_User_JewelCulture WHERE UserID=@uid", conn))
                {
                    cmd.Parameters.AddWithValue("@uid", userId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            var loaded = new JewelCultureInfo
                            {
                                UserID = userId,
                                Place = Convert.ToInt32(rd["Place"]),
                                MachiningExp = Convert.ToInt32(rd["MachiningExp"]),
                                QuenchingExp = Math.Min(Convert.ToInt32(rd["QuenchingExp"]), GetQuenchingMaxExp()),
                                SkillStr = rd["SkillStr"] == DBNull.Value ? "" : rd["SkillStr"].ToString()
                            };
                            int slots = GetQuenchingSlots(GetQuenchingLevel(loaded.QuenchingExp));
                            loaded.SkillStr = EnsureSkillStr(slots, loaded.SkillStr);
                            result[loaded.Place] = loaded;
                        }
                    }
                }
            }
        }
        catch { }
        return result;
    }

    public static JewelCultureInfo GetOrCreate(int userId, int place)
    {
        JewelCultureInfo info = null;
        try
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Place,MachiningExp,QuenchingExp,SkillStr FROM Sys_User_JewelCulture WHERE UserID=@uid AND Place=@place", conn))
                {
                    cmd.Parameters.AddWithValue("@uid", userId);
                    cmd.Parameters.AddWithValue("@place", place);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            info = new JewelCultureInfo
                            {
                                UserID = userId,
                                Place = place,
                                MachiningExp = Convert.ToInt32(rd["MachiningExp"]),
                                QuenchingExp = Convert.ToInt32(rd["QuenchingExp"]),
                                SkillStr = rd["SkillStr"] == DBNull.Value ? "" : rd["SkillStr"].ToString()
                            };
                        }
                    }
                }
            }
        }
        catch { }
        if (info != null)
        {
            int slots = GetQuenchingSlots(GetQuenchingLevel(info.QuenchingExp));
            info.SkillStr = EnsureSkillStr(slots, info.SkillStr);
        }
        return info ?? new JewelCultureInfo { UserID = userId, Place = place };
    }

    public static void Save(JewelCultureInfo info)
    {
        try
        {
            int slots = GetQuenchingSlots(GetQuenchingLevel(info.QuenchingExp));
            info.SkillStr = EnsureSkillStr(slots, info.SkillStr);
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("IF EXISTS (SELECT 1 FROM Sys_User_JewelCulture WHERE UserID=@uid AND Place=@place) UPDATE Sys_User_JewelCulture SET MachiningExp=@exp,QuenchingExp=@qexp,SkillStr=@skill WHERE UserID=@uid AND Place=@place ELSE INSERT INTO Sys_User_JewelCulture(UserID,Place,MachiningExp,QuenchingExp,SkillStr) VALUES(@uid,@place,@exp,@qexp,@skill)", conn))
                {
                    cmd.Parameters.AddWithValue("@uid", info.UserID);
                    cmd.Parameters.AddWithValue("@place", info.Place);
                    cmd.Parameters.AddWithValue("@exp", info.MachiningExp);
                    cmd.Parameters.AddWithValue("@qexp", info.QuenchingExp);
                    cmd.Parameters.AddWithValue("@skill", info.SkillStr ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { }
    }

    public static int GetMachiningLevel(int exp)
    {
        int level = 0;
        for (int i = 0; i < MachiningLevels.Length; i++)
        {
            if (exp >= MachiningLevels[i][0]) level = i;
            else break;
        }
        return level;
    }

    public static int GetMachiningValue(int exp)
    {
        return MachiningLevels[GetMachiningLevel(exp)][1];
    }

    public static int GetMachiningMaxExp()
    {
        return MachiningLevels[MachiningLevels.Length - 1][0];
    }

    public static int GetQuenchingLevel(int exp)
    {
        int level = 0;
        for (int i = 0; i < QuenchingNeedExp.Length; i++)
        {
            if (exp >= QuenchingNeedExp[i]) level = i + 1;
            else break;
        }
        return level;
    }

    public static int GetQuenchingNextExp(int exp)
    {
        for (int i = 0; i < QuenchingNeedExp.Length; i++)
        {
            if (exp < QuenchingNeedExp[i]) return QuenchingNeedExp[i];
        }
        return QuenchingNeedExp[QuenchingNeedExp.Length - 1];
    }

    public static int GetQuenchingMaxExp()
    {
        return QuenchingNeedExp[QuenchingNeedExp.Length - 1];
    }

    public static int GetQuenchingSlots(int level)
    {
        // Số dòng đã mở theo tiến độ Mã Não (1..5)
        return level <= 0 ? 0 : (level > 5 ? 5 : level);
    }

    public static int GetOpenedByJewelryLevel(int strengthenLevel)
    {
        int count = 0;
        for (int i = 0; i < JewelrySlotLevels.Length; i++)
        {
            if (strengthenLevel >= JewelrySlotLevels[i]) count++;
            else break;
        }
        return count;
    }

    // Các kỹ năng đặc biệt của trang sức (petskillinfo ID 1087..1156)
    private static readonly int[] JewelrySpecialSkills =
    {
        1087,1088,1089,1090,1091, 1092,1093,1094,1095,1096,
        1097,1098,1099,1100,1101, 1102,1103,1104,1105,1106,
        1107,1108,1109,1110,1111, 1112,1113,1114,1115,1116,
        1117,1118,1119,1120,1121, 1122,1123,1124,1125,1126,
        1127,1128,1129,1130,1131,
        1132,1133,1134,1135,1136, 1137,1138,1139,1140,1141,
        1142,1143,1144,1145,1146, 1147,1148,1149,1150,1151,
        1152,1153,1154,1155,1156
    };

    private static readonly Random Rng = new Random();

    private static int NextRandom(int max)
    {
        lock (Rng) return Rng.Next(max);
    }

    // Tỷ lệ xuất hiện dòng kỹ năng đặc biệt (mỗi dòng random độc lập)
    private const int SpecialSkillChancePercent = 8;

    private static bool IsSpecialType(int type)
    {
        return type == 3 || type == 30;
    }

    private static int GetSkillType(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return -1;
        string[] parts = value.Split(',');
        int type;
        return parts.Length >= 2 && int.TryParse(parts[0], out type) ? type : -1;
    }

    private static string MakeSpecialSkillValue()
    {
        return "3," + JewelrySpecialSkills[NextRandom(JewelrySpecialSkills.Length)];
    }

    private static string MakeNormalSkillValue(int slot)
    {
        int[] types = { 1, 4, 5, 6, 7, 8 };
        int type = types[NextRandom(types.Length)];
        return type + "," + MakeNormalValue(type, slot);
    }

    private static string MakeRandomSkillValue(int slot)
    {
        return NextRandom(100) < SpecialSkillChancePercent
            ? MakeSpecialSkillValue()
            : MakeNormalSkillValue(slot);
    }

    private static int MakeNormalValue(int type, int slot)
    {
        int idx = slot < 5 ? slot : 4;
        if (type == 8)
        {
            int[] lows = { 640, 720, 800, 880, 960 };
            int[] highs = { 1536, 1728, 1920, 2112, 2304 };
            return NextRandom(highs[idx] - lows[idx] + 1) + lows[idx];
        }
        if (type == 1)
        {
            int[] lows = { 24, 27, 30, 33, 36 };
            int[] highs = { 58, 65, 72, 79, 86 };
            return NextRandom(highs[idx] - lows[idx] + 1) + lows[idx];
        }
        int[] baseLows = { 80, 90, 100, 110, 120 };
        int[] baseHighs = { 192, 216, 240, 264, 288 };
        return NextRandom(baseHighs[idx] - baseLows[idx] + 1) + baseLows[idx];
    }

    /// <summary>
    /// Reroll giá trị nhưng GIỮ NGUYÊN loại của dòng đã khóa.
    /// </summary>
    private static string RerollSameType(string old, int slot)
    {
        int type = GetSkillType(old);
        if (type < 0 || IsSpecialType(type)) return old;
        return type + "," + MakeNormalValue(type, slot);
    }

    /// <summary>
    /// Chỉ chống trùng: tối đa 1 dòng kỹ năng đặc biệt.
    /// KHÔNG tự thêm kỹ năng đặc biệt khi chưa random ra.
    /// </summary>
    private static void NormalizeSpecialLines(List<string> parts, int slots)
    {
        int seen = 0;
        for (int i = 0; i < slots && i < parts.Count; i++)
        {
            if (!IsSpecialType(GetSkillType(parts[i]))) continue;
            seen++;
            if (seen > 1) parts[i] = MakeNormalSkillValue(i);
        }
    }

    private static string TrimSkillParts(List<string> parts)
    {
        int last = parts.Count - 1;
        while (last >= 0 && string.IsNullOrEmpty(parts[last])) last--;
        if (last < 0) return "";
        return string.Join("|", parts.GetRange(0, last + 1));
    }

    public static string EnsureSkillStr(int slots, string old)
    {
        int safeSlots = slots < 0 ? 0 : (slots > 5 ? 5 : slots);
        string[] oldParts = (old ?? "").Split('|');
        var parts = new List<string>();
        for (int i = 0; i < safeSlots; i++) parts.Add(i < oldParts.Length ? oldParts[i] : "");
        for (int i = 0; i < safeSlots; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) parts[i] = MakeRandomSkillValue(i);
        }
        NormalizeSpecialLines(parts, safeSlots);
        return TrimSkillParts(parts);
    }

    public static string GenerateSkillStr(int slots, string old, HashSet<int> locked)
    {
        return EnsureSkillStr(slots, old);
    }

    /// <summary>
    /// Tẩy lại CHỈ 1 dòng.
    /// Dòng chưa khóa: random loại + giá trị; kỹ năng đặc biệt có tỷ lệ, tối đa 1 dòng toàn trang sức.
    /// Dòng đã khóa: giữ nguyên loại, chỉ tăng/giảm giá trị.
    /// </summary>
    public static string GenerateOneSkillStr(int slots, string old, HashSet<int> locked)
    {
        int safeSlots = slots < 0 ? 0 : (slots > 5 ? 5 : slots);
        string[] oldParts = (old ?? "").Split('|');
        var parts = new List<string>();
        for (int i = 0; i < safeSlots; i++) parts.Add(i < oldParts.Length ? oldParts[i] : "");
        for (int i = 0; i < safeSlots; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) parts[i] = MakeRandomSkillValue(i);
        }
        NormalizeSpecialLines(parts, safeSlots);
        if (safeSlots <= 0) return TrimSkillParts(parts);

        int slot = NextRandom(safeSlots);
        bool isLocked = locked != null && locked.Contains(slot);
        if (isLocked)
        {
            parts[slot] = RerollSameType(parts[slot], slot);
        }
        else
        {
            parts[slot] = MakeRandomSkillValue(slot);
        }
        NormalizeSpecialLines(parts, safeSlots);
        return TrimSkillParts(parts);
    }

    public static void AddQuenchingProps(string skillStr, ref int attack, ref int defence, ref int agility, ref int lucky, ref int hp)
    {
        if (string.IsNullOrWhiteSpace(skillStr)) return;
        foreach (string line in skillStr.Split('|'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split(',');
            if (parts.Length < 2) continue;
            int type, val;
            if (!int.TryParse(parts[0], out type) || !int.TryParse(parts[1], out val)) continue;
            switch (type)
            {
                case 1: attack += val; defence += val; agility += val; lucky += val; break;
                case 4: attack += val; break;
                case 5: defence += val; break;
                case 6: agility += val; break;
                case 7: lucky += val; break;
                case 8: hp += val; break;
                case 17: attack += val; break;
                case 18: agility += val; break;
                case 19: defence += val; break;
                case 20: attack += val; break;
            }
        }
    }
}
