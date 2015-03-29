using System;

using ColossalFramework;
using ColossalFramework.Plugins;

using ICities;

using Object = UnityEngine.Object;

namespace TransportCapacity
{
	public class TransportCapacityLoading : LoadingExtensionBase, IUserMod
	{
		#region IUserMod Members

		public string Name { get { return "Transport Capacity"; } }
		public string Description { get { return "Changes the capacity of citizen public transport. A configuration file is available for this mod!"; } }

		#endregion

		private void RunOnVehicle<TAI>(ref Vehicle v, Action<TAI> runner) where TAI : VehicleAI
		{
			if (runner == null)
			{
				return;
			}

			if (v.Info == null)
				return;

			var ai = v.Info.GetAI() as TAI;
			if (ai != null)
			{
				runner(ai);
			}
		}

		private void CreateNewVehicleAI<TPrefab, TAI>(Action<TAI> runner) where TPrefab : VehicleInfo where TAI : VehicleAI
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
					// Remove the "old" AI
					// And create a new one (literally the same AI type)
					// Then run the "runner" on it which can do things like updating capacities
					// And ticket prices.
					Object.Destroy(prefab.GetComponent<TAI>());
					var newAi = prefab.gameObject.GetComponent<TAI>();

					runner(newAi);

					prefab.m_vehicleAI = newAi;
					newAi.InitializeAI();
				}
			}
		}

		public override void OnLevelLoaded(LoadMode mode)
		{
			//base.OnLevelLoaded(mode);
			//return;
			if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame)
			{
				base.OnLevelLoaded(mode);
				return;
			}

			ModSettings.Load();

			if (ModSettings.Instance.ModifyTransportCapacities)
			{
				// Modify prefabs afterwards so that they get loaded with new default values.
				CreateNewVehicleAI<VehicleInfo, BusAI>(p => p.m_passengerCapacity = ModSettings.Instance.BusCapacity);
				CreateNewVehicleAI<VehicleInfo, PassengerPlaneAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerPlaneCapacity);
				CreateNewVehicleAI<VehicleInfo, PassengerShipAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerShipCapacity);
				CreateNewVehicleAI<VehicleInfo, PassengerTrainAI>(p => p.m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity);
				CreateNewVehicleAI<VehicleInfo, MetroTrainAI>(p => p.m_passengerCapacity = ModSettings.Instance.MetroCapacity);

				
				// Update already-created stuff with new passenger capacities.
				// Note: this will also update the transport "lines" with new capacities, since it just calls to vehicles under the hood.
				var inst = Singleton<VehicleManager>.instance.m_vehicles;
				// Fuck it, update every single vehicle if we can.
				for (int i = 0; i < inst.m_buffer.Length; i++)
				{
					UpdateCreatedVehicle(ref inst.m_buffer[i], (ushort)i);
				}
			}

			base.OnLevelLoaded(mode);
		}

		private void DebugMessage(string message)
		{
			DebugOutputPanel.AddMessage(PluginManager.MessageType.Warning, message);
		}

		private void UpdateCreatedVehicle(ref Vehicle vehicle, ushort vehicleId)
		{
			// Now comes the fun part, we need to create the extra units per-vehicle
			// So that the capacity can be properly raised to the values we want.
			// Actual math here is (passengerCapacity+4)/5 units created, per vehicle.

			int newCapacity = 0;
			RunOnVehicle<BusAI>(ref vehicle, p => newCapacity = p.m_passengerCapacity);
			RunOnVehicle<PassengerPlaneAI>(ref vehicle, p => newCapacity = p.m_passengerCapacity);
			RunOnVehicle<PassengerShipAI>(ref vehicle, p => newCapacity = p.m_passengerCapacity);
			RunOnVehicle<PassengerTrainAI>(ref vehicle, p => newCapacity = p.m_passengerCapacity);
			RunOnVehicle<MetroTrainAI>(ref vehicle, p => newCapacity = p.m_passengerCapacity);


			if (newCapacity != 0)
			{
				// Release the current units, and create new ones. This is what effectively increases the capacity
				// on already-created units.
				// Note: Other mods do not call ReleaseUnits here, which ends up leaking units, and hitting the unit cap
				// Specifically call ReleaseUnits here so that we're not choking the game of possible usable resources.

				uint firstUnitId;
				Singleton<CitizenManager>.instance.ReleaseUnits(vehicle.m_citizenUnits);

				Singleton<CitizenManager>.instance.CreateUnits(out firstUnitId,
					ref SimulationManager.instance.m_randomizer,
					0,
					vehicleId,
					0,
					0,
					0,
					newCapacity,
					0);

				vehicle.m_citizenUnits = firstUnitId;
			}
		}

		/// <summary>
		/// Because CO uses struct refs in their code. This is ever so slightly faster than just replacing the structure at the given index in the m_units array.
		/// </summary>
		/// <param name="unit"></param>
		/// <param name="next"></param>
		void SetNextUnit(ref CitizenUnit unit, uint next)
		{
			unit.m_nextUnit = next;
		}
	}
}