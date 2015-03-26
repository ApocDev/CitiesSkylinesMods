using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace ApocDev.CitySkylines.Mod.MonoHooks
{
	internal unsafe class MonoWrap
	{
		private static _mono_runtime_free_method_Delegate mono_runtime_free_method;

		public MonoWrap()
		{
			var hMono = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().FirstOrDefault(m => m.ModuleName == "mono.dll").BaseAddress;
			var baseAddr = 0x76FAC;

			mono_runtime_free_method =
				Marshal.GetDelegateForFunctionPointer(new IntPtr(hMono.ToInt64() + baseAddr), typeof(_mono_runtime_free_method_Delegate)) as _mono_runtime_free_method_Delegate;
		}

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_method_get_header(IntPtr method);

		[DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_domain_get();

		[DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_compile_method(IntPtr method);

		public void NativeDetour(MethodInfo targetMethod, MethodInfo detourMethod, out byte[] originalBytes)
		{
			// Plain old .NET support here.
			RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);
			RuntimeHelpers.PrepareMethod(detourMethod.MethodHandle);

			// 2 birds, 1 stone. Compile targetMethod, and get the func ptr to it.
			byte* target = (byte*) targetMethod.MethodHandle.GetFunctionPointer();
			// Force compile this so we can swap it.
			detourMethod.MethodHandle.GetFunctionPointer();

			// For "unpatching" later
			originalBytes = new byte[13];
			for (int i = 0; i < originalBytes.Length; i++)
			{
				originalBytes[i] = target[i];
			}

			// TODO: jmp qword [funcPtr]
			// 5 byte patch to just far jump
			// Assume x64 shadow space stack is alloc'd correctly for locals
			// Also assume that the incoming calling convention is proper.
			//target[0] = 0xEC; // 0xEA maybe?
			//*((IntPtr*)&target[1]) = detourMethod.MethodHandle.GetFunctionPointer();

			// mono itself uses the r11 register (which isn't good on Windows due to syscall stuff)
			// So we'll just move to r11, and jmp to r11

			// mov r11, funcptr
			target[0] = 0x49;
			target[1] = 0xBB;
			*((IntPtr*) &target[2]) = detourMethod.MethodHandle.GetFunctionPointer();

			// jmp r11
			target[10] = 0x41;
			target[11] = 0xFF;
			target[12] = 0xE3;
		}

		public void ReplaceIL(MethodInfo targetMethod, byte[] newiLBytes, int newMaxStack = -1)
		{
			// TODO: Some IL parsing to make it easier to deal with the locals count maybe?
			// Just offers mainly debugging support

			// get the old header
			var targetHeader = (MonoMethodHeader*) mono_method_get_header(targetMethod.MethodHandle.Value);

			// TODO: Free the original code bytes
			// This will result in a mem leak if we do a ton of replacements
			var newCodePtr = Marshal.AllocHGlobal(newiLBytes.Length + 5);
			Marshal.Copy(newiLBytes, 0, newCodePtr, newiLBytes.Length);
			targetHeader->code = (byte*) newCodePtr;
			targetHeader->code_size = (uint) newiLBytes.Length;
			if (newMaxStack != -1)
			{
				targetHeader->bitvector1 = newMaxStack & 0x7FFF;
			}

			// Free the target method, so we can recompile with new IL
			mono_runtime_free_method(mono_domain_get(), targetMethod.MethodHandle.GetFunctionPointer());

			// Force-compile the method
			// Plain old .NET support here.
			RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);
			targetMethod.MethodHandle.GetFunctionPointer();
		}

		#region Nested type: _mono_runtime_free_method_Delegate

		[UnmanagedFunctionPointer(CallingConvention.FastCall)]
		internal delegate void _mono_runtime_free_method_Delegate(IntPtr domain, IntPtr method);

		#endregion

		#region Nested type: AssemblyForEachCallback

		[UnmanagedFunctionPointer(CallingConvention.FastCall)] // mono.dll with CS uses rcx, rdx and assumes the callback cleans stack. That's a fastcall folks!
		internal delegate void AssemblyForEachCallback(IntPtr assembly, IntPtr userData);

		#endregion

		#region Nested type: MonoClass

		[StructLayout(LayoutKind.Sequential)]
		public struct MonoClass
		{
			[StructLayout(LayoutKind.Explicit)]
			public struct Sizes
			{
				/// int
				[FieldOffset(0)]
				public int class_size;

				/// int
				[FieldOffset(0)]
				public int element_size;

				/// int
				[FieldOffset(0)]
				public int generic_param_token;

				/// <summary>
				///     Returns the fully qualified type name of this instance.
				/// </summary>
				/// <returns>
				///     A <see cref="T:System.String" /> containing a fully qualified type name.
				/// </returns>
				public override string ToString()
				{
					return string.Format("Size: " + element_size);
				}
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct FieldOrMethod
			{
				/// guint32->DWORD->unsigned int
				public uint first;

				/// guint32->DWORD->unsigned int
				public uint count;

				/// <summary>
				///     Returns the fully qualified type name of this instance.
				/// </summary>
				/// <returns>
				///     A <see cref="T:System.String" /> containing a fully qualified type name.
				/// </returns>
				public override string ToString()
				{
					return string.Format("First: {0}, Count: {1}", first, count);
				}
			}

			/// MonoClass*
			public IntPtr element_class;

			/// MonoClass*
			public IntPtr cast_class;

			/// MonoClass**
			public IntPtr supertypes;

			/// guint16->WORD->unsigned short
			public ushort idepth;

			/// guint8->BYTE->unsigned char
			public byte rank;

			/// int
			public int instance_size;

			/// inited : 1
			/// init_pending : 1
			/// size_inited : 1
			/// valuetype : 1
			/// enumtype : 1
			/// blittable : 1
			/// unicode : 1
			/// wastypebuilder : 1
			public uint bitvector1;

			/// guint8->BYTE->unsigned char
			public byte min_align;

			/// packing_size : 4
			/// ghcimpl : 1
			/// has_finalize : 1
			/// marshalbyref : 1
			/// contextbound : 1
			/// delegate : 1
			/// gc_descr_inited : 1
			/// has_cctor : 1
			/// has_references : 1
			/// has_static_refs : 1
			/// no_special_static_fields : 1
			/// is_com_object : 1
			/// nested_classes_inited : 1
			/// interfaces_inited : 1
			/// simd_type : 1
			/// is_generic : 1
			/// is_inflated : 1
			/// has_finalize_inited : 1
			/// fields_inited : 1
			/// setup_fields_called : 1
			public uint bitvector2;

			/// guint8->BYTE->unsigned char
			public byte exception_type;

			/// MonoClass*
			public IntPtr parent;

			/// MonoClass*
			public IntPtr nested_in;

			/// MonoImage*
			public IntPtr image;

			private readonly IntPtr name_ptr;

			public string name
			{
				get
				{
					if (name_ptr == IntPtr.Zero)
					{
						return "<INVALID_NAME>";
					}
					return Marshal.PtrToStringAnsi(name_ptr);
				}
			}

			private readonly IntPtr name_space_ptr;

			public string name_space
			{
				get
				{
					if (name_space_ptr == IntPtr.Zero)
					{
						return "<INVALID_NAMESPACE>";
					}
					return Marshal.PtrToStringAnsi(name_space_ptr);
				}
			}

			/// guint32->DWORD->unsigned int
			public uint type_token;

			/// int
			public int vtable_size;

			/// guint16->WORD->unsigned short
			public ushort interface_count;

			/// guint16->WORD->unsigned short
			public ushort interface_id;

			/// guint16->WORD->unsigned short
			public ushort max_interface_id;

			/// guint16->WORD->unsigned short
			public ushort interface_offsets_count;

			/// MonoClass**
			public IntPtr interfaces_packed;

			/// guint16*
			public IntPtr interface_offsets_packed;

			/// guint8*
			public IntPtr interface_bitmap;

			/// MonoClass**
			public IntPtr interfaces;

			/// Sizes
			public Sizes sizes;

			/// guint32->DWORD->unsigned int
			public uint flags;

			/// FieldOrMethod
			public FieldOrMethod field;

			/// FieldOrMethod
			public FieldOrMethod method;

			/// guint32->DWORD->unsigned int
			public uint ref_info_handle;

			/// MonoMarshalType*
			public IntPtr marshal_info;

			/// MonoClassField*
			public IntPtr fields;

			/// MonoMethod**
			public void** methods;

			/// MonoType->void*
			public IntPtr this_arg;

			/// MonoType->void*
			public IntPtr byval_arg;

			/// MonoGenericClass*
			public IntPtr generic_class;

			/// MonoGenericContainer*
			public IntPtr generic_container;

			/// void*
			public IntPtr gc_descr;

			/// MonoClassRuntimeInfo*
			public IntPtr runtime_info;

			/// MonoClass*
			public IntPtr next_class_cache;

			/// MonoMethod**
			public void** vtable;

			/// MonoClassExt*
			public IntPtr ext;

			/// <summary>
			///     Returns the fully qualified type name of this instance.
			/// </summary>
			/// <returns>
			///     A <see cref="T:System.String" /> containing a fully qualified type name.
			/// </returns>
			public override string ToString()
			{
				return
					string.Format(
						"ElementClass: {0}, CastClass: {1}, Supertypes: {2}, Idepth: {3}, Rank: {4}, InstanceSize: {5}, Bitvector1: {6}, MinAlign: {7}, Bitvector2: {8}, ExceptionType: {9}, Parent: {10}, NestedIn: {11}, Image: {12}, Name: {13}, NameSpace: {14}, TypeToken: {15}, VtableSize: {16}, InterfaceCount: {17}, InterfaceId: {18}, MaxInterfaceId: {19}, InterfaceOffsetsCount: {20}, InterfacesPacked: {21}, InterfaceOffsetsPacked: {22}, InterfaceBitmap: {23}, Interfaces: {24}, Sizes: {25}, Flags: {26}, Field: {27}, Method: {28}, RefInfoHandle: {29}, MarshalInfo: {30}, Fields: {31}, Methods: {32}, ThisArg: {33}, ByvalArg: {34}, GenericClass: {35}, GenericContainer: {36}, GcDescr: {37}, RuntimeInfo: {38}, NextClassCache: {39}, Vtable: {40}, Ext: {41}",
						element_class,
						cast_class,
						supertypes,
						idepth,
						rank,
						instance_size,
						bitvector1,
						min_align,
						bitvector2,
						exception_type,
						parent,
						nested_in,
						image,
						name,
						name_space,
						type_token,
						vtable_size,
						interface_count,
						interface_id,
						max_interface_id,
						interface_offsets_count,
						interfaces_packed,
						interface_offsets_packed,
						interface_bitmap,
						interfaces,
						sizes,
						flags,
						field,
						method,
						ref_info_handle,
						marshal_info,
						fields,
						(IntPtr) methods,
						this_arg,
						byval_arg,
						generic_class,
						generic_container,
						gc_descr,
						runtime_info,
						next_class_cache,
						(IntPtr) vtable,
						ext);
			}
		}

		#endregion

		#region Nested type: MonoMethod

		[StructLayout(LayoutKind.Sequential)]
		internal struct MonoMethod
		{
			/// guint16->WORD->unsigned short
			public ushort flags;

			/// guint16->WORD->unsigned short
			public ushort iflags;

			/// guint32->DWORD->unsigned int
			public uint token;

			/// MonoClass*
			public IntPtr klass;

			/// MonoMethodSignature*
			public IntPtr signature;

			private readonly IntPtr name_ptr;

			public string name
			{
				get
				{
					if (name_ptr == IntPtr.Zero)
					{
						return "<INVALID_NAME>";
					}
					return Marshal.PtrToStringAnsi(name_ptr);
				}
			}

			/// inline_info : 1
			/// inline_failure : 1
			/// wrapper_type : 5
			/// string_ctor : 1
			/// save_lmf : 1
			/// dynamic : 1
			/// sre_method : 1
			/// is_generic : 1
			/// is_inflated : 1
			/// skip_visibility : 1
			/// verification_success : 1
			/// is_mb_open : 1
			/// slot : 16
			public uint bitvector1;

			public uint inline_info { get { return bitvector1 & 1; } set { bitvector1 = value | bitvector1; } }

			public uint inline_failure { get { return (bitvector1 & 2) / 2; } set { bitvector1 = (value * 2) | bitvector1; } }

			public uint wrapper_type { get { return (bitvector1 & 0x7C) / 4; } set { bitvector1 = (value * 4) | bitvector1; } }

			public uint string_ctor { get { return (bitvector1 & 0x80) / 0x80; } set { bitvector1 = (value * 128) | bitvector1; } }

			public uint save_lmf { get { return (bitvector1 & 0x100) / 0x100; } set { bitvector1 = (value * 256) | bitvector1; } }

			public uint dynamic { get { return (bitvector1 & 0x200) / 0x200; } set { bitvector1 = (value * 512) | bitvector1; } }

			public uint sre_method { get { return (bitvector1 & 0x400) / 0x400; } set { bitvector1 = (value * 1024) | bitvector1; } }

			public uint is_generic { get { return (bitvector1 & 0x800) / 0x800; } set { bitvector1 = (value * 2048) | bitvector1; } }

			public uint is_inflated { get { return (bitvector1 & 0x1000) / 0x1000; } set { bitvector1 = (value * 4096) | bitvector1; } }

			public uint skip_visibility { get { return (bitvector1 & 0x2000) / 0x2000; } set { bitvector1 = (value * 8192) | bitvector1; } }

			public uint verification_success { get { return (bitvector1 & 0x4000) / 0x4000; } set { bitvector1 = (value * 16384) | bitvector1; } }

			public uint is_mb_open { get { return (bitvector1 & 0x8000) / 0x8000; } set { bitvector1 = (value * 32768) | bitvector1; } }

			public uint slot { get { return (bitvector1 & 0xffff0000) / 0x10000; } set { bitvector1 = (value * 65536) | bitvector1; } }
		}

		#endregion

		#region Nested type: MonoMethodHeader

		[StructLayout(LayoutKind.Sequential)]
		private struct MonoMethodHeader
		{
			public byte* code;
			public uint code_size;
			public int bitvector1;
		}

		#endregion

		#region Nested type: MonoMethodSignature

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct MonoMethodSignature
		{
			// The laziness of PInvoke Interop Assistant to the rescue!

			/// MonoType*
			internal readonly IntPtr ret;

			/// guint16->WORD->unsigned short
			internal readonly ushort param_count;

			/// gint16->int
			internal readonly short sentinelpos;

			/// generic_param_count : 16
			/// call_convention : 6
			/// hasthis : 1
			/// explicit_this : 1
			/// pinvoke : 1
			/// is_inflated : 1
			/// has_type_parameters : 1
			internal readonly uint bitvector1;


			// Params is basically the last part of the method signature.
			// So we'll use Marshal.Sizeof to get the base size, and read the param array after.
			// MonoType*[] 0xE
			// internal IntPtr @params;
		}

		#endregion

		#region Nested type: MonoType

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private struct MonoType
		{
			[StructLayout(LayoutKind.Sequential)]
			internal struct MonoCustomMod
			{
				/// required : 1
				/// token : 31
				internal uint bitvector1;

				internal uint required { get { return bitvector1 & 1u; } set { bitvector1 = value | bitvector1; } }

				internal uint token { get { return (bitvector1 & 4294967294u) / 2; } set { bitvector1 = (value * 2) | bitvector1; } }
			}

			/// MonoTypeDataUnion
			internal readonly IntPtr data;

			/// attrs : 16
			/// type : 8
			/// num_mods : 6
			/// byref : 1
			/// pinned : 1
			internal readonly uint bitvector1;

			/// MonoCustomMod[]
			internal readonly MonoCustomMod modifiers;

			internal uint attrs { get { return bitvector1 & 0xffff; } }

			internal uint type { get { return (bitvector1 & 0xff0000) / 0x10000; } }

			internal uint num_mods { get { return (bitvector1 & 0x3f000000) / 0x1000000; } }

			internal uint byref { get { return (bitvector1 & 0x40000000) / 0x40000000; } }

			internal uint pinned { get { return (bitvector1 & 0x80000000) / 0x80000000; } }
		}

		#endregion
	}
}