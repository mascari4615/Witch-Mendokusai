using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assemblies;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 노드 타입 → Provider 카탈로그 (reflection 기반). 첫 lookup 시 <see cref="AppDomain.CurrentDomain"/> 의 모든 어셈블리에서
	/// <see cref="NodeRuntimeViewAttribute"/> 박힌 <see cref="INodeRuntimeViewProvider"/> 구현체 수집 + Activator.CreateInstance.
	///
	/// Unity Domain Reload 시 정적 캐시가 초기화돼 다음 lookup 에서 재스캔 — assembly hot reload 자동 반영.
	/// 명시적 무효화는 <see cref="Invalidate"/> — 테스트나 동적 attribute 변경 시.
	/// </summary>
	public static class NodeRuntimeProviderRegistry
	{
		private static readonly DefaultNodeRuntimeViewProvider DEFAULT_PROVIDER = new();

		private static Dictionary<Type, INodeRuntimeViewProvider> providersByNodeType;

		/// <summary>노드 타입에 등록된 Provider, 미등록 시 default fallback.</summary>
		public static INodeRuntimeViewProvider GetProvider(Type nodeType)
		{
			EnsureInitialized();

			if (nodeType == null)
				return DEFAULT_PROVIDER;

			if (providersByNodeType.TryGetValue(nodeType, out INodeRuntimeViewProvider provider))
				return provider;

			return DEFAULT_PROVIDER;
		}

		/// <summary>캐시 무효화 — 다음 lookup 시 재스캔. 테스트 / 동적 attribute 변경 시.</summary>
		public static void Invalidate()
		{
			providersByNodeType = null;
		}

		private static void EnsureInitialized()
		{
			if (providersByNodeType != null)
				return;

			providersByNodeType = new Dictionary<Type, INodeRuntimeViewProvider>();

			foreach (Assembly assembly in CurrentAssemblies.GetLoadedAssemblies())
			{
				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException loadException)
				{
					types = loadException.Types;
				}

				foreach (Type type in types)
				{
					if (type == null)
						continue;

					if (type.IsAbstract || type.IsInterface)
						continue;

					if (typeof(INodeRuntimeViewProvider).IsAssignableFrom(type) == false)
						continue;

					NodeRuntimeViewAttribute attribute = type.GetCustomAttribute<NodeRuntimeViewAttribute>();
					if (attribute == null)
						continue;

					if (attribute.NodeType == null)
					{
						Debug.LogWarning($"[NodeRuntimeProviderRegistry] {type.FullName} 의 NodeRuntimeViewAttribute.NodeType 이 null — 등록 스킵");
						continue;
					}

					INodeRuntimeViewProvider provider;
					try
					{
						provider = (INodeRuntimeViewProvider)Activator.CreateInstance(type);
					}
					catch (Exception createException)
					{
						Debug.LogError($"[NodeRuntimeProviderRegistry] {type.FullName} 인스턴스화 실패: {createException.Message}");
						continue;
					}

					providersByNodeType[attribute.NodeType] = provider;
				}
			}
		}
	}
}
