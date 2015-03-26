using System;
using System.Diagnostics;

using ColossalFramework;

using ICities;

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
		private float GetPercentage(int capacity, int consumption, int consumptionMin = 45, int consumptionMax = 55)
		{
			if (capacity == 0)
			{
				return 0f;
			}
			float pct = capacity / (float) consumption;
			float mod = (consumptionMin + (float) consumptionMax) / 2;
			return pct * mod;
		}

		private void UpdateBudget(EconomyManager eco, ref District district, ItemClass.Service service, ItemClass.SubService subService = ItemClass.SubService.None)
		{
			var electricBudget = eco.GetBudget(service, subService);

			float usage = 0f;
			switch (service)
			{
				case ItemClass.Service.Electricity:
					usage = GetPercentage(district.GetElectricityCapacity(), district.GetElectricityConsumption());
					break;
				case ItemClass.Service.Water:
					var waterUsage = GetPercentage(district.GetWaterCapacity(), district.GetWaterConsumption());
					var sewageUsage = GetPercentage(district.GetSewageCapacity(), district.GetSewageAccumulation());

					// Use the lowest of the two usages. Since they're not tied to eachother as far as the network goes
					// But they are tied together for the budget.
					usage = Math.Min(waterUsage, sewageUsage);
					break;
			}

			if (usage < 55)
			{
				eco.SetBudget(service, subService, Clamp(electricBudget + 2, 50, 150));
			}
			else if (usage > 58)
			{
				eco.SetBudget(service, subService, Clamp(electricBudget - 1, 50, 150));
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