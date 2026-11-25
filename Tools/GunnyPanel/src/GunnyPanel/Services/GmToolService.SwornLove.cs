using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GunnySuite.Core;

namespace GunnyPanel.Services;

// Feature "Nhẫn Cầu Hôn — Tình Ý" (nâng max điểm tình ý của nhẫn cầu hôn).
// Owner: sworn-dev. Chỉ sửa file này; dispatcher/feature list đã có trong GmToolService.cs.
//
// Cơ chế (đã xác minh từ source + dữ liệu):
//   - "Điểm tình ý" của Hệ thống Nhẫn Cưới (client: bagAndInfo.bag.ring.RingSystemView,
//     tiêu đề "Tình ý" — ddt.bagandinfo.ringSystem.titleText) = PlayerInfo.RingExp,
//     lưu tại Db_Tank.dbo.Sys_Users_Detail.RingExp.
//   - Bảng mốc cấp tình ý: Db_Tank_All.dbo.Love_level (client tải LoveLeveList.xml):
//     60 cấp, cấp cuối 60 = 99.690 điểm tình ý. Server dùng
//     LoveLevelMgr.GetLevel(RingExp) để cộng % chỉ số cho nhẫn cưới
//     (9022/9122/9222/9322/9422/9522) trong PlayerEquipInventory.
//   - KHÔNG liên quan Sys_Users_Sworn (Ngọc Kim Lan / điểm nghĩa khí, tối đa 124.860
//     ở Level 100) → feature này không đụng vào bảng đó.
public sealed partial class GmToolService
{
    /// <summary>
    /// Nâng tối đa "điểm tình ý" của Nhẫn Cầu Hôn cho user (giữ nguyên cấp nhẫn,
    /// Ngọc Kim Lan/nghĩa khí, kỹ năng và trạng thái kết hôn).
    /// </summary>
    private async Task<(bool, string)> SwornLoveAsync(int userId)
    {
        // Mốc tình ý tối đa lấy động từ Love_level (không hard-code; hiện tại Level 60 = 99.690).
        var maxRes = await _db.QueryAsync("Db_Tank_All",
            "SELECT TOP 1 Level, Exp FROM Love_level ORDER BY Level DESC", 1, 30);
        if (!maxRes.Ok) return (false, "Không đọc được bảng Love_level: " + maxRes.Error);
        if (maxRes.Rows.Count == 0) return (false, "Bảng Love_level rỗng — chưa import dữ liệu cấp tình ý.");

        var maxLevel = int.TryParse(maxRes.Rows[0][0], out var ml) ? ml : 0;
        var maxExp = int.TryParse(maxRes.Rows[0][1], out var me) ? me : 0;
        if (maxExp <= 0) return (false, "Dữ liệu Love_level không hợp lệ (Exp tối đa <= 0).");

        // Đọc trạng thái hiện tại + số nhẫn cưới đang trang bị (để báo cáo).
        var curRes = await _db.QueryAsync("Db_Tank", @"
SELECT d.RingExp,
       (SELECT COUNT(*) FROM Sys_Users_Goods g
         WHERE g.UserID = d.UserID AND g.BagType = 0
           AND g.TemplateID IN (9022, 9122, 9222, 9322, 9422, 9522)) AS EquippedRing
FROM Sys_Users_Detail d WHERE d.UserID = @uid",
            new Dictionary<string, object?> { ["uid"] = userId }, 1, 30);
        if (!curRes.Ok) return (false, "Lỗi đọc Sys_Users_Detail: " + curRes.Error);
        if (curRes.Rows.Count == 0) return (false, $"Không tìm thấy user {userId} trong Sys_Users_Detail.");

        var beforeExp = int.TryParse(curRes.Rows[0][0], out var be) ? be : 0;
        var equippedRing = curRes.Rows[0].Length > 1 && int.TryParse(curRes.Rows[0][1], out var er) ? er : 0;

        // Cấp tình ý hiện tại = số mốc Love_level có Exp <= RingExp (mốc 1 = 0 tình ý).
        var lvRes = await _db.QueryAsync("Db_Tank_All",
            "SELECT COUNT(*) FROM Love_level WHERE Exp <= @exp",
            new Dictionary<string, object?> { ["exp"] = beforeExp }, 1, 30);
        var beforeLevel = lvRes.Ok && int.TryParse(lvRes.FirstCell(), out var bl) ? bl : 0;

        // Idempotent: đã max thì không ghi DB.
        if (beforeExp >= maxExp)
        {
            return (true,
                $"Nhẫn Cầu Hôn — Tình Ý: đã ở MAX từ trước (Lv{beforeLevel} — {beforeExp:N0}/{maxExp:N0} tình ý). " +
                "Không thay đổi gì.");
        }

        var (rows, err) = await _db.ExecuteAsync("Db_Tank",
            "UPDATE Sys_Users_Detail SET RingExp = @max WHERE UserID = @uid",
            new Dictionary<string, object?> { ["max"] = maxExp, ["uid"] = userId }, 60);
        if (err != null) return (false, "Lỗi cập nhật điểm tình ý (RingExp): " + err);
        if (rows == 0) return (false, $"Không cập nhật được điểm tình ý cho user {userId} (0 dòng).");

        var message =
            $"Nhẫn Cầu Hôn — Tình Ý: đã nâng MAX điểm tình ý Lv{beforeLevel} ({beforeExp:N0}) → " +
            $"Lv{maxLevel} ({maxExp:N0}). Giữ nguyên cấp nhẫn, Ngọc Kim Lan/nghĩa khí, kỹ năng và trạng thái kết hôn.";
        if (equippedRing == 0)
        {
            message += " Lưu ý: chưa trang bị nhẫn cưới (9022–9522) nên % chỉ số chỉ áp dụng khi trang bị nhẫn.";
        }
        message += " Nhân vật cần đăng nhập lại để server nạp lại điểm tình ý.";
        return (true, message);
    }
}
