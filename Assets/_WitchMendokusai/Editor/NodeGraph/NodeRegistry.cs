using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// `NodeBase` 서브클래스 카탈로그 — Editor 의 Create Node 메뉴에서 사용.
	/// `TypeCache.GetTypesDerivedFrom` 으로 reflection 1회 (Domain Reload 마다).
	/// 도메인 분리 (TASK-WM-034 C) — `[NodeDomain(...)]` attribute 기반 필터.
	/// </summary>
	public static class NodeRegistry
	{
		public static IEnumerable<Type> AllNodeTypes()
		{
			return TypeCache.GetTypesDerivedFrom<NodeBase>()
				.Where(t => t.IsAbstract == false && t.IsGenericTypeDefinition == false);
		}

		/// <summary>
		/// 특정 도메인의 카탈로그 노드 목록.
		/// `Generic` 도메인 = fallback (모든 노드 — 마이그레이션 전 NodeGraph 자산 호환).
		/// 다른 도메인 = `[NodeDomain]` attribute 일치만 (미마킹 노드 = Generic 으로 간주, 매칭 X).
		/// </summary>
		public static IEnumerable<Type> NodeTypesForDomain(NodeDomain domain)
		{
			if (domain == NodeDomain.Generic)
				return AllNodeTypes();

			return AllNodeTypes().Where(t => GetDomain(t) == domain);
		}

		private static NodeDomain GetDomain(Type t)
		{
			NodeDomainAttribute attr = t.GetCustomAttribute<NodeDomainAttribute>(inherit: false);
			return attr == null ? NodeDomain.Generic : attr.Domain;
		}
	}
}
