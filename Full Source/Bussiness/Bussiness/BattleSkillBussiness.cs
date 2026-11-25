using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using log4net;

namespace Bussiness
{
    /// <summary>
    /// Chien Ky (Battle Skill) cua pho ban su kien - luu trong Db_Tank.Sys_Users_BattleSkill.
    /// Moi dong: 1 ky nang da mo (SkillID) + slot trang bi (0 = chua trang bi, 1..3 chu dong, 4..6 bi dong).
    /// </summary>
    public class BattleSkillBussiness : BaseBussiness
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Dictionary<int, int> GetSkills(int userId)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();
            SqlDataReader reader = null;
            try
            {
                SqlParameter[] ps = new SqlParameter[1] { new SqlParameter("@UserID", userId) };
                db.GetReader(ref reader, "SP_Users_BattleSkill_Get", ps);
                while (reader.Read())
                {
                    int skillId = Convert.ToInt32(reader["SkillID"]);
                    int slot = Convert.ToInt32(reader["Slot"]);
                    if (!result.ContainsKey(skillId)) result.Add(skillId, slot);
                }
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("GetBattleSkills", ex);
            }
            finally
            {
                if (reader != null && !reader.IsClosed) reader.Close();
            }
            return result;
        }

        public bool SaveSkill(int userId, int skillId, int slot)
        {
            try
            {
                SqlParameter[] ps = new SqlParameter[3]
                {
                    new SqlParameter("@UserID", userId),
                    new SqlParameter("@SkillID", skillId),
                    new SqlParameter("@Slot", slot)
                };
                return db.RunProcedure("SP_Users_BattleSkill_Save", ps);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("SaveBattleSkill", ex);
                return false;
            }
        }

        public bool UpgradeSkill(int userId, int oldSkillId, int newSkillId)
        {
            try
            {
                SqlParameter[] ps = new SqlParameter[3]
                {
                    new SqlParameter("@UserID", userId),
                    new SqlParameter("@OldSkillID", oldSkillId),
                    new SqlParameter("@NewSkillID", newSkillId)
                };
                return db.RunProcedure("SP_Users_BattleSkill_Upgrade", ps);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("UpgradeBattleSkill", ex);
                return false;
            }
        }
    }
}
