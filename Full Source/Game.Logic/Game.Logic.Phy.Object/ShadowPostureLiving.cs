using System;

namespace Game.Logic.Phy.Object;

/// <summary>
/// Ảo ảnh của Tư Thế Phân Thân.
///
/// Đây là một <see cref="Living"/> THẬT trong map (nhưng không nằm trong danh sách
/// người chơi và không có turn), nên:
/// - đạn/nổ có thể trúng nó theo vị trí (hên xui trúng ảo ảnh hay trúng người thật);
/// - máu của ảo ảnh bị trừ như bình thường (không phải "gánh 1 máu" cho chủ thể);
/// - chủ thể (Player thật) vẫn nhận damage bình thường nếu bị bắn trúng.
///
/// Client vẽ ảo ảnh bằng packet ADD_PLAYER_SHADOWN (157) với cùng Id, nên các packet
/// cập nhật máu / xoá living theo Id này sẽ áp dụng đúng lên hình ảo ảnh.
/// </summary>
public class ShadowPostureLiving : Living
{
	private bool m_removed;

	/// <summary>Đang trong một lệnh rơi (tránh xếp chồng nhiều lệnh rơi).</summary>
	private bool m_falling;

	/// <summary>Gọi khi ảo ảnh bị xoá (hết turn, hết máu, hoặc chủ thể chết).</summary>
	public Action<ShadowPostureLiving> Removed;

	public ShadowPostureLiving(int id, BaseGame game, int team, string name, string modelId, int maxBlood, int immunity, int direction)
		: base(id, game, team, name, modelId, maxBlood, immunity, direction)
	{
		Type = eLivingType.Living;
		IsLiving = true;
		SyncAtTime = true;
		Config.CanTakeDamage = true;
		Config.CanCountKill = false;
		Config.KeepLife = false;
		Config.IsDown = false;
		Config.IsHelper = false;
		Config.HasTurn = false;
		Config.CanFrost = false;
	}

	public bool IsRemoved => m_removed;

	/// <summary>
	/// StartMoving của Living sẽ tự kiểm tra đất dưới chân và cho rơi nếu bị đào mất.
	/// Log lại để biết server có gọi tới (bomb nổ trong bán kính sẽ gọi hàm này).
	/// </summary>
	public override void StartMoving(int delay, int speed)
	{
		PetCloneDiag.Write(string.Format(
			"[SHADOW-MOVE] shadow={0} pos=({1},{2}) delay={3} speed={4}",
			Id, X, Y, delay, speed));
		base.StartMoving(delay, speed);
	}

	/// <summary>Nếu địa hình dưới ảo ảnh đã bị đào mất thì cho ảo ảnh rơi xuống.</summary>
	public void CheckGround()
	{
		// Đang rơi rồi thì không xếp thêm lệnh rơi nữa (bomb nổ liên tiếp / event
		// chuyển turn bắn nhiều lần sẽ làm ảo ảnh rơi nhanh gấp nhiều lần).
		if (m_removed || m_falling)
		{
			return;
		}
		try
		{
			if (FallFrom(X, Y, null, 0, 0, 12, OnFallFinished))
			{
				m_falling = true;
				PetCloneDiag.Write(string.Format("[SHADOW-FALL] shadow={0} from=({1},{2})", Id, X, Y));
			}
		}
		catch (Exception ex)
		{
			m_falling = false;
			PetCloneDiag.Write("[SHADOW-FALL-ERROR] " + ex.Message);
		}
	}

	private void OnFallFinished()
	{
		m_falling = false;
		PetCloneDiag.Write(string.Format("[SHADOW-FALL-END] shadow={0} pos=({1},{2})", Id, X, Y));
	}

	public override bool TakeDamage(Living source, ref int damageAmount, ref int criticalAmount, string msg)
	{
		int before = Blood;
		bool result = base.TakeDamage(source, ref damageAmount, ref criticalAmount, msg);
		PetCloneDiag.Write(string.Format(
			"[SHADOW-HIT] shadow={0} source={1} blood={2}->{3} dmg={4} crit={5} result={6}",
			Id, PetCloneDiag.Name(source), before, Blood, damageAmount, criticalAmount, result));
		if (Blood <= 0)
		{
			Remove("hp-zero");
		}
		return result;
	}

	public override void Die()
	{
		Remove("die");
	}

	public override void Die(int delay)
	{
		Remove("die");
	}

	/// <summary>Xoá ảo ảnh khỏi server (map + danh sách living) và báo client xoá hình.</summary>
	public void Remove(string reason)
	{
		if (m_removed)
		{
			return;
		}
		m_removed = true;
		IsLiving = false;
		Blood = 0;
		PetCloneDiag.Write(string.Format("[SHADOW-REMOVE] shadow={0} reason={1}", Id, reason));
		try
		{
			Game.RemoveLiving(Id);
		}
		catch (Exception ex)
		{
			PetCloneDiag.Write("[SHADOW-REMOVE-ERROR] " + ex);
			// Dù lỗi thì vẫn phải báo client xoá hình ảo ảnh.
			try { Game.SendRemoveShadowVisual(Id); } catch { }
		}
		try
		{
			Removed?.Invoke(this);
		}
		catch (Exception ex2)
		{
			PetCloneDiag.Write("[SHADOW-CALLBACK-ERROR] " + ex2.Message);
		}
	}
}
