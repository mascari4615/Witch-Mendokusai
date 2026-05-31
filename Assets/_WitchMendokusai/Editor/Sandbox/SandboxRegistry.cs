using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace WitchMendokusai.Sandbox
{
	// ISandboxDemo 구현을 TypeCache 로 자동발견(어셈블리 무관). 새 데모 = 인터페이스 구현 + 파라미터 없는 ctor 만.
	public static class SandboxRegistry
	{
		public static IReadOnlyList<ISandboxDemo> Discover()
		{
			List<ISandboxDemo> demos = new();
			foreach (Type type in TypeCache.GetTypesDerivedFrom<ISandboxDemo>())
			{
				if (type.IsAbstract || type.IsInterface)
				{
					continue;
				}

				if (type.GetConstructor(Type.EmptyTypes) == null)
				{
					continue;
				}

				demos.Add((ISandboxDemo)Activator.CreateInstance(type));
			}

			return demos.OrderBy(demo => demo.Category).ThenBy(demo => demo.Title).ToList();
		}

		public static ISandboxDemo Find(string title)
		{
			foreach (ISandboxDemo demo in Discover())
			{
				if (string.Equals(demo.Title, title, StringComparison.OrdinalIgnoreCase))
				{
					return demo;
				}
			}

			return null;
		}
	}
}
