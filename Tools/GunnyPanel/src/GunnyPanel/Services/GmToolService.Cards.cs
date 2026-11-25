using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GunnySuite.Core;

namespace GunnyPanel.Services;

// Feature "Thẻ Bài — Full Album" (data mới của NPH có thêm album/thẻ mới).
// Owner: cards-dev. Chỉ sửa file này; dispatcher/feature list đã có trong GmToolService.cs.
public sealed partial class GmToolService
{
    /// <summary>
    /// Mở full toàn bộ album thẻ (mọi CardType / bộ thẻ) cho user theo dữ liệu mới nhất
    /// trong Db_Tank_All (Card_Template_Info / TS_CardBooklet / TS_CardSuit / Card_Groove_Update).
    /// </summary>
    private async Task<(bool, string)> AlbumAsync(int userId)
    {
        // Ghi chú thiết kế (đã đối chiếu code game.server):
        // - CardInventory (GamePlayer.cs: new CardInventory(this, true, 100, 0)) chỉ có 100 ô:
        //   Place 0..4 = 5 ô thẻ đang trang bị, Place 5..99 = album. Vì vậy chỉ giữ nguyên Place 0..4,
        //   xoá + cấp lại album liên tục từ Place 5. Capability túi thẻ đã nâng 100 -> 150 ở game.server
        //   (GamePlayer.cs: new CardInventory(this, saveTodb: true, 150, 0); CardNewSystemMgr.GetSuitValue
        //   GetCards(5, 150)) => album chứa tới 145 thẻ (Place 5..149), đủ cho 102 thẻ data NPH hiện tại.
        //   Thứ tự cấp Place: thẻ thuộc 4 bộ mới #27-#30 trước, rồi thẻ mới nhất trước (CardID giảm dần) —
        //   chỉ có ý nghĩa khi tương lai data vượt capability; hiện tại mở đủ 102/102.
        // - CardNewSystemMgr.GetSuitValue() chạy ngay khi login (GamePlayer.UpdateCardInfo) và tra
        //   TS_CardBooklet theo (TemplateId, Profile = 5 - CardType): thiếu profile là NullReferenceException
        //   => không cho user login. Vì vậy chỉ insert cặp (CardID, CardType) có profile tương ứng.
        // - Chỉ insert card có trong CẢ Card_Template_Info (nguồn stats cũ + điều kiện CardAbstractInventory
        //   .AddCardTo lúc load) LẪN TS_CardBooklet. CardId chỉ có trong TS_CardBooklet (10 thẻ mới
        //   314189..314198) hoặc chỉ có 1 profile (Hồn Thẻ 314150) sẽ bị server reset TemplateID=0 khi
        //   login và gây crash GetSuitValue => chủ động loại, khi NPH import đủ Card_Template_Info thì
        //   feature tự mở thêm (SQL đọc trực tiếp bảng nên không cần sửa code).
        // - Sys_Users_CardActive = danh sách album/bộ thẻ đã kích hoạt (TS_CardSuit). Chỉ giữ đúng 1 bộ
        //   IsActive=1 và bộ đó BẮT BUỘC phải có ít nhất 1 thẻ user sở hữu, nếu không GetSuitValue sẽ
        //   IndexOutOfRange ở AddPercents (num = 5 - min CardType = 5 khi không sở hữu thẻ nào).
        // - Level / CardGP / Type lấy max theo Card_Groove_Update (type cao nhất) thay vì hardcode 30/0/1.
        const string albumSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Capability túi thẻ = 150 ô (game.server GamePlayer.cs đã nâng 100 -> 150; CardNewSystemMgr cũng
-- dùng GetCards(5, 150)) => album tối đa 145 thẻ ở Place 5..149, đủ cho 102 thẻ data NPH hiện tại.
DECLARE @cap int;
SET @cap = 150;

-- Mốc Level / CardGP / groove Type cao nhất lấy trực tiếp từ Card_Groove_Update.
DECLARE @grooveType int;
SET @grooveType = ISNULL((SELECT MAX(Type) FROM Db_Tank_All.dbo.Card_Groove_Update), 1);
DECLARE @cardLevel int;
SET @cardLevel = ISNULL((SELECT MAX(Level) FROM Db_Tank_All.dbo.Card_Groove_Update WHERE Type = @grooveType), 0);
DECLARE @cardGP int;
SET @cardGP = ISNULL((SELECT MAX(Exp) FROM Db_Tank_All.dbo.Card_Groove_Update WHERE Type = @grooveType), 0);

DECLARE @activeSuit int;

BEGIN TRANSACTION;

-- 1) Reset album của đúng user; giữ nguyên Place 0..4 (5 ô thẻ đang trang bị).
DELETE FROM dbo.Sys_Users_Card
WHERE UserID = @uid AND Place >= 5;

-- 2) Mở toàn bộ thẻ: lấy cấp chất lượng cao nhất (CardType max) của mỗi CardID có mặt
--    trong cả Card_Template_Info (stats + điều kiện load của server) và TS_CardBooklet
--    (profile = 5 - CardType, bắt buộc để GetSuitValue không crash khi login).
;WITH Tpl AS
(
    SELECT t.CardID,
           t.CardType,
           t.AddAttack,
           t.AddDefend,
           t.AddAgility,
           t.AddLucky,
           t.AddDamage,
           t.AddGuard,
           ROW_NUMBER() OVER (ORDER BY
               -- Ưu tiên 1: thẻ thuộc 4 bộ mới #27-#30 (CardID 314184-314203) - thẻ NPH vừa thêm.
               CASE WHEN EXISTS (SELECT 1
                                 FROM Db_Tank_All.dbo.TS_CardSuit s
                                 WHERE s.SuitTemplateId BETWEEN 27 AND 30
                                   AND ',' + s.NeedCardTempIds + ',' LIKE '%,' + CAST(t.CardID AS varchar(20)) + ',%')
                    THEN 0 ELSE 1 END,
               -- Ưu tiên 2: thẻ mới nhất trước (CardID giảm dần) để 7 thẻ bị cắt là thẻ cũ nhất.
               t.CardID DESC) AS rn
    FROM Db_Tank_All.dbo.Card_Template_Info t
    INNER JOIN Db_Tank_All.dbo.TS_CardBooklet b
        ON b.TemplateId = t.CardID
       AND b.Profile = 5 - t.CardType
    WHERE t.CardType = (SELECT MAX(t2.CardType)
                        FROM Db_Tank_All.dbo.Card_Template_Info t2
                        INNER JOIN Db_Tank_All.dbo.TS_CardBooklet b2
                            ON b2.TemplateId = t2.CardID
                           AND b2.Profile = 5 - t2.CardType
                        WHERE t2.CardID = t.CardID)
)
INSERT INTO dbo.Sys_Users_Card
    (CardType, UserID, Place, TemplateID, isFirstGet, Attack, Defence, Luck, Agility,
     Damage, Guard, IsExit, Level, CardGP, Type)
