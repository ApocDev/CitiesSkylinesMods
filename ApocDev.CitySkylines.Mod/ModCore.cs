using System;
using System.Reflection;
using System.Runtime.InteropServices;

using ApocDev.CitySkylines.Mod.MonoHooks;
using ApocDev.CitySkylines.Mod.Utils;

using ColossalFramework;

using ICities;

namespace ApocDev.CitySkylines.Mod
{
	public class ModCore : IUserMod
	{
		public string Name { get { return "ApocDev's Mods"; } }
		public string Description { get { return "A collection of modifications to the game to make the experience all the more better!"; } }
	}


	public class CoreLoading : LoadingExtensionBase
	{
		private void RunOnPrefabs<TPrefab, TAI>(Action<TAI> runner) where TPrefab : PrefabInfo where TAI : PrefabAI
		{
			if (runner == null)
				return;

			var prefabCount = PrefabCollection<TPrefab>.PrefabCount();
			for (uint i = 0; i < prefabCount; i++)
			{
				var prefab = PrefabCollection<TPrefab>.GetPrefab(i);
				var ai = prefab.GetAI();
				if (ai is TAI)
					runner(ai as TAI);
			}
		}


		public override void OnLevelLoaded(LoadMode mode)
		{
			if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame)
			{
				base.OnLevelLoaded(mode);
				return;
			}

			if (ModSettings.Instance.ModifyCapacities)
			{
				// Do public transit vehicles
				RunOnPrefabs<VehicleInfo, BusAI>(p => p.m_passengerCapacity = ModSettings.Instance.BusCapacity);
				RunOnPrefabs<VehicleInfo, PassengerPlaneAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerPlaneCapacity);
				RunOnPrefabs<VehicleInfo, PassengerShipAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerShipCapacity);
				RunOnPrefabs<VehicleInfo, PassengerTrainAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity);
				RunOnPrefabs<VehicleInfo, MetroTrainAI>(p => p.m_passengerCapacity = ModSettings.Instance.MetroCapacity);
			}

			// So, for the sake of "fun"
			// Lets increase the max citizen count ^^
			ArrayUtils.ResizeArray32(Singleton<CitizenManager>.instance.m_citizens, 10000000);

			// Need to now resize the "unused" buffers

			
			//CitizenAIModifications.ApplyWalkingDistanceMod();

			base.OnLevelLoaded(mode);
		}

	}
}
