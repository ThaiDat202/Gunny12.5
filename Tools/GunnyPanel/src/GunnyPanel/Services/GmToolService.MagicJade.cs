using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GunnySuite.Core;

namespace GunnyPanel.Services;

// Feature "Ma Ngọc" (category 90, item 110901..110906 — UI Ma Ngọc của client mới).
// Owner: magicjade-ui-dev (task-7). Chỉ sửa file này; dispatcher/feature list đã có trong GmToolService.cs.
//
// Cơ chế đúng theo client decompiled (ddt.manager.PlayerManager::__updateInventorySlot,
// magicStone.views.MagicJadeView, ddt.view.tips.GoodTip):
//  - UI Ma Ngọc đọc 9 ô Place 21..29 của túi Ma Thạch (BagType 41), KHÔNG phải Place 32+.
//  - Tooltip đọc `magicJadeStr` = danh sách property type id (server patch AbstractPacketLib ghi
//    "1,2,3,4" cho CategoryID 90) để tra Magicstone_Template; item.Hole1 = nhóm kỹ năng 1..6
//    (tank.magicStone.txt4 = Nước,Lửa,Gió,Đất,Sáng,Tối). Hole1 <= 0 => client hiện "(undefined)".
//  - Vì vậy feature này ghi TRỰC TIẾP 9 row vào Sys_Users_Goods: BagType=41, Place=21..29,
//    Hole1=1, đủ 6 chỉ số max từ Magicstone_Template (Lv10: 92/92/92/92/92/92).
public sealed partial class GmToolService
{
    // Client mới: túi Ma Thạch có 9 ô Ma Ngọc (place 21..29) => cấp đủ 9 viên là full bộ.
    private const int MagicJadeSlotCount = 9;

    // Ma Ngọc Tứ Tượng = bản 6 chỉ số của Ma Thạch Tứ Tượng (110906), CategoryID 90.
    private const int MagicJadeTemplateId = 110906;
    private const int MagicJadeBagType = 41;
    private const int MagicJadeFirstPlace = 21;
    private const int MagicJadeHole1 = 1; // nhóm kỹ năng 1..6; 1 = "Nước" theo language client
    private const string MagicJadeOldMailTitle = "Ma Ngọc GM";

    // Quy ước server (game.server Game.Base.Packets.AbstractPacketLib ghi magicJadeStr cho CategoryID 90):
    // Hole2..Hole5 = 4 property type id (1=Attack,2=Defence,3=Agility,4=Luck,5=MagicAttack,
    // 6=MagicDefence, 7..14 = nguyên tố). Client mới đọc magicJadeStr từ 4 cột này; item tạo bằng
    // panel phải set đúng để tooltip hiện 4 dòng chỉ số (không thì fallback "1,2,3,4").
    private const int MagicJadeHole2 = 1;
    private const int MagicJadeHole3 = 2;
    private const int MagicJadeHole4 = 3;
    private const int MagicJadeHole5 = 4;

    // Nguyên liệu "Đá Ma Ngọc" (item 12268) cho Transform (258/10) + Reset Pro/Xilian (258/18).
    // Transform tiêu thụ Template.Property7 của Ma Thạch (100906 => 6400), Xilian tiêu thụ
    // Template.Property7 của Ma Ngọc (110906 => 6400) — nếu không cấp thì người chơi không dùng được.
    // MaxCount của 12268 trong Shop_Goods = 999999 nên cấp 1 stack MaxCount là hợp lệ.
    private const int MagicJadeMaterialTemplateId = 12268;
    private const int MagicJadeMaterialGrantCount = 999999;

