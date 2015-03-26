using System;

using ApocDev.CitySkylines.Mod.Utils;

using ColossalFramework;

using ICities;

namespace ApocDev.CitySkylines.Mod
{
	public class CoreLoading : LoadingExtensionBase
	{
		private void RunOnPrefabs<TPrefab, TAI>(Action<TAI> runner) where TPrefab : PrefabInfo where TAI : PrefabAI
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

		private void RunOnVehicle<TAI>(ref Vehicle v, Action<TAI> runner) where TAI : VehicleAI
		{
			if (runner == null)
				return;

			var ai = v.Info.GetAI();
			if (ai is TAI)
				runner(ai as TAI);
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

			var inst = Singleton<VehicleManager>.instance.m_vehicles;
			for (int i = 0; i < inst.m_buffer.Length; i++)
			{
				RunOnVehicle<BusAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.BusCapacity);
				RunOnVehicle<PassengerPlaneAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.PassengerPlaneCapacity);
				RunOnVehicle<PassengerShipAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.PassengerShipCapacity);
				RunOnVehicle<PassengerTrainAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity);
				RunOnVehicle<MetroTrainAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.MetroCapacity);
			}

			// So, for the sake of "fun"
			// Lets increase the max citizen count ^^
			// Default size of this array is 0x100000 which is 1,048,576
			// We'll set this to basically 10x that size, at 10 million.
			ArrayUtils.ResizeArray32(Singleton<CitizenManager>.instance.m_citizens, 10000000);
			// TEST: Need to see if we also need to increase m_instance, and m_units.
			// m_units = (0x80000) 524288
			// m_instance = (0x10000) 65536

			//CitizenAIModifications.ApplyWalkingDistanceMod();

			base.OnLevelLoaded(mode);
		}
	}
}