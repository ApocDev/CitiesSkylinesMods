using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

using ColossalFramework;

using UnityEngine;

namespace ApocDev.CitySkylines.Mod.MonoHooks
{
	internal unsafe class MonoWrap
	{
		public enum MonoTypeEnum : uint
		{
			// ReSharper disable UnusedMember.Global
			End = 0x00, /* End of List */
			Void = 0x01,
			Boolean = 0x02,
			Char = 0x03,
			I1 = 0x04,
			U1 = 0x05,
			I2 = 0x06,
			U2 = 0x07,
			I4 = 0x08,
			U4 = 0x09,
			I8 = 0x0a,
			U8 = 0x0b,
			R4 = 0x0c,
			R8 = 0x0d,
			String = 0x0e,
			Ptr = 0x0f, /* arg: <type> token */
			ByRef = 0x10, /* arg: <type> token */
			ValueType = 0x11, /* arg: <type> token */
			Class = 0x12, /* arg: <type> token */
			Var = 0x13, /* number */
			Array = 0x14, /* type, rank, boundsCount, bound1, loCount, lo1 */
			GenericInst = 0x15, /* <type> <type-arg-count> <type-1> \x{2026} <type-n> */
			TypedByRef = 0x16,
			I = 0x18,
			U = 0x19,
			FnPtr = 0x1b, /* arg: full method signature */
			Object = 0x1c,
			SzArray = 0x1d, /* 0-based one-dim-array */
			Mvar = 0x1e, /* number */
			CmodReqd = 0x1f, /* arg: typedef or typeref token */
			CmodOpt = 0x20, /* optional arg: typedef or typref token */
			Internal = 0x21, /* CLR internal type */

			Modifier = 0x40, /* Or with the following types */
			Sentinel = 0x41, /* Sentinel for varargs method signature */
			Pinned = 0x45, /* Local var that points to pinned object */

			Enum = 0x55 /* an enumeration */
			// ReSharper restore UnusedMember.Global
		}

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_assembly_open(string fileName, out int status);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_assembly_foreach(IntPtr callback, IntPtr userData);

		//[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.FastCall)]
		//[SuppressUnmanagedCodeSecurity]
		//private static extern IntPtr mono_assembly_get_image(IntPtr assembly);

