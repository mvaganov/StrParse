using System;
using System.Collections.Generic;

public static class IListExtension {
	public static Int32 BinarySearchIndexOf<T>(this IList<T> list, T value, IComparer<T> comparer = null) {
		if (list == null)
			throw new ArgumentNullException("list");
		if (comparer == null) { comparer = Comparer<T>.Default; }
		Int32 lower = 0, upper = list.Count - 1;
		while (lower <= upper) {
			Int32 middle = lower + (upper - lower) / 2, comparisonResult = comparer.Compare(value, list[middle]);
			if (comparisonResult == 0)
				return middle;
			else if (comparisonResult < 0)
				upper = middle - 1;
			else
				lower = middle + 1;
		}
		return ~lower;
	}

	public static T[] GetRange<T>(this IList<T> source, int index, int length) {
		T[] list = new T[length];
		for (int i = 0; i < length; ++i) { list[i] = source[index + i]; }
		return list;
	}

	public static string Join<T>(this IList<T> source, string separator, Func<T, string> toString = null) {
		string[] strings = new string[source.Count];
		if (toString == null) { toString = o => o.ToString(); }
		for (int i = 0; i < strings.Length; ++i) {
			strings[i] = toString.Invoke(source[i]);
		}
		return string.Join(separator, strings);
	}
}
