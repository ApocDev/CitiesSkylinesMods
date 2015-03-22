using System;
using System.Linq;
using System.Reflection;

namespace ApocDev.CitySkylines.Mod.Utils
{
	// The copy/pasta is strong in this one. No real way to do simple static/nonstatic methods without copying code.
	internal class ReflectionUtils
	{
		#region Invoke
		/// <summary>
		/// Invokes a static method on the specified type.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="type"></param>
		/// <param name="methodName"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public static T InvokeMethod<T>(Type type, string methodName, params object[] args)
		{
			// Try and find the method via the arguments passed in.
			var methodArgumentTypes = args.Select(a => a.GetType()).ToArray();

			// Pass a null array to GetMethod as it shortcuts early instead of doing some sanity checks inside GetMethod itself.
			if (methodArgumentTypes.Length == 0)
			{
				methodArgumentTypes = null;
			}

			var methodInfo = type.GetMethod(methodName,
				BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
				null,
				methodArgumentTypes,
				null);

			if (methodInfo == null)
			{
				throw new ArgumentException(
					string.Format("Method '{0}({1})' could not be found on object of type {2}",
						methodName,
						methodArgumentTypes != null ? string.Join(", ", methodArgumentTypes.Select(t => t.Name).ToArray()) : string.Empty,
						type.FullName),
					"methodName");
			}
			
			// Note: The invokes here are specifically not in a try/catch. The exception will bubble up to the caller so it can be handled there properly,
			// rather than suppressing anything we'd do here.
			if (methodInfo.ReturnType == typeof(void))
			{
				methodInfo.Invoke(null, args);
				return default(T);
			}
			return (T)methodInfo.Invoke(null, args);
		}

		/// <summary>
		/// Invokes an instance method on the specified object.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="instance"></param>
		/// <param name="methodName"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public static T InvokeMethod<T>(object instance, string methodName, params object[] args)
		{
			// Try and find the method via the arguments passed in.
			var methodArgumentTypes = args.Select(a => a.GetType()).ToArray();

			// Pass a null array to GetMethod as it shortcuts early instead of doing some sanity checks inside GetMethod itself.
			if (methodArgumentTypes.Length == 0)
			{
				methodArgumentTypes = null;
			}

			var methodInfo = instance.GetType().GetMethod(methodName,
				BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
				null,
				methodArgumentTypes,
				null);

			if (methodInfo == null)
			{
				throw new ArgumentException(
					string.Format("Method '{0}({1})' could not be found on object of type {2}",
						methodName,
						methodArgumentTypes != null ? string.Join(", ", methodArgumentTypes.Select(t => t.Name).ToArray()) : string.Empty,
						instance.GetType().FullName),
					"methodName");
			}

			// Note: The invokes here are specifically not in a try/catch. The exception will bubble up to the caller so it can be handled there properly,
			// rather than suppressing anything we'd do here.
			if (methodInfo.ReturnType == typeof(void))
			{
				methodInfo.Invoke(instance, args);
				return default(T);
			}
			return (T) methodInfo.Invoke(instance, args);
		}

		#endregion

		#region Get/SetField

		public static T GetField<T>(Type type, string fieldName)
		{
			var field = type.GetField(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (field == null)
			{
				throw new ArgumentException("Field '" + fieldName + "' could not be found on object of type " + type.FullName, "fieldName");
			}
			return (T) field.GetValue(null);
		}

		public static void SetField(Type type, string fieldName, object value)
		{
			var field = type.GetField(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (field == null)
			{
				throw new ArgumentException("Field '" + fieldName + "' could not be found on object of type " + type.FullName, "fieldName");
			}
			field.SetValue(null, value);
		}
		public static T GetField<T>(object instance, string fieldName)
		{
			var field = instance.GetType().GetField(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (field == null)
			{
				throw new ArgumentException("Field '" + fieldName + "' could not be found on object of type " + instance.GetType().FullName, "fieldName");
			}
			return (T) field.GetValue(instance);
		}

		public static void SetField(object instance, string fieldName, object value)
		{
			var field = instance.GetType().GetField(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (field == null)
			{
				throw new ArgumentException("Field '" + fieldName + "' could not be found on object of type " + instance.GetType().FullName, "fieldName");
			}
			field.SetValue(instance, value);
		}
		#endregion
	}
}