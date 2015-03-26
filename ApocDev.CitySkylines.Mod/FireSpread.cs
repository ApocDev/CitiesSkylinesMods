using System;
using System.Collections.Generic;

using ColossalFramework;
using ColossalFramework.Math;

using ICities;

using UnityEngine;

using Random = System.Random;

namespace ApocDev.CitySkylines.Mod
{
	public class FireSpread : ThreadingExtensionBase
	{
		private Random _rand = new Random(Environment.TickCount);
		private ushort _updateIndex;

		#region Overrides of ThreadingExtensionBase

		public override void OnAfterSimulationTick()
		{
			if (ModSettings.Instance.EnableFireSpread)
			{
				DoFireSpread();
			}

			base.OnAfterSimulationTick();
		}

		#endregion

		internal static float GetBuildingFireSpreadChance(ref Building building)
		{
			// Flat 5% chance by default.
			float chance = ModSettings.Instance.BaseFireSpreadChance;

			// Big chance of fire spread if there's no water.
			if ((building.m_problems & Notification.Problem.WaterNotConnected) != 0)
			{
				chance += ModSettings.Instance.NoWaterFireSpreadAdditional;
			}

			// Dumb citizens... probably a good chance of actually lighting the fires themselves
			if ((building.m_problems & Notification.Problem.NoEducatedWorkers) != 0)
			{
				chance += ModSettings.Instance.UneducatedFireSpreadAdditional;
			}

			// MAX: 14

			return chance * ModSettings.Instance.FireSpreadModifier;
		}

		internal static float DistanceSqr(ref Vector3 a, ref Vector3 b)
		{
			Vector3 vector3 = new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
			return vector3.x * vector3.x + vector3.y * vector3.y + vector3.z * vector3.z;
		}

		internal static IEnumerable<ushort> GetNearestBuildings(Vector3 position, float radius)
		{
			// Pulled from NotificationManager.AddWaveEvent
			var instance = Singleton<BuildingManager>.instance;

			// All this nonsense here, seems really silly.
			// I get the need for it, but at the same time... wtf?
			float minX = Mathf.Min(position.x - radius, position.x + radius);
			float minZ = Mathf.Min(position.z - radius, position.y + radius);

			float maxX = Mathf.Max(position.x - radius, position.x + radius);
			float maxZ = Mathf.Max(position.z - radius, position.y + radius);

			int minXGrid = Mathf.Max((int) ((minX / 64f) + 135f), 0);
			int minZGrid = Mathf.Max((int) ((minZ / 64f) + 135f), 0);
			int maxXGrid = Mathf.Min((int) ((maxX / 64f) + 135f), 269);
			int maxZGrid = Mathf.Min((int) ((maxZ / 64f) + 135f), 269);

			for (int z = minZGrid; z < maxZGrid; z++)
			{
				for (int x = minXGrid; x < maxXGrid; x++)
				{
					int iters = 0;
					ushort buildingId = instance.m_buildingGrid[(z * 270) + x];
					while (buildingId != 0)
					{
						var dist = DistanceSqr(ref instance.m_buildings.m_buffer[buildingId].m_position, ref position);
						if (dist < radius * radius)
						{
							yield return buildingId;
						}

						buildingId = instance.m_buildings.m_buffer[buildingId].m_nextGridBuilding;
						if (++iters >= 32768)
						{
							break;
						}
					}
				}
			}
		}

		internal static float GetSphereOfInfluence(ref Building building)
		{
			// Blah!
			// Bigger buildings = bigger influence?
			return (Mathf.Abs(building.Info.m_generatedInfo.m_max.x - building.Info.m_generatedInfo.m_min.x) +
			        Mathf.Abs(building.Info.m_generatedInfo.m_max.z - building.Info.m_generatedInfo.m_min.z)) * 1.5f;
		}

