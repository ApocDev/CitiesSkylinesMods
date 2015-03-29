using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TransportCapacity
{
	public static class EnumUtils
	{
		public delegate void RefAction<T>(uint index, ref T val) where T : struct;

		public delegate uint NextIndex32<T>(ref T val) where T : struct;
		public delegate ushort NextIndex16<T>(ref T val) where T : struct;
		public delegate byte NextIndex8<T>(ref T val) where T : struct;

		public static void IterateLinkedList<T>(this Array32<T> arr, uint startIndex, NextIndex32<T> nextIndex, RefAction<T> action) where T : struct
		{
			var cur = startIndex;
			while (cur != 0)
			{
				action(cur, ref arr.m_buffer[cur]);
				cur = nextIndex(ref arr.m_buffer[cur]);
			}
		}

		public static void IterateLinkedList<T>(this Array16<T> arr, uint startIndex, NextIndex16<T> nextIndex, RefAction<T> action) where T : struct
		{
			var cur = startIndex;
			while (cur != 0)
			{
				action(cur, ref arr.m_buffer[cur]);
				cur = nextIndex(ref arr.m_buffer[cur]);
			}
		}

		public static void IterateLinkedList<T>(this Array8<T> arr, uint startIndex, NextIndex8<T> nextIndex, RefAction<T> action) where T : struct
		{
			var cur = startIndex;
			while (cur != 0)
			{
				action(cur, ref arr.m_buffer[cur]);
				cur = nextIndex(ref arr.m_buffer[cur]);
			}
		}
	}
}