    /// <summary>
    /// Mở full Ma Ngọc: đặt trực tiếp 9 viên Ma Ngọc Tứ Tượng (110906) vào túi Ma Thạch
    /// của user tại Place 21..29 (đúng 9 ô UI Ma Ngọc client mới), Lv10 + 6 chỉ số max,
    /// Hole1=1 để client hiện kỹ năng nhóm 1 (hết "(undefined)").
    /// Idempotent: mỗi lần bấm xoá/tái tạo đúng 9 row Ma Ngọc + dọn mail "Ma Ngọc GM" của
    /// implementation cũ nên không bao giờ trùng item/thư rác.
    /// </summary>
    private async Task<(bool, string)> MagicJadeAsync(int userId)
    {
        // 0) Đọc level max/exp + 6 chỉ số max của Ma Ngọc từ dữ liệu NPH (không hardcode 92/1134400).
        var tplResult = await _db.QueryAsync("Db_Tank", @"
SELECT TOP (1)
       t.Level, t.Exp, t.Attack, t.Defence, t.Agility, t.Luck, t.MagicAttack, t.MagicDefence
FROM Db_Tank_All.dbo.Magicstone_Template AS t
WHERE t.TemplateID = @tpl
ORDER BY t.Level DESC",
            new Dictionary<string, object?> { ["tpl"] = MagicJadeTemplateId }, 1, 30);

        if (!tplResult.Ok)
            return (false, "Lỗi đọc bảng chuẩn Ma Ngọc (Db_Tank_All.dbo.Magicstone_Template): " + tplResult.Error);

        if (tplResult.Rows.Count == 0)
            return (false, "Không có dữ liệu Ma Ngọc " + MagicJadeTemplateId + " trong Db_Tank_All.dbo.Magicstone_Template.");

        var tpl = tplResult.Rows[0];
        int Cell(int index) => index < tpl.Length && int.TryParse(tpl[index], out var value) ? value : 0;

        var jadeLevel = Cell(0);
        var jadeExp = Cell(1);
        var jadeAttack = Cell(2);
        var jadeDefence = Cell(3);
        var jadeAgility = Cell(4);
        var jadeLuck = Cell(5);
        var jadeMagicAttack = Cell(6);
        var jadeMagicDefence = Cell(7);

        if (jadeLevel <= 0)
            return (false, "Dữ liệu Ma Ngọc " + MagicJadeTemplateId + " trong Magicstone_Template không hợp lệ (Level = 0).");

        // 1) Kiểm tra user tồn tại + item 110906 có trong Shop_Goods (CategoryID = 90)
        //    + nguyên liệu 12268 "Đá Ma Ngọc" có trong Shop_Goods (CategoryID = 11).
        var who = await _db.QueryAsync("Db_Tank", @"
SELECT (SELECT COUNT(*) FROM dbo.Sys_Users_Detail WHERE UserID = @uid) AS UserExists,
       (SELECT COUNT(*) FROM Db_Tank_All.dbo.Shop_Goods WHERE TemplateID = @tpl AND CategoryID = 90) AS TemplateExists,
       (SELECT COUNT(*) FROM Db_Tank_All.dbo.Shop_Goods WHERE TemplateID = @materialTpl AND CategoryID = 11) AS MaterialExists",
            new Dictionary<string, object?> { ["uid"] = userId, ["tpl"] = MagicJadeTemplateId, ["materialTpl"] = MagicJadeMaterialTemplateId }, 1, 30);

        if (!who.Ok) return (false, "Lỗi kiểm tra dữ liệu user: " + who.Error);
        if (who.Rows.Count == 0) return (false, "Không đọc được dữ liệu user.");

        var userExists = int.TryParse(who.Rows[0].Length > 0 ? who.Rows[0][0] : null, out var ue) && ue > 0;
        var templateExists = int.TryParse(who.Rows[0].Length > 1 ? who.Rows[0][1] : null, out var te) && te > 0;
        var materialExists = int.TryParse(who.Rows[0].Length > 2 ? who.Rows[0][2] : null, out var me) && me > 0;
        if (!userExists) return (false, "Không tìm thấy nhân vật UserID=" + userId + ".");
        if (!templateExists) return (false, "Không có Ma Ngọc " + MagicJadeTemplateId + " (CategoryID=90) trong Db_Tank_All.dbo.Shop_Goods.");
        if (!materialExists) return (false, "Không có Đá Ma Ngọc " + MagicJadeMaterialTemplateId + " (CategoryID=11) trong Db_Tank_All.dbo.Shop_Goods.");

        // 2) Xử lý "dọn + đặt 9 viên" trong 1 transaction duy nhất (idempotent, chạy lại bao nhiêu lần cũng đúng 9):
        //    a) Dọn mail "Ma Ngọc GM" do implementation cũ tạo (kèm item đính kèm còn sót, nếu có).
        //    b) Xoá mọi item Ma Ngọc (CategoryID 90) user đang có trong túi Ma Thạch (kể cả 9 viên cũ ở 34..42).
        //    c) Đặt đúng 9 row mới tại Place 21..29 bằng SP chuẩn của server (SP_Users_Items_Add).
        var (_, sqlError) = await _db.ExecuteAsync("Db_Tank", @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
BEGIN TRY
    -- a) Mail Ma Ngọc GM cũ + item đính kèm còn sót trong User_Messages.Annex1
    DELETE g
    FROM dbo.Sys_Users_Goods AS g
    INNER JOIN dbo.User_Messages AS m
        ON m.Annex1 = CONVERT(nvarchar(20), g.ItemID)
    WHERE m.ReceiverID = @uid AND m.Title = @oldTitle;

    DELETE FROM dbo.User_Messages WHERE ReceiverID = @uid AND Title = @oldTitle;

    -- b) Xoá mọi Ma Ngọc (CategoryID 90) user đang giữ trong túi Ma Thạch
    DELETE g
    FROM dbo.Sys_Users_Goods AS g
    INNER JOIN Db_Tank_All.dbo.Shop_Goods AS s ON s.TemplateID = g.TemplateID
    WHERE g.UserID = @uid AND g.BagType = @bag AND s.CategoryID = 90;