		public void DoFireSpread()
		{
			var bm = Singleton<BuildingManager>.instance;
			if (bm == null)
			{
				return;
			}

			// Are fires currently disabled?
			if (bm.m_firesDisabled)
			{
				return;
			}

			// No fire departments unlocked, so don't try doing any fire spread yet.
			if (!Singleton<UnlockManager>.instance.Unlocked(ItemClass.Service.FireDepartment))
			{
				return;
			}

			var sim = Singleton<SimulationManager>.instance;
			if (sim == null)
			{
				return;
			}

			_updateIndex++;
			if (_updateIndex >= 1000)
			{
				_updateIndex = 0;
			}

			int firesHandledThisTick = 0;
			for (ushort i = _updateIndex; i < bm.m_buildings.m_buffer.Length; i += 1000)
			{
				//var building = bm.m_buildings.m_buffer[i];
				if (bm.m_buildings.m_buffer[i].Info == null)
				{
					continue;
				}

				// Only check/light buildings that can be placed on a road, so fire stations have access.
				// Some semblance of balance, right?
				if (bm.m_buildings.m_buffer[i].Info.m_placementMode != BuildingInfo.PlacementMode.Roadside)
				{
					continue;
				}

				// Only checking for "on fire" buildings here.
				if ((bm.m_buildings.m_buffer[i].m_problems & Notification.Problem.Fire) == 0)
				{
					continue;
				}

				float influence = GetSphereOfInfluence(ref bm.m_buildings.m_buffer[i]);

				foreach (var nearbyId in GetNearestBuildings(bm.m_buildings.m_buffer[i].m_position, influence))
				{
					// Building is already on fire. Don't check it.
					if ((bm.m_buildings.m_buffer[nearbyId].m_problems & Notification.Problem.Fire) != 0)
					{
						continue;
					}

					if (_rand.NextDouble() < GetBuildingFireSpreadChance(ref bm.m_buildings.m_buffer[nearbyId]))
					{
						LightBuildingOnFire(nearbyId, ref bm.m_buildings.m_buffer[nearbyId]);
					}
				}

				firesHandledThisTick++;
				// Only try and light 20 fires per "tick"
				// Maybe break this down to 5?
				if (firesHandledThisTick >= 20)
				{
					break;
				}
			}
		}

		private static float RandomFloat()
		{
			// WTB: .Float or .Single
			return new Randomizer(Environment.TickCount).Int32(0, int.MaxValue) / (float) int.MaxValue;
		}

		internal static void LightBuildingOnFire(ushort buildingId, ref Building data)
		{
			var bm = Singleton<BuildingManager>.instance;

			if ((bm.m_buildings.m_buffer[buildingId].m_problems & Notification.Problem.Fire) != 0)
			{
				return;
			}
			
			int fireHazard;
			int fireSize;
			int fireTolerance;
			bm.m_buildings.m_buffer[buildingId].Info.m_buildingAI.GetFireParameters(buildingId, ref data, out fireHazard, out fireSize, out fireTolerance);

			if (fireHazard != 0 && (data.m_flags & (Building.Flags.Abandoned | Building.Flags.Completed)) == Building.Flags.Completed && data.m_fireIntensity == 0 &&
			    data.GetLastFrameData().m_fireDamage == 0)
			{
				// Is the building under water by chance?
				float waterLevel = Singleton<TerrainManager>.instance.WaterLevel(new Vector2(data.m_position.x, data.m_position.z));
				if (waterLevel <= data.m_position.y)
				{
					Building.Flags preDeactivateFlags = data.m_flags;
					data.m_fireIntensity = (byte) fireSize;
					data.Info.m_buildingAI.BuildingDeactivated(buildingId, ref data);
					Building.Flags postDeactivateFlags = data.m_flags;
					Singleton<BuildingManager>.instance.UpdateBuildingRenderer(buildingId, true);
					if (postDeactivateFlags != preDeactivateFlags)
					{
						Singleton<BuildingManager>.instance.UpdateFlags(buildingId, postDeactivateFlags ^ preDeactivateFlags);
					}
				}
			}
		}
	}
}