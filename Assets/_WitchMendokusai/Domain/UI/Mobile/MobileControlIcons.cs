using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 폰 조작 버튼 그림의 단일 출처. 버튼 글자 대신 이 그림을 띄우고, 이름은 접근성 이름으로만 남김.
	/// 그림 원본은 game-icons.net (CC BY 3.0, README 크레딧). 키 버튼은 InputEventType 으로, 키가 없는 버튼은 이름 칸으로 찾음
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MobileControlIcons), menuName = "WM/UI/" + nameof(MobileControlIcons))]
	public class MobileControlIcons : ScriptableObject
	{
		[Serializable]
		public struct Entry
		{
			public InputEventType EventType;
			public Texture2D Icon;
		}

		[SerializeField] private List<Entry> entries = new();
		[field: SerializeField] public Texture2D WindowMenu { get; private set; }
		[field: SerializeField] public Texture2D LayoutEdit { get; private set; }
		[field: SerializeField] public Texture2D LayoutDone { get; private set; }
		[field: SerializeField] public Texture2D LayoutReset { get; private set; }

		// 등록 누락은 조용히 글자로 돌아가지 않고 바로 드러나게 예외
		public Texture2D For(InputEventType eventType)
		{
			foreach (Entry entry in entries)
			{
				if (entry.EventType == eventType)
					return entry.Icon;
			}
			throw new KeyNotFoundException($"{nameof(MobileControlIcons)} 에 {eventType} 그림이 없음");
		}
	}
}