    -- c) Đặt đúng 9 viên vào Place 21..29 (thứ tự ổn định theo ô, đúng view MagicJadeView)
    DECLARE @place int = @firstPlace;
    DECLARE @itemId int;
    DECLARE @now datetime = GETDATE();
    WHILE @place < @firstPlace + @slots
    BEGIN
        SET @itemId = 0;
        EXEC dbo.SP_Users_Items_Add
            @ItemID = @itemId OUTPUT,
            @UserID = @uid,
            @TemplateID = @tpl,
            @Place = @place,
            @Count = 1,
            @IsJudge = 0,
            @Color = N'',
            @IsExist = 1,
            @StrengthenLevel = @level,
            @AttackCompose = @attack,
            @DefendCompose = @defence,
            @LuckCompose = @luck,
            @AgilityCompose = @agility,
            @IsBinds = 1,
            @BeginDate = @now,
            @ValidDate = 0,
            @BagType = @bag,
            @Skin = N'',
            @IsUsed = 0,
            @RemoveType = 0,
            @Hole1 = @hole1,
            @Hole2 = @hole2,
            @Hole3 = @hole3,
            @Hole4 = @hole4,
            @Hole5 = @hole5,
            @Hole6 = 0,
            @StrengthenTimes = 0,
            @Hole5Level = 0,
            @Hole5Exp = 0,
            @Hole6Level = 0,
            @Hole6Exp = 0,
            @IsGold = 0,
            @goldValidDate = 0,
            @StrengthenExp = @exp,
            @beadExp = 0,
            @beadLevel = 0,
            @beadIsLock = 0,
            @isShowBind = 0,
            @latentEnergyCurStr = N'',
            @latentEnergyNewStr = N'',
            @Damage = 0,
            @Guard = 0,
            @Blood = 0,
            @Bless = 0,
            @goldBeginTime = @now,
            @latentEnergyEndTime = @now,
            @AdvanceDate = @now,
            @AvatarActivity = 0,
            @goodsLock = 0,
            @MagicAttack = @magicAttack,
            @MagicDefence = @magicDefence,
            @MagicExp = 0,
            @MagicLevel = 1,
            @curExp = 0,
            @cellLocked = 0,
            @canTransfer = 1,
            @canRenew = 0,
            @enchantSpiritLevel = 0;
        SET @place = @place + 1;
    END

    -- d) Cấp nguyên liệu Đá Ma Ngọc (item 12268) vào PropBag (BagType=1) để dùng Transform + Reset Pro.
    --    Idempotent: dọn các stack 12268 cũ rồi thêm đúng 1 stack MaxCount (= 999999, MaxCount trong Shop_Goods).
    DELETE g
    FROM dbo.Sys_Users_Goods AS g
    WHERE g.UserID = @uid AND g.BagType = 1 AND g.TemplateID = @materialTpl;

    -- Chọn ô trống đầu tiên của PropBag (0..99) để không đè item khác.
    DECLARE @matPlace int;
    SELECT @matPlace = MIN(v.number)
    FROM master.dbo.spt_values AS v
    WHERE v.type = 'P' AND v.number >= 0 AND v.number < 100
      AND NOT EXISTS (SELECT 1 FROM dbo.Sys_Users_Goods AS g
                      WHERE g.UserID = @uid AND g.BagType = 1 AND g.Place = v.number AND g.IsExist = 1);
    IF @matPlace IS NULL SET @matPlace = 0;

    SET @itemId = 0;
    EXEC dbo.SP_Users_Items_Add
        @ItemID = @itemId OUTPUT,
        @UserID = @uid,
        @TemplateID = @materialTpl,
        @Place = @matPlace,
        @Count = @materialCount,
        @IsJudge = 0,
        @Color = N'',
        @IsExist = 1,
        @StrengthenLevel = 0,
        @AttackCompose = 0,
        @DefendCompose = 0,
        @LuckCompose = 0,
        @AgilityCompose = 0,
        @IsBinds = 1,
        @BeginDate = @now,
        @ValidDate = 0,
        @BagType = 1,
        @Skin = N'',
        @IsUsed = 0,
        @RemoveType = 0,
        @Hole1 = 0,
        @Hole2 = 0,
        @Hole3 = 0,
        @Hole4 = 0,
        @Hole5 = 0,
        @Hole6 = 0,
        @StrengthenTimes = 0,
        @Hole5Level = 0,
        @Hole5Exp = 0,
        @Hole6Level = 0,
        @Hole6Exp = 0,
        @IsGold = 0,
        @goldValidDate = 0,
        @StrengthenExp = 0,
        @beadExp = 0,
        @beadLevel = 0,
        @beadIsLock = 0,
        @isShowBind = 0,
        @latentEnergyCurStr = N'',
        @latentEnergyNewStr = N'',
        @Damage = 0,
        @Guard = 0,
        @Blood = 0,
        @Bless = 0,
        @goldBeginTime = @now,
        @latentEnergyEndTime = @now,
        @AdvanceDate = @now,
        @AvatarActivity = 0,
        @goodsLock = 0,
        @MagicAttack = 0,
        @MagicDefence = 0,
        @MagicExp = 0,
        @MagicLevel = 0,
        @curExp = 0,
        @cellLocked = 0,
        @canTransfer = 1,
        @canRenew = 0,
        @enchantSpiritLevel = 0;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;",
            new Dictionary<string, object?>
            {
                ["uid"] = userId,
                ["tpl"] = MagicJadeTemplateId,
                ["bag"] = MagicJadeBagType,
                ["firstPlace"] = MagicJadeFirstPlace,
                ["slots"] = MagicJadeSlotCount,
                ["hole1"] = MagicJadeHole1,
                ["hole2"] = MagicJadeHole2,
                ["hole3"] = MagicJadeHole3,
                ["hole4"] = MagicJadeHole4,
                ["hole5"] = MagicJadeHole5,
                ["materialTpl"] = MagicJadeMaterialTemplateId,
                ["materialCount"] = MagicJadeMaterialGrantCount,
                ["oldTitle"] = MagicJadeOldMailTitle,
                ["level"] = jadeLevel,
                ["exp"] = jadeExp,
                ["attack"] = jadeAttack,
                ["defence"] = jadeDefence,
                ["agility"] = jadeAgility,
                ["luck"] = jadeLuck,
                ["magicAttack"] = jadeMagicAttack,
                ["magicDefence"] = jadeMagicDefence
            }, 60);
        if (sqlError != null) return (false, "Lỗi đặt Ma Ngọc vào túi: " + sqlError);

        // 3) Đọc lại thực tế để báo cáo/verify (9 row đúng BagType 41, Place 21..29, Hole1..5 đúng quy ước).
        var verifySql = @"
