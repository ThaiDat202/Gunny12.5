using System.Collections.Generic;
using Bussiness;
using Game.Base.Packets;
using Game.Logic;
using Game.Server.GameObjects;
using Game.Server.Packets;

namespace Game.Server.BattleSkill
{
    /// <summary>Gui/nhan goi tin 132 cho Chien Ky (Battle Skill).</summary>
    public static class BattleSkillOut
    {
        public const int NeedItemTemplate = 12548; // Manh Chien Ky

        public static void SendInfo(GamePlayer player)
        {
            if (player == null || player.Out == null) return;
            Dictionary<int, int> skills = player.BattleSkills;
            GSPacketIn pkg = new GSPacketIn(132, player.PlayerCharacter.ID);
            pkg.WriteByte(8);
            pkg.WriteInt(skills.Count);
            foreach (KeyValuePair<int, int> kv in skills)
            {
                pkg.WriteInt(kv.Key);
                pkg.WriteInt(kv.Value);
            }
            player.Out.SendTCP(pkg);
        }

        public static void SendUpdate(GamePlayer player, int newSkillId)
        {
            GSPacketIn pkg = new GSPacketIn(132, player.PlayerCharacter.ID);
            pkg.WriteByte(6);
            pkg.WriteInt(newSkillId);
            player.Out.SendTCP(pkg);
        }

        public static void SendBring(GamePlayer player, int skillId, int slot)
        {
            GSPacketIn pkg = new GSPacketIn(132, player.PlayerCharacter.ID);
            pkg.WriteByte(7);
            pkg.WriteInt(skillId);
            pkg.WriteInt(slot);
            player.Out.SendTCP(pkg);
        }

        /// <summary>Mo khoa (activate) hoac nang cap (upgrade) 1 ky nang, tieu hao nguyen lieu theo template.</summary>
        public static bool UpgradeOrActivate(GamePlayer player, int skillId)
        {
            BattleSkillConfigMgr.EnsureLoaded();
            BattleSkillNode node = BattleSkillConfigMgr.GetNode(skillId);
            if (node == null)
            {
                player.SendMessage(eMessageType.Normal, "Dữ liệu chiến kỹ không tồn tại.");
                return false;
            }
            if (player.CurrentRoom != null && player.CurrentRoom.RoomType == eRoomType.SpecialDungeon3
                && !BattleSkillConfigMgr.IsAllowedInEventRoom(skillId))
            {
                player.SendMessage(eMessageType.Normal, "Chiến kỹ này không dùng được trong phó bản sự kiện.");
                return false;
            }

            bool activated = player.BattleSkills.ContainsKey(skillId);
            BattleSkillNode target = activated ? BattleSkillConfigMgr.GetNextNode(skillId) : node;
            if (target == null)
            {
                player.SendMessage(eMessageType.Normal, "Chiến kỹ đã đạt cấp tối đa.");
                return false;
            }

            List<BattleSkillMaterial> materials = BattleSkillConfigMgr.GetMaterials(skillId);
            foreach (BattleSkillMaterial m in materials)
            {
                if (m.TemplateID > 0 && m.Count > 0 && player.GetItemCount(m.TemplateID) < m.Count)
                {
                    player.SendMessage(eMessageType.Normal, "Không đủ Mảnh Chiến Kỹ để " + (activated ? "nâng cấp" : "mở khóa") + ".");
                    return false;
                }
            }
            foreach (BattleSkillMaterial m in materials)
            {
                if (m.TemplateID > 0 && m.Count > 0 && !player.RemoveTemplate(m.TemplateID, m.Count))
                {
                    player.SendMessage(eMessageType.Normal, "Không đủ Mảnh Chiến Kỹ.");
                    return false;
                }
            }

            BattleSkillBussiness bs = new BattleSkillBussiness();
            if (activated)
            {
                bs.UpgradeSkill(player.PlayerCharacter.ID, skillId, target.SkillID);
            }
            else
            {
                bs.SaveSkill(player.PlayerCharacter.ID, target.SkillID, 0);
            }
            player.SaveBattleSkillChange(skillId, target.SkillID, activated ? player.BattleSkills[skillId] : 0, activated);
            player.SendMessage(eMessageType.Normal, activated ? "Đã tăng cấp chiến kỹ!" : "Đã mở khóa chiến kỹ!");
            SendUpdate(player, target.SkillID);
            return true;
        }

        /// <summary>Trang bi / bo trang bi chien ky vao slot 1..6 (0 = bo).</summary>
        public static bool Bring(GamePlayer player, int skillId, int slot)
        {
            BattleSkillConfigMgr.EnsureLoaded();
            if (!player.BattleSkills.ContainsKey(skillId))
            {
                player.SendMessage(eMessageType.Normal, "Chiến kỹ chưa được mở khóa.");
                return false;
            }
            if (slot < 0 || slot > 6) slot = 0;
            bool active = BattleSkillConfigMgr.IsActiveSkill(skillId);
            if (slot > 0)
            {
                if (active && (slot < 1 || slot > 3)) { player.SendMessage(eMessageType.Normal, "Chiến kỹ chủ động chỉ đặt vào ô 1-3."); return false; }
                if (!active && (slot < 4 || slot > 6)) { player.SendMessage(eMessageType.Normal, "Chiến kỹ bị động chỉ đặt vào ô 4-6."); return false; }
            }
            BattleSkillBussiness bs = new BattleSkillBussiness();
            if (slot > 0)
            {
                foreach (KeyValuePair<int, int> kv in player.BattleSkills)
                {
                    if (kv.Value == slot && kv.Key != skillId)
                    {
                        bs.SaveSkill(player.PlayerCharacter.ID, kv.Key, 0);
                    }
                }
            }
            bs.SaveSkill(player.PlayerCharacter.ID, skillId, slot);
            player.SetBattleSkillSlot(skillId, slot);
            SendBring(player, skillId, slot);
            return true;
        }
    }
}
