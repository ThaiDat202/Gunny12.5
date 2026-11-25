using Game.Base.Packets;
using Game.Server.GameObjects;
using SqlDataProvider.Data;

namespace Game.Server.Pet.Handle;

[PetHandleAttbute(21)]
public class DelPetEquip : IPetCommandHadler
{
	public bool CommandHandler(GamePlayer player, GSPacketIn packet)
	{
		int place = packet.ReadInt();
		int eqPlace = packet.ReadInt();
		if (player.PetBag.RemoveEqPet(place, eqPlace))
		{
			UsersPetInfo pet = player.PetBag.GetPetAt(place);
			if (pet != null)
			{
				// Rebuild kỹ năng cho đúng pet vừa tháo trang bị: loại bỏ skill thức tỉnh
				// của trang bị đã tháo, giữ skill của các trang bị còn lại.
				player.PetBag.RebuildPetSkill(pet);
				player.PetBag.UpdatePet(pet);
				player.PetBag.SaveToDatabase(saveAdopt: false);
			}
			player.PetBag.OnChangedPetEquip(place);
		}
		return false;
	}
}