SELECT g.Place, g.TemplateID, g.StrengthenLevel, g.StrengthenExp,
       g.AttackCompose, g.DefendCompose, g.AgilityCompose, g.LuckCompose,
       g.MagicAttack, g.MagicDefence, g.Hole1, g.Hole2, g.Hole3, g.Hole4, g.Hole5,
       CAST(g.IsBinds AS int) AS IsBinds, CAST(g.IsExist AS int) AS IsExist
FROM dbo.Sys_Users_Goods AS g
INNER JOIN Db_Tank_All.dbo.Shop_Goods AS s ON s.TemplateID = g.TemplateID
WHERE g.UserID = @uid AND g.BagType = @bag AND s.CategoryID = 90
ORDER BY g.Place";

        var verify = await _db.QueryAsync("Db_Tank", verifySql,
            new Dictionary<string, object?> { ["uid"] = userId, ["bag"] = MagicJadeBagType }, MagicJadeSlotCount + 5, 30);
        if (!verify.Ok) return (false, "Lỗi đọc lại Ma Ngọc sau khi đặt: " + verify.Error);

        var okSlots = 0;
        var badSlots = new List<string>();
        foreach (var row in verify.Rows)
        {
            int Col(int i) => i < row.Length && int.TryParse(row[i], out var v) ? v : -1;
            var place = Col(0);
            var templateOk = Col(1) == MagicJadeTemplateId;
            var levelOk = Col(2) == jadeLevel;
            var statsOk = Col(3) == jadeExp
                          && Col(4) == jadeAttack && Col(5) == jadeDefence
                          && Col(6) == jadeAgility && Col(7) == jadeLuck
                          && Col(8) == jadeMagicAttack && Col(9) == jadeMagicDefence;
            var holeOk = Col(10) == MagicJadeHole1
                         && Col(11) == MagicJadeHole2 && Col(12) == MagicJadeHole3
                         && Col(13) == MagicJadeHole4 && Col(14) == MagicJadeHole5;
            var flagsOk = Col(15) == 1 && Col(16) == 1;
            if (place >= MagicJadeFirstPlace && place < MagicJadeFirstPlace + MagicJadeSlotCount
                && templateOk && levelOk && statsOk && holeOk && flagsOk)
            {
                okSlots++;
            }
            else
            {
                badSlots.Add("ô " + place);
            }
        }

        if (okSlots != MagicJadeSlotCount || verify.Rows.Count != MagicJadeSlotCount)
        {
            return (false,
                $"Đã ghi dữ liệu nhưng verify chưa đủ: {okSlots}/{MagicJadeSlotCount} viên đúng chuẩn" +
                (badSlots.Count > 0 ? " (sai: " + string.Join(", ", badSlots) + ")" : "") +
                ". Kiểm tra lại Sys_Users_Goods của user " + userId + ".");
        }

        // 3b) Verify nguyên liệu "Đá Ma Ngọc" 12268 đã nằm trong PropBag (BagType=1) đúng số lượng.
        var matVerify = await _db.QueryAsync("Db_Tank", @"
SELECT SUM(g.[Count]) AS TotalCount, COUNT(*) AS [RowCount]
FROM dbo.Sys_Users_Goods AS g
WHERE g.UserID = @uid AND g.BagType = 1 AND g.TemplateID = @materialTpl",
            new Dictionary<string, object?> { ["uid"] = userId, ["materialTpl"] = MagicJadeMaterialTemplateId }, 1, 30);
        if (!matVerify.Ok) return (false, "Lỗi đọc lại Đá Ma Ngọc sau khi cấp: " + matVerify.Error);
        var materialTotal = 0;
        if (matVerify.Rows.Count > 0 && matVerify.Rows[0].Length > 0 && int.TryParse(matVerify.Rows[0][0], out var mt))
            materialTotal = mt;
        if (materialTotal < MagicJadeMaterialGrantCount)
        {
            return (false,
                $"Đã đặt Ma Ngọc nhưng nguyên liệu Đá Ma Ngọc ({MagicJadeMaterialTemplateId}) chưa đúng: " +
                $"{materialTotal}/{MagicJadeMaterialGrantCount}. Kiểm tra lại PropBag của user " + userId + ".");
        }

        var statText = $"{jadeAttack}/{jadeDefence}/{jadeAgility}/{jadeLuck}/{jadeMagicAttack}/{jadeMagicDefence}";
        return (true,
            $"Đã đặt {MagicJadeSlotCount}/{MagicJadeSlotCount} Ma Ngọc Tứ Tượng (110906) vào túi Ma Thạch — ô {MagicJadeFirstPlace}..{MagicJadeFirstPlace + MagicJadeSlotCount - 1}, " +
            $"cấp {jadeLevel} (exp {jadeExp}), 6 chỉ số {statText}, Hole1={MagicJadeHole1}, Hole2..5={MagicJadeHole2},{MagicJadeHole3},{MagicJadeHole4},{MagicJadeHole5} (4 property type). " +
            $"Đã cấp {MagicJadeMaterialGrantCount} Đá Ma Ngọc ({MagicJadeMaterialTemplateId}) vào PropBag để dùng Transform/Reset Pro. " +
            $"Đã dọn mail \"{MagicJadeOldMailTitle}\", 9 viên cũ và stack Đá Ma Ngọc cũ (nếu có) — bấm lại không tạo trùng. " +
            "Thoát ra vào lại game để client nhận dữ liệu item mới.");
    }
}
