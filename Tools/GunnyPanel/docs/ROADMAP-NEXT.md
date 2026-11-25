# Đề xuất phát triển tiếp — GunnyPanel

> Trạng thái: **v0.26 — đã làm Phase A + B.1/B.2 + C.1/C.3 (v0.25) + Road GM API live
> và fix Vinh Dự/Châu Báu/Ma Thạch (v0.26)**. Tài liệu giữ lại các phần còn lại
> (Phase C.2/C.4, D, E) để làm tiếp.

## Đã hoàn thành ở v0.26

| Hạng mục | Trạng thái | Tài liệu |
|---|---|---|
| **Road GM API** (HTTP JSON trong Road.Service, token, live per-button) | ✅ 15 feature: 13 live + beads/magicstone managed | [`ROAD-GM-API.md`](ROAD-GM-API.md) |
| **Fix Vinh Dự không cộng HP** | ✅ Sửa `PlayerEquipInventory.UpdatePlayerProperties` + build lại `Game.Server.dll` | [`ROAD-GM-API.md`](ROAD-GM-API.md) |
| **Châu Báu: mở 6 mũi khoan (drill 6 lỗ) live** | ✅ `BeadBag.SendPlayerDrill` + DB qua PlayerBussiness | [`ROAD-GM-API.md`](ROAD-GM-API.md) |
| **Ma Thạch: mail đủ 6 chỉ số** | ✅ Gửi qua `PlayerBussiness.SendMailAndItemByNickName` (ApplyMagicStoneMailStats) | [`ROAD-GM-API.md`](ROAD-GM-API.md) |
| Fix build legacy `game.server` (2 folder script trùng) | ✅ `<Compile Remove="...GameServerScript.AI\**" />` | [`ROAD-GM-API.md`](ROAD-GM-API.md) |

## Đã hoàn thành ở v0.25

| Hạng mục | Trạng thái | Tài liệu |
|---|---|---|
| **Phase A** — Road Live Controls (notice/kick/state/reload/server list/chat) | ✅ **Không sửa legacy**: gọi thẳng `CenterService` WCF có sẵn (basicHttp 4109) | [`WEB-SERVER-LIVE.md`](WEB-SERVER-LIVE.md) |
| **Phase B.1** — Ban chat / Unban chat | ✅ Có xác nhận NickName + audit | [`WEB-USER-EXTRAS.md`](WEB-USER-EXTRAS.md) |
| **Phase B.2** — Gửi mail tùy chỉnh (title/content + Gold/Money/GiftToken, không item) | ✅ Ghi `User_Messages` + MailNotice nếu online | [`WEB-USER-EXTRAS.md`](WEB-USER-EXTRAS.md) |
| **Phase C.1** — Shop_Goods explorer (read-only) | ✅ Tìm/lọc/sort/paging/ảnh/chi tiết | [`WEB-SHOP-EXPLORER.md`](WEB-SHOP-EXPLORER.md) |
| **Phase C.3** — Mail/User_Messages viewer (read-only) | ✅ Lọc theo user/type/ngày/đọc + thống kê + chi tiết annex | [`WEB-MAIL-VIEWER.md`](WEB-MAIL-VIEWER.md) |
| Fix phân trang `?page=` trên Users / Users/Details / Audit | ✅ Razor Pages route value `page` che `[BindProperty]` → chuyển sang `[FromQuery]` | — |

Phase A **không cần** sửa `Full Source`/`Service`/`Road.Service`: `Center.Service.exe.config`
đã host sẵn endpoint basicHttp `http://127.0.0.1:4109/CenterServicePhP/` với đầy đủ
`SystemNotice`, `KitoffUser`, `GetPlayerState`, `Reload`, `ReLoadServerList`, `GetAllChat`, `Shutdown`.
Đề xuất gốc [`PROPOSAL-ROAD-GM-API.md`](PROPOSAL-ROAD-GM-API.md) (thêm API mới vào Road.Service)
**không còn cần thiết** trừ khi muốn thêm chức năng ngoài phạm vi CenterService.

## Phase A — đã hoàn thành (giữ mô tả gốc)

**Phát hiện:** Road.Service đã có sẵn `CenterService` WCF tại `net.tcp://127.0.0.1:2009/`
(contract `ICenterService`) với các thao tác live:

| Chức năng | Operation có sẵn | Ghi chú |
|---|---|---|
| Gửi thông báo hệ thống toàn server | `SystemNotice(string msg)` | Hiện toast/notice trong game |
| Kick người chơi online | `KitoffUser(playerID, zoneId, msg)` | Dùng khi ban / cần offline để sửa dữ liệu |
| Xem trạng thái online của player | `GetPlayerState(playerID, zoneId)` | Read-only |
| Xem chat gần đây | `GetAllChat()` | Read-only, cần kiểm tra dữ liệu trả về |
| Reload danh sách server | `ReLoadServerList()` | Thận trọng |
| Reload cấu hình / server | `Reload(string type)` | Theo kiểu Admincp (cần xác nhận) |
| Shutdown server | `Shutdown(string type)` | Nguy hiểm — chỉ Develop + xác nhận 2 lớp |

