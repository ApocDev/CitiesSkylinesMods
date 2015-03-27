using System;

using ColossalFramework;
using ColossalFramework.Plugins;

using ICities;

namespace TransportCapacity
{
	public class TransportCapacityLoading : LoadingExtensionBase, IUserMod
	{
		#region IUserMod Members

		public string Name { get { return "Transport Capacity"; } }
		public string Description { get { return "Changes the capacity of citizen public transport. A configuration file is available for this mod!"; } }

		#endregion

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
			{
				return;
			}

			var ai = v.Info.GetAI();
			if (ai is TAI)
			{
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

				var transport = Singleton<TransportManager>.instance;
				for (int i = 0; i < transport.m_lines.m_size; i++)
				{
					var line = transport.m_lines.m_buffer[i];

					var vehicles = line.m_vehicles;
					int iterCount = 0;

					// So each line has "vehicles" that also have sub-vehicles in the case of train/metro
					// And then it has a "pointer" to a next vehicle in the line.
					// m_vehicles is the "first" vehicle for the transport line
					while (vehicles != 0)
					{
						// So, while CO's API is already pretty bad in terms of "good practice" C#
						// The need to create tiny wrapper funcs so we can get structures as a by-ref is quite a pain.
						// I understand the need for it, since they act as "bulk storage" with high throughput
						// But at the same time you wind up with an API that is clumsy and bulky in the long run.
						// Oh well, nothing I can do about it now.
						UpdateTransportLineVehicle(ref inst.m_buffer[vehicles], line.m_lineNumber);

						var nextLineVehicle = inst.m_buffer[vehicles].m_nextLineVehicle;

						vehicles = nextLineVehicle;

						if (++iterCount > 0x4000)
						{
							break;
						}
					}
				}
			}

			base.OnLevelLoaded(mode);
		}

		private void DebugMessage(string message)
		{
			DebugOutputPanel.AddMessage(PluginManager.MessageType.Warning, message);
		}

		private void UpdateTransportLineVehicle(ref Vehicle vehicle, ushort lineNumber)
		{
			RunOnVehicle<BusAI>(ref vehicle,
				p =>
				{
					p.m_passengerCapacity = ModSettings.Instance.BusCapacity;
					DebugMessage("Updated bus to capacity " + ModSettings.Instance.BusCapacity + " on line " + lineNumber);
				});
			RunOnVehicle<PassengerPlaneAI>(ref vehicle,
				p =>
				{
					p.m_passengerCapacity = ModSettings.Instance.PassengerPlaneCapacity;
					DebugMessage("Updated passenger plane to capacity " + ModSettings.Instance.PassengerPlaneCapacity + " on line " + lineNumber);
				});
			RunOnVehicle<PassengerShipAI>(ref vehicle,
				p =>
				{
					p.m_passengerCapacity = ModSettings.Instance.PassengerShipCapacity;
					DebugMessage("Updated passenger ship to capacity " + ModSettings.Instance.PassengerShipCapacity + " on line " + lineNumber);
				});
			RunOnVehicle<PassengerTrainAI>(ref vehicle,
				p =>
				{
					p.m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity;
					if (p.m_info.m_trailers != null && p.m_info.m_trailers.Length > 0)
					{
						foreach (var trailer in p.m_info.m_trailers)
						{
							if (trailer.m_info.m_class.m_service == ItemClass.Service.PublicTransport && trailer.m_info.m_class.m_subService == ItemClass.SubService.PublicTransportTrain)
							{
								(trailer.m_info.m_vehicleAI as PassengerTrainAI).m_passengerCapacity = ModSettings.Instance.PassengerTrainCapacity;
								DebugMessage("Updated passenger train to capacity " + ModSettings.Instance.PassengerTrainCapacity + " on line " + lineNumber);
							}
						}
					}
				});
			RunOnVehicle<MetroTrainAI>(ref vehicle,
				p =>
				{
					p.m_passengerCapacity = ModSettings.Instance.MetroCapacity;
					if (p.m_info.m_trailers != null && p.m_info.m_trailers.Length > 0)
					{
						foreach (var trailer in p.m_info.m_trailers)
						{
							if (trailer.m_info.m_class.m_service == ItemClass.Service.PublicTransport && trailer.m_info.m_class.m_subService == ItemClass.SubService.PublicTransportMetro)
							{
								(trailer.m_info.m_vehicleAI as MetroTrainAI).m_passengerCapacity = ModSettings.Instance.MetroCapacity;
								DebugMessage("Updated passenger train to capacity " + ModSettings.Instance.MetroCapacity + " on line " + lineNumber);
							}
						}
					}
				});
		}
	}
}