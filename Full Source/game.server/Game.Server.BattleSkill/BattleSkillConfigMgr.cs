using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using SqlDataProvider.Data;
using log4net;

namespace Game.Server.BattleSkill
{
    public class BattleSkillNode
    {
        public int ID;
        public int Level;
        public int NextID;
        public int SkillID;
        public int Type;
    }

    public class BattleSkillMaterial
    {
        public int TemplateID;
        public int Count;
        public int OrderID;
    }

    /// <summary>
    /// Chien Ky (battle skill) - doc 2 file request cua client (zlib):
    ///  - fairbattleskillgettemplate.xml   : cay ky nang (ID, Level, NextID, SkillID, Type)
    ///  - fairbattleskillmaterialtemplate.xml: nguyen lieu nang cap (Skill SkillID -> SkillDetail TemplateID/Count/OrderID)
    /// Dung cho pho ban su kien (roomType 123).
    /// </summary>
    public static class BattleSkillConfigMgr
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly object SyncRoot = new object();
        private static bool _loaded;

        private static readonly Dictionary<int, BattleSkillNode> Nodes = new Dictionary<int, BattleSkillNode>();
        private static readonly Dictionary<int, List<BattleSkillMaterial>> Materials = new Dictionary<int, List<BattleSkillMaterial>>();

        public static string RequestDir
        {
            get
            {
                string dir = ConfigurationManager.AppSettings["Request"];
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Request");
                }
                return dir;
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (SyncRoot)
            {
                if (_loaded) return;
                try
                {
                    LoadNodes();
                    LoadMaterials();
                    Log.Warn(string.Format("BattleSkillConfigMgr: loaded {0} skill nodes, {1} material lists", Nodes.Count, Materials.Count));
                }
                catch (Exception ex)
                {
                    Log.Error("BattleSkillConfigMgr load error", ex);
                }
                finally
                {
                    _loaded = true;
                }
            }
        }

        private static XDocument LoadRequestXml(string fileName)
        {
            string path = Path.Combine(RequestDir, fileName);
            if (!File.Exists(path))
            {
                Log.Warn("BattleSkillConfigMgr: missing file " + path);
                return null;
            }
            byte[] raw = File.ReadAllBytes(path);
            byte[] xml;
            try { xml = Game.Base.Marshal.Uncompress(raw); }
            catch { xml = raw; }
            return XDocument.Parse(Encoding.UTF8.GetString(xml));
        }

        private static int GetInt(XElement e, string name, int fallback = 0)
        {
            XAttribute a = e.Attribute(name);
            if (a == null) return fallback;
            int v;
            return int.TryParse(a.Value, out v) ? v : fallback;
        }

        private static void LoadNodes()
        {
            Nodes.Clear();
            XDocument doc = LoadRequestXml("fairbattleskillgettemplate.xml");
            if (doc == null || doc.Root == null) return;
            foreach (XElement e in doc.Root.Descendants("Item"))
            {
                BattleSkillNode node = new BattleSkillNode
                {
                    ID = GetInt(e, "ID"),
                    Level = GetInt(e, "Level"),
                    NextID = GetInt(e, "NextID"),
                    SkillID = GetInt(e, "SkillID"),
                    Type = GetInt(e, "Type")
                };
                if (node.SkillID > 0 && !Nodes.ContainsKey(node.SkillID))
                {
                    Nodes.Add(node.SkillID, node);
                }
            }
        }

        private static void LoadMaterials()
        {
            Materials.Clear();
            XDocument doc = LoadRequestXml("fairbattleskillmaterialtemplate.xml");
            if (doc == null || doc.Root == null) return;
            foreach (XElement skill in doc.Root.Descendants("Skill"))
            {
                int skillId = GetInt(skill, "SkillID");
                if (skillId <= 0) continue;
                List<BattleSkillMaterial> list = new List<BattleSkillMaterial>();
                foreach (XElement d in skill.Descendants("SkillDetail"))
                {
                    list.Add(new BattleSkillMaterial
                    {
                        TemplateID = GetInt(d, "TemplateID"),
                        Count = GetInt(d, "Count"),
                        OrderID = GetInt(d, "OrderID")
                    });
                }
                list.Sort((a, b) => a.OrderID.CompareTo(b.OrderID));
                Materials[skillId] = list;
            }
        }

        public static BattleSkillNode GetNode(int skillId)
        {
            EnsureLoaded();
            BattleSkillNode node;
            return Nodes.TryGetValue(skillId, out node) ? node : null;
        }

        public static BattleSkillNode GetNextNode(int skillId)
        {
            BattleSkillNode node = GetNode(skillId);
            if (node == null || node.NextID <= 0) return null;
            return GetNode(node.NextID);
        }

        public static List<BattleSkillMaterial> GetMaterials(int skillId)
        {
            EnsureLoaded();
            List<BattleSkillMaterial> list;
            return Materials.TryGetValue(skillId, out list) ? list : new List<BattleSkillMaterial>();
        }

        /// <summary>Ky nang chu dong (Probability == -1) hay bi dong (Probability == 10000) theo MountSkillTemplate.</summary>
        public static bool IsActiveSkill(int skillId)
        {
            MountSkillTemplateInfo info = Game.Logic.MountMgr.GetMountSkillTemplate(skillId);
            return info != null && info.Probability == -1;
        }

        public static bool IsPassiveSkill(int skillId)
        {
            MountSkillTemplateInfo info = Game.Logic.MountMgr.GetMountSkillTemplate(skillId);
            return info != null && info.Probability == 10000;
        }

        /// <summary>Room 123 (pho ban su kien) chi cho phep ky nang co GameType == 0 (giong client isFilterByCurRoomType).</summary>
        public static bool IsAllowedInEventRoom(int skillId)
        {
            MountSkillTemplateInfo info = Game.Logic.MountMgr.GetMountSkillTemplate(skillId);
            return info != null && info.GameType == 0;
        }
    }
}
