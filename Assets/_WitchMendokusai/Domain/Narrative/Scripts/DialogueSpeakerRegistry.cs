using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 원고에 쓴 이름 → 그 이름으로 말하는 대상 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 대본은 사람 이름으로 쓴다(`&gt; 욘: "귀찮아."`). 그런데 게임에는 「욘」이라는 글자가
	///   가리키는 것이 없었다 — 그래서 말풍선이 **누가 말하든 카메라 앞**에 떴다. 이 표가 그 사이를 잇는다.
	///
	/// 이름은 **앞뒤 공백만 걷어내고 그대로** 쓴다. 대소문자를 무시하지 않는 이유: 한글은 그 개념이 없고,
	/// 영어 이름을 섞어 쓸 때 「Ring」과 「ring」을 굳이 같게 볼 근거가 없다(원고는 사람이 쓰는 것이고
	/// 오타는 조용히 맞춰 주는 것보다 드러나는 게 낫다).
	///
	/// 등록·해제는 캐릭터 쪽이 한다(Awake/OnDestroy). 못 찾으면 <see cref="TryGetAnchor"/> 가 false —
	/// 부르는 쪽이 「그럼 어디에 띄울지」를 정한다(터뜨리지 않는다. 이름 하나 안 맞았다고 대화가 죽으면 안 된다).
	/// </summary>
	public sealed class DialogueSpeakerRegistry
	{
		private readonly Dictionary<string, Transform> anchorsByName = new(StringComparer.Ordinal);

		public int Count => anchorsByName.Count;

		/// <summary>이 이름으로 말할 때 말풍선이 붙을 자리. 같은 이름을 다시 등록하면 나중 것이 이긴다.</summary>
		public void Register(string speakerName, Transform anchor)
		{
			string key = Normalize(speakerName);
			if (key == null || anchor == null)
			{
				return;
			}
			anchorsByName[key] = anchor;
		}

		/// <summary>자기 것만 해제 — 다른 대상이 이미 그 이름을 가져갔으면 안 건드린다(재등장 race 안전).</summary>
		public void Unregister(string speakerName, Transform anchor)
		{
			string key = Normalize(speakerName);
			if (key == null)
			{
				return;
			}
			if (anchorsByName.TryGetValue(key, out Transform current) && current == anchor)
			{
				anchorsByName.Remove(key);
			}
		}

		public bool TryGetAnchor(string speakerName, out Transform anchor)
		{
			anchor = null;
			string key = Normalize(speakerName);
			if (key == null)
			{
				return false;
			}
			if (anchorsByName.TryGetValue(key, out Transform found) == false)
			{
				return false;
			}

			// 등록된 대상이 이미 사라졌으면(씬 전환·파괴) 없는 것으로 본다 — Unity 의 "죽은 참조" 함정.
			if (found == null)
			{
				anchorsByName.Remove(key);
				return false;
			}

			anchor = found;
			return true;
		}

		public void Clear() => anchorsByName.Clear();

		private static string Normalize(string speakerName)
		{
			if (string.IsNullOrWhiteSpace(speakerName))
			{
				return null;
			}
			return speakerName.Trim();
		}
	}

	/// <summary>
	/// 화자 표 static accessor — <see cref="DialogueHistoryBridge"/> 동형(Bridge 패턴).
	/// 캐릭터(NPC)가 자기 이름을 등록하고, 대화 러너가 여기서 찾는다.
	/// </summary>
	public static class DialogueSpeakerBridge
	{
		private static DialogueSpeakerRegistry registry;

		public static void Register(DialogueSpeakerRegistry speakerRegistry) => registry = speakerRegistry;

		public static void Clear(DialogueSpeakerRegistry speakerRegistry)
		{
			if (registry == speakerRegistry)
			{
				registry = null;
			}
		}

		/// <summary>등록된 표. 아직 없으면 null — 부르는 쪽이 판단한다.</summary>
		public static DialogueSpeakerRegistry Current => registry;

		/// <summary>표가 없거나 이름이 없으면 false. 캐릭터 쪽 배선이 아직이어도 대화는 돌아야 한다.</summary>
		public static bool TryGetAnchor(string speakerName, out Transform anchor)
		{
			anchor = null;
			return registry != null && registry.TryGetAnchor(speakerName, out anchor);
		}
	}
}