**Cách làm:** mở rộng helper WCF PowerShell (giống `_tools/notify-mail.ps1`) gọi trực tiếp
`CenterService` — **không sửa `Full Source` / `Service`**, không cần rebuild Road.Service.

**UI dự kiến:**
- Trang **Server Live** (mới hoặc mở rộng `/Server`):
  - Ô gửi thông báo + nút Gửi (admin/develop).
  - Danh sách player online (từ DB `State > 0`) + nút **Kick**.
  - Nút Reload (xác nhận), Shutdown (2 lớp, mặc định ẩn).
- Trang **User Detail**: nút **Kick** khi user đang online; sau kick có thể sửa tiền/level ngay.
- Audit đầy đủ: `road.notice`, `road.kick`, `road.reload`, `road.shutdown`.

**Effort:** ~1–2 phiên. **Rủi ro:** thao tác live ảnh hưởng người chơi → cần xác nhận + phân quyền.

---

## Phase B — Quản lý người chơi nâng cao (DB writes, cần duyệt từng mục)

1. **Ban chat / Unban chat**: cập nhật `IsBanChat`, `BanChatEndDate`; cảnh báo user online cần relogin.
2. **Gửi mail tùy chỉnh** (title/content, không kèm item): dùng `SP_Mail_Send` / MailNotice.
3. **Xem đầy đủ trường user** (160 cột) + tìm item theo TemplateID.
4. **Lịch sử nạp thẻ / ChargeMoney** (read-only nếu có bảng; cần xác định bảng).
5. **Reset mật khẩu cấp 2 / mật khẩu game** — nhạy cảm, chỉ làm sau khi duyệt riêng.

---

## Phase C — Game Data Explorer (đọc trước, ghi sau khi duyệt)

1. **Shop_Goods explorer**: tìm theo tên/mã/category, xem ảnh, giá, stack; read-only trước.
2. **Item/Box/Drop/Quest/Activity browser** từ `Db_Gm` (`Table_Games`, XML) — read-only.
3. **Mail/User_Messages viewer**: tìm theo user/title; xoá mail test (cần duyệt).
4. **Shop editor**: sửa giá/tên/category — **cần duyệt riêng** (ảnh hưởng game).

---

## Phase D — Dashboard & vận hành

1. Dashboard metrics: số online, top 10 lực chiến/xu, user mới theo ngày, job queue.
2. Audit log: lọc theo action/user/time + export CSV.
3. `resource-sync` theo lô; sửa encoding XML nguồn VNG.
4. Fallback CSS cho 37abc (grid/variables) để giao diện đẹp hơn.

---

## Phase E — Port Admincp (read-only trước)

- Award/Actives/Settings/Chat của Admincp → trang read-only trong GunnyPanel;
  sau đó mới mở thao tác ghi nếu cần.

---

## Thứ tự đề xuất

1. ~~**Phase A** (Road Live Controls)~~ — ✅ đã làm v0.25 qua `CenterService` có sẵn.
2. ~~**Phase C.1/C.3** (Shop explorer + Mail viewer read-only)~~ — ✅ đã làm v0.25.
3. ~~**Phase B.1/B.2** (ban chat + mail tùy chỉnh)~~ — ✅ đã làm v0.25.
4. **Phase C.2** (Item/Box/Drop/Quest browser read-only) — an toàn, nên làm tiếp.
5. **Phase D** (dashboard metrics, audit filter/CSV, resource-sync hàng loạt) và **Phase E** (port Admincp read-only).

## Cần bạn xác nhận

- [x] Đồng ý làm **Phase A** (notice/kick/reload) qua `CenterService` có sẵn — **không sửa** `Full Source`/`Service`.
- [x] Có cần **Shutdown** trong Phase A không? → Đã làm nhưng **mặc định tắt** (`Panel:EnableShutdown=false`),
      chỉ bật khi role Develop + cấu hình + xác nhận 2 lớp.
- [x] Ưu tiên tiếp theo: Shop explorer (C.1), Mail viewer (C.3), Ban chat (B.1), Mail tùy chỉnh (B.2) — đã xong.
- [ ] Ưu tiên kế tiếp sau v0.25: C.2 (browser Db_Gm/XML) / D (dashboard + audit filter) / E (Admincp read-only)?
- [ ] Có mở **xoá mail test** (soft-delete `User_Messages`) và **Shop editor** (ghi) không? Hiện để read-only.
