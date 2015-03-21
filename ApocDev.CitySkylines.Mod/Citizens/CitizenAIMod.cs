using System;
using System.Collections.Generic;

using ColossalFramework;

using ICities;

using UnityEngine;

using Object = UnityEngine.Object;

namespace ApocDev.CitySkylines.Mod.Citizens
{
	public class CitizenAIReplacer : LoadingExtensionBase
	{
		private void ReplaceResidentAI(CitizenInfo ci, Dictionary<Type, Type> componentRemap)
		{
			var component = ci.GetComponent<CitizenAI>();
			if (component != null)
			{
				var key = component.GetType();
				Type remap;
				if (componentRemap.TryGetValue(key, out remap))
				{
					Object.DestroyImmediate(component);
					var newAi = ci.gameObject.AddComponent(remap) as CitizenAI;
					newAi.m_info = ci;
					ci.m_citizenAI = newAi;
					newAi.InitializeAI();
				}
			}
		}

		#region Overrides of LoadingExtensionBase

		public override void OnLevelLoaded(LoadMode mode)
		{
			if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame)
			{
				var typeMap = new Dictionary<Type, Type> { { typeof(ResidentAI), typeof(CitizenAIMod) } };
				var prefabCount = PrefabCollection<CitizenInfo>.PrefabCount();
				for (uint i = 0; i < prefabCount; i++)
				{
					var prefab = PrefabCollection<CitizenInfo>.GetPrefab(i);
					ReplaceResidentAI(prefab, typeMap);
				}
			}

			base.OnLevelLoaded(mode);
		}

		#endregion
	}

	public class CitizenAIMod : ResidentAI
	{
		// C# trix to override a non-virtual method.
		protected new bool StartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo)
		{
			// So, this method doesn't really have an quick early-out
			// Since they don't let us pass around the "laneTypes" that are allowed
			// That means this method is subject to change each client version.
			// I'll write some IL-injection later, but this is good enough for now.

			PathUnit.Position startPathPos;
			PathUnit.Position endPathPos;
			NetInfo.LaneType laneTypes = 0;

			// Citizens can no longer walk 20k
			// 500 seems more reasonable, no?
			// Maybe 1k.
			if (Vector3.Distance(startPos, endPos) <= 500)
			{
				laneTypes |= NetInfo.LaneType.Pedestrian;
			}

			VehicleInfo.VehicleType none = VehicleInfo.VehicleType.None;
			if (vehicleInfo != null)
			{
				laneTypes = (NetInfo.LaneType) ((byte) (laneTypes | NetInfo.LaneType.Vehicle));
				none |= vehicleInfo.m_vehicleType;
			}
			PathUnit.Position pathPos = new PathUnit.Position();
			ushort parkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
			if (parkedVehicle != 0)
			{
				PathManager.FindPathPosition(Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicle].m_position,
					ItemClass.Service.Road,
					NetInfo.LaneType.Vehicle,
					VehicleInfo.VehicleType.Car,
					32f,
					out pathPos);
			}
			if (FindPathPosition(instanceID, ref citizenData, startPos, laneTypes, none, out startPathPos) &&
			    FindPathPosition(instanceID, ref citizenData, endPos, laneTypes, none, out endPathPos))
			{
				uint path;
				if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None)
				{
					laneTypes = (NetInfo.LaneType) ((byte) (laneTypes | NetInfo.LaneType.PublicTransport));
				}
				PathUnit.Position startPosB = new PathUnit.Position();
				if (Singleton<PathManager>.instance.CreatePath(out path,
					ref Singleton<SimulationManager>.instance.m_randomizer,
					Singleton<SimulationManager>.instance.m_currentBuildIndex,
					startPathPos,
					startPosB,
					endPathPos,
					startPosB,
					pathPos,
					laneTypes,
					none,
					20000f,
					false,
					false,
					false,
					false))
				{
					if (citizenData.m_path != 0)
					{
						Singleton<PathManager>.instance.ReleasePath(citizenData.m_path);
					}

					citizenData.m_path = path;
					citizenData.m_flags |= CitizenInstance.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}
	}
}