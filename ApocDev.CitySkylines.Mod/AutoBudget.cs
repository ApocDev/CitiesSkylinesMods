using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ColossalFramework;

using ICities;

namespace ApocDev.CitySkylines.Mod
{
	public class BudgetMod : IUserMod
	{
		#region Implementation of IUserMod

		public string Name { get { return "AutoBudget"; } }
		public string Description { get { return "Automatically sets budgets to the lowest possible value to maintain a 'green' status."; } }

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
			float num = ((float)capacity) / ((float)consumption);
			float num2 = (consumptionMin + consumptionMax) / 2;
			return (num * num2);
		}


		

		#region Overrides of ThreadingExtensionBase

		public override void OnAfterSimulationTick()
		{
			base.OnAfterSimulationTick();

			// Probably not in a normal game type if the district manager doesn't actually exist.
			if (!Singleton<DistrictManager>.exists)
				return;


			var district = Singleton<DistrictManager>.instance.m_districts.m_buffer[0];
			var eco = Singleton<EconomyManager>.instance;
			var electricBudget = eco.GetBudget(ItemClass.Service.Electricity, ItemClass.SubService.None);
			var electricUsage = GetPercentage(district.GetElectricityCapacity(), district.GetElectricityConsumption());

			if (electricUsage < 55)
			{
				eco.SetBudget(ItemClass.Service.Electricity, ItemClass.SubService.None, electricBudget + 5);
			}
			else
			{
				eco.SetBudget(ItemClass.Service.Electricity, ItemClass.SubService.None, electricBudget - 1);
			}
		}

		#endregion
	}
}
