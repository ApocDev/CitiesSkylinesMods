using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ApocDev.CitySkylines.Mod.Utils
{
	public static class ArrayUtils
	{
		public static void ResizeArray32<T>(Array32<T> array, uint newSize)
		{
			array.m_size = newSize;
			Array.Resize(ref array.m_buffer, (int)newSize);
			var unusedCount = ReflectionUtils.GetField<uint>(array, "m_unusedCount");
			var unusedItems = ReflectionUtils.GetField<uint[]>(array, "m_unusedItems");

			uint[] newUnusedItems = new uint[newSize];
			Buffer.BlockCopy(unusedItems, 0, newUnusedItems, 0, 4 * unusedItems.Length);

			// Now add our own unused items
			for (uint i = (uint)unusedItems.Length; i < newSize + 1; i++)
			{
				newUnusedItems[i - 1] = i;
			}

			// Update the unusedCount to be in line with the new array size
			// This is just adding the newly sized additions.
			unusedCount += newSize - unusedCount;

			ReflectionUtils.SetField(array, "m_unusedCount", unusedCount);
			ReflectionUtils.SetField(array, "m_unusedItems", unusedItems);

			// var nextFree = ReflectionUtils.InvokeMethod<uint>(array, "NextFreeItem");
			// var nextFree = array.NextFreeItem();
		}

		public static void ResizeArray16<T>(Array16<T> array, uint newSize)
		{
			if (newSize > 0xFFFF)
				throw new ArgumentException("ResizeArray8 can only accept sizes up to 0xFFFF (65535)", "newSize");

			array.m_size = newSize;
			Array.Resize(ref array.m_buffer, (int)newSize);

			var unusedCount = (uint)array.GetType().GetField("m_unusedCount").GetValue(array);
			var unusedItems = (ushort[])array.GetType().GetField("m_unusedItems").GetValue(array);

			ushort[] newUnusedItems = new ushort[newSize];
			Buffer.BlockCopy(unusedItems, 0, newUnusedItems, 0, 4 * unusedItems.Length);

			// Now add our own unused items
			for (uint i = (uint)unusedItems.Length; i < newSize + 1; i++)
			{
				newUnusedItems[i - 1] = (ushort)i;
			}

			// Update the unusedCount to be in line with the new array size
			// This is just adding the newly sized additions.
			unusedCount += newSize - unusedCount;

			array.GetType().GetField("m_unusedCount").SetValue(array, unusedCount);
			array.GetType().GetField("m_unusedItems").SetValue(array, newUnusedItems);
		}
		public static void ResizeArray8<T>(Array8<T> array, uint newSize)
		{
			if (newSize > 0xFF)
				throw new ArgumentException("ResizeArray8 can only accept sizes up to 0xFF (255)", "newSize");

			array.m_size = newSize;
			Array.Resize(ref array.m_buffer, (int)newSize);

			var unusedCount = (uint)array.GetType().GetField("m_unusedCount").GetValue(array);
			var unusedItems = (byte[])array.GetType().GetField("m_unusedItems").GetValue(array);

			byte[] newUnusedItems = new byte[newSize];
			Buffer.BlockCopy(unusedItems, 0, newUnusedItems, 0, 4 * unusedItems.Length);

			// Now add our own unused items
			for (uint i = (uint)unusedItems.Length; i < newSize + 1; i++)
			{
				newUnusedItems[i - 1] = (byte)i;
			}

			// Update the unusedCount to be in line with the new array size
			// This is just adding the newly sized additions.
			unusedCount += newSize - unusedCount;

			array.GetType().GetField("m_unusedCount").SetValue(array, unusedCount);
			array.GetType().GetField("m_unusedItems").SetValue(array, newUnusedItems);
		}
	}
}
