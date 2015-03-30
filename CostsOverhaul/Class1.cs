using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ICities;

namespace CostsOverhaul
{
    public class CostsOverhaulMod:LoadingExtensionBase, IUserMod
    {
	    #region Implementation of IUserMod

	    public string Name { get { return "Costs Overhaul"; } }
	    public string Description { get { return "Overhauls the cost, and maintenance of everything in C:SL to more appropriate values."; } }

	    #endregion

	    public void SetServicesCosts()
	    {
	    }

		private void RunOnPrefabs<TPrefab, TAI>(Action<TAI> runner)
			where TPrefab : PrefabInfo
			where TAI : PrefabAI
		{
			if (runner == null)
			{
				return;
			}

			var prefabCount = PrefabCollection<TPrefab>.PrefabCount();
			for (uint i = 0; i < prefabCount; i++)
			{
				var prefab = PrefabCollection<TPrefab>.GetPrefab(i);
				var ai = prefab.GetAI();
				if (ai is TAI)
				{
					runner(ai as TAI);
				}
			}
		}
    }
}
