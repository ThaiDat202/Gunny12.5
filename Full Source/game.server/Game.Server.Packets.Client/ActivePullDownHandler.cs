using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using Bussiness;
using Bussiness.Managers;
using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;

namespace Game.Server.Packets.Client
{
    /// <summary>
    /// Nhận thưởng Hoạt động hấp dẫn (packet 11).
    /// Riêng "Quà Tân Thủ" (ActiveID 2026110701) xử lý trực tiếp: mỗi nhân vật chỉ nhận 1 lần,
    /// phần thưởng gửi vào hộp thư (5 item/mail).
    /// </summary>
    [PacketHandler(11, "领取活动奖励")]
    public class ActivePullDownHandler : IPacketHandler
    {
        private const int NewbieGiftActiveId = 2026110701;
        private const string GiftTitle = "Quà Tân Thủ";

        public int HandlePacket(GameClient client, GSPacketIn packet)
        {
            int activeID = packet.ReadInt();
            string code = packet.ReadString();

            if (client == null || client.Player == null || client.Player.PlayerCharacter == null)
                return 0;
            if (activeID != NewbieGiftActiveId)
                return 0;

            var player = client.Player;
            string message;
            GrantNewbieGiftForUser(player.PlayerCharacter.ID, player.PlayerCharacter.NickName,
                player.ZoneId, out message);
            player.SendMessage(message);
            return 0;
        }

        /// <summary>
        /// Cấp Quà Tân Thủ cho 1 user (dùng chung cho packet 11 và test nội bộ).
        /// Trả false nếu đã nhận (message = Msg6) hoặc lỗi.
        /// </summary>
        public static bool GrantNewbieGiftForUser(int userId, string nickName, int zoneId, out string message)
        {
            if (HasClaimed(userId) || !TryMarkClaimed(userId))
            {
                message = LanguageMgr.GetTranslation("ActiveBussiness.Msg6");
                return false;
            }

            try
            {
                var items = BuildGiftItems();
                WorldEventMgr.SendItemsToMails(items, userId, nickName, zoneId, GiftTitle);
                try
                {
                    using (var center = new Bussiness.CenterService.CenterServiceClient())
                        center.MailNotice(userId, zoneId);
                }
                catch { }
                message = LanguageMgr.GetTranslation("ActiveBussiness.Msg0");
                return true;
            }
            catch (Exception ex)
            {
                try { log.Error("ActivePullDownHandler.NewbieGift", ex); } catch { }
                message = "Có lỗi khi gửi quà tân thủ, vui lòng báo GM.";
                return false;
            }
        }

        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static string ConnString
        {
            get { return ConfigurationManager.AppSettings["conString"]; }
        }

        private static bool HasClaimed(int userId)
        {
            using (var conn = new SqlConnection(ConnString))
            using (var cmd = new SqlCommand(@"
SELECT COUNT(*) FROM dbo.Sys_Users_NewbieGift WHERE UserID=@uid;
", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                conn.Open();
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return true;
            }
            using (var conn = new SqlConnection(ConnString))
            using (var cmd = new SqlCommand(@"
SELECT COUNT(*) FROM dbo.Active_Number
WHERE ActiveID=@aid AND AwardID=CAST(@aid AS nvarchar(20)) + '-3-' + CAST(@uid AS nvarchar(20));
", conn))
            {
                cmd.Parameters.AddWithValue("@aid", NewbieGiftActiveId);
                cmd.Parameters.AddWithValue("@uid", userId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>Trả true nếu giành được quyền nhận (ghi được cả 2 bảng), false nếu đã nhận.</summary>
        private static bool TryMarkClaimed(int userId)
        {
            using (var conn = new SqlConnection(ConnString))
            using (var cmd = new SqlCommand(@"
SET XACT_ABORT ON;
BEGIN TRAN;
IF EXISTS (SELECT 1 FROM dbo.Sys_Users_NewbieGift WHERE UserID=@uid)
   OR EXISTS (SELECT 1 FROM dbo.Active_Number WHERE ActiveID=@aid AND AwardID=CAST(@aid AS nvarchar(20)) + '-3-' + CAST(@uid AS nvarchar(20)))
BEGIN
    COMMIT;
    SELECT 0;
END
ELSE
BEGIN
    INSERT INTO dbo.Sys_Users_NewbieGift (UserID, ClaimTime) VALUES (@uid, GETDATE());
    INSERT INTO dbo.Active_Number (AwardID, ActiveID, PullDown, GetDate, UserID, Mark)
    VALUES (CAST(@aid AS nvarchar(20)) + '-3-' + CAST(@uid AS nvarchar(20)), @aid, 1, GETDATE(), @uid, 0);
    COMMIT;
    SELECT 1;
END", conn))
            {
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@aid", NewbieGiftActiveId);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        private static ItemInfo MakeItem(int templateId, int count, int strengthen,
            int attack, int defence, int agility, int luck, bool isGold, int goldDays)
        {
            var template = ItemMgr.FindItemTemplate(templateId);
            if (template == null) return null;
            var item = new ItemInfo(template)
            {
                Count = count,
                StrengthenLevel = strengthen,
                AttackCompose = attack,
                DefendCompose = defence,
                AgilityCompose = agility,
                LuckCompose = luck,
                IsGold = isGold,
                goldBeginTime = DateTime.Now,
                goldValidDate = goldDays,
                IsBinds = true,
                IsExist = true,
                BeginDate = DateTime.Now,
                ValidDate = 0,
                BagType = 0,
                Place = -1,
                // Các cột datetime/chuỗi NOT NULL của SP_Users_Items_Add không nhận MinValue/null.
                latentEnergyEndTime = DateTime.Now.AddYears(50),
                AdvanceDate = DateTime.Now,
                RemoveDate = DateTime.Now,
                Color = "",
                Skin = "",
                latentEnergyCurStr = "",
                latentEnergyNewStr = ""
            };
            return item;
        }

        private static List<ItemInfo> BuildGiftItems()
        {
            var list = new List<ItemInfo>();
            using (var conn = new SqlConnection(ConnString))
            using (var cmd = new SqlCommand(
                "SELECT TemplateID,[Count],StrengthenLevel,AttackCompose,DefendCompose,AgilityCompose,LuckCompose,IsGold,GoldValidDate FROM dbo.Sys_NewbieGiftItems ORDER BY SortOrder,TemplateID", conn))
            {
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var item = MakeItem(rd.GetInt32(0), rd.GetInt32(1), rd.GetInt32(2),
                            rd.GetInt32(3), rd.GetInt32(4), rd.GetInt32(5), rd.GetInt32(6),
                            rd.GetBoolean(7), rd.GetInt32(8));
                        if (item != null) list.Add(item);
                    }
                }
            }
            return list;
        }
    }
}
