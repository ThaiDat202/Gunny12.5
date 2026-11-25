using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;

namespace Game.Server.MagicStone.Handle;

public class MagicStoneHandleMgr
{
	private Dictionary<int, IMagicStoneCommandHadler> handles = new Dictionary<int, IMagicStoneCommandHadler>();

	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	public IMagicStoneCommandHadler LoadCommandHandler(int code)
	{
		if (handles.ContainsKey(code))
		{
			return handles[code];
		}
		log.Error("LoadCommandHandler code020: " + code);
		return null;
	}

	public MagicStoneHandleMgr()
	{
		handles.Clear();
		SearchCommandHandlers(Assembly.GetAssembly(typeof(GameServer)));
	}

	protected int SearchCommandHandlers(Assembly assembly)
	{
		int num = 0;
		Type[] types = assembly.GetTypes();
		foreach (Type type in types)
		{
			if (type.IsClass && !(type.GetInterface("Game.Server.MagicStone.Handle.IMagicStoneCommandHadler") == null))
			{
				MagicStoneHandleAttbute[] array = (MagicStoneHandleAttbute[])type.GetCustomAttributes(typeof(MagicStoneHandleAttbute), inherit: true);
				if (array.Length != 0)
				{
					num++;
					RegisterCommandHandler(array[0].Code, Activator.CreateInstance(type) as IMagicStoneCommandHadler);
				}
			}
		}
		return num;
	}

	protected void RegisterCommandHandler(int code, IMagicStoneCommandHadler handle)
	{
		// Defensive: khong de 1 handler trung code lam hong constructor (vd: 2 class cung [MagicStoneHandleAttbute(N)])
		// vi MagicStoneHandleMgr duoc tao trong GamePlayer..ctor => trung code se khien player KHONG THE LOGIN.
		if (handle == null) return;
		if (handles.ContainsKey(code))
		{
			log.Warn("Duplicate MagicStone handler code " + code + " -> skip " + handle.GetType().Name);
			return;
		}
		handles.Add(code, handle);
	}
}