SELECT t.CardType, @uid, 4 + t.rn, t.CardID, 1,
       ISNULL(t.AddAttack, 0), ISNULL(t.AddDefend, 0), ISNULL(t.AddLucky, 0), ISNULL(t.AddAgility, 0),
       ISNULL(t.AddDamage, 0), ISNULL(t.AddGuard, 0), 1,
       @cardLevel, @cardGP, @grooveType
FROM Tpl t
WHERE 4 + t.rn < @cap;

-- 3) Chọn album đang kích hoạt: giữ bộ user đang chọn nếu còn hợp lệ; nếu chưa có
--    thì chọn bộ mới nhất mà user đã sở hữu ít nhất 1 thẻ (xem lý do ở comment C#).
SELECT TOP (1) @activeSuit = a.MainID
FROM dbo.Sys_Users_CardActive a
WHERE a.UserID = @uid
  AND a.IsActive = 1
  AND EXISTS (SELECT 1
              FROM Db_Tank_All.dbo.TS_CardSuit s
              INNER JOIN dbo.Sys_Users_Card c
                  ON c.UserID = @uid
                 AND c.Place >= 5
                 AND ',' + s.NeedCardTempIds + ',' LIKE '%,' + CAST(c.TemplateID AS varchar(20)) + ',%'
              WHERE s.SuitTemplateId = a.MainID)
ORDER BY a.MainID;

IF @activeSuit IS NULL
BEGIN
    SELECT TOP (1) @activeSuit = s.SuitTemplateId
    FROM Db_Tank_All.dbo.TS_CardSuit s
    WHERE EXISTS (SELECT 1
                  FROM dbo.Sys_Users_Card c
                  WHERE c.UserID = @uid
                    AND c.Place >= 5
                    AND ',' + s.NeedCardTempIds + ',' LIKE '%,' + CAST(c.TemplateID AS varchar(20)) + ',%')
    ORDER BY s.SuitTemplateId DESC;
END

-- 4) Đồng bộ danh sách album (bộ thẻ) trong Sys_Users_CardActive: bỏ bộ không còn trong data,
--    thêm bộ còn thiếu, chuẩn hoá đúng 1 bộ IsActive = 1.
DELETE a
FROM dbo.Sys_Users_CardActive a
WHERE a.UserID = @uid
  AND NOT EXISTS (SELECT 1 FROM Db_Tank_All.dbo.TS_CardSuit s WHERE s.SuitTemplateId = a.MainID);

