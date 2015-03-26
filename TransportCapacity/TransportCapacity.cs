using System;

using ColossalFramework;

using ICities;

namespace TransportCapacity
{
	public class TransportCapacityMod : IUserMod
	{
		#region Implementation of IUserMod

		public string Name { get { return "Transport Capacity"; } }
		public string Description { get { return "Changes the capacity of citizen public transport. A configuration file is available for this mod!"; } }

		#endregion
	}

	public class TransportCapacityLoading : LoadingExtensionBase
	{
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

			if (ModSettings.Instance.ModifyTransportCapacities)
			{
				// Do public transit vehicles
				RunOnPrefabs<VehicleInfo, BusAI>(p => p.m_passengerCapacity = ModSettings.Instance.BusCapacity);
				RunOnPrefabs<VehicleInfo, PassengerPlaneAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerPlaneCapacity);
				RunOnPrefabs<VehicleInfo, PassengerShipAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerShipCapacity);
				RunOnPrefabs<VehicleInfo, PassengerTrainAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity);
				RunOnPrefabs<VehicleInfo, MetroTrainAI>(p => p.m_passengerCapacity = ModSettings.Instance.MetroCapacity);


				// Update already-created stuff with new passenger capacities.
				// Note: this will also update the transport "lines" with new capacities, since it just calls to vehicles under the hood.
				var inst = Singleton<VehicleManager>.instance.m_vehicles;
				for (int i = 0; i < inst.m_buffer.Length; i++)
				{
					RunOnVehicle<BusAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.BusCapacity);
					RunOnVehicle<PassengerPlaneAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.PassengerPlaneCapacity);
					RunOnVehicle<PassengerShipAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.PassengerShipCapacity);
					RunOnVehicle<PassengerTrainAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity);
					RunOnVehicle<MetroTrainAI>(ref inst.m_buffer[i], p => p.m_passengerCapacity = ModSettings.Instance.MetroCapacity);
				}
			}

			base.OnLevelLoaded(mode);
		}
	}
}