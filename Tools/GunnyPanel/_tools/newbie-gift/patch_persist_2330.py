#!/usr/bin/env python3
"""Patch: lưu DB ngay sau live apply + panel Phụ Ma/Đúc Hồn chấp nhận ring đang ở túi."""
import pathlib

GM = pathlib.Path(r"C:\Gunny\Gunny12.6\Full Source\Road.Service\Game.Service.GMService\GmApiActions.cs")
SVC = pathlib.Path(r"C:\Gunny\ToolGunny\GunnyPanel\src\GunnyPanel\Services\GmToolService.cs")


def main() -> int:
    # 1) Road API: RefreshProperties → lưu EquipBag xuống DB ngay
    text = GM.read_text(encoding="utf-8-sig")
    if "player.EquipBag.SaveToDatabase();" not in text:
        old = """    private static void RefreshProperties(GamePlayer player)
    {
        player.EquipBag.UpdatePlayerProperties();"""
        new = """    private static void RefreshProperties(GamePlayer player)
    {
        try { player.EquipBag.SaveToDatabase(); } catch (Exception ex) { log.Warn("[GMApi] SaveToDatabase: " + ex.Message); }
        player.EquipBag.UpdatePlayerProperties();"""
        if old in text:
            text = text.replace(old, new, 1)
            GM.write_text(text, encoding="utf-8-sig")
            print("GM RefreshProperties patched")
        else:
            print("GM: RefreshProperties pattern not found")
    else:
        print("GM: SaveToDatabase already present")

    # 2) Panel: Phụ Ma/Đúc Hồn chỉ cần SỞ HỮU ring (kể cả trong túi); online API sẽ tự trang bị
    svc = SVC.read_text(encoding="utf-8-sig")
    before = svc
    old_count = """        var count = await CountEquippedRingsAsync(userId);
        if (count == 0) return (false, "Bạn chưa trang bị nhẫn và vòng tay.");"""
    new_count = """        var count = await CountOwnedRingsAsync(userId);
        if (count == 0) return (false, "Bạn chưa có nhẫn/vòng tay.");"""
    if old_count in svc:
        svc = svc.replace(old_count, new_count)
    # CountEquippedRingsAsync -> CountOwnedRingsAsync (bỏ điều kiện Place < 31)
    if "private async Task<int> CountEquippedRingsAsync" in svc:
        svc = svc.replace("private async Task<int> CountEquippedRingsAsync", "private async Task<int> CountOwnedRingsAsync")
        svc = svc.replace("WHERE UserID = @uid AND BagType = 0 AND Place < 31\n  AND TemplateID IN (SELECT TemplateID FROM Db_Tank_All.dbo.Shop_Goods WHERE CategoryID IN (8,9,28,29))",
                          "WHERE UserID = @uid AND BagType = 0\n  AND TemplateID IN (SELECT TemplateID FROM Db_Tank_All.dbo.Shop_Goods WHERE CategoryID IN (8,9,28,29))")
    if svc != before:
        SVC.write_text(svc, encoding="utf-8-sig")
        print("SVC patched ring ownership check")
    else:
        print("SVC: no changes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