		static IntPtr mono_assembly_get_image(IntPtr assembly)
		{
			return Marshal.ReadIntPtr(new IntPtr(assembly.ToInt64() + 0x58));
		}

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_class_get_methods(IntPtr klass, IntPtr itr);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_method_get_header(IntPtr method);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		internal static extern IntPtr mono_get_method(IntPtr image, int token, IntPtr @class);
		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		internal static extern int mono_metadata_translate_token_index(IntPtr image, int table, uint idx);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_class_get_fields(IntPtr monoClass, IntPtr itr);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_class_get_parent(IntPtr monoClass);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_method_get_name(IntPtr methodPtr); // returns char_t*

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.FastCall)]
		[SuppressUnmanagedCodeSecurity]
		[return:MarshalAs(UnmanagedType.LPStr)]
		private static extern string mono_class_get_name(IntPtr classPtr); // returns char_t*

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.FastCall)]
		[SuppressUnmanagedCodeSecurity]
		[return: MarshalAs(UnmanagedType.LPStr)]
		private static extern string mono_class_get_namespace(IntPtr classPtr); // returns char_t*

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_class_get_nesting_type(IntPtr classPtr);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_method_signature(IntPtr monoMethod);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern IntPtr mono_class_get(IntPtr image, int token);

		[DllImport("mono.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		[SuppressUnmanagedCodeSecurity]
		private static extern int mono_image_get_table_rows(IntPtr image, int table); // table = 2 = MONO_TABLE_TYPEDEF

		private IEnumerable<IntPtr> AssemblyGetClasses(IntPtr image)
		{
			var numTypes = mono_image_get_table_rows(image, 2 /*MONO_TABLE_TYPEDEF*/);

			// Mono expects indices starting at 1, not 0.
			for (int i = 1; i < numTypes + 1; i++)
			{
				yield return mono_class_get(image,
					i | 0x02000000 // MONO_TOKEN_TYPE_DEF
					);
			}
		}

		private string ClassGetName(IntPtr classPtr)
		{
			var nested = mono_class_get_nesting_type(classPtr);
			Debug.Log("[ClassGetName] Nested: " + nested.ToString("X"));
			string ret = "";
			while (nested != IntPtr.Zero)
			{
				ret = ClassGetName(nested) + "." + ret;
				Debug.Log("[ClassGetName] ret: " + ret);
				nested = mono_class_get_nesting_type(nested);
				Debug.Log("[ClassGetName] Nested: " + nested.ToString("X"));
			}
			ret += mono_class_get_name(classPtr);
			Debug.Log("[ClassGetName] ret: " + ret);
			return ret;
		}

		private MonoTypeEnum[] GetMethodArguments(IntPtr monoMethod)
		{
			var args = new List<MonoTypeEnum>();
			IntPtr sigPtr = mono_method_signature(monoMethod);

			if (sigPtr == IntPtr.Zero)
				return new MonoTypeEnum[0];

			var sig = (MonoMethodSignature) Marshal.PtrToStructure(sigPtr, typeof(MonoMethodSignature));

			IntPtr paramStart = new IntPtr(sigPtr.ToInt64() + Marshal.SizeOf(sig));
			for (int i = 1; i < sig.param_count + 1; i++)
			{
				var paramPtr = Marshal.ReadIntPtr(paramStart, i * IntPtr.Size);
				var param = ((MonoType) Marshal.PtrToStructure(paramPtr, typeof(MonoType)));
				var paramType = (MonoTypeEnum) param.type;
				args.Add(paramType);
			}

			return args.ToArray();
		}

		internal IntPtr FindClass(string classNamespace, string className)
		{
			// TODO: Main mono thread checks
			//UnityEngine.Debug.Log("FindClass assemblyPath: " + assemblyPath);
			//UnityEngine.Debug.Log("FindClass classNamespace: " + classNamespace);
			//UnityEngine.Debug.Log("FindClass className: " + className);

			IntPtr foundClassPtr = IntPtr.Zero;
			AssemblyForEachCallback cb = (asm, ud) =>
			{
				if (foundClassPtr != IntPtr.Zero)
					return;

				//Debug.Log("Assembly: " + asm.ToString("X"));
				var image = mono_assembly_get_image(asm);
				//Debug.Log("Image: " + image.ToString("X"));
				foreach (var classPtr in AssemblyGetClasses(image))
				{
					if (classPtr != IntPtr.Zero)
					{
						var c = (MonoClass) Marshal.PtrToStructure(classPtr, typeof(MonoClass));
						//Debug.Log("classPtr: " + classPtr.ToString("X") + ", name: " + c.name + ", namespace: " + c.name_space);

						if (c.name == className && c.name_space == classNamespace)
						{
							foundClassPtr = classPtr;
							break;
						}
					}
				}
			};
			var callbackPtr = Marshal.GetFunctionPointerForDelegate(cb);
			mono_assembly_foreach(callbackPtr, IntPtr.Zero);

			//Debug.Log("Found class ptr: " + foundClassPtr.ToString("X"));
			return foundClassPtr;
		}

		internal IntPtr FindMethodPtr(IntPtr monoClass, string methodName, out int index, params MonoTypeEnum[] arguments)
		{
			Debug.Log("Searching for " + methodName + " on class " + monoClass.ToString("X"));
			while (monoClass != IntPtr.Zero)
			{
				IntPtr monoMethod;
				IntPtr itrHandle = Marshal.AllocHGlobal(IntPtr.Size);
				Marshal.WriteIntPtr(itrHandle, IntPtr.Zero);
				index = 0;
				while ((monoMethod = mono_class_get_methods(monoClass, itrHandle)) != IntPtr.Zero)
				{
					//Debug.Log("monoMethod: " + monoMethod.ToString("X"));
					var pMethod = (MonoMethod*) monoMethod;
					//Debug.Log(pMethod->name);

					var name = pMethod->name;

					if (name == methodName)
					{
						Marshal.FreeHGlobal(itrHandle);
						Debug.Log("Found " + methodName + " at index " + index);
						return monoMethod;
					}
					index++;
				}
				Marshal.FreeHGlobal(itrHandle);

				// Iterate to the parent class, and see if the method is in there instead.
				monoClass = mono_class_get_parent(monoClass);
				//Log.Debug("\tCould not find in passed type. Trying parent at 0x" + classPtr.ToString("X"));
			}
			index = -1;
			//throw new Exception("Could not find method: " + methodName);
			return IntPtr.Zero;
		}

		internal MonoTypeEnum[] BuildFunctionArgs(MethodBase method)
		{
			List<MonoTypeEnum> types = new List<MonoTypeEnum>();
			foreach (var param in method.GetParameters())
			{
				if (param.ParameterType == typeof(bool))
				{
					types.Add(MonoTypeEnum.Boolean);
				}
				else if (param.ParameterType == typeof(char))
				{
					types.Add(MonoTypeEnum.Char);
				}
				else if (param.ParameterType == typeof(sbyte))
				{
					types.Add(MonoTypeEnum.I1);
				}
				else if (param.ParameterType == typeof(byte))
				{
					types.Add(MonoTypeEnum.U1);
				}
				else if (param.ParameterType == typeof(short))
				{
					types.Add(MonoTypeEnum.I2);
				}
				else if (param.ParameterType == typeof(ushort))
				{
					types.Add(MonoTypeEnum.U2);
				}
				else if (param.ParameterType == typeof(int))
				{
					types.Add(MonoTypeEnum.I4);
				}
				else if (param.ParameterType == typeof(uint))
				{
					types.Add(MonoTypeEnum.U4);
				}
				else if (param.ParameterType == typeof(long))
				{
					types.Add(MonoTypeEnum.I8);
				}
				else if (param.ParameterType == typeof(ulong))
				{
					types.Add(MonoTypeEnum.U8);
				}

				else if (param.ParameterType == typeof(float))
				{
					types.Add(MonoTypeEnum.R4);
				}
				else if (param.ParameterType == typeof(double))
				{
					types.Add(MonoTypeEnum.R8);
				}
				else if (param.ParameterType == typeof(string))
				{
					types.Add(MonoTypeEnum.String);
				}
				else if (param.ParameterType == typeof(IntPtr))
				{
					types.Add(MonoTypeEnum.Ptr);
				}
				else if (param.ParameterType.IsValueType)
				{
					types.Add(MonoTypeEnum.ValueType);
				}
				else if (param.ParameterType.IsClass)
				{
					types.Add(MonoTypeEnum.Class);
				}
			}
			return types.ToArray();
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct MonoMethodSignature
		{
			// The laziness of PInvoke Interop Assistant to the rescue!

			/// MonoType*
			internal IntPtr ret;

			/// guint16->WORD->unsigned short
			internal ushort param_count;

			/// gint16->int
			internal short sentinelpos;

			/// generic_param_count : 16
			/// call_convention : 6
			/// hasthis : 1
			/// explicit_this : 1
			/// pinvoke : 1
			/// is_inflated : 1
			/// has_type_parameters : 1
			internal uint bitvector1;


			// Params is basically the last part of the method signature.
			// So we'll use Marshal.Sizeof to get the base size, and read the param array after.
			// MonoType*[] 0xE
			// internal IntPtr @params;
		}

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

				internal uint token
				{
					get
					{
						return (bitvector1 & 4294967294u)
						       / 2;
					}
					set
					{
						bitvector1 = (value * 2)
						             | bitvector1;
					}
				}
			}

			/// MonoTypeDataUnion
			internal IntPtr data;

			/// attrs : 16
			/// type : 8
			/// num_mods : 6
			/// byref : 1
			/// pinned : 1
			internal uint bitvector1;

			/// MonoCustomMod[]
			internal MonoCustomMod modifiers;

			internal uint attrs { get { return bitvector1 & 0xffff; } }

			internal uint type { get { return (bitvector1 & 0xff0000) / 0x10000; } }

			internal uint num_mods { get { return (bitvector1 & 0x3f000000) / 0x1000000; } }

			internal uint byref { get { return (bitvector1 & 0x40000000) / 0x40000000; } }

			internal uint pinned { get { return (bitvector1 & 0x80000000) / 0x80000000; } }
		}

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct MonoClass
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
				/// Returns the fully qualified type name of this instance.
				/// </summary>
				/// <returns>
				/// A <see cref="T:System.String"/> containing a fully qualified type name.
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
				/// Returns the fully qualified type name of this instance.
				/// </summary>
				/// <returns>
				/// A <see cref="T:System.String"/> containing a fully qualified type name.
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

			private IntPtr name_ptr;

			public string name
			{
				get
				{
					if (name_ptr == IntPtr.Zero)
						return "<INVALID_NAME>";
					return Marshal.PtrToStringAnsi(name_ptr);
				}
			}
			
			private IntPtr name_space_ptr;

			public string name_space
			{
				get
				{
					if (name_space_ptr == IntPtr.Zero)
						return "<INVALID_NAMESPACE>";
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
			/// Returns the fully qualified type name of this instance.
			/// </summary>
			/// <returns>
			/// A <see cref="T:System.String"/> containing a fully qualified type name.
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
						(IntPtr)methods,
						this_arg,
						byval_arg,
						generic_class,
						generic_container,
						gc_descr,
						runtime_info,
						next_class_cache,
						(IntPtr)vtable,
						ext);
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.FastCall)] // mono.dll with CS uses rcx, rdx and assumes the callback cleans stack. That's a fastcall folks!
		internal delegate void AssemblyForEachCallback(IntPtr assembly, IntPtr userData);
		[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
		internal struct MonoMethod
		{

			/// guint16->WORD->unsigned short
			public ushort flags;

			/// guint16->WORD->unsigned short
			public ushort iflags;

			/// guint32->DWORD->unsigned int
			public uint token;

			/// MonoClass*
			public System.IntPtr klass;

			/// MonoMethodSignature*
			public System.IntPtr signature;
			
			private IntPtr name_ptr;

			public string name
			{
				get
				{
					if (name_ptr == IntPtr.Zero)
						return "<INVALID_NAME>";
					return Marshal.PtrToStringAnsi(name_ptr);
				}
			}

			/// inline_info : 1
			///inline_failure : 1
			///wrapper_type : 5
			///string_ctor : 1
			///save_lmf : 1
			///dynamic : 1
			///sre_method : 1
			///is_generic : 1
			///is_inflated : 1
			///skip_visibility : 1
			///verification_success : 1
			///is_mb_open : 1
			///slot : 16
			public uint bitvector1;

			public uint inline_info
			{
				get
				{
					return ((uint)((this.bitvector1 & 1u)));
				}
				set
				{
					this.bitvector1 = ((uint)((value | this.bitvector1)));
				}
			}

			public uint inline_failure
			{
				get
				{
					return ((uint)(((this.bitvector1 & 2u)
								/ 2)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 2)
								| this.bitvector1)));
				}
			}

			public uint wrapper_type
			{
				get
				{
					return ((uint)(((this.bitvector1 & 124u)
								/ 4)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 4)
								| this.bitvector1)));
				}
			}

			public uint string_ctor
			{
				get
				{
					return ((uint)(((this.bitvector1 & 128u)
								/ 128)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 128)
								| this.bitvector1)));
				}
			}

			public uint save_lmf
			{
				get
				{
					return ((uint)(((this.bitvector1 & 256u)
								/ 256)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 256)
								| this.bitvector1)));
				}
			}

			public uint dynamic
			{
				get
				{
					return ((uint)(((this.bitvector1 & 512u)
								/ 512)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 512)
								| this.bitvector1)));
				}
			}

			public uint sre_method
			{
				get
				{
					return ((uint)(((this.bitvector1 & 1024u)
								/ 1024)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 1024)
								| this.bitvector1)));
				}
			}

			public uint is_generic
			{
				get
				{
					return ((uint)(((this.bitvector1 & 2048u)
								/ 2048)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 2048)
								| this.bitvector1)));
				}
			}

			public uint is_inflated
			{
				get
				{
					return ((uint)(((this.bitvector1 & 4096u)
								/ 4096)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 4096)
								| this.bitvector1)));
				}
			}

			public uint skip_visibility
			{
				get
				{
					return ((uint)(((this.bitvector1 & 8192u)
								/ 8192)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 8192)
								| this.bitvector1)));
				}
			}

			public uint verification_success
			{
				get
				{
					return ((uint)(((this.bitvector1 & 16384u)
								/ 16384)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 16384)
								| this.bitvector1)));
				}
			}

			public uint is_mb_open
			{
				get
				{
					return ((uint)(((this.bitvector1 & 32768u)
								/ 32768)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 32768)
								| this.bitvector1)));
				}
			}

			public uint slot
			{
				get
				{
					return ((uint)(((this.bitvector1 & 4294901760u)
								/ 65536)));
				}
				set
				{
					this.bitvector1 = ((uint)(((value * 65536)
								| this.bitvector1)));
				}
			}
		}

	}

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