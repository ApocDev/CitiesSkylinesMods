using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using ColossalFramework;

namespace ApocDev.CitySkylines.Mod.MonoHooks
{
	public unsafe class DotNetFunctionDetour
	{
		private static MonoWrap Mono = new MonoWrap();
		private MethodInfo _targetMethod, _detourMethod;

		public DotNetFunctionDetour(MethodInfo targetMethod, MethodInfo detourMethod)
		{
			if (targetMethod == null)
			{
				Log("Target method is null!");
				return;
			}
			if (detourMethod == null)
			{
				Log("Detour method is null!");
				return;
			}
			CODebugBase<LogChannel>.Error(LogChannel.Core, "Detour: " + detourMethod.Name);
			CODebugBase<LogChannel>.Error(LogChannel.Core, "Target: " + targetMethod.Name);

			// Make sure both of these are JIT'd (in Mono-land this does nothing)
			// This is required so we can properly get the method pointers
			// So they can be overwritten in Mono itself.
			RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);
			RuntimeHelpers.PrepareMethod(detourMethod.MethodHandle);

			targetMethod.MethodHandle.GetFunctionPointer();
			detourMethod.MethodHandle.GetFunctionPointer();


			_targetMethod = targetMethod;
			_detourMethod = detourMethod;
		}

		void Log(string msg)
		{
			CODebugBase<LogChannel>.Error(LogChannel.Core, msg);
		}
		public void ApplyDetour()
		{
			if (_targetMethod == null || _detourMethod == null)
				return;

			byte[] originalBytes;
			Mono.NativeDetour(_targetMethod, _detourMethod, out originalBytes);
		}
	}
}