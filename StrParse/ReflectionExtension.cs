using System;
using System.Collections.Generic;
using System.Reflection;

namespace NonStandard {
	public static class GetSubClassesExtension {
		public static Type GetICollectionType(this Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)) {
					return i.GetGenericArguments()[0];
				}
			}
			return null;
		}
		public static Type GetIListType(this Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)) {
					return i.GetGenericArguments()[0];
				}
			}
			return null;
		}
		public static KeyValuePair<Type, Type> GetIDictionaryType(this Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)) {
					return new KeyValuePair<Type, Type>(i.GetGenericArguments()[0], i.GetGenericArguments()[1]);
				}
			}
			if (type.BaseType != null) { return GetIDictionaryType(type.BaseType); }
			return new KeyValuePair<Type, Type>(null, null);
		}
		public static Type[] GetSubClasses(this Type type) {
			Type[] allLocalTypes = Assembly.GetAssembly(type).GetTypes();
			List<Type> subTypes = new List<Type>();
			for (int i = 0; i < allLocalTypes.Length; ++i) {
				Type t = allLocalTypes[i];
				if (t.IsClass && !t.IsAbstract && t.IsSubclassOf(type)) { subTypes.Add(t); }
			}
			return subTypes.ToArray();
		}

		public static object GetNewInstance(this Type t) { return Activator.CreateInstance(t); }
	}
}