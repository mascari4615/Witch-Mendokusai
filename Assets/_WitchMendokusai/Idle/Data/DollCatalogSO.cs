using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleDollCatalog", menuName = "WM/Idle/Doll Catalog")]
	public sealed class DollCatalogSO : ScriptableObject
	{
		[SerializeField] private List<DollDefinitionSO> dolls = new List<DollDefinitionSO>();

		[Tooltip("새 판이 처음부터 가진 인형. 첫 번째가 대표. 비우면 0번 하나 (사용자 결정 2026-09-21: 욘, 링, 알리사)")]
		[SerializeField] private List<DollDefinitionSO> starters = new List<DollDefinitionSO>();

		public int Count => dolls.Count;

		public IdleDollCatalog ToDomain()
		{
			IdleDollKind[] definitions = new IdleDollKind[dolls.Count];
			for (int index = 0; index < dolls.Count; index++)
			{
				DollDefinitionSO doll = dolls[index];
				if (doll == null)
				{
					throw new InvalidOperationException("인형 카탈로그 " + index + "번 항목이 비었다.");
				}

				definitions[index] = doll.ToDomain();
			}

			List<int> starterIds = new List<int>();
			for (int index = 0; index < starters.Count; index++)
			{
				if (starters[index] != null)
				{
					starterIds.Add(starters[index].ID);
				}
			}

			return new IdleDollCatalog(definitions, starterIds.Count > 0 ? starterIds : null);
		}

		public Sprite SpriteOf(int id)
		{
			return id >= 0 && id < dolls.Count && dolls[id] != null ? dolls[id].Sprite : null;
		}

		/// <summary>카드와 자리 아이콘용 얼굴</summary>
		public Sprite FaceOf(int id)
		{
			return id >= 0 && id < dolls.Count && dolls[id] != null ? dolls[id].Face : null;
		}

		public bool TryValidate(out string error)
		{
			if (dolls.Count == 0)
			{
				error = "dolls must not be empty";
				return false;
			}

			for (int index = 0; index < dolls.Count; index++)
			{
				DollDefinitionSO doll = dolls[index];
				if (doll == null)
				{
					error = "dolls contains an empty entry at " + index;
					return false;
				}

				if (doll.ID != index)
				{
					error = "doll ID must match its catalog index at " + index;
					return false;
				}

				if (string.IsNullOrWhiteSpace(doll.Name))
				{
					error = "doll name must not be empty at " + index;
					return false;
				}

				if (doll.Sprite == null)
				{
					error = "doll portrait must not be empty at " + index;
					return false;
				}
			}

			error = string.Empty;
			return true;
		}
	}
}
