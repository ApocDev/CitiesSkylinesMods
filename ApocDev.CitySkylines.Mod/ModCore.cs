using System;
using System.Reflection;
using System.Runtime.InteropServices;

using ApocDev.CitySkylines.Mod.MonoHooks;

using ColossalFramework;

using ICities;

namespace ApocDev.CitySkylines.Mod
{
#if !MOD_FIRESPREAD
	public class ModCore : IUserMod
	{
		public string Name { get { return "ApocDev's Mods"; } }
		public string Description { get { return "A collection of modifications to the game to make the experience all the more better!"; } }
	}
#elif MOD_FIRESPREAD
	public class ModCore : IUserMod
	{
		public string Name { get { return "Fire Spread"; } }
		public string Description { get { return "Because one fire isn't nearly enough."; } }
	}
#endif


#if !MOD_FIRESPREAD
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
		void ResizeArray32<T>(Array32<T> array, uint newSize)
		{
			array.m_size = newSize;
			Array.Resize(ref array.m_buffer, (int)newSize);

			var unusedCount = (uint)array.GetType().GetField("m_unusedCount").GetValue(array);
			var unusedItems = (uint[])array.GetType().GetField("m_unusedItems").GetValue(array);

			uint[] newUnusedItems = new uint[newSize];
			Buffer.BlockCopy(unusedItems, 0, newUnusedItems, 0, 4 * unusedItems.Length);

			// Now add our own unused items
			for (uint i = (uint)unusedItems.Length; i < newSize + 1; i++)
			{
				newUnusedItems[i - 1] = i;
			}

			// Update the unusedCount to be in line with the new array size
			// This is just adding the newly sized additions.
			unusedCount += newSize - unusedCount;

			array.GetType().GetField("m_unusedCount").SetValue(array, unusedCount);
			array.GetType().GetField("m_unusedItems").SetValue(array, newUnusedItems);
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
			ResizeArray32(Singleton<CitizenManager>.instance.m_citizens, 10000000);

			// Need to now resize the "unused" buffers

			
			//CitizenAIModifications.ApplyWalkingDistanceMod();

			base.OnLevelLoaded(mode);
		}

	}
#endif
}
