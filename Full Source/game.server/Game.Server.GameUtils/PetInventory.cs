using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Bussiness;
using Bussiness.Helpers;
using Bussiness.Managers;
using DDtankHelp;
using Game.Logic;
using Game.Server.GameObjects;
using Game.Server.Pet;
using log4net;
using Newtonsoft.Json;
using SqlDataProvider.Data;

namespace Game.Server.GameUtils;

public class PetInventory : PetAbstractInventory
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private bool m_saveToDb;

    private List<UsersPetInfo> m_removedList;

    protected GamePlayer m_player;

    private UsersPetFormInfo m_petForm;

    private List<PetFormActiveListInfo> m_activeList;

    private EatPetsInfo m_eatPets;

    public int lastAwakenId;

    public List<ItemAwakenPet> ItemAwakens;

    public GamePlayer Player => m_player;

    /// <summary>
    /// Số ô pet đã mở của người chơi — lưu trong cột Sys_Users_Detail.SlotPet
    /// (field chuẩn sẵn có của server 12.6). Luôn nằm trong khoảng
    /// [PetExtConfigMgr.FreeCellCount, PetExtConfigMgr.MaxPetCellCount].
    /// </summary>
    public int UnlockedCellCount
    {
        get
        {
            int value = (m_player != null && m_player.PlayerCharacter != null) ? m_player.PlayerCharacter.SlotPet : 0;
            int free = PetExtConfigMgr.FreeCellCount;
            int max = PetExtConfigMgr.MaxPetCellCount;
            if (value < free)
            {
                value = free;
            }
            if (value > max)
            {
                value = max;
            }
            return value;
        }
    }

    public UsersPetFormInfo PetForm => m_petForm;

    public List<PetFormActiveListInfo> ActiveList => m_activeList;

    public EatPetsInfo EatPets => m_eatPets;

    public int MaxLevel => Convert.ToInt32(PetMgr.FindConfig("MaxLevel").Value);

    public int MaxLevelByGrade
    {
        get
        {
            if (m_player == null)
            {
                return MaxLevel;
            }
            return (m_player.Level > MaxLevel) ? MaxLevel : m_player.Level;
        }
    }

    public PetInventory(GamePlayer player, bool saveTodb, int capibility, int aCapability, int beginSlot)
        : base(capibility, aCapability, beginSlot)
    {
        m_player = player;
        m_saveToDb = saveTodb;
        m_removedList = new List<UsersPetInfo>();
        m_activeList = new List<PetFormActiveListInfo>();
        lastAwakenId = 0;
        ItemAwakens = new List<ItemAwakenPet>();
    }

    public virtual void saveEqAwakenPet(PlayerBussiness pb)
    {
        for (int i = 0; i < ItemAwakens.Count; i++)
        {
            ItemAwakenPet itemAwakenPet = ItemAwakens[i];
            if (itemAwakenPet != null)
            {
                if (itemAwakenPet.isSave)
                {
                    pb.UpdateEqAwakenPet(itemAwakenPet, m_player.PlayerCharacter.ID);
                }
                else
                {
                    pb.AddEqAwakenPet(itemAwakenPet, m_player.PlayerCharacter.ID);
                }
            }
        }
    }

    public virtual void LoadFromDatabase()
    {
        if (!m_saveToDb)
        {
            return;
        }
        using PlayerBussiness playerBussiness = new PlayerBussiness();
        int iD = m_player.PlayerCharacter.ID;
        NormalizeSlotPet();
        UsersPetInfo[] userPetSingles = playerBussiness.GetUserPetSingles(iD, m_player.PlayerCharacter.VIPLevel);
        ItemAwakenPet[] eqPetWaken = playerBussiness.GetEqPetWaken(iD);
        if (GameProperties.VERSION < 7600)
        {
            UsersPetInfo[] userAdoptPetSingles = playerBussiness.GetUserAdoptPetSingles(iD);
            UsersPetInfo[] array = userAdoptPetSingles;
            foreach (UsersPetInfo usersPetInfo in array)
            {
                AddAdoptPetTo(usersPetInfo, usersPetInfo.Place);
            }
        }
        if (GameProperties.VERSION >= 10100)
        {
            lacresPet = playerBussiness.GetUserLacresPet(iD);
            if (lacresPet == null || lacresPet.Count == 0)
            {
                List<UsersPetLacrarInfo> list = new List<UsersPetLacrarInfo>();
                UsersPetLacrarInfo usersPetLacrarInfo = new UsersPetLacrarInfo();
                usersPetLacrarInfo.place = 6;
                usersPetLacrarInfo.lockArr = new List<StoreFineSealLockInfo>();
                usersPetLacrarInfo.isSeal = false;
                list.Add(usersPetLacrarInfo);
                UsersPetLacrarInfo usersPetLacrarInfo2 = new UsersPetLacrarInfo();
                usersPetLacrarInfo2.place = 15;
                usersPetLacrarInfo2.lockArr = new List<StoreFineSealLockInfo>();
                usersPetLacrarInfo2.isSeal = false;
                list.Add(usersPetLacrarInfo2);
                lacresPet = list;
            }
        }
        UsersPetFormInfo allUsersPetFormByID = playerBussiness.GetAllUsersPetFormByID(iD);
        EatPetsInfo allEatPetsByID = playerBussiness.GetAllEatPetsByID(iD);
        BeginChanges();
        try
        {
            for (int j = 0; j < eqPetWaken.Length; j++)
            {
                ItemAwakens.Add(eqPetWaken[j]);
                lastAwakenId = ItemAwakens[j].itemId;
            }
            UsersPetInfo[] array2 = userPetSingles;
            foreach (UsersPetInfo pet in array2)
            {
                if (string.IsNullOrEmpty(pet.BaseProp))
                {
                    string text = pet.TemplateID.ToString();
                    int num = -1;
                    num = ((text.Substring(text.Length - 1, 1) == "1") ? ((pet.Level < 30) ? pet.TemplateID : ((pet.Level >= 50) ? (pet.TemplateID - 2) : (pet.TemplateID - 1))) : ((!(text.Substring(text.Length - 1, 1) == "2")) ? (pet.TemplateID - 2) : (pet.TemplateID - 1)));
                    PetTemplateInfo petTemplateInfo = PetMgr.FindPetTemplate(num);
                    if (petTemplateInfo != null)
                    {
                        UsersPetInfo usersPetInfo2 = PetMgr.CreatePet(petTemplateInfo, pet.UserID, pet.Place, pet.Level, Player.PlayerCharacter.VIPLevel);
                        pet.BaseProp = JsonConvert.SerializeObject(usersPetInfo2);
                        UpdateEvolutionPet(usersPetInfo2, pet.Level, MaxLevelByGrade, Player.PlayerCharacter.VIPLevel);
                        pet.AttackGrow = usersPetInfo2.AttackGrow;
                        pet.DefenceGrow = usersPetInfo2.DefenceGrow;
                        pet.AgilityGrow = usersPetInfo2.AgilityGrow;
                        pet.LuckGrow = usersPetInfo2.LuckGrow;
                        pet.BloodGrow = usersPetInfo2.BloodGrow;
                        pet.DamageGrow = usersPetInfo2.DamageGrow;
                        pet.GuardGrow = usersPetInfo2.GuardGrow;
                    }
                }
                pet.GetSkillEquip();
                if (pet.IsSeal && !lacresPet.Any((UsersPetLacrarInfo info) => info.isSeal && info.petId == pet.ID))
                {
                    pet.IsSeal = false;
                    Debugger.Log("Lacre para o pet não foi encontrado, então o pet foi liberado", "LogLiberarPet" + iD);
                }
                AddPetTo(pet, pet.Place);
            }
            foreach (UsersPetLacrarInfo item in lacresPet)
            {
                if (!item.isSeal)
                {
                    continue;
                }
                UsersPetInfo petById = GetPetById(item.petId);
                if (petById == null)
                {
                    Debugger.Log("O pet do lacre não foi encontrado, então o lacre foi liberado", "LogLiberarLacre" + iD);
                    item.isSeal = false;
                }
                else if (!petById.IsSeal)
                {
                    if (petById.IsEquip)
                    {
                        petById.IsEquip = false;
                    }
                    petById.IsSeal = true;
                    OnPlaceChanged(petById.Place);
                }
            }
            if (allUsersPetFormByID == null)
            {
                lock (m_lock)
                {
                    m_petForm = new UsersPetFormInfo
                    {
                        UserID = iD,
                        PetsID = -1,
                        ActivePets = "[]"
                    };
                }
            }
            else
            {
                lock (m_lock)
                {
                    m_petForm = allUsersPetFormByID;
                }
            }
            if (allEatPetsByID == null)
            {
                lock (m_lock)
                {
                    m_eatPets = new EatPetsInfo
                    {
                        UserID = iD
                    };
                    return;
                }
            }
            lock (m_lock)
            {
                m_eatPets = allEatPetsByID;
            }
        }
        finally
        {
            // Rebuild Skill pool cho toàn bộ pet sau khi nạp trang bị: bổ sung 2 skill thức tỉnh
            // của trang bị đang equip (nếu record.petId trùng KindID pet) + chuẩn hoá exclusiveID
            // để client vẽ viền vàng / battle nhận đúng skill. Phải chạy TRƯỚC CommitChanges
            // để packet 68/1 gửi đi đã có Skill mới.
            RebuildAllPetSkills();
            DeserializePetForms();
            CommitChanges();
        }
    }

    public virtual void SaveToDatabase(bool saveAdopt)
    {
        if (!m_saveToDb)
        {
            return;
        }
        using PlayerBussiness playerBussiness = new PlayerBussiness();
        lock (m_lock)
        {
            for (int i = 0; i < m_pets.Length; i++)
            {
                UsersPetInfo usersPetInfo = m_pets[i];
                if (usersPetInfo != null && usersPetInfo.IsDirty)
                {
                    usersPetInfo.eQPets = SerializePetEquip(usersPetInfo.PetEquips);
                    if (usersPetInfo.ID > 0)
                    {
                        playerBussiness.UpdateUserPet(usersPetInfo);
                    }
                    else
                    {
                        playerBussiness.AddUserPet(usersPetInfo);
                    }
                }
            }
            saveEqAwakenPet(playerBussiness);
        }
        if (saveAdopt)
        {
            lock (m_lock)
            {
                for (int j = 0; j < m_adoptPets.Length; j++)
                {
                    UsersPetInfo usersPetInfo2 = m_adoptPets[j];
                    if (usersPetInfo2 != null && usersPetInfo2.IsDirty && usersPetInfo2.ID == 0)
                    {
                        playerBussiness.AddUserAdoptPet(usersPetInfo2, isUse: false);
                    }
                }
            }
        }
        lock (m_lock)
        {
            if (m_petForm != null && m_petForm.IsDirty)
            {
                SerializePetForms();
                if (m_petForm.ID == 0)
                {
                    playerBussiness.AddUsersPetForm(m_petForm);
                }
                else
                {
                    playerBussiness.UpdateUsersPetForm(m_petForm);
                }
            }
        }
        lock (m_lock)
        {
            if (m_eatPets != null && m_eatPets.IsDirty)
            {
                if (m_eatPets.ID == 0)
                {
                    playerBussiness.AddEatPets(m_eatPets);
                }
                else
                {
                    playerBussiness.UpdateEatPets(m_eatPets);
                }
            }
        }
        lock (m_lock)
        {
            foreach (UsersPetInfo removed in m_removedList)
            {
                playerBussiness.UpdateUserPet(removed);
            }
            m_removedList.Clear();
        }
    }

    public void DeserializePetForms()
    {
        try
        {
            if (PetForm == null || string.IsNullOrEmpty(PetForm.ActivePets))
            {
                return;
            }
            lock (m_lock)
            {
                m_activeList = (from p in JsonConvert.DeserializeObject<List<PetFormActiveListInfo>>(PetForm.ActivePets)
                                group p by p.TemplateID into g
                                select g.First()).ToList();
            }
        }
        catch (Exception exception)
        {
            log.Error("DeserializeMagicHouse fail: ", exception);
        }
    }

    public void SerializePetForms()
    {
        try
        {
            lock (m_lock)
            {
                m_petForm.ActivePets = JsonConvert.SerializeObject(m_activeList);
            }
        }
        catch (Exception exception)
        {
            log.Error("DeserializeMagicHouse fail: ", exception);
        }
    }

    public void UpdatePetFormProp(int hp, ref int enhanceHp, ref int reduceDamagePow)
    {
        int num = 0;
        int num2 = 0;
        int num3 = 0;
        int num4 = 0;
        foreach (PetFormActiveListInfo active in ActiveList)
        {
            PetFormDataInfo petFormDataInfo = PetMgr.FindPetFormData(active.TemplateID);
            if (petFormDataInfo != null)
            {
                num += petFormDataInfo.HeathUp;
                num2 += petFormDataInfo.DamageReduce;
                num3 += petFormDataInfo.NoBombReduceDamage;
                num4 += petFormDataInfo.BurningResistance;
            }
        }
        enhanceHp = ((hp * (num / 100)) / 2);
        reduceDamagePow = num2;
    }

    public override bool AddPetTo(UsersPetInfo pet, int place)
    {
        if (base.AddPetTo(pet, place))
        {
            pet.UserID = m_player.PlayerCharacter.ID;
            pet.PetEquips = DeserializePetEquip(pet.eQPets);
            return true;
        }
        return false;
    }

    public bool AddPetForm(int petId)
    {
        if (petId > 0)
        {
            if (m_activeList.Find((PetFormActiveListInfo o) => o.TemplateID == petId) != null)
            {
                return false;
            }
            for (int i = 0; i < ActiveList.Count; i++)
            {
                ActiveList[i].IsFollow = false;
            }
            PetFormActiveListInfo petFormActiveListInfo = new PetFormActiveListInfo();
            petFormActiveListInfo.TemplateID = petId;
            petFormActiveListInfo.IsFollow = true;
            lock (m_lock)
            {
                m_activeList.Add(petFormActiveListInfo);
            }
            return true;
        }
        return false;
    }

    public List<PetFormActiveListInfo> GetAllPetForms()
    {
        List<PetFormActiveListInfo> list = new List<PetFormActiveListInfo>();
        foreach (PetFormActiveListInfo active in ActiveList)
        {
            if (PetMgr.FindPetFormData(active.TemplateID) != null)
            {
                list.Add(active);
            }
        }
        return list;
    }

    public PetFormActiveListInfo GetPetForm(int petId)
    {
        foreach (PetFormActiveListInfo active in ActiveList)
        {
            if (active.TemplateID == petId)
            {
                return active;
            }
        }
        return null;
    }

    public bool PetFormFollow(int petId, bool isFollow)
    {
        for (int i = 0; i < ActiveList.Count; i++)
        {
            ActiveList[i].IsFollow = false;
            if (ActiveList[i].TemplateID == petId)
            {
                ActiveList[i].IsFollow = isFollow;
                m_petForm.PetsID = petId;
            }
        }
        return false;
    }

    public List<PetEquipInfo> DeserializePetEquip(string eqString)
    {
        if (string.IsNullOrEmpty(eqString))
        {
            return new List<PetEquipInfo>();
        }
        List<PetEquipInfo> list = JsonConvert.DeserializeObject<List<PetEquipInfo>>(eqString);
        List<PetEquipInfo> list2 = new List<PetEquipInfo>();
        foreach (PetEquipInfo item in list)
        {
            if (item.Template == null)
            {
                ItemTemplateInfo itemTemplateInfo = ItemMgr.FindItemTemplate(item.eqTemplateID);
                if (itemTemplateInfo != null)
                {
                    PetEquipInfo petEquipInfo = new PetEquipInfo(itemTemplateInfo);
                    petEquipInfo.eqTemplateID = item.eqTemplateID;
                    petEquipInfo.eqType = item.eqType;
                    petEquipInfo.ValidDate = item.ValidDate;
                    petEquipInfo.startTime = item.startTime;
                    petEquipInfo.AttackCompose = item.AttackCompose;
                    petEquipInfo.DefendCompose = item.DefendCompose;
                    petEquipInfo.AgilityCompose = item.AgilityCompose;
                    petEquipInfo.LuckCompose = item.LuckCompose;
                    petEquipInfo.itemID = item.itemID;
                    list2.Add(petEquipInfo);
                }
            }
            else
            {
                list2.Add(item);
            }
        }
        return list2;
    }

    public string SerializePetEquip(List<PetEquipInfo> eqs)
    {
        return JsonConvert.SerializeObject(eqs);
    }

    public void UpdateEatPets()
    {
        m_player.Out.SendEatPetsInfo(EatPets);
    }

    public virtual bool OnChangedPetEquip(int place)
    {
        lock (m_lock)
        {
            if (m_pets[place] != null && m_pets[place].IsEquip)
            {
                m_player.EquipBag.UpdatePlayerProperties();
            }
        }
        OnPlaceChanged(place);
        return true;
    }

    public virtual void ReduceHunger()
    {
        UsersPetInfo petIsEquip = GetPetIsEquip();
        if (petIsEquip == null)
        {
            return;
        }
        int num = 0;
        int num2 = 0;
        if (petIsEquip.Hunger >= num2)
        {
            if (petIsEquip.Level >= 60)
            {
                petIsEquip.Hunger -= num2;
            }
            else
            {
                petIsEquip.Hunger -= num;
            }
            UpdatePet(petIsEquip, petIsEquip.Place);
        }
    }

    public bool CanAdd(ItemInfo item, List<PetEquipInfo> infos)
    {
        if (infos.Count == 3 || item.Template == null)
        {
            return false;
        }
        foreach (PetEquipInfo info in infos)
        {
            if (item.eqType() == info.eqType)
            {
                return false;
            }
        }
        return true;
    }

    public bool CheckEqPetLevel(int place, ItemInfo item)
    {
        UsersPetInfo petAt = GetPetAt(place);
        if (petAt == null)
        {
            return true;
        }
        if (petAt.Level < item.Template.Property2)
        {
            return true;
        }
        return false;
    }

    public bool AddEqPet(int place, ItemInfo item)
    {
        UsersPetInfo petAt = GetPetAt(place);
        if (petAt == null)
        {
            return false;
        }
        if (CanAdd(item, m_pets[place].PetEquips))
        {
            PetEquipInfo item2 = new PetEquipInfo(item.Template)
            {
                eqTemplateID = item.TemplateID,
                eqType = item.eqType(),
                ValidDate = item.ValidDate,
                startTime = item.BeginDate,
                AttackCompose = item.AttackCompose,
                DefendCompose = item.DefendCompose,
                AgilityCompose = item.AgilityCompose,
                LuckCompose = item.LuckCompose,
                itemID = item.ItemID
            };
            petAt.PetEquips.Add(item2);
            return true;
        }
        return false;
    }

    public PetEquipInfo GetEqPet(List<PetEquipInfo> infos, int place)
    {
        foreach (PetEquipInfo info in infos)
        {
            if (info.eqType == place)
            {
                return info;
            }
        }
        return null;
    }

    public bool RemoveEqPet(int petPlace, int eqPlace)
    {
        UsersPetInfo petAt = GetPetAt(petPlace);
        if (petAt == null)
        {
            return false;
        }
        PetEquipInfo eqPet = GetEqPet(petAt.PetEquips, eqPlace);
        if (eqPet != null)
        {
            ChangeEqPetToItem(eqPet);
            return petAt.PetEquips.Remove(eqPet);
        }
        return false;
    }

    public void ChangeEqPetToItem(PetEquipInfo eq)
    {
        if (eq.Template != null)
        {
            ItemInfo itemInfo = ItemInfo.CreateFromTemplate(eq.Template, 1, 105);
            itemInfo.IsBinds = true;
            itemInfo.IsUsed = true;
            itemInfo.ValidDate = eq.ValidDate;
            itemInfo.BeginDate = eq.startTime;
            itemInfo.AttackCompose = eq.AttackCompose;
            itemInfo.DefendCompose = eq.DefendCompose;
            itemInfo.AgilityCompose = eq.AgilityCompose;
            itemInfo.LuckCompose = eq.LuckCompose;
            itemInfo.ItemID = eq.itemID;
            m_player.AddTemplate(itemInfo);
        }
    }

    public void RemoveAllEqPet(List<PetEquipInfo> infos)
    {
        foreach (PetEquipInfo info in infos)
        {
            ChangeEqPetToItem(info);
        }
    }

    public override bool RemovePet(UsersPetInfo pet)
    {
        if (base.RemovePet(pet))
        {
            if (pet.PetEquips != null && pet.PetEquips.Count > 0)
            {
                RemoveAllEqPet(pet.PetEquips);
            }
            lock (m_removedList)
            {
                pet.IsExit = false;
                m_removedList.Add(pet);
            }
            return true;
        }
        return false;
    }

    public override void UpdateChangedPlaces()
    {
        if (m_changedPlaces.Count > 20)
        {
            IEnumerable<IEnumerable<int>> enumerable = m_changedPlaces.ToArray().Split(20);
            foreach (IEnumerable<int> item in enumerable)
            {
                m_player.Out.SendUpdateUserPet(this, item.ToArray());
            }
        }
        else
        {
            m_player.Out.SendUpdateUserPet(this, m_changedPlaces.ToArray());
        }
        base.UpdateChangedPlaces();
    }

    public virtual void ClearAdoptPets()
    {
        using PlayerBussiness playerBussiness = new PlayerBussiness();
        lock (m_lock)
        {
            for (int i = 0; i < base.ACapalility; i++)
            {
                if (m_adoptPets[i] != null && m_adoptPets[i].ID > 0)
                {
                    playerBussiness.ClearAdoptPet(m_adoptPets[i].ID);
                }
                m_adoptPets[i] = null;
            }
        }
    }

    public UsersPetLacrarInfo GetLacreByPlace(int place)
    {
        lock (m_lock)
        {
            foreach (UsersPetLacrarInfo item in lacresPet)
            {
                if (item.place == place)
                {
                    return item;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Tìm ô pet trống đầu tiên trong phạm vi các ô đã mở (0 .. maxExclusive-1).
    /// Dùng cho AddPet/AdoptPet để pet mới không rơi vào ô chưa mở.
    /// </summary>
    public int FindFirstEmptyPetSlot(int maxExclusive)
    {
        lock (m_lock)
        {
            int max = Math.Min(maxExclusive, Capalility);
            for (int i = 0; i < max; i++)
            {
                if (m_pets[i] == null)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Chuẩn hoá SlotPet (số ô pet đã mở) khi login: đảm bảo tối thiểu bằng
    /// số ô miễn phí (config) và không vượt quá số ô tối đa.
    /// </summary>
    private void NormalizeSlotPet()
    {
        try
        {
            PetExtConfigMgr.EnsureLoaded();
            if (m_player == null || m_player.PlayerCharacter == null)
            {
                return;
            }
            int free = PetExtConfigMgr.FreeCellCount;
            if (m_player.PlayerCharacter.SlotPet < free)
            {
                m_player.PlayerCharacter.SlotPet = free;
            }
        }
        catch (Exception exception)
        {
            log.Error("PetInventory.NormalizeSlotPet error userID=" + ((m_player == null || m_player.PlayerCharacter == null) ? 0 : m_player.PlayerCharacter.ID), exception);
        }
    }

    /// <summary>
    /// Mở thêm <paramref name="times"/> ô pet: tăng SlotPet (giới hạn
    /// PetExtConfigMgr.MaxPetCellCount) và lưu ngay vào DB.
    /// Trả về false nếu không tăng được (DB lỗi hoặc đã tối đa).
    /// </summary>
    public bool UnlockPetCells(int times)
    {
        if (times <= 0)
        {
            return false;
        }
        lock (m_lock)
        {
            int oldValue = UnlockedCellCount;
            int maxValue = PetExtConfigMgr.MaxPetCellCount;
            if (oldValue >= maxValue)
            {
                return false;
            }
            int newValue = oldValue + times;
            if (newValue > maxValue)
            {
                newValue = maxValue;
            }
            if (newValue <= oldValue)
            {
                return false;
            }
            try
            {
                m_player.PlayerCharacter.SlotPet = newValue;
                // Ghi DB ngay và kiểm tra kết quả thật (UpdatePlayer trả false nếu SP lỗi).
                using PlayerBussiness playerBussiness = new PlayerBussiness();
                if (!playerBussiness.UpdatePlayer(m_player.PlayerCharacter))
                {
                    m_player.PlayerCharacter.SlotPet = oldValue;
                    log.Error("PetInventory.UnlockPetCells: save DB fail userID=" + m_player.PlayerCharacter.ID + " value=" + newValue);
                    return false;
                }
            }
            catch (Exception exception)
            {
                log.Error("PetInventory.UnlockPetCells error userID=" + m_player.PlayerCharacter.ID, exception);
                try
                {
                    m_player.PlayerCharacter.SlotPet = oldValue;
                }
                catch
                {
                }
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Thêm record trang bị thức tỉnh (itemId -&gt; pet KindID + 2 skill) và ghi DB ngay,
    /// tránh việc saveEqAwakenPet insert trùng về sau.
    /// </summary>
    public void AddAwakenEquipRecord(ItemAwakenPet record)
    {
        if (record == null)
        {
            return;
        }
        bool saved = false;
        try
        {
            using PlayerBussiness playerBussiness = new PlayerBussiness();
            playerBussiness.AddEqAwakenPet(record, m_player.PlayerCharacter.ID);
            saved = true;
        }
        catch (Exception exception)
        {
            log.Error("PetInventory.AddAwakenEquipRecord error userID=" + m_player.PlayerCharacter.ID + " itemID=" + record.itemId, exception);
        }
        record.isSave = saved;
        lock (m_lock)
        {
            if (ItemAwakens == null)
            {
                ItemAwakens = new List<ItemAwakenPet>();
            }
            ItemAwakens.Add(record);
        }
    }

    /// <summary>Tìm record trang bị thức tỉnh theo ItemID (null nếu trang bị chưa thức tỉnh).</summary>
    public ItemAwakenPet GetAwakenRecord(int itemId)
    {
        if (itemId <= 0)
        {
            return null;
        }
        lock (m_lock)
        {
            List<ItemAwakenPet> records = ItemAwakens;
            if (records == null)
            {
                return null;
            }
            for (int i = 0; i < records.Count; i++)
            {
                ItemAwakenPet record = records[i];
                if (record != null && record.itemId == itemId)
                {
                    return record;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Build lại danh sách kỹ năng của 1 pet: kỹ năng gốc theo KindID/Level + 2 skill thức tỉnh
    /// của từng trang bị đang trang bị (chỉ khi record.petId trùng KindID của pet).
    /// Chuỗi Skill trả về dạng "skillId,exclusiveID|..." trong đó exclusiveID = skillId với skill
    /// thức tỉnh và 0 với skill thường — client dựa vào exclusiveID để vẽ viền vàng và battle
    /// dùng AwakenMC (PetSkillBar).
    /// </summary>
    public void RebuildPetSkill(UsersPetInfo pet)
    {
        if (pet == null || m_player == null || m_player.PlayerCharacter == null)
        {
            return;
        }
        try
        {
            string skill = PetMgr.UpdateSkillPet(pet.Level, pet.TemplateID, m_player.Level);
            PetTemplateInfo template = PetMgr.FindPetTemplate(pet.TemplateID);
            int petKind = (template != null) ? template.KindID : -1;
            if (pet.PetEquips != null)
            {
                for (int i = 0; i < pet.PetEquips.Count; i++)
                {
                    PetEquipInfo equip = pet.PetEquips[i];
                    if (equip == null)
                    {
                        continue;
                    }
                    ItemAwakenPet record = GetAwakenRecord(equip.itemID);
                    if (record == null)
                    {
                        continue;
                    }
                    // Chỉ kích hoạt skill thức tỉnh khi trang bị đúng loại pet.
                    if (petKind > 0 && record.petId > 0 && record.petId != petKind)
                    {
                        continue;
                    }
                    skill = PetMgr.UpdateAwakenSkill(skill, pet.TemplateID, record.skillId1, record.skillId2);
                }
            }
            pet.Skill = NormalizeSkillExclusiveIds(skill);
            // Giữ nguyên các ô kỹ năng người chơi đã gắn (chỉ cập nhật ô mở/khóa theo level/VIP
            // và loại bỏ entry của skill không còn trong danh sách kỹ năng, vd skill thức tỉnh
            // vừa bị tháo trang bị). KHÔNG reset về ActiveEquipSkill mặc định.
            pet.SkillEquip = MergeSkillEquip(pet.SkillEquip, pet.Level, m_player.PlayerCharacter.VIPLevel, pet.Skill);
        }
        catch (Exception exception)
        {
            log.Error("PetInventory.RebuildPetSkill error userID=" + m_player.PlayerCharacter.ID + " petID=" + pet.ID, exception);
        }
    }

    /// <summary>Rebuild kỹ năng cho toàn bộ pet (dùng khi login, sau khi nạp trang bị từ DB).</summary>
    public void RebuildAllPetSkills()
    {
        if (m_pets == null)
        {
            return;
        }
        for (int i = 0; i < m_pets.Length; i++)
        {
            if (m_pets[i] != null)
            {
                RebuildPetSkill(m_pets[i]);
            }
        }
    }

    /// <summary>
    /// Chuẩn hoá lại phần thứ 2 của từng entry trong chuỗi Skill thành exclusiveID đúng:
    /// skill thức tỉnh -&gt; skillId (&gt; 0, client vẽ viền vàng), skill thường -&gt; 0.
    /// (Chuỗi do PetMgr.UpdateSkillPet/UpdateAwakenSkill tạo đang dùng index 0,1,2... nên
    /// client không phân biệt được skill thức tỉnh.)
    /// </summary>
    private static string NormalizeSkillExclusiveIds(string skill)
    {
        if (string.IsNullOrEmpty(skill))
        {
            return string.Empty;
        }
        string[] entries = skill.Split('|');
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }
            string[] parts = entry.Split(',');
            int skillId;
            if (!int.TryParse(parts[0], out skillId) || skillId <= 0)
            {
                continue;
            }
            int exclusiveId = PetExtConfigMgr.IsAwakenSkillId(skillId) ? skillId : 0;
            if (builder.Length > 0)
            {
                builder.Append('|');
            }
            builder.Append(skillId).Append(',').Append(exclusiveId);
        }
        return builder.ToString();
    }
}
