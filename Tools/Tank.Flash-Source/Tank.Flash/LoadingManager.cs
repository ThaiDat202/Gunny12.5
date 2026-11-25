using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;

namespace Tank.Flash;

public class LoadingManager
{
	private class PlayerData
	{
		public string Name;

		public string Pass;

		public string Key;

		public DateTime Date;

		public int Count;

		public bool IsAdmin;
	}

	public static int LoadingCount = 0;

	private static Dictionary<string, PlayerData> m_players = new Dictionary<string, PlayerData>();

	private static object sys_obj = new object();

	private static Timer m_timer;

	private static int m_timeout = 30;

	public static void Setup()
	{
		m_timeout = int.Parse(ConfigurationManager.AppSettings["LoginSessionTimeOut"]);
		m_timer = new Timer(CheckTimerCallback, null, 0, 60000);
	}

	protected static bool CheckTimeOut(DateTime dt)
	{
		return (DateTime.Now - dt).TotalMinutes > (double)m_timeout;
	}

	private static void CheckTimerCallback(object state)
	{
		lock (sys_obj)
		{
			List<string> list = new List<string>();
			foreach (PlayerData value in m_players.Values)
			{
				if (CheckTimeOut(value.Date))
				{
					list.Add(value.Name);
				}
			}
			foreach (string item in list)
			{
				m_players.Remove(item);
			}
		}
	}

	public static void Add(string name, string pass)
	{
		Add(name, pass, isAdmin: false);
	}

	public static void Add(string name, string pass, bool isAdmin)
	{
		lock (sys_obj)
		{
			if (m_players.ContainsKey(name))
			{
				m_players[name].Name = name;
				m_players[name].Pass = pass;
				m_players[name].Date = DateTime.Now;
				m_players[name].Count = 0;
				m_players[name].IsAdmin = isAdmin;
			}
			else
			{
				PlayerData playerData = new PlayerData();
				playerData.Name = name;
				playerData.Pass = pass;
				playerData.Date = DateTime.Now;
				playerData.IsAdmin = isAdmin;
				m_players.Add(name, playerData);
			}
		}
	}

	public static bool Login(string name, string pass)
	{
		lock (sys_obj)
		{
			if (m_players.ContainsKey(name) && m_players[name].Pass == pass)
			{
				PlayerData playerData = m_players[name];
				if (playerData.Pass == pass && !CheckTimeOut(playerData.Date))
				{
					return true;
				}
				return false;
			}
			return false;
		}
	}

	public static bool Update(string name, string pass)
	{
		lock (sys_obj)
		{
			if (m_players.ContainsKey(name))
			{
				m_players[name].Pass = pass;
				m_players[name].Count++;
				return true;
			}
		}
		return false;
	}

	public static bool UpdateKey(string name, string key)
	{
		lock (sys_obj)
		{
			if (m_players.ContainsKey(name))
			{
				m_players[name].Key = key;
				return true;
			}
		}
		return false;
	}

	public static bool Remove(string name)
	{
		lock (sys_obj)
		{
			return m_players.Remove(name);
		}
	}

	public static bool CheckUser(string name)
	{
		lock (sys_obj)
		{
			if (m_players.ContainsKey(name))
			{
				return true;
			}
		}
		return false;
	}

	public static bool GetByUserIsFirst(string name)
	{
		lock (sys_obj)
		{
			if (m_players.ContainsKey(name))
			{
				return m_players[name].Count == 0;
			}
		}
		return false;
	}
}