UPDATE dbo.Sys_Users_CardActive
SET IsActive = CASE WHEN MainID = @activeSuit THEN 1 ELSE 0 END
WHERE UserID = @uid;

INSERT INTO dbo.Sys_Users_CardActive (UserID, MainID, IsActive)
SELECT @uid, s.SuitTemplateId, CASE WHEN s.SuitTemplateId = @activeSuit THEN 1 ELSE 0 END
FROM Db_Tank_All.dbo.TS_CardSuit s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Sys_Users_CardActive a
                  WHERE a.UserID = @uid AND a.MainID = s.SuitTemplateId);

COMMIT TRANSACTION;";

        var (_, err) = await _db.ExecuteAsync("Db_Tank", albumSql,
            new Dictionary<string, object?> { ["uid"] = userId }, 180);

        if (err != null) return (false, "Lỗi mở Album Thẻ: " + err);

        // Đọc lại số liệu thực tế (data NPH có thể đổi sau mỗi lần import).
        // TotalCards = số thẻ CÓ THỂ mở theo data hiện tại (để báo giới hạn khi túi thẻ không đủ ô).
        const string countSql = @"
SELECT (SELECT COUNT(*) FROM dbo.Sys_Users_Card WHERE UserID = @uid AND Place >= 5) AS Cards,
       (SELECT COUNT(DISTINCT t.CardID)
        FROM Db_Tank_All.dbo.Card_Template_Info t
        INNER JOIN Db_Tank_All.dbo.TS_CardBooklet b
            ON b.TemplateId = t.CardID AND b.Profile = 5 - t.CardType
        WHERE t.CardType = (SELECT MAX(t2.CardType)
                            FROM Db_Tank_All.dbo.Card_Template_Info t2
                            INNER JOIN Db_Tank_All.dbo.TS_CardBooklet b2
                                ON b2.TemplateId = t2.CardID AND b2.Profile = 5 - t2.CardType
                            WHERE t2.CardID = t.CardID)) AS TotalCards,
       (SELECT COUNT(*) FROM dbo.Sys_Users_CardActive WHERE UserID = @uid) AS Albums,
       ISNULL((SELECT TOP (1) MainID FROM dbo.Sys_Users_CardActive
               WHERE UserID = @uid AND IsActive = 1), 0) AS ActiveAlbum;";

        var counts = await _db.QueryAsync("Db_Tank", countSql,
            new Dictionary<string, object?> { ["uid"] = userId }, 1, 30);

        if (!counts.Ok) return (false, "Lỗi đọc lại dữ liệu thẻ: " + counts.Error);

        var cards = 0;
        var totalCards = 0;
        var albums = 0;
        var activeAlbum = 0;
        if (counts.Rows.Count > 0)
        {
            var row = counts.Rows[0];
            if (row.Length > 0) int.TryParse(row[0], out cards);
            if (row.Length > 1) int.TryParse(row[1], out totalCards);
            if (row.Length > 2) int.TryParse(row[2], out albums);
            if (row.Length > 3) int.TryParse(row[3], out activeAlbum);
        }

        if (cards == 0)
            return (false, "Chưa có dữ liệu thẻ để mở (kiểm tra Card_Template_Info / TS_CardBooklet / TS_CardSuit trong Db_Tank_All).");

        var activeText = activeAlbum > 0 ? $", đang dùng bộ thẻ #{activeAlbum}" : "";
        if (totalCards > cards)
            return (true,
                $"Đã mở Album Thẻ: {cards}/{totalCards} thẻ — giới hạn {cards} ô album do túi thẻ capability 100 " +
                $"(cần nâng capability lên 150 trong game.server để mở đủ {totalCards} thẻ); " +
                $"{albums} bộ thẻ (album){activeText}. Cần đăng nhập lại game để cập nhật.");

        return (true,
            $"Đã mở full Album Thẻ: {cards} thẻ / {albums} bộ thẻ (album){activeText}. Cần đăng nhập lại game để cập nhật.");
    }
}
