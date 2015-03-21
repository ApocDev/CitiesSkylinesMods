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
		private MethodBase _targetMethod, _detourMethod;

		public DotNetFunctionDetour(MethodBase targetMethod, MethodBase detourMethod)
		{
			throw new InvalidOperationException("DotNetFunctionDetours on Mono is not fully implemented. Do not use!");
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

			// Make sure both of these are JIT'd (in Mono-land this is AOT'd)
			// This is required so we can properly get the method pointers
			// So they can be overwritten in Mono itself.
			RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);
			RuntimeHelpers.PrepareMethod(detourMethod.MethodHandle);


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


			var targetBody = _targetMethod.GetMethodBody();
			targetBody.GetILAsByteArray();
			var getDynamicHandle =
				Delegate.CreateDelegate(typeof(Func<DynamicMethod, RuntimeMethodHandle>), typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic))
					as Func<DynamicMethod, RuntimeMethodHandle>;

			var newMethod = new DynamicMethod("foo", (_targetMethod as MethodInfo).ReturnType, _targetMethod.GetParameters().Select(p => p.ParameterType).ToArray());
			var body = newMethod.GetILGenerator();
			body.Emit(OpCodes.Jmp, _targetMethod as MethodInfo);
			body.Emit(OpCodes.Ret);

			var handle = getDynamicHandle(newMethod);
			RuntimeHelpers.PrepareMethod(handle);
			*((IntPtr*) new IntPtr(((IntPtr*) _targetMethod.MethodHandle.Value.ToPointer() + 2)).ToPointer()) = handle.GetFunctionPointer();


			var targetClassPtr = Mono.FindClass(_targetMethod.DeclaringType.Namespace, _targetMethod.DeclaringType.Name);
			Log("TargetClassPtr: " + targetClassPtr.ToString("X"));
			int targetIndex;
			var targetMethod = Mono.FindMethodPtr(targetClassPtr, _targetMethod.Name, out targetIndex, Mono.BuildFunctionArgs(_targetMethod));
			Log("TargetMethod: " + targetMethod.ToString("X") + ", @ " + targetIndex);

			var detourClassPtr = Mono.FindClass(_detourMethod.DeclaringType.Namespace, _detourMethod.DeclaringType.Name);
			Log("DetourClassPtr: " + detourClassPtr.ToString("X"));
			int detourIndex;
			var detourMethod = Mono.FindMethodPtr(detourClassPtr, _detourMethod.Name, out detourIndex, Mono.BuildFunctionArgs(_detourMethod));
			Log("DetourMethod: " + detourMethod.ToString("X") + ", @ " + detourIndex);

			var targetClass = (MonoWrap.MonoClass*) targetClassPtr;

			Log("Target class has " + targetClass->method.count + " methods. " + targetClass->name_space + "@" + targetClass->name);
			

			var targetIdx = (IntPtr) targetClass->methods[targetIndex];
			Log("TargetFromMethods: " + targetIdx.ToString("X"));

			var detourIdx = (IntPtr) targetClass->methods[detourIndex];
			Log("DetourFromMethods: " + detourIdx.ToString("X"));
		}
	}
}