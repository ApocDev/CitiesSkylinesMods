using System;
using System.Collections.Generic;

using ColossalFramework;
using ColossalFramework.Math;

using UnityEngine;

namespace ApocDev.CitySkylines.Mod.AI
{
#if !MOD_FIRESPREAD
	internal class CommercialBuildingAIMod : CommercialBuildingAI
	{
		protected override void SimulationStepActive(ushort buildingID, ref Building buildingData, ref Building.Frame frameData)
		{
			base.SimulationStepActive(buildingID, ref buildingData, ref frameData);
			if (buildingData.m_fireIntensity != 0 && frameData.m_fireDamage > 12)
			{
				ResidentialBuildingAIMod.DoSpreadFire(buildingID, ref buildingData, 50, m_info.m_size.y);
			}
		}
	}

	public class IndustrialBuildingAIMod : IndustrialBuildingAI
	{
		// Methods
		protected override void SimulationStepActive(ushort buildingID, ref Building buildingData, ref Building.Frame frameData)
		{
			base.SimulationStepActive(buildingID, ref buildingData, ref frameData);
			if (buildingData.m_fireIntensity != 0 && frameData.m_fireDamage > 12)
			{
				ResidentialBuildingAIMod.DoSpreadFire(buildingID, ref buildingData, 64, m_info.m_size.y);
			}
		}
	}

	public class ResidentialBuildingAIMod : ResidentialBuildingAI
	{
		// Fields
		private static Queue<ushort> fireQueue = new Queue<ushort>();

		public static void DoSpreadFire(ushort buildingID, ref Building buildingData, int damageAccumulation, float sizeY)
		{
			Quad2 quad;
			int width = buildingData.Width;
			int length = buildingData.Length;
			Vector2 pos2D = VectorUtils.XZ(buildingData.m_position);
			Vector2 rot2D = new Vector2(Mathf.Cos(buildingData.m_angle), Mathf.Sin(buildingData.m_angle)) * 8f;
			Vector2 forward2D = new Vector2(rot2D.y, -rot2D.x);
			quad.a = (pos2D - (((width * 0.5f) + 1.5f) * rot2D)) - (((length * 0.5f) + 1.5f) * forward2D);
			quad.b = (pos2D + (((width * 0.5f) + 1.5f) * rot2D)) - (((length * 0.5f) + 1.5f) * forward2D);
			quad.c = (pos2D + (((width * 0.5f) + 1.5f) * rot2D)) + (((length * 0.5f) + 1.5f) * forward2D);
			quad.d = (pos2D - (((width * 0.5f) + 1.5f) * rot2D)) + (((length * 0.5f) + 1.5f) * forward2D);
			Vector2 min = quad.Min();
			Vector2 max = quad.Max();
			min.y -= buildingData.m_baseHeight;
			max.y += sizeY;
			int gridMinX = Mathf.Max((int) (((min.x - 72f) / 64f) + 135f), 0);
			int gridMinZ = Mathf.Max((int) (((min.y - 72f) / 64f) + 135f), 0);
			int gridMaxX = Mathf.Min((int) (((max.x + 72f) / 64f) + 140f), 269);
			int gridMaxZ = Mathf.Min((int) (((max.y + 72f) / 64f) + 140f), 269);
			BuildingManager instance = Singleton<BuildingManager>.instance;
			for (int i = gridMinZ; i <= gridMaxZ; i++)
			{
				for (int j = gridMinX; j <= gridMaxX; j++)
				{
					ushort nextGridBuilding = instance.m_buildingGrid[(i * 270) + j];
					int iterCount = 0;
					while (nextGridBuilding != 0)
					{
						if (nextGridBuilding != buildingID && Singleton<SimulationManager>.instance.m_randomizer.Int32(100) < damageAccumulation)
						{
							SpreadFire(quad, min.y, max.y, nextGridBuilding, ref instance.m_buildings.m_buffer[nextGridBuilding]);
						}
						nextGridBuilding = instance.m_buildings.m_buffer[nextGridBuilding].m_nextGridBuilding;
						if (++iterCount >= 32768)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
		}

		public static void SpreadFire(Quad2 quad, float minY, float maxY, ushort buildingID, ref Building buildingData)
		{
			int num;
			int num2;
			int num3;
			BuildingInfo info = buildingData.Info;
			info.m_buildingAI.GetFireParameters(buildingID, ref buildingData, out num, out num2, out num3);
			if (num != 0 && ((buildingData.m_flags & (Building.Flags.Abandoned | Building.Flags.Completed)) == Building.Flags.Completed) &&
			    ((buildingData.m_fireIntensity == 0) && (buildingData.GetLastFrameData().m_fireDamage == 0)) && buildingData.OverlapQuad(buildingID, quad, minY, maxY) &&
			    !fireQueue.Contains(buildingID))
			{
				fireQueue.Enqueue(buildingID);
				if (fireQueue.Count > 24)
				{
					fireQueue.Dequeue();
				}
				float introduced7 = Singleton<TerrainManager>.instance.WaterLevel(VectorUtils.XZ(buildingData.m_position));
				if (introduced7 <= buildingData.m_position.y)
				{
					Building.Flags flags = buildingData.m_flags;
					buildingData.m_fireIntensity = (byte) num2;
					info.m_buildingAI.BuildingDeactivated(buildingID, ref buildingData);
					Building.Flags flags2 = buildingData.m_flags;
					Singleton<BuildingManager>.instance.UpdateBuildingRenderer(buildingID, true);
					if (flags2 != flags)
					{
						Singleton<BuildingManager>.instance.UpdateFlags(buildingID, flags2 ^ flags);
					}
				}
			}
		}

		protected override void SimulationStepActive(ushort buildingID, ref Building buildingData, ref Building.Frame frameData)
		{
			base.SimulationStepActive(buildingID, ref buildingData, ref frameData);
			if ((buildingData.m_fireIntensity != 0) && (frameData.m_fireDamage > 12))
			{
				DoSpreadFire(buildingID, ref buildingData, 50, m_info.m_size.y);
			}
		}
	}
#endif
}