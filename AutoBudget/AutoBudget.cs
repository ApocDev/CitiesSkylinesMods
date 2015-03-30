using System;
using System.Diagnostics;

using ColossalFramework;
using ColossalFramework.Plugins;

using ICities;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace AutoBudget
{
	public class BudgetMod : IUserMod
	{
		#region Implementation of IUserMod

		public string Name { get { return "AutoBudget"; } }
		public string Description { get { return "Automatically sets budgets to the lowest possible value to maintain a 'green' status for electricity and water/sewage."; } }

		#endregion
	}

	public class AutoBudget : ThreadingExtensionBase
	{
		public static int GetProductionRate(int productionRate, int budget)
		{
			if (budget < 100)
				budget = (budget * budget + 99) / 100;
			else if (budget > 150)
				budget = 125;
			else if (budget > 100)
				budget -= (100 - budget) * (100 - budget) / 100;
			return (productionRate * budget + 99) / 100;
		}

		private void DebugMessage(string message)
		{
			Debug.Log(message);
			//DebugOutputPanel.AddMessage(PluginManager.MessageType.Warning, message);
		}

		private delegate int CapacityGetter<in TAI>(ref Building building, TAI ai);
		int GetTotalCapacity<TAI>(ItemClass.Service service, CapacityGetter<TAI> getter) where TAI: PlayerBuildingAI
		{
			int capacity = 0;

			var serviceBuildings = BuildingManager.instance.GetServiceBuildings(service);

			if (serviceBuildings == null || serviceBuildings.m_buffer == null || serviceBuildings.m_size > serviceBuildings.m_buffer.Length)
				return 0;

			var buildings = serviceBuildings.m_buffer;
			for (int i = 0; i < serviceBuildings.m_size; i++)
			{
				if (buildings[i] == 0)
					continue;
				var b = BuildingManager.instance.m_buildings.m_buffer[buildings[i]];

				// Building is not active. Ignore it.
				if ((b.m_flags & Building.Flags.Active) == 0)
					continue;
				var ai = b.Info.m_buildingAI as TAI;
				if (ai != null)
				{
					capacity += getter(ref BuildingManager.instance.m_buildings.m_buffer[buildings[i]], ai);
				}
			}
			return capacity;
		}

		private void UpdateBudget(EconomyManager eco, ref District district, ItemClass.Service service, ItemClass.SubService subService = ItemClass.SubService.None)
		{
			try
			{
				int capacity = 0;
				int consumption = 0;
				switch (service)
				{
					case ItemClass.Service.Electricity:
						//capacity = (int) district.m_productionData.m_tempElectricityCapacity * 16;
						capacity = GetTotalCapacity(ItemClass.Service.Electricity,
							(ref Building data, PowerPlantAI ai) =>
							{
								int min;
								int max;
								ai.GetElectricityProduction(out min, out max);
								if (ai is WindTurbineAI)
								{
									// Get the wind for the specific area.
									var turbineProduction = Mathf.RoundToInt(PlayerBuildingAI.GetProductionRate(data.m_productionRate, 100) * WindManager.instance.SampleWindSpeed(data.m_position));
									return turbineProduction * max / 100;
								}
								if (ai is DamPowerHouseAI)
								{
									var damProduction = (data.m_productionRate * 100 / 100 * data.GetLastFrameData().m_productionState + 99) / 100;
									return damProduction * max / 100;
								}
								if (ai is FusionPowerPlantAI)
								{
									max = 1000000;
								}
								int a;
								var productionRate = data.m_productionRate;
								if ((data.m_flags & Building.Flags.Active) != Building.Flags.None)
								{
									a = PlayerBuildingAI.GetProductionRate(productionRate, 100);
									if (ai.m_resourceType != TransferManager.TransferReason.None)
									{
										int num = (int) data.m_customBuffer1;
										a = Mathf.Min(a, num / (ai.m_resourceCapacity / 400) + 10);
									}
								}
								else
									a = 0;

								return a * max / 100;
							}) * 16;


						// Now check for incinerators, as they also add to our max capacity!!!
						capacity += GetTotalCapacity(ItemClass.Service.Garbage,
							(ref Building data, LandfillSiteAI ai) =>
							{
								if (ai.m_electricityProduction != 0)
								{
									var ret =
										(Mathf.Min(PlayerBuildingAI.GetProductionRate(data.m_productionRate, 100),
											(data.m_customBuffer1 * 1000 + data.m_garbageBuffer) / (ai.m_garbageCapacity / 200)) * ai.m_electricityProduction + 99) / 100;
									return ret;
								}
								return 0;
							}) * 16;

						consumption = district.GetElectricityConsumption();
						break;
					case ItemClass.Service.Water:
						var sewageConsumption = district.GetSewageAccumulation();
						var waterConsumption = district.GetWaterConsumption();
						var waterCapacity = GetTotalCapacity(ItemClass.Service.Water,
							(ref Building data, WaterFacilityAI ai) =>
							{
								if (ai.m_waterIntake == 0)
									return 0;

								if (ai.m_useGroundWater || data.m_waterSource != 0)
									return ai.m_waterIntake * data.m_productionRate / 100;
								return 0;
							}) * 16;
						var sewageCapacity = GetTotalCapacity(ItemClass.Service.Water,
							(ref Building data, WaterFacilityAI ai) =>
							{
								if (ai.m_sewageOutlet == 0)
									return 0;

								if (ai.m_useGroundWater || data.m_waterSource != 0)
									return ai.m_sewageOutlet * data.m_productionRate / 100;
								return 0;
							}) * 16;

						// Use the higher consumption here so we get accurate results
						// The water/sewage is tied together on the same budget.
						if (GetPercentage(waterCapacity, waterConsumption) > GetPercentage(sewageCapacity, sewageConsumption))
						{
							capacity = sewageCapacity;
							consumption = sewageConsumption;
						}
						else
						{
							capacity = waterCapacity;
							consumption = waterConsumption;
						}

						break;
					case ItemClass.Service.Garbage:
						capacity = (int) district.m_productionData.m_tempIncinerationCapacity * 16;
						consumption = district.GetGarbageAccumulation();
						break;
				}


				if (capacity == 0 || consumption == 0)
					return;

				int budget;
				for (budget = 1; budget < 150; budget++)
				{
					if (GetProductionRate(capacity, budget) >= consumption)
						break;
				}

				// How much of our capacity do we need to meet our demands?
				// This is odd for water specifically.
				var neededUsage = (int)Math.Ceiling(((float)consumption / capacity) * 100);
				DebugMessage("Service: " + service + ", Capacity: " + capacity + ", Consumption: " + consumption + ", Budget: " + budget +", Needed: " + neededUsage);

				// Add 2% to the required amount so we don't end up with crazy stuff happening
				// When there are big "booms" in player buildings.
				eco.SetBudget(service, subService, Clamp(budget + 2, 10, 150));
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}

		private int Clamp(int val, int min, int max)
		{
			if (val < min)
			{
				val = min;
			}
			if (val > max)
			{
				val = max;
			}
			return val;
		}
		private float GetPercentage(int capacity, int consumption, int consumptionMin = 45, int consumptionMax = 55)
		{
			if (capacity == 0)
			{
				return 0f;
			}
			float pct = capacity / (float)consumption;
			float mod = (consumptionMin + (float)consumptionMax) / 2;
			return pct * mod;
		}

		#region Overrides of ThreadingExtensionBase

		private readonly Stopwatch _throttle = Stopwatch.StartNew();

		public override void OnAfterSimulationTick()
		{
			base.OnAfterSimulationTick();

			// Probably not in a normal game type if the district manager doesn't actually exist.
			if (!Singleton<DistrictManager>.exists)
			{
				return;
			}

			if (_throttle.ElapsedMilliseconds < 1000)
			{
				return;
			}

			var eco = Singleton<EconomyManager>.instance;

			UpdateBudget(eco, ref Singleton<DistrictManager>.instance.m_districts.m_buffer[0], ItemClass.Service.Electricity);
			UpdateBudget(eco, ref Singleton<DistrictManager>.instance.m_districts.m_buffer[0], ItemClass.Service.Water);

			_throttle.Reset();
			_throttle.Start();
		}

		#endregion
	}
}