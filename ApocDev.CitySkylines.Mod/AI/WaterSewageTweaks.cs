using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ColossalFramework;

using UnityEngine;

namespace ApocDev.CitySkylines.Mod.AI
{
	// The idea here is to tweak how water and sewage work.
	// Currently, you plop down an intake, and outflow building, and then drop pipes like dipshits.

	// This will change that so that water can only fill so many "cells" of a pipe (radially from the pump location)
	// Multiple pumps on the same pipe start location increase the water flow by a factor of 0.75x so that you can't constantly drop new stations when water
	// is an issue. You'll have to plan ahead.
	class WaterSewageTweaks : WaterPipeAI
	{
		#region Overrides of WaterPipeAI

		#region Overrides of PlayerNetAI

		public override int GetConstructionCost()
		{
			return base.GetConstructionCost();
		}

		#endregion

		public override void GetEffectRadius(out float radius, out bool capped, out Color color)
		{
			base.GetEffectRadius(out radius, out capped, out color);

			// Change the radius to something much smaller. (1/2 of the current value, to force you to spend more money on laying pipes)
			radius = 45f;
		}
		public override void UpdateNode(ushort nodeID, ref NetNode data)
		{
			if ((data.m_flags & NetNode.Flags.Untouchable) != NetNode.Flags.None)
			{
				ushort index = NetNode.FindOwnerBuilding(nodeID, 32f);
				if (index != 0)
				{
					BuildingManager buildingManager = Singleton<BuildingManager>.instance;
					Notification.Problem oldProblems = buildingManager.m_buildings.m_buffer[index].m_problems;
					Notification.Problem newProblems;
					if (data.CountSegments() != 0)
					{
						newProblems = Notification.RemoveProblems(oldProblems, Notification.Problem.WaterNotConnected);
					}
					else
					{
						newProblems = Notification.AddProblems(oldProblems, Notification.Problem.WaterNotConnected);
					}
					if (newProblems != oldProblems)
					{
						buildingManager.m_buildings.m_buffer[index].m_problems = newProblems;
						buildingManager.UpdateNotifications(index, oldProblems, newProblems);
					}
				}
				data.m_problems = Notification.RemoveProblems(data.m_problems, Notification.Problem.WaterNotConnected);
			}
			float minX = data.m_position.x - 100f;
			float maxX = data.m_position.x + 100f;
			float minZ = data.m_position.z - 100f;
			float maxZ = data.m_position.z + 100f;
			Singleton<WaterManager>.instance.UpdateGrid(minX, minZ, maxX, maxZ);
		}



		#endregion
	}
}
