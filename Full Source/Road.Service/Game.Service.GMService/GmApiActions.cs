using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using Bussiness;
using Bussiness.Managers;
using Bussiness.Protocol;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.CardNewSystem;
using Game.Server.Elf;
using Game.Server.GameObjects;
using Game.Server.GameUtils;
using Game.Server.Managers;
using Game.Service.Helper;
using Newtonsoft.Json;
using SqlDataProvider.Data;
using log4net;

namespace Game.Service.GMService;

/// <summary>
/// Thực thi các thao tác GM "Set Full" trực tiếp trên người chơi (in-process),
/// giống logic online của Form1 desktop: sửa object in-memory → tính lại thuộc tính
/// (EquipBag.UpdatePlayerProperties) → gửi packet cho client (UpdatePrivateInfo/PublicPlayer,
/// rank, avatar, card, drill...). Không cần người chơi đăng nhập lại.
///
/// - Các feature stat (king/guard/honor/sworn/...) yêu cầu user đang online.
/// - beads (Châu Báu) và magicstone (Ma Thạch) chạy được cả offline: mở drill/gửi mail qua
///   PlayerBussiness (mail Ma Thạch dùng ApplyMagicStoneMailStats nên đủ 6 chỉ số),
///   nếu online thì cập nhật luôn bộ nhớ + gửi packet.
/// </summary>
public static class GmApiActions
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly object ActionLock = new object();

    /// <summary>Feature cần user online để cập nhật trực tiếp.</summary>
    public static readonly string[] LiveFeatures =
    {
        "king", "guard", "honor", "sworn", "sworn_love", "explorer", "titles",
        "amulet_level", "amulet_wash", "potential", "phuma", "duchon",
        "avatar", "album",
        "pet_rebirth", "pet_evolve", "pet_strengthen", "pet_breakthrough",
        "mount_level", "mount_skill", "mount_avatar", "mount_amulet",
        "elf_nurture", "elf_train", "elf_linhbao", "elf_learn",
        "mark_train", "mark_refine",
        "notice"
    };

    /// <summary>
    /// Các feature port từ Form1 desktop (v0.29): API xử lý cả online (in-memory + packet) lẫn
    /// offline (SQL), trừ các feature game chỉ cho phép khi online (chiến hồn, tăng sao, tiến hoá VK phụ,
    /// tu luyện/thẻ bài/vật tổ/miếu thần/chế tạo vẫn hỗ trợ offline theo Form1).
    /// </summary>
    public static readonly string[] ExtraApiFeatures =
    {
        "training_basic", "training_second", "training_element",
        "card_level", "card_break",
        "totem_level", "totem_rank",
        "temple_bless", "temple_exalt",
        "gold_weapon", "gold_hat", "gold_cloth",
        "battle_soul", "craft",
        "star_weapon", "star_hat", "star_cloth",
        "subweapon_evolve",
        "necklace_train", "necklace_cast", "newbie_gift"
    };

    /// <summary>Feature do API xử lý toàn bộ (cả offline): mở drill + gửi mail đúng chỉ số.</summary>
    public static readonly string[] ApiManagedFeatures =
        new[] { "beads", "magicstone" }.Concat(ExtraApiFeatures).ToArray();

    public static readonly string[] SupportedFeatures =
        LiveFeatures.Concat(ApiManagedFeatures).ToArray();

    public static bool IsSupported(string feature) =>
        feature != null && SupportedFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase);

    public static bool IsApiManaged(string feature) =>
        feature != null && ApiManagedFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase);

    public static GamePlayer FindOnlinePlayer(int userId) => WorldMgr.GetPlayerById(userId);

    private static int ZoneId =>
        int.TryParse(ConfigurationManager.AppSettings["AreaID"], out var zone) ? zone : 1001;

    public static GmActionResult Apply(int userId, string feature)
    {
        var result = new GmActionResult { UserId = userId, Feature = feature ?? "" };
        if (userId <= 0)
        {
            result.Message = "UserID không hợp lệ.";
            return result;
        }
        if (!IsSupported(feature))
        {
            result.Message = "Chức năng không hỗ trợ live: " + feature;
            return result;
        }

        GamePlayer player;
        try
        {
            player = FindOnlinePlayer(userId);
        }
        catch (Exception ex)
        {
            result.Message = "Lỗi tìm người chơi: " + ex.Message;
            return result;
        }

        var apiManaged = IsApiManaged(feature);
        if (player == null || player.PlayerCharacter == null)
        {
            result.Online = false;
            if (!apiManaged)
            {
                result.Message = "Người chơi không online.";
                return result;
            }
        }
        else
        {
            result.Online = true;
        }

        try
        {
            string liveMessage = null;
            lock (ActionLock)
            {
                switch (feature.ToLowerInvariant())
                {
                    case "king": King(player); break;
                    case "guard": Guard(player); break;
                    case "honor": Honor(player); break;
                    case "sworn": Sworn(player); break;
                    case "sworn_love": liveMessage = SwornLove(player); break;
                    case "explorer": Explorer(player); break;
                    case "titles": Titles(player); break;
                    case "amulet_level": AmuletLevel(player); break;
                    case "amulet_wash": AmuletWash(player); break;
                    case "potential": Potential(player); break;
                    case "phuma": PhuMa(player); break;
                    case "duchon": DucHon(player); break;
                    case "avatar": Avatar(player); break;
                    case "album": Album(player); break;
                    case "beads": Beads(userId, player); break;
                    case "magicstone": MagicStone(userId, player); break;
                    case "training_basic": TrainingBasic(userId, player); break;
                    case "training_second": TrainingSecond(userId, player); break;
                    case "training_element": TrainingElement(userId, player); break;
                    case "card_level": CardLevel(userId, player); break;
                    case "card_break": CardBreak(userId, player); break;
                    case "totem_level": TotemLevel(userId, player); break;
                    case "totem_rank": TotemRank(userId, player); break;
                    case "temple_bless": TempleBless(userId, player); break;
                    case "temple_exalt": TempleExalt(userId, player); break;
                    case "gold_weapon": GoldEquip(userId, player, 6, 7); break;
                    case "gold_hat": GoldEquip(userId, player, 0, 1); break;
                    case "gold_cloth": GoldEquip(userId, player, 4, 5); break;
                    case "battle_soul": BattleSoul(userId, player); break;
                    case "craft": Craft(userId, player); break;
                    case "star_weapon": StarUpgrade(userId, player, 7); break;
                    case "star_hat": StarUpgrade(userId, player, 1); break;
                    case "star_cloth": StarUpgrade(userId, player, 5); break;
                    case "subweapon_evolve": SubWeaponEvolve(player); break;
                    case "necklace_train": NecklaceTrain(userId, player); break;
                    case "necklace_cast": NecklaceCast(userId, player); break;
                    case "newbie_gift": NewbieGift(userId); break;
                    case "pet_rebirth": PetRebirth(player); break;
                    case "pet_evolve": PetEvolve(player); break;
                    case "pet_strengthen": PetStrengthen(player); break;
                    case "pet_breakthrough": PetBreakthrough(player); break;
                    case "mount_level": MountLevel(player); break;
                    case "mount_skill": MountSkill(player); break;
                    case "mount_avatar": MountAvatar(player); break;
                    case "mount_amulet": MountAmulet(player); break;
                    case "elf_nurture": ElfNurture(player); break;
                    case "elf_train": ElfTrain(player); break;
                    case "elf_linhbao": ElfLinhBao(player); break;
                    case "elf_learn": ElfLearn(player); break;
                    case "mark_train": MarkTrain(player); break;
                    case "mark_refine": MarkRefine(player); break;
                    case "notice": Notice(player); break;
                    default: throw new GmActionException("Chức năng không hỗ trợ live.");
                }
            }

            result.Ok = true;
            result.Message = liveMessage ?? MessageFor(feature, player);
        }
        catch (GmActionException ex)
        {
            result.Message = ex.Message;
        }
        catch (Exception ex)
        {
            log.Error("[GMApi] Apply " + feature + " user=" + userId + " failed", ex);
            result.Message = "Lỗi áp dụng: " + ex.Message;
        }

        if (player != null) result.Snapshot = TrySnapshot(player);
        try { log.Info($"[GMApi] apply user={userId} feature={feature} online={result.Online} ok={result.Ok} msg={result.Message}"); } catch { }
        return result;
    }

    public static GmPlayerSnapshot Snapshot(GamePlayer player) => TrySnapshot(player);

    private static string FormatPlayerProp(GamePlayer player, string key)
    {
        try
        {
            var cur = player.PlayerProp != null ? player.PlayerProp.Current : null;
            if (cur == null || !cur.ContainsKey(key) || cur[key] == null) return "";
            return string.Join(";", cur[key].OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
        }
        catch { return ""; }
    }

    private static int SumPlayerProp(GamePlayer player, string key)
    {
        try
        {
            var cur = player.PlayerProp != null ? player.PlayerProp.Current : null;
            if (cur == null || !cur.ContainsKey(key) || cur[key] == null) return 0;
            long sum = 0;
            foreach (var v in cur[key].Values) sum += v;
            if (sum > int.MaxValue) return int.MaxValue;
            if (sum < int.MinValue) return int.MinValue;
            return (int)sum;
        }
        catch { return 0; }
    }

    private static GmPlayerSnapshot TrySnapshot(GamePlayer player)
    {
        try
        {
            var pc = player.PlayerCharacter;
            var snap = new GmPlayerSnapshot
            {
                UserId = pc.ID,
                NickName = pc.NickName ?? "",
                State = pc.State,
                Grade = pc.Grade,
                GP = pc.GP,
                Money = pc.Money,
                Gold = pc.Gold,
                DdtKingGrade = pc.ddtKingGrade,
                GuardCoreGrade = pc.guardCoreGrade,
                HonorLevel = pc.HonorInfo != null ? pc.HonorInfo.HonorLevel : 0,
                SwornId = pc.SwornInfo != null ? pc.SwornInfo.swornId : 0,
                ExplorerManualLevel = pc.explorerManualInfo != null ? pc.explorerManualInfo.manualLevel : 0,
                Attack = pc.Attack,
                Defence = pc.Defence,
                Agility = pc.Agility,
                Luck = pc.Luck,
                Hp = pc.hp,
                MagicAttack = pc.MagicAttack,
                MagicDefence = pc.MagicDefence,
                FightPower = pc.FightPower,
                FightPowerShow = pc.FightPower_Show,
                FightPowerDisplay = pc.FightPower + pc.FightPower_Show,
                Damage = player.PlayerProp != null ? player.PlayerProp.totalDamage : 0,
                Armor = player.PlayerProp != null ? player.PlayerProp.totalArmor : 0,
                ArmorBreakdown = FormatPlayerProp(player, "Armor"),
                DamageBreakdown = FormatPlayerProp(player, "Damage"),
                BaseAttack = player.BaseAttack,
                BaseDefence = player.BaseDefence,
                GuardPlus = player.GuardPlus,
                GemSkillId = player.GemSkillId,
                GemSkillCount = player.GemSkillCount
            };
            try { snap.RankCount = player.Rank != null ? player.Rank.GetRanks().Count : 0; } catch { }
            try { snap.AvatarCount = player.AvatarBag != null ? player.AvatarBag.AvatarColect.Count : 0; } catch { }
            try { snap.CardCount = player.CardBag != null ? player.CardBag.GetCards().Count : 0; } catch { }
            try { snap.EquipCount = player.EquipBag != null ? player.EquipBag.GetItems().Count(x => x != null) : 0; } catch { }
            try
            {
                var drills = player.BeadBag != null ? player.BeadBag.UserDrills : null;
                if (drills != null)
                    snap.DrillHoles = string.Join(",", drills.OrderBy(kv => kv.Key)
                        .Select(kv => kv.Value != null ? kv.Value.HoleLv : 0));
            }
            catch { }
            try
            {
                snap.MagicStoneCount = player.MagicStoneBag != null
                    ? player.MagicStoneBag.GetItems().Count(x => x != null && x.IsMagicStone())
                    : 0;
            }
            catch { }
            return snap;
        }
        catch (Exception ex)
        {
            log.Warn("[GMApi] snapshot failed: " + ex.Message);
            return null;
        }
    }


    // ------------------------------------------------------------------ Pet & Mount features

    private static void PetRebirth(GamePlayer player)
    {
        if (player.PetBag == null) throw new GmActionException("Không có dữ liệu thú cưng.");
        var pets = player.PetBag.GetPets().Where(p => p != null && p.Level >= 60).ToList();
        if (pets.Count == 0) throw new GmActionException("Không có thú cưng level ≥ 60 để trùng sinh.");
        int changed = 0;
        foreach (var pet in pets)
        {
            var t = PetMgr.FindPetTemplate(pet.TemplateID);
            if (t == null) continue;
            pet.BloodGrow = t.HighBloodGrow;
            pet.AttackGrow = t.HighAttackGrow;
            pet.DefenceGrow = t.HighDefenceGrow;
            pet.AgilityGrow = t.HighAgilityGrow;
            pet.LuckGrow = t.HighLuckGrow;
            pet.DamageGrow = t.HighDamageGrow;
            pet.GuardGrow = t.HighGuardGrow;
            pet.BuildProp();
            player.PetBag.UpdatePet(pet);
            changed++;
        }
        if (changed == 0) throw new GmActionException("Không tìm thấy dữ liệu mẫu thú cưng phù hợp.");
        player.PetBag.SaveToDatabase(false);
        RefreshProperties(player);
    }

    private static void PetEvolve(GamePlayer player)
    {
        int max = PetMgr.GetEvolutionMax();
        if (max <= 0) throw new GmActionException("Chưa có cấu hình tiến hóa thú cưng.");
        var info = PetMgr.FindFightProperty(max);
        player.PlayerCharacter.evolutionGrade = max;
        player.PlayerCharacter.evolutionExp = info != null ? info.Exp : 0;
        player.UpdatePlayer();
        var pkt = new GSPacketIn(68);
        pkt.WriteByte(23);
        pkt.WriteBoolean(true);
        player.SendTCP(pkt);
        RefreshProperties(player);
    }

    private static void PetStrengthen(GamePlayer player)
    {
        if (player.PetBag == null || player.PetBag.EatPets == null) throw new GmActionException("Không có dữ liệu Pet Mạnh Hóa.");
        int max = PetMoePropertyMgr.FindMaxLevel();
        if (max <= 1) throw new GmActionException("Chưa có cấu hình Pet Mạnh Hóa.");
        var e = player.PetBag.EatPets;
        e.weaponLevel = max; e.weaponExp = 0;
        e.clothesLevel = max; e.clothesExp = 0;
        e.hatLevel = max; e.hatExp = 0;
        player.PetBag.SaveToDatabase(false);
        player.PetBag.UpdateEatPets();
        RefreshProperties(player);
    }

    private static void PetBreakthrough(GamePlayer player)
    {
        if (player.PetBag == null) throw new GmActionException("Không có dữ liệu thú cưng.");
        var pets = player.PetBag.GetPets().Where(p => p != null && p.Level >= 60).ToList();
        if (pets.Count == 0) throw new GmActionException("Không có thú cưng level ≥ 60 để đột phá.");
        foreach (var pet in pets)
        {
            pet.breakGrade = 4; // MaxLevel(): breakGrade 4 => 70, không đổi level hiện tại
            player.PetBag.UpdatePet(pet);
        }
        player.PetBag.SaveToDatabase(false);
        var pkt = new GSPacketIn(68);
        pkt.WriteByte(31);
        pkt.WriteBoolean(true);
        player.SendTCP(pkt);
    }

    private static int GetMaxMountGrade()
    {
        int g = -1;
        while (g < 1000 && MountMgr.GetMountTemplate(g + 1) != null) g++;
        return g;
    }

    private static void MountLevel(GamePlayer player)
    {
        if (player.Horse == null || player.Horse.Info == null) throw new GmActionException("Không có dữ liệu thú cưỡi.");
        int max = GetMaxMountGrade();
        if (max <= 0) throw new GmActionException("Chưa có cấu hình cấp thú cưỡi.");
        player.Horse.Info.curLevel = max;
        player.Horse.Info.curExp = 0;
        player.PlayerCharacter.MountLv = max;
        player.Horse.SaveToDatabase();
        player.UpdatePlayer();
        player.Out.SendUpHorse(true, max, 0);
        player.Horse.UpdateHorsePacket();
        RefreshProperties(player);
    }

    private static void MountSkill(GamePlayer player)
    {
        if (player.Horse == null || player.Horse.Info == null) throw new GmActionException("Không có dữ liệu thú cưỡi.");
        if (player.Horse.curHasSkill == null || player.Horse.curHasSkill.Count == 0)
            throw new GmActionException("Thú cưỡi chưa có kỹ năng nào.");
        int changed = 0;
        foreach (var pair in player.Horse.curHasSkill.ToList())
        {
            var data = pair.Value;
            var arr = MountMgr.GetSkillGetTemplates(data.SKillID);
            if (arr == null || arr.Length == 0) continue;
            var best = arr.OrderBy(x => x.SkillID).ThenBy(x => x.Exp).Last();
            if (best.SkillID == data.SKillID && best.Exp == data.Exp) continue;
            int old = data.SKillID;
            data.SKillID = best.SkillID;
            data.Exp = best.Exp;
            player.Horse.UpSkill(old, data);
            changed++;
        }
        if (changed == 0) throw new GmActionException("Không có kỹ năng thú cưỡi nào cần nâng.");
        player.Horse.SaveToDatabase();
        player.Horse.UpdateHorsePacket();
    }

    private static void MountAvatar(GamePlayer player)
    {
        if (player.Horse == null || player.Horse.Info == null) throw new GmActionException("Không có dữ liệu thú cưỡi.");
        if (player.Horse.horsePicCherish == null || player.Horse.horsePicCherish.Count == 0)
            throw new GmActionException("Chưa có biến hình thú cưỡi nào.");
        int max = ItemMgr.maxGradeSublimarImagemMontaria();
        if (max <= 0) throw new GmActionException("Chưa có cấu hình biến hình thú cưỡi.");
        int changed = 0;
        foreach (var d in player.Horse.horsePicCherish.Values)
        {
            if (d.qiLingGrade >= max) continue;
            d.qiLingGrade = max;
            changed++;
        }
        if (changed == 0) throw new GmActionException("Biến hình thú cưỡi đã đạt tối đa.");
        player.Horse.SaveToDatabase();
        player.Horse.UpdateHorsePacket();
    }

    private static void MountAmulet(GamePlayer player)
    {
        if (player.HorseAmuletBag == null) throw new GmActionException("Không có dữ liệu bùa thú.");
        int changed = 0;
        for (int i = 0; i < 20; i++)
        {
            var item = player.HorseAmuletBag.GetItemAt(i);
            if (item == null) continue;
            var tal = MountMgr.FindMountTalismans(item.TemplateID);
            if (tal == null || tal.ID == 0 || tal.ExtendType1MaxValue <= 0) continue;
            if (item.Hole1 == tal.ExtendType1MaxValue) continue;
            item.Hole1 = tal.ExtendType1MaxValue;
            player.HorseAmuletBag.UpdateItem(item);
            changed++;
        }
        if (changed == 0) throw new GmActionException("Không có bùa thú nào cần tẩy luyện.");
        player.HorseAmuletBag.SaveToDatabase();
        player.EquipBag.UpdatePlayerProperties();
        player.SendUpdatePublicPlayer();
    }


    // ------------------------------------------------------------------ Thần Hộ Mệnh (Elf)

    private static int ElfGrowthCap(string csv)
    {
        try
        {
            var parts = (csv ?? "").Split(',');
            return parts.Length >= 2 && int.TryParse(parts[1].Trim(), out var v) ? v : 0;
        }
        catch { return 0; }
    }

    private static void ElfNurture(GamePlayer player)
    {
        var elfs = player.PlayerCharacter != null ? player.PlayerCharacter.ElfInfo : null;
        if (elfs == null || elfs.Count == 0) throw new GmActionException("Không có Thần Hộ Mệnh nào.");
        int changed = 0;
        foreach (var elf in elfs)
        {
            var grades = ElfMgr.UpgradeList(elf);
            if (grades == null || grades.Count == 0) continue;
            int max = grades.Keys.Max();
            int old = elf.Grade;
            if (old < max)
            {
                elf.Grade = max;
                elf.Exp = 0;
                ElfMgr.CalculateGradeProps(elf, max - old);
                changed++;
            }
            ElfMgr.Out.SendUpdateGrade(player, elf);
        }
        if (changed == 0) throw new GmActionException("Thần Hộ Mệnh đã ở cấp tối đa.");
        player.SaveElfDataToDatabase();
        player.EquipBag.UpdatePlayerProperties();
    }

    private static void ElfTrain(GamePlayer player)
    {
        var elfs = player.PlayerCharacter != null ? player.PlayerCharacter.ElfInfo : null;
        if (elfs == null || elfs.Count == 0) throw new GmActionException("Không có Thần Hộ Mệnh nào.");
        int changed = 0;
        foreach (var elf in elfs)
        {
            var tpl = ElfMgr.Elfs != null ? ElfMgr.Elfs.FirstOrDefault(e => e.TemplateID == elf.TempID) : null;
            if (tpl == null) continue;
            elf.HpGrowth = ElfGrowthCap(tpl.HPGrowths);
            elf.AttackGrowth = ElfGrowthCap(tpl.AtckGrowths);
            elf.HuoGrowth = ElfGrowthCap(tpl.FireGrowths);
            elf.ShuiGrowth = ElfGrowthCap(tpl.WateGrowths);
            elf.FengGrowth = ElfGrowthCap(tpl.WindGrowths);
            elf.TuGrowth = ElfGrowthCap(tpl.LandGrowths);
            elf.GuangGrowth = ElfGrowthCap(tpl.LighGrowths);
            elf.AnGrowth = ElfGrowthCap(tpl.DrakGrowths);
            elf.Quality = ElfMgr.CalculateQuality(elf);
            ElfMgr.ResetElfProps(elf);
            ElfMgr.CalculateGradeProps(elf, Math.Max(0, elf.Grade - 1));
            ElfMgr.Out.SendUpdateProps(player, elf);
            changed++;
        }
        if (changed == 0) throw new GmActionException("Không có dữ liệu Thần Hộ Mệnh để luyện.");
        player.SaveElfDataToDatabase();
        player.EquipBag.UpdatePlayerProperties();
    }

    private static void ElfLinhBao(GamePlayer player)
    {
        var equips = ElfMgr.GetElfEquips(player);
        if (equips == null || equips.Count == 0) throw new GmActionException("Không có Linh Bảo nào trong túi.");
        int changed = 0;
        foreach (var group in equips.GroupBy(e => e.Type))
        {
            int type = group.Key;
            int maxLevel = 0;
            foreach (var equip in group)
            {
                int max = ElfEquipConfigMgr.GetMaxLevel(equip.Type, equip.Quality);
                if (max <= 0) continue;
                if (equip.Level != max)
                {
                    equip.Level = max;
                    changed++;
                }
                if (max > maxLevel) maxLevel = max;
            }
            if (maxLevel > 0)
            {
                ElfMgr.Out.SendUpgradeElfEquip(player, type, maxLevel);
                ElfMgr.Out.SendUpdateElfEquipLvInfo(player);
            }
        }
        if (changed == 0) throw new GmActionException("Linh Bảo đã ở cấp tối đa.");
        player.SaveElfDataToDatabase();
        player.EquipBag.UpdatePlayerProperties();
    }


    private static void ElfLearn(GamePlayer player)
    {
        var elfs = player.PlayerCharacter != null ? player.PlayerCharacter.ElfInfo : null;
        if (elfs == null || elfs.Count == 0) throw new GmActionException("Không có Thần Hộ Mệnh nào.");
        var skills = new Dictionary<int, int[]>
        {
            // 1 = Lửa: Thuần Khiết, Bùng Cháy, Lửa Xâm Lấn, Hồn Lửa Mạnh, Nguyên Tố Lửa, Sức Lửa, Lửa Chúc Phúc, Vũ Điệu Lửa
            { 1, new[] { 1055, 1054, 953, 950, 947, 944, 932, 920 } },
            // 2 = Gió: Lời Của Gió, Chuỗi Sấm Sét, Gió Sắc, Hồn Gió Mạnh, Sấm Chớp, Hồn Gió, Làn Điệu Gió, Bão Lốc
            { 2, new[] { 1059, 1058, 983, 980, 977, 974, 938, 926 } },
            // 3 = Đất: Kiên Cố, Hóa Đá, Đất Xâm Thực, Gai Đất, Đất Bảo Vệ, Đất Giận Dữ, Giáp Thạch, Gươm Trong Đá
            { 3, new[] { 1061, 1060, 9992, 986, 941, 929, 917, 905 } },
            // 4 = Nước: Phản Chiếu, Kết Giới Nước, Sương Giá, Hồn Nước Mạnh, Nguyên Tố Nước, Gợn Sóng, Băng Sương, Giáp Băng
            { 4, new[] { 1057, 1056, 968, 965, 962, 923, 959, 935 } }
        };
        int changed = 0;
        foreach (var elf in elfs)
        {
            var tpl = ElfMgr.Elfs != null ? ElfMgr.Elfs.FirstOrDefault(e => e.TemplateID == elf.TempID) : null;
            if (tpl == null || !skills.TryGetValue(tpl.ElfType, out var ids)) continue;
            var list = string.Join(",", ids);
            if (!string.Equals(elf.SkillList, list, StringComparison.Ordinal))
            {
                elf.SkillList = list;
                changed++;
            }
            ElfMgr.Out.SendUpdateSkill(player, elf);
        }
        if (changed == 0) throw new GmActionException("Kỹ năng Thần Hộ Mệnh đã full.");
        player.SaveElfDataToDatabase();
        player.EquipBag.UpdatePlayerProperties();
    }


    // ------------------------------------------------------------------ Ấn / Mark

    private static List<MarkChipItemInfo> GetBagMarks(GamePlayer player)
    {
        var result = new List<MarkChipItemInfo>();
        if (player == null || player.Marks == null) return result;
        foreach (var bag in player.Marks.GetAllChipBags())
        {
            if (bag.Key == player.Marks.TYPE_EQUIP) continue;
            foreach (var chip in bag.Value)
            {
                if (chip != null && chip.IsExist) result.Add(chip);
            }
        }
        return result;
    }

    private static void MaxMarkLevelAndMainProp(GamePlayer player, MarkChipItemInfo chip)
    {
        var info = EngraveMgr.GetSingleEngraveDebris(chip.TemplateID);
        if (info == null) return;
        int top = EngraveMgr.GetTemperTopLevel(info.Character);
        if (top <= 0) return;
        chip.hLv = top;
        var main = chip.MainProp;
        if (main != null && main.type != 0)
        {
            main.attachValue = EngraveMgr.GetAttributeAdd(info, top, (eMarkProType)main.type);
            if (main.hummerCount < 1) main.hummerCount = 1;
            chip.SetMainProp(main);
        }
        player.Marks.CreateSecondProp(chip);
    }

    private static void MarkTrain(GamePlayer player)
    {
        var chips = GetBagMarks(player);
        if (chips.Count == 0) throw new GmActionException("Không có Ấn nào trong túi ấn.");
        int count = 0;
        foreach (var chip in chips)
        {
            MaxMarkLevelAndMainProp(player, chip);
            player.Marks.OnChipUpdate(chip, false);
            count++;
        }
        if (count == 0) throw new GmActionException("Không có Ấn nào trong túi ấn.");
        player.Marks.SendUpdateChip();
        player.Marks.SaveToDatabase();
        player.EquipBag.UpdatePlayerProperties();
    }

    /// <summary>
    /// Bật cờ "Thông Báo" cho client: gửi packet 644 (ePackageType.NOTICE).
    /// Client chỉ đặt isAutoOpen = true; cửa sổ Thông Báo hiện ở lần vào sảnh kế tiếp
    /// (đúng cơ chế của NPH — relog hoặc rời phòng/vào lại sảnh).
    /// </summary>
    private static void Notice(GamePlayer player)
    {
        player.SendTCP(new GSPacketIn(644));
        log.Info("[GM API] notice -> userId=" + player.PlayerCharacter.ID);
    }

    private static void MarkRefine(GamePlayer player)
    {
        var chips = GetBagMarks(player);
        if (chips.Count == 0) throw new GmActionException("Không có Ấn nào trong túi ấn.");

        // B1: luyện max cấp + chỉ số chính trước.
        foreach (var chip in chips)
        {
            MaxMarkLevelAndMainProp(player, chip);
        }

        // B2: tìm giá trị cao nhất của từng loại chỉ số phụ trong toàn bộ túi ấn.
        var maxByType = new Dictionary<int, int>();
        foreach (var chip in chips)
        {
            var props = chip.SecondProps;
            if (props == null) continue;
            foreach (var prop in props)
            {
                if (prop == null || prop.type <= 0) continue;
                int cur;
                if (!maxByType.TryGetValue(prop.type, out cur) || prop.value > cur)
                    maxByType[prop.type] = prop.value;
            }
        }

        // B3: nâng từng chỉ số phụ lên mức cao nhất của loại đó (hoặc max config của template).
        int changed = 0;
        foreach (var chip in chips)
        {
            var props = chip.SecondProps;
            if (props != null)
            {
                bool chipChanged = false;
                foreach (var prop in props)
                {
                    if (prop == null || prop.type <= 0) continue;
                    int max;
                    maxByType.TryGetValue(prop.type, out max);
                    var cfg = EngraveMgr.GetDebrisProperty(chip.TemplateID, prop.type);
                    if (cfg != null && cfg.MaxValue > max) max = cfg.MaxValue;
                    if (prop.value < max)
                    {
                        prop.value = max;
                        prop.hummerCount++;
                        chipChanged = true;
                    }
                }
                if (chipChanged)
                {
                    chip.SetSecondProps(props);
                    changed++;
                }
            }
            player.Marks.OnChipUpdate(chip, false);
        }

        player.Marks.SendUpdateChip();
        player.Marks.SaveToDatabase();
        player.EquipBag.UpdatePlayerProperties();
        if (changed == 0 && chips.Count == 0) throw new GmActionException("Không có Ấn nào trong túi ấn.");
    }

    private static string MessageFor(string feature, GamePlayer player)
    {
        var online = player != null;
        switch (feature.ToLowerInvariant())
        {
            case "king": return "Đã nâng level 70 + Cấp Vua Gà level 10 (live).";
            case "guard": return "Đã nâng Bảo Vệ level 20 (live).";
            case "honor": return "Đã nâng Vinh Dự level 100 và tính lại chỉ số (live).";
            case "sworn": return "Đã nâng Nhẫn Cầu Hôn level 100 và tính lại chỉ số (live).";
            case "sworn_love": return "Đã nâng MAX điểm tình ý Nhẫn Cầu Hôn và cập nhật cho client (live).";
            case "explorer": return "Đã mở full Sổ Tay Thám Hiểm level 21 (live).";
            case "titles": return "Đã thêm toàn bộ danh hiệu và đẩy danh sách mới cho client (live).";
            case "amulet_level": return "Đã nâng Bùa Hộ Thân level 50 (live).";
            case "amulet_wash": return "Đã tẩy luyện Bùa Hộ Thân 10 sao (live).";
            case "potential": return "Đã kích hoạt full Tiềm Năng (live).";
            case "phuma": return "Đã nâng full Phụ Ma level 40 (live).";
            case "duchon": return "Đã nâng full Đúc Hồn level 20 (live).";
            case "avatar": return "Đã mở full Bộ Sưu Tập và cập nhật cho client (live).";
            case "album": return "Đã mở full Album Thẻ và cập nhật cho client (live).";
            case "beads":
                return online
                    ? "Đã mở full 6 mũi khoan (live) và gửi châu báu vào hộp thư."
                    : "Đã mở full 6 mũi khoan và gửi châu báu vào hộp thư (user offline — sẽ thấy khi đăng nhập).";
            case "magicstone":
                return online
                    ? "Đã xử lý Ma Thạch (live) và gửi/nâng ma thạch đủ 6 chỉ số."
                    : "Đã xử lý Ma Thạch, gửi/nâng đủ 6 chỉ số vào hộp thư (user offline).";
            case "training_basic": return "Đã nâng Tu luyện cơ bản lên tối đa.";
            case "training_second": return "Đã nâng Tu luyện thuộc tính cấp 2 lên tối đa.";
            case "training_element": return "Đã nâng Tu luyện nguyên tố lên tối đa.";
            case "card_level": return "Đã nâng Thẻ Bài lên cấp tối đa.";
            case "card_break": return "Đã đột phá Thẻ Bài lên bộ tối đa.";
            case "totem_level": return "Đã nâng Vật Tổ lên cấp tối đa.";
            case "totem_rank": return "Đã tăng bậc Vật Tổ lên tối đa.";
            case "temple_bless": return "Đã chúc phúc Miếu Thần lên tối đa.";
            case "temple_exalt": return "Đã thăng hoa Miếu Thần lên tối đa.";
            case "gold_weapon": return "Đã dát vàng Vũ Khí.";
            case "gold_hat": return "Đã dát vàng Nón.";
            case "gold_cloth": return "Đã dát vàng Quần Áo.";
            case "battle_soul": return "Đã nâng Chiến Hồn lên tối đa.";
            case "craft": return "Đã nâng Chế Tạo lên tối đa.";
            case "star_weapon": return "Đã tăng sao Vũ Khí lên tối đa.";
            case "star_hat": return "Đã tăng sao Nón lên tối đa.";
            case "star_cloth": return "Đã tăng sao Quần Áo lên tối đa.";
            case "subweapon_evolve": return "Đã tiến hoá VK phụ lên level tối đa.";
            case "necklace_train": return "Đã luyện Dây Chuyền lên tối đa.";
            case "necklace_cast": return "Đã đúc Dây Chuyền lên tối đa.";
            case "newbie_gift": return "Đã gửi Quà Tân Thủ vào hộp thư.";
            case "pet_rebirth": return "Đã trùng sinh (max chỉ số) thú cưng level ≥ 60.";
            case "pet_evolve": return "Đã nâng Tiến Hóa thú cưng lên tối đa.";
            case "pet_strengthen": return "Đã nâng Mạnh Hóa thú cưng lên tối đa.";
            case "pet_breakthrough": return "Đã đột phá thú cưng level ≥ 60 lên mốc 70.";
            case "mount_level": return "Đã nâng cấp thú cưỡi lên tối đa.";
            case "mount_skill": return "Đã nâng toàn bộ kỹ năng thú cưỡi lên tối đa.";
            case "mount_avatar": return "Đã nâng biến hình thú cưỡi lên tối đa.";
            case "mount_amulet": return "Đã tẩy luyện max chỉ số toàn bộ bùa thú.";
            case "elf_nurture": return "Đã nâng Thần Hộ Mệnh lên cấp tối đa.";
            case "elf_train": return "Đã luyện max tư chất toàn bộ Thần Hộ Mệnh.";
            case "elf_linhbao": return "Đã nâng max cấp toàn bộ Linh Bảo trong túi.";
            case "elf_learn": return "Đã học và trang bị full kỹ năng cao cho Thần Hộ Mệnh đang có.";
            case "mark_train": return "Đã luyện max cấp + chỉ số chính cho toàn bộ Ấn trong túi ấn.";
            case "mark_refine": return "Đã luyện hóa max cấp + chỉ số phụ cho toàn bộ Ấn trong túi ấn.";
            case "notice": return "Đã bật cờ Thông Báo — cửa sổ sẽ hiện khi người chơi vào sảnh (relog hoặc rời phòng).";
            default: return "Đã áp dụng.";
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Tính lại toàn bộ thuộc tính (HP/Công/Thủ/...) từ dữ liệu in-memory rồi gửi packet.
    /// Bắt buộc sau khi sửa Honor/Sworn/King/title/trang bị — nếu chỉ gọi UpdateProperties()
    /// thì chỉ gửi packet, KHÔNG cộng lại chỉ số (client thấy số liệu cũ).
    /// </summary>
    private static void RefreshProperties(GamePlayer player)
    {
        try { player.EquipBag.SaveToDatabase(); } catch (Exception ex) { log.Warn("[GMApi] SaveToDatabase: " + ex.Message); }
        player.EquipBag.UpdatePlayerProperties();
        player.UpdateProperties();
        player.SendUpdatePublicPlayer();
    }

    private static string GetNickName(int userId)
    {
        using (var pb = new PlayerBussiness())
        {
            var info = pb.GetUserSingleByUserID(userId);
            return info != null ? info.NickName : "";
        }
    }

    private static void SendMailItem(PlayerBussiness pb, string nickName, int templateId, int count, int strengthenLevel)
    {
        pb.SendMailAndItemByNickName("GM Panel", "Phần thưởng GM", nickName, ZoneId,
            templateId, count, 0, 0, 0, strengthenLevel, 0, 0, 0, 0, true);
    }

    // ------------------------------------------------------------------ features

    private static void King(GamePlayer player)
    {
        var pc = player.PlayerCharacter;
        pc.Grade = 70;
        pc.GP = 857997577L;

        var king = player.KingGrade;
        if (king != null)
        {
            if (king.Data == null) king.LoadFromDatabase();
            if (king.Data != null)
            {
                king.Data.ddtKingGrade = 10;
                king.Data.usableNum = 10;
                king.Data.AttackLevel = 10;
                king.Data.DefenceLevel = 10;
                king.Data.AgilityLevel = 10;
                king.Data.LuckyLevel = 10;
                king.Data.MagicAttackLevel = 10;
                king.Data.MagicDefenceLevel = 10;
            }
        }
        pc.ddtKingGrade = 10;
        RefreshProperties(player);
    }

    private static void Guard(GamePlayer player)
    {
        var pc = player.PlayerCharacter;
        if (pc.Grade < 50)
        {
            pc.Grade = 50;
            pc.GP = 63832646L;
        }
        pc.guardCoreGrade = 20;
        RefreshProperties(player);
    }

    private static void Honor(GamePlayer player)
    {
        var honor = player.PlayerCharacter.HonorInfo;
        if (honor == null) throw new GmActionException("Người chơi chưa có dữ liệu Vinh Dự (HonorInfo).");
        honor.HonorLevel = 100;
        honor.HonorExp = 0;
        RefreshProperties(player);
    }

    private static void Sworn(GamePlayer player)
    {
        var sworn = player.PlayerCharacter.SwornInfo;
        if (sworn == null) throw new GmActionException("Người chơi chưa có dữ liệu Nhẫn Cầu Hôn (SwornInfo).");
        sworn.swornId = 9522;
        sworn.swornCount = 124860;
        sworn.isHasSwornItem = 1;
        sworn.skillId = 1072;
        RefreshProperties(player);
    }

    // ------------------------------------------------------------------ Nhẫn Cầu Hôn — Tình Ý (RingExp)

    /// <summary>Mốc tình ý dự phòng khi không đọc được Db_Tank_All.dbo.Love_level (dữ liệu 12.6: Lv60 = 99.690).</summary>
    private const int FallbackMaxRingLevel = 60;
    private const int FallbackMaxRingExp = 99690;

    /// <summary>
    /// Nâng MAX "điểm tình ý" của Nhẫn Cầu Hôn — cột <c>Sys_Users_Detail.RingExp</c>.
    ///
    /// Vì sao phải làm LIVE (không chỉ UPDATE DB): panel ghi DB = max, nhưng người chơi đang online
    /// vẫn giữ <c>PlayerCharacter.RingExp = 0</c> trong bộ nhớ Road; khi logout server save lại
    /// in-memory nên DB bị ghi đè về 0. Ở đây set in-memory = max + gửi packet (67 public player,
    /// kèm private info) để client nhận ngay lập tức, và lần save kế tiếp ghi đúng max.
    /// Idempotent: gọi lại nhiều lần vẫn chỉ ra MAX, vẫn gửi lại packet để client refresh.
    /// Trả về message tiếng Việt (dùng cho response thay vì MessageFor).
    /// </summary>
    private static string SwornLove(GamePlayer player)
    {
        var pc = player.PlayerCharacter;
        if (pc == null) throw new GmActionException("Người chơi không có dữ liệu nhân vật.");

        var max = GetMaxLoveLevel();
        var beforeExp = pc.RingExp;
        var beforeLevel = GetLoveLevel(beforeExp);

        pc.RingExp = max.Exp;          // setter tự đánh dấu dirty → logout save ghi RingExp mới
        RefreshProperties(player);     // tính lại chỉ số nhẫn cưới theo % Love_level + gửi packet 67

        // Ghi DB ngay (an toàn kép): panel thường đã UPDATE trước khi gọi API, còn nếu API được
        // gọi trực tiếp thì vẫn đảm bảo DB = max. Lỗi DB không làm hỏng live — logout sẽ save từ bộ nhớ.
        try
        {
            ExecSql($"UPDATE Sys_Users_Detail SET RingExp={max.Exp} WHERE UserID={pc.ID}");
        }
        catch (Exception ex)
        {
            log.Warn("[GMApi] sworn_love ghi DB lỗi (bỏ qua, logout sẽ save): " + ex.Message);
        }

        if (beforeExp >= max.Exp)
        {
            return $"Nhẫn Cầu Hôn — Tình Ý: đã ở MAX từ trước (Lv{max.Level} — {max.Exp:N0} tình ý); " +
                   "đã gửi lại dữ liệu cho client (live).";
        }

        return $"Nhẫn Cầu Hôn — Tình Ý: đã nâng MAX Lv{beforeLevel} ({beforeExp:N0}) → " +
               $"Lv{max.Level} ({max.Exp:N0} tình ý) (live).";
    }

    /// <summary>
    /// Đọc mốc tình ý cao nhất (Level, Exp) từ <c>Db_Tank_All.dbo.Love_level</c>.
    /// Fallback Lv60 / 99.690 nếu DB lỗi hoặc bảng rỗng — giống dữ liệu client (LoveLeveList.xml).
    /// </summary>
    private static (int Level, int Exp) GetMaxLoveLevel()
    {
        try
        {
            var ds = new DataBaseHelper("conString").GetDateSet(
                "SELECT TOP 1 Level, Exp FROM Db_Tank_All.dbo.Love_level ORDER BY Level DESC");
            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                var row = ds.Tables[0].Rows[0];
                var level = Convert.ToInt32(row["Level"]);
                var exp = Convert.ToInt32(row["Exp"]);
                if (exp > 0) return (level, exp);
            }
        }
        catch (Exception ex)
        {
            log.Warn("[GMApi] Đọc Love_level thất bại, dùng fallback 99690: " + ex.Message);
        }
        return (FallbackMaxRingLevel, FallbackMaxRingExp);
    }

    /// <summary>
    /// Cấp tình ý hiện tại theo Exp = số mốc <c>Love_level</c> có Exp &lt;= exp
    /// (khớp cách panel tính). Fallback 0 nếu không đọc được DB.
    /// </summary>
    private static int GetLoveLevel(int exp)
    {
        try
        {
            var text = ScalarSql($"SELECT COUNT(*) FROM Db_Tank_All.dbo.Love_level WHERE Exp <= {exp}");
            return int.TryParse(text, out var level) ? level : 0;
        }
        catch (Exception ex)
        {
            log.Warn("[GMApi] Đọc cấp tình ý Love_level lỗi: " + ex.Message);
            return 0;
        }
    }

    private static void Explorer(GamePlayer player)
    {
        var userId = player.PlayerCharacter.ID;
        using (var pb = new PlayerBussiness())
        {
            var info = pb.InitExplorerManualInfo(userId);
            if (info == null) throw new GmActionException("Không đọc được dữ liệu Sổ Tay Thám Hiểm từ DB.");
            info.activesPage = pb.InitExplorerManualInfoPages(userId);
            info.debris = pb.InitExplorerManualDebris(userId);
            player.PlayerCharacter.explorerManualInfo = info;
        }
        RefreshProperties(player);
    }

    private static void Titles(GamePlayer player)
    {
        if (player.Rank == null) throw new GmActionException("Người chơi chưa có dữ liệu danh hiệu (Rank).");
        player.Rank.LoadFromDatabase();
        player.Rank.SendUserRanks();
        RefreshProperties(player);
    }

    private static void AmuletLevel(GamePlayer player)
    {
        var amulet = FindAmulet(player);
        if (amulet == null || amulet.TemplateID != 390101)
            throw new GmActionException("Bạn chưa trang bị bùa hộ thân.");
        amulet.StrengthenLevel = 50;
        amulet.StrengthenTimes = 0;
        amulet.IsBinds = true;
        player.EquipBag.UpdateItem(amulet);
        RefreshProperties(player);
    }

    private static void AmuletWash(GamePlayer player)
    {
        var amulet = FindAmulet(player);
        if (amulet == null || amulet.TemplateID != 390101)
            throw new GmActionException("Bạn chưa trang bị bùa hộ thân.");
        ApplyAmuletWash(amulet);
        player.EquipBag.UpdateItem(amulet);
        RefreshProperties(player);
    }


    /// <summary>Tìm Bùa Hộ Thân (390101): ô 18, ô trang bị khác, hoặc trong túi → tự trang bị vào ô 18.</summary>
    private static ItemInfo FindAmulet(GamePlayer player)
    {
        var at18 = player.EquipBag.GetItemAt(18);
        if (at18 != null && at18.TemplateID == 390101) return at18;

        // đang trang bị ở ô khác (0..30)
        foreach (var it in player.EquipBag.GetItems(0, 31))
        {
            if (it != null && it.TemplateID == 390101) return it;
        }

        // nằm trong túi (31..80) → thử trang bị vào ô 18
        foreach (var it in player.EquipBag.GetItems(31, 81))
        {
            if (it == null || it.TemplateID != 390101) continue;
            if (player.EquipBag.GetItemAt(18) == null)
            {
                try { player.EquipBag.MoveItem(it.Place, 18, it.Count); } catch { }
                var moved = player.EquipBag.GetItemAt(18);
                if (moved != null && moved.TemplateID == 390101) return moved;
            }
            return it;
        }
        return null;
    }

    private static void ApplyAmuletWash(ItemInfo amulet)
    {
        amulet.StrengthenExp = 10000;
        amulet.Hole1 = 1;
        amulet.Hole2 = 2;
        amulet.Hole3 = 3;
        amulet.Hole4 = 4;
        amulet.Hole5 = 10000;
        amulet.Hole6 = 10000;
        amulet.Hole5Exp = 10000;
        amulet.Hole6Exp = 10000;
        amulet.IsBinds = true;
    }

    /// <summary>Các CategoryID được phép dùng Tiềm Năng: Kính(2), Mắt(6), Mặt(4), Tóc(3), Set quần áo(13), Set cánh(15).</summary>
    private static readonly int[] PotentialCategories = { 2, 3, 4, 6, 13, 15 };

    private static void Potential(GamePlayer player)
    {
        var any = false;
        // EquipBag.GetItems() chỉ trả slot >= 31 (bag/dress) → phải lấy slot trang bị 0..30.
        foreach (var item in player.EquipBag.GetItems(0, 31))
        {
            if (item == null || item.Template == null) continue;
            if (!PotentialCategories.Contains(item.Template.CategoryID)) continue;
            any = true;
            item.latentEnergyCurStr = "80,80,80,80";
            item.latentEnergyNewStr = "80,80,80,80";
            item.latentEnergyEndTime = DateTime.Now.AddYears(50);
            item.IsBinds = true;
            player.EquipBag.UpdateItem(item);
        }
        if (!any) throw new GmActionException("Bạn chưa trang bị Kính, Mắt, Mặt, Tóc, Set quần áo, Set cánh.");
        RefreshProperties(player);
    }


    /// <summary>
    /// Tự trang bị Nhẫn/Vòng tay/Nhẫn cầu hôn đang nằm trong túi (slot 31..80) vào ô trang bị
    /// tương ứng, để Phụ Ma/Đúc Hồn hoạt động cả khi người chơi chỉ mới sở hữu item.
    /// Trả về số trang bị đã ở trong ô sau khi xử lý.
    /// </summary>
    /// <summary>Ô hợp lệ cho từng loại: vòng tay 7-8, nhẫn 9-10, vật định tình chỉ vào ô Nhẫn Cầu Hôn (16).</summary>
    private static int[] RingSlotsFor(ItemTemplateInfo temp)
    {
        int cat = temp.CategoryID;
        if (cat == 8 || cat == 28) return new[] { 7, 8 };
        if (cat == 9 || cat == 29)
            return Equip.isWeddingRing(temp) ? new[] { 16 } : new[] { 9, 10 };
        return new int[0];
    }

    /// <summary>
    /// Tự trang bị Nhẫn/Vòng tay/Nhẫn cầu hôn đang nằm trong túi (slot 31..80) vào đúng ô trang bị.
    /// Vật định tình chỉ vào ô 16; nhẫn thường chỉ 9/10; vòng tay 7/8.
    /// Trả về số trang bị đã ở trong ô sau khi xử lý.
    /// </summary>
    private static int EnsureRingsEquipped(GamePlayer player)
    {
        var bag = player.EquipBag;
        foreach (var item in bag.GetItems(31, 81))
        {
            if (item == null || item.Template == null) continue;
            var slots = RingSlotsFor(item.Template);
            if (slots.Length == 0) continue;
            foreach (var slot in slots)
            {
                if (bag.GetItemAt(slot) != null) continue;
                if (!bag.CanEquipSlotContains(slot, item.Template)) continue;
                try
                {
                    if (bag.MoveItem(item.Place, slot, item.Count)) break;
                }
                catch { }
            }
        }

        int count = 0;
        foreach (var item in bag.GetItems(0, 31))
        {
            if (item == null || item.Template == null) continue;
            int cat = item.Template.CategoryID;
            if (cat == 8 || cat == 9 || cat == 28 || cat == 29) count++;
        }
        return count;
    }

    private static void PhuMa(GamePlayer player)
    {
        if (EnsureRingsEquipped(player) == 0) throw new GmActionException("Bạn chưa trang bị nhẫn và vòng tay.");
        var any = false;
        // EquipBag.GetItems() chỉ trả slot >= 31 (bag/dress) → phải lấy slot trang bị 0..30.
        foreach (var item in player.EquipBag.GetItems(0, 31))
        {
            if (!IsMagicEnchantable(item)) continue;
            any = true;
            item.MagicLevel = 40;
            item.MagicExp = 298100;
            item.IsBinds = true;
            player.EquipBag.UpdateItem(item);
        }
        if (!any) throw new GmActionException("Bạn chưa trang bị nhẫn và vòng tay.");
        RefreshProperties(player);
    }

    private static void DucHon(GamePlayer player)
    {
        if (EnsureRingsEquipped(player) == 0) throw new GmActionException("Bạn chưa trang bị nhẫn và vòng tay.");
        var any = false;
        // EquipBag.GetItems() chỉ trả slot >= 31 (bag/dress) → phải lấy slot trang bị 0..30.
        foreach (var item in player.EquipBag.GetItems(0, 31))
        {
            if (!IsMagicEnchantable(item)) continue;
            any = true;
            item.enchantSpiritLevel = 20;
            item.IsBinds = true;
            player.EquipBag.UpdateItem(item);
        }
        if (!any) throw new GmActionException("Bạn chưa trang bị nhẫn và vòng tay.");
        RefreshProperties(player);
    }

    /// <summary>
    /// Nhẫn/Vòng tay ĐANG TRANG BỊ mới được Phụ Ma/Đúc Hồn (gồm cả Nhẫn Cầu Hôn - Vật Định Tình).
    /// Item chỉ nằm trong túi (Place >= 31) hoặc không có → coi như chưa trang bị.
    /// </summary>
    private static bool IsMagicEnchantable(ItemInfo item)
    {
        if (item == null || item.Template == null) return false;
        var cat = item.Template.CategoryID;
        if (cat != 8 && cat != 9 && cat != 28 && cat != 29) return false;
        return true;
    }

    private static void Avatar(GamePlayer player)
    {
        var bag = player.AvatarBag;
        if (bag == null) throw new GmActionException("Người chơi chưa có dữ liệu Bộ Sưu Tập (AvatarBag).");

        // Panel vừa DELETE + INSERT User_Avatar_Colection → clear cache in-memory rồi nạp lại.
        // AddAvatarColectionInfo chỉ thêm key mới, không ghi đè key cũ → phải clear qua reflection.
        ClearAvatarCollection(bag);
        bag.LoadFromDatabase();
        bag.UpdateInfo();
        RefreshProperties(player);
    }

    private static void ClearAvatarCollection(object avatarBag)
    {
        try
        {
            var field = avatarBag.GetType().GetField("m_avatarColect", BindingFlags.Instance | BindingFlags.NonPublic);
            var dictionary = field != null ? field.GetValue(avatarBag) as IDictionary : null;
            dictionary?.Clear();
        }
        catch (Exception ex)
        {
            log.Warn("[GMApi] ClearAvatarCollection warning: " + ex.Message);
        }
    }

    private static void Album(GamePlayer player)
    {
        var bag = player.CardBag;
        if (bag == null) throw new GmActionException("Người chơi chưa có dữ liệu Album Thẻ (CardBag).");

        // Panel vừa DELETE + INSERT Sys_Users_Card (Place >= 5) → xoá cache rồi nạp lại từ DB.
        bag.Clear();
        bag.LoadFromDatabase();
        try { bag.ResetAtributosCardsEquiped(); } catch { }
        CardNewSystemMgr.Out.SendCardInfo(player);
        RefreshProperties(player);
    }

    // ------------------------------------------------------------------ Form1 features (v0.29)

    private static void ExecSql(string sql)
    {
        try
        {
            var helper = new DataBaseHelper("conString");
            if (!helper.ExecuteNonQuery(sql))
                throw new GmActionException("Không cập nhật được dữ liệu DB.");
        }
        catch (GmActionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GmActionException("Lỗi DB: " + ex.Message);
        }
    }

    private static string ScalarSql(string sql)
    {
        try
        {
            var ds = new DataBaseHelper("conString").GetDateSet(sql);
            if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0) return null;
            var value = ds.Tables[0].Rows[0][0];
            return value == null || value == DBNull.Value ? null : value.ToString();
        }
        catch (Exception ex)
        {
            throw new GmActionException("Lỗi đọc DB: " + ex.Message);
        }
    }

    private static void TrainingBasic(int userId, GamePlayer player)
    {
        const int v = 56655500;
        if (player != null)
        {
            var t = player.PlayerCharacter.Texp;
            t.attTexpExp = v; t.defTexpExp = v; t.lukTexpExp = v; t.spdTexpExp = v;
            t.hpTexpExp = v; t.magicAtkTexpExp = v; t.magicDefTexpExp = v;
            player.UpdateProperties();
            player.SendUpdatePublicPlayer();
            return;
        }
        ExecSql($"UPDATE Sys_Users_Texp SET attTexpExp={v}, defTexpExp={v}, lukTexpExp={v}, spdTexpExp={v}, hpTexpExp={v}, magicAtkTexpExp={v}, magicDefTexpExp={v} WHERE UserID={userId}");
    }

    private static void TrainingSecond(int userId, GamePlayer player)
    {
        const int v = 56655500;
        if (player != null)
        {
            var t = player.PlayerCharacter.Texp;
            t.TrainCritTexpExp = v; t.TrainSunderTexpExp = v; t.TrainCritDmgTexpExp = v;
            t.TrainSpeedTexpExp = v; t.TrainTricKillTexpExp = v; t.TrainDmgTexpExp = v;
            t.TrainArmorTexpExp = v;
            player.UpdateProperties();
            player.SendUpdatePublicPlayer();
            return;
        }
        ExecSql($"UPDATE Sys_Users_Texp SET TrainCritTexpExp={v}, TrainSunderTexpExp={v}, TrainCritDmgTexpExp={v}, TrainSpeedTexpExp={v}, TrainTricKillTexpExp={v}, TrainDmgTexpExp={v}, TrainArmorTexpExp={v} WHERE UserID={userId}");
    }

    private static void TrainingElement(int userId, GamePlayer player)
    {
        const int v = 56655500;
        if (player != null)
        {
            var t = player.PlayerCharacter.Texp;
            t.TrainAvoidDmgTextExp = v; t.TrainTricDefTexpExp = v; t.TrainTenacTexpExp = v;
            t.TrainGuardTexpExp = v; t.TrainFireDefTexpExp = v; t.TrainWaterDefTexpExp = v;
            t.TrainWindDefTexpExp = v; t.TrainLandDefTexpExp = v;
            player.UpdateProperties();
            player.SendUpdatePublicPlayer();
            return;
        }
        ExecSql($"UPDATE Sys_Users_Texp SET TrainAvoidDmgTextExp={v}, TrainTricDefTexpExp={v}, TrainTenacTexpExp={v}, TrainGuardTexpExp={v}, TrainFireDefTexpExp={v}, TrainWaterDefTexpExp={v}, TrainWindDefTexpExp={v}, TrainLandDefTexpExp={v} WHERE UserID={userId}");
    }

    private static void CardLevel(int userId, GamePlayer player)
    {
        if (player != null)
        {
            var c = player.PlayerCharacter.CardInfo;
            c.CardLevel = 90; c.Point = 209; c.Armor = 0; c.Crits = 0; c.Trick = 0; c.Viole = 0;
            c.Attack = 240; c.Defence = 240; c.Agility = 240; c.Lucky = 240;
            CardNewSystemMgr.Out.SendCardInfo(player);
            player.UpdateProperties();
            player.SendUpdatePublicPlayer();
            return;
        }
        ExecSql($"UPDATE Sys_Users_CardInfo SET CardLevel=90, Point=209, Armor=0, Crits=0, Trick=0, Viole=0, Attack=240, Defence=240, Agility=240, Lucky=240 WHERE UserID={userId}");
    }

    private static void CardBreak(int userId, GamePlayer player)
    {
        if (player != null)
        {
            player.PlayerCharacter.CardInfo.SuitLevel = 31;
            CardNewSystemMgr.Out.SendCardInfo(player);
            player.UpdateProperties();
            player.SendUpdatePublicPlayer();
            return;
        }
        ExecSql($"UPDATE Sys_Users_CardInfo SET SuitLevel=31 WHERE UserID={userId}");
    }

    private static void TotemLevel(int userId, GamePlayer player)
    {
        if (player != null)
        {
            player.PlayerCharacter.totemId = 10350;
            player.Out.SendPlayerRefreshTotem(player.PlayerCharacter);
            RefreshProperties(player);
            return;
        }
        ExecSql($"UPDATE Sys_Users_Detail SET totemId=10350 WHERE UserID={userId}");
    }

    private static void TotemRank(int userId, GamePlayer player)
    {
        if (player != null)
        {
            var dict = player.PlayerCharacter.GetTotemUpgrade() ?? new Dictionary<int, int>();
            for (var i = 1; i <= 5; i++) dict[i] = 20;
            player.PlayerCharacter.SetTotemUpgrade(dict);
            player.Out.SendTotemUpgradeInfo(player.PlayerCharacter.GetTotemUpgrade());
            RefreshProperties(player);
            return;
        }
        var json = JsonConvert.SerializeObject(new Dictionary<int, int> { { 1, 20 }, { 2, 20 }, { 3, 20 }, { 4, 20 }, { 5, 20 } });
        ExecSql($"UPDATE Sys_Users_Detail SET TotemUpgrade='{json.Replace("'", "''")}' WHERE UserID={userId}");
    }

    private static void SendTempleInfo(GamePlayer player)
    {
        var info = player.Actives.Info;
        var pkg = new GSPacketIn(297);
        pkg.WriteByte(0);
        pkg.WriteInt(info.CurrentSelectIndex);
        pkg.WriteInt(info.CurrentLevel);
        pkg.WriteInt(info.HomeTempleExp);
        pkg.WriteInt(info.promoteLevel);
        player.SendTCP(pkg);
        player.UpdateProperties();
        player.SendUpdatePublicPlayer();
    }

    private static void TempleBless(int userId, GamePlayer player)
    {
        if (player != null)
        {
            player.Actives.Info.CurrentSelectIndex = 6;
            player.Actives.Info.CurrentLevel = 60;
            player.Actives.Info.HomeTempleExp = 54940724;
            SendTempleInfo(player);
            return;
        }
        ExecSql($"UPDATE Sys_Active_System_Data SET CurrentSelectIndex=6, CurrentLevel=60, HomeTempleExp=54940724 WHERE UserID={userId}");
    }

    private static void TempleExalt(int userId, GamePlayer player)
    {
        if (player != null)
        {
            player.Actives.Info.promoteLevel = 60;
            SendTempleInfo(player);
            return;
        }
        ExecSql($"UPDATE Sys_Active_System_Data SET promoteLevel=60 WHERE UserID={userId}");
    }

    private static void GoldEquip(int userId, GamePlayer player, int place, int categoryId)
    {
        // Chỉ Nón (category 1) / Quần Áo (5) / Vũ Khí (7) ĐANG TRANG BỊ ở ô tương ứng mới được dát vàng.
        if (player != null)
        {
            var item = player.EquipBag.GetItemAt(place);
            if (item == null || item.Template == null)
                throw new GmActionException("Không có trang bị ở ô cần dát vàng.");
            if (item.Template.CategoryID != categoryId)
                throw new GmActionException("Ô này không phải " + GoldCategoryName(categoryId) + " — không dát vàng.");
            // Dưới +12 thì set lên +12; +12..+15 giữ nguyên strengthen hiện tại.
            if (item.StrengthenLevel < 12) item.StrengthenLevel = 12;
            item.IsGold = true;
            item.goldBeginTime = DateTime.Now;
            item.goldValidDate = 36500; // ~100 năm
            item.IsBinds = true;
            player.EquipBag.UpdateItem(item);
            RefreshProperties(player);
            return;
        }

        // Offline: đọc đúng item đang trang bị ở ô + kiểm tra thể loại.
        DataSet ds;
        try
        {
            ds = new DataBaseHelper("conString").GetDateSet($@"
SELECT g.StrengthenLevel, g.TemplateID, s.CategoryID
FROM Sys_Users_Goods AS g
INNER JOIN Db_Tank_All.dbo.Shop_Goods AS s ON s.TemplateID = g.TemplateID
WHERE g.UserID={userId} AND g.BagType=0 AND g.Place={place}");
        }
        catch (Exception ex)
        {
            throw new GmActionException("Lỗi đọc DB: " + ex.Message);
        }
        if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            throw new GmActionException("Không có trang bị ở ô cần dát vàng.");
        var row = ds.Tables[0].Rows[0];
        var lv = Convert.ToInt32(row[0]);
        var cat = Convert.ToInt32(row[2]);
        if (cat != categoryId)
            throw new GmActionException("Ô này không phải " + GoldCategoryName(categoryId) + " — không dát vàng.");
        var newLv = lv < 12 ? 12 : lv; // +12..+15 giữ nguyên
        ExecSql($@"UPDATE Sys_Users_Goods
SET StrengthenLevel={newLv}, IsGold=1, goldBeginTime=GETDATE(), goldValidDate=36500, IsBinds=1
WHERE UserID={userId} AND BagType=0 AND Place={place}");
    }

    private static string GoldCategoryName(int categoryId) =>
        categoryId == 1 ? "Nón" : categoryId == 5 ? "Quần Áo" : "Vũ Khí";

    private static void StarUpgrade(int userId, GamePlayer player, int spiritType)
    {
        var spirits = SpiritInfoMgr.GetSpirit(spiritType);
        var spirit = spirits != null ? spirits.FirstOrDefault() : null;
        if (spirit == null) throw new GmActionException("Không tìm thấy dữ liệu Tăng Sao.");

        if (player != null)
        {
            var ghost = player.GetGhostEquip(spirit.BagType, spirit.BagPlace);
            if (ghost == null)
            {
                ghost = new UserEquipGhostInfo
                {
                    UserID = userId,
                    BagType = spirit.BagType,
                    Place = spirit.BagPlace,
                    Level = 10,
                    TotalGhost = 0
                };
                player.AddEquipGhost(ghost);
            }
            else
            {
                ghost.Level = 10;
            }
            player.Out.SendUserSyncEquipGhost(player);
            RefreshProperties(player);
            return;
        }

        // Offline: GhostEquipList là JSON trong Sys_Users_Detail (login đọc lại).
        var raw = ScalarSql($"SELECT GhostEquipList FROM Sys_Users_Detail WHERE UserID={userId}");
        var dict = string.IsNullOrWhiteSpace(raw)
            ? new Dictionary<string, UserEquipGhostInfo>()
            : (JsonConvert.DeserializeObject<Dictionary<string, UserEquipGhostInfo>>(raw) ?? new Dictionary<string, UserEquipGhostInfo>());
        var key = spirit.BagType + "_" + spirit.BagPlace;
        if (dict.TryGetValue(key, out var g) && g != null)
        {
            g.Level = 10;
        }
        else
        {
            dict[key] = new UserEquipGhostInfo
            {
                UserID = userId,
                BagType = spirit.BagType,
                Place = spirit.BagPlace,
                Level = 10,
                TotalGhost = 0
            };
        }
        var json = JsonConvert.SerializeObject(dict).Replace("'", "''");
        ExecSql($"UPDATE Sys_Users_Detail SET GhostEquipList='{json}' WHERE UserID={userId}");
    }

    private static void BattleSoul(int userId, GamePlayer player)
    {
        var places = new[] { 11, 5, 2, 3, 13 };
        if (player != null)
        {
            using (var pb = new PlayerBussiness())
            {
                // Reload gemstone từ DB (m_GemStone trong memory có thể chưa có nếu user vừa login
                // hoặc dữ liệu vừa tạo) — tránh báo nhầm "chưa có dữ liệu Chiến Hồn".
                try { player.LoadGemStone(pb); } catch (Exception ex) { log.Warn("[GMApi] LoadGemStone: " + ex.Message); }
                var any = false;
                foreach (var place in places)
                {
                    var gem = player.GetGemStone(place);
                    if (gem == null) continue;
                    any = true;
                    gem.FigSpiritIdValue = "6,0,0|6,0,1|6,0,2";
                    pb.UpdateGemStoneInfo(gem);
                    player.UpdateGemStone(place, gem);
                    player.Out.SendPlayerFigSpiritUp(player.PlayerCharacter.ID, gem, true, true, true, 0, 0);
                }
                if (!any) throw new GmActionException("Người chơi chưa có dữ liệu Chiến Hồn.");
            }
            RefreshProperties(player);
            return;
        }
        ExecSql($"UPDATE Sys_User_Gemstone SET FigSpiritIdValue='6,0,0|6,0,1|6,0,2' WHERE UserID={userId} AND EquipPlace IN (11,5,2,3,13)");
    }

    private static void Craft(int userId, GamePlayer player)
    {
        const int exp = 345860;
        if (player != null)
        {
            player.PlayerCharacter.fineSuitExp = exp;
            player.UpdateProperties();
            player.SendUpdatePublicPlayer();
            return;
        }
        ExecSql($"UPDATE Sys_Users_Detail SET fineSuitExp={exp} WHERE UserID={userId}");
    }

    private static void SubWeaponEvolve(GamePlayer player)
    {
        if (player == null) throw new GmActionException("Cần người chơi online để tiến hoá VK phụ.");
        var second = player.SecondWeapon;
        if (second == null) throw new GmActionException("Người chơi chưa trang bị VK phụ.");
        var max = SubWeaponEvolutionTemplateMgr.GetAllSubWeaponEvolutionTemplate()
            .OrderByDescending(x => x.Exp).FirstOrDefault();
        if (max == null) throw new GmActionException("Không có template tiến hoá VK phụ.");
        second.curExp = max.Exp;
        second.IsDirty = true;
        player.StoreBag.UpdateItem(second);
        player.UpdateProperties();
        player.SendUpdatePublicPlayer();
    }

    private static void NecklaceTrain(int userId, GamePlayer player)
    {
        const int exp = 630000;
        if (player != null)
        {
            player.PlayerCharacter.necklaceExp = exp;
            NecklaceStrengthMgr.UpdateNecklaceInfo(player);
            RefreshProperties(player);
            return;
        }
        ExecSql($"UPDATE Sys_Users_Detail SET necklaceExp={exp} WHERE UserID={userId}");
    }

    private static void NecklaceCast(int userId, GamePlayer player)
    {
        const int level = 40;
        if (player != null)
        {
            player.PlayerCharacter.necklaceCastLevel = level;
            NecklaceStrengthMgr.SendNecklaceCastInfo(player);
            NecklaceStrengthMgr.UpdateNecklaceInfo(player);
            RefreshProperties(player);
            return;
        }
        ExecSql($"UPDATE Sys_Users_Detail SET necklaceCastLevel={level} WHERE UserID={userId}");
    }

    /// <summary>Test/áp dụng Quà Tân Thủ cho 1 user (dùng chung logic handler packet 11).</summary>
    private static void NewbieGift(int userId)
    {
        var nick = GetNickName(userId);
        if (string.IsNullOrWhiteSpace(nick))
            throw new GmActionException("Không tìm thấy nhân vật UserID=" + userId);
        string message;
        var ok = Game.Server.Packets.Client.ActivePullDownHandler.GrantNewbieGiftForUser(userId, nick, ZoneId, out message);
        if (!ok) throw new GmActionException(message);
    }

    // ------------------------------------------------------------------ beads / magicstone

    private static void Beads(int userId, GamePlayer player)
    {
        OpenAllSixDrills(userId, player);

        var nickName = player != null ? player.PlayerCharacter.NickName : GetNickName(userId);
        if (string.IsNullOrWhiteSpace(nickName)) throw new GmActionException("Không tìm thấy nhân vật UserID=" + userId + " để gửi mail.");

        using (var pb = new PlayerBussiness())
        {
            var mainIds = new[] { 313615, 313623, 313622, 313621 };
            foreach (var templateId in mainIds) SendMailItem(pb, nickName, templateId, 15, 0);

            var extraIds = new[] { 311545, 312519, 312521, 311541, 311540 };
            foreach (var templateId in extraIds) SendMailItem(pb, nickName, templateId, 1, 0);
        }

        if (player != null) RefreshProperties(player);
    }

    private static void OpenAllSixDrills(int userId, GamePlayer player)
    {
        using (var pb = new PlayerBussiness())
        {
            var drills = pb.GetPlayerDrillByID(userId) ?? new Dictionary<int, UserDrillInfo>();
            for (var i = 0; i < 6; i++)
            {
                if (drills.TryGetValue(i, out var drill) && drill != null)
                {
                    drill.HoleLv = 6;
                    drill.HoleExp = 0;
                    pb.UpdateUserDrillInfo(drill);
                }
                else
                {
                    drill = new UserDrillInfo
                    {
                        UserID = userId,
                        BeadPlace = 13 + i,
                        DrillPlace = i,
                        HoleLv = 6,
                        HoleExp = 0
                    };
                    pb.AddUserUserDrill(drill);
                    drills[i] = drill;
                }
            }

            if (player != null && player.BeadBag != null)
            {
                foreach (var kv in drills)
                    player.BeadBag.UserDrills[kv.Key] = kv.Value;
                player.BeadBag.SendPlayerDrill();
            }
        }
    }

    private static void MagicStone(int userId, GamePlayer player)
    {
        try { MagicStoneMgr.Init(); } catch { }

        var oldCount = CountMagicStones(userId, player);
        UpgradeAllMagicStones(userId, player);

        var max = GetMaxMagicStone(100906);
        if (oldCount == 0)
        {
            var maxLevel = max != null ? max.Level : 10;

            var nickName = player != null ? player.PlayerCharacter.NickName : GetNickName(userId);
            if (string.IsNullOrWhiteSpace(nickName)) throw new GmActionException("Không tìm thấy nhân vật UserID=" + userId + " để gửi mail.");

            using (var pb = new PlayerBussiness())
            {
                for (var i = 0; i < 8; i++)
                    SendMailItem(pb, nickName, 100906, 1, maxLevel);
            }
        }

        // SP_Admin_SendUserItem chỉ insert 4 compose stat, KHÔNG có MagicAttack/MagicDefence/StrengthenExp
        // → patch lại các row item trong mail để client hiển thị đủ 6 chỉ số khi mở thư.
        PatchMagicStoneMails(userId, 100906);

        if (player != null) RefreshProperties(player);
    }

    private static void PatchMagicStoneMails(int userId, int templateId)
    {
        // SP_Admin_SendUserItem tạo item mail với UserID=0 + Place=-1 + BagType=0 (theo ItemInfo mặc định),
        // và KHÔNG insert MagicAttack/MagicDefence/StrengthenExp → patch theo Annex1 (ItemID) của mail.
        // Bảng chuẩn: Db_Tank_All.dbo.Magicstone_Template (Lv10: Exp 1134400, 6 chỉ số 154).
        // Lưu ý: T-SQL KHÔNG dùng được tiền tố alias trong mệnh đề SET (SET g.col=... trả 0 dòng).
        ExecSql($@"UPDATE g
SET StrengthenLevel = t.Level, StrengthenExp = t.Exp,
    AttackCompose = t.Attack, DefendCompose = t.Defence,
    AgilityCompose = t.Agility, LuckCompose = t.Luck,
    MagicAttack = t.MagicAttack, MagicDefence = t.MagicDefence
FROM Sys_Users_Goods AS g
INNER JOIN User_Messages AS m ON m.Annex1 = CONVERT(nvarchar(20), g.ItemID)
INNER JOIN Db_Tank_All.dbo.Magicstone_Template AS t
    ON t.TemplateID = g.TemplateID
   AND t.Level = (SELECT MAX(t2.Level) FROM Db_Tank_All.dbo.Magicstone_Template AS t2 WHERE t2.TemplateID = g.TemplateID)
WHERE m.ReceiverID={userId} AND g.TemplateID={templateId}");
    }

    private static int CountMagicStones(int userId, GamePlayer player)
    {
        if (player != null && player.MagicStoneBag != null)
            return player.MagicStoneBag.GetItems().Count(x => x != null && x.IsMagicStone());
        using (var pb = new PlayerBussiness())
            return pb.GetUserBagByType(userId, 41).Count(x => x != null && x.IsMagicStone());
    }

    private static MagicStoneInfo GetMaxMagicStone(int templateId)
    {
        var levels = MagicStoneMgr.FindMagicStone(templateId);
        if (levels == null || levels.Count == 0) return null;
        return levels.OrderByDescending(x => x.Level).FirstOrDefault();
    }

    private static void ApplyMaxMagicStoneStats(ItemInfo item, MagicStoneInfo max)
    {
        item.StrengthenLevel = max.Level;
        item.StrengthenExp = max.Exp;
        item.AttackCompose = max.Attack;
        item.DefendCompose = max.Defence;
        item.AgilityCompose = max.Agility;
        item.LuckCompose = max.Luck;
        item.MagicAttack = max.MagicAttack;
        item.MagicDefence = max.MagicDefence;
    }

    private static void UpgradeAllMagicStones(int userId, GamePlayer player)
    {
        if (player != null && player.MagicStoneBag != null)
        {
            foreach (var item in player.MagicStoneBag.GetItems())
            {
                if (item == null || !item.IsMagicStone()) continue;
                var max = GetMaxMagicStone(item.TemplateID);
                if (max == null) continue;
                ApplyMaxMagicStoneStats(item, max);
                MagicStoneMgr.SetupMagicStoneWithLevel(item);
                player.MagicStoneBag.UpdateItem(item);
            }
            return;
        }

        using (var pb = new PlayerBussiness())
        {
            foreach (var item in pb.GetUserBagByType(userId, 41))
            {
                if (item == null || !item.IsMagicStone()) continue;
                var max = GetMaxMagicStone(item.TemplateID);
                if (max == null) continue;
                ApplyMaxMagicStoneStats(item, max);
                MagicStoneMgr.SetupMagicStoneWithLevel(item);
                pb.UpdateGoods(item);
            }
        }
    }
}

public sealed class GmActionException : Exception
{
    public GmActionException(string message) : base(message) { }
}

public sealed class GmActionResult
{
    public bool Ok { get; set; }
    public bool Online { get; set; }
    public int UserId { get; set; }
    public string Feature { get; set; } = "";
    public string Message { get; set; } = "";
    public GmPlayerSnapshot Snapshot { get; set; }
}

public sealed class GmPlayerSnapshot
{
    public int UserId { get; set; }
    public string NickName { get; set; } = "";
    public int State { get; set; }
    public int Grade { get; set; }
    public long GP { get; set; }
    public int Money { get; set; }
    public int Gold { get; set; }
    public int DdtKingGrade { get; set; }
    public int GuardCoreGrade { get; set; }
    public int HonorLevel { get; set; }
    public int SwornId { get; set; }
    public int ExplorerManualLevel { get; set; }
    public int Attack { get; set; }
    public int Defence { get; set; }
    public int Agility { get; set; }
    public int Luck { get; set; }
    public int Hp { get; set; }
    public int MagicAttack { get; set; }
    public int MagicDefence { get; set; }
    public long FightPower { get; set; }
    /// <summary>Lực chiến hiển thị trong game (FightPower_PacketInt + FightPower_Show).</summary>
    public long FightPowerDisplay { get; set; }
    public long FightPowerShow { get; set; }
    /// <summary>Tổng Sát thương từ PlayerProp.Current["Damage"] (client hiển thị).</summary>
    public int Damage { get; set; }
    /// <summary>Tổng Hộ giáp từ PlayerProp.Current["Armor"] (client hiển thị).</summary>
    public int Armor { get; set; }
    public string ArmorBreakdown { get; set; } = "";
    public string DamageBreakdown { get; set; } = "";
    public double BaseAttack { get; set; }
    public double BaseDefence { get; set; }
    public int GuardPlus { get; set; }
    public int GemSkillId { get; set; }
    public int GemSkillCount { get; set; }
    public int RankCount { get; set; }
    public int AvatarCount { get; set; }
    public int CardCount { get; set; }
    public int EquipCount { get; set; }
    public int MagicStoneCount { get; set; }
    /// <summary>HoleLv của 6 drill, ví dụ "6,6,6,6,6,6".</summary>
    public string DrillHoles { get; set; } = "";
}
