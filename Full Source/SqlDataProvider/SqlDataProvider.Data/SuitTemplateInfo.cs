namespace SqlDataProvider.Data;

public class SuitTemplateInfo
{
	public int SuitId { get; set; }

	public string SuitName { get; set; }

	public int EqipCount1 { get; set; }

	public string SkillDescribe1 { get; set; }

	public string Skill1 { get; set; }

	public int EqipCount2 { get; set; }

	public string SkillDescribe2 { get; set; }

	public string Skill2 { get; set; }

	public int EqipCount3 { get; set; }

	public string SkillDescribe3 { get; set; }

	public string Skill3 { get; set; }

	public int EqipCount4 { get; set; }

	public string SkillDescribe4 { get; set; }

	public string Skill4 { get; set; }

	public int EqipCount5 { get; set; }

	public string SkillDescribe5 { get; set; }

	public string Skill5 { get; set; }

	// ---- Bonus so cho bo trang bi (Cap Nhan / Cap Vong Tay ...) ----
	public int BonusAttack { get; set; }

	public int BonusDefence { get; set; }

	public int BonusAgility { get; set; }

	public int BonusLuck { get; set; }

	public int BonusHP { get; set; }

	public int BonusDamage { get; set; }

	public int BonusArmor { get; set; }

	public int BonusMagicAttack { get; set; }

	public int BonusMagicDefence { get; set; }

	public int BonusReducePercent { get; set; }

	public int MinStrengthen { get; set; }
}
