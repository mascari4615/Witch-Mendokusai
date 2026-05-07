using System;

namespace WitchMendokusai
{
	/// <summary>
	/// InputEventType 항목에 메타데이터(카테고리·표시명·기본키)를 단일 출처로 부착한다.
	/// KeybindRegistry 가 enum 을 스캔해 자동 수집 → KeybindView 안내·검증·향후 리바인드의 기반.
	///
	/// path 는 Unity Input System 바인딩 경로 형식 — `&lt;Keyboard&gt;/j`, `&lt;Mouse&gt;/leftButton` 등.
	/// `WMInput.inputactions` JSON 의 path 와 일치해야 하며 KeybindRegistry 가 부팅 시 검증한다.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class InputEventAttribute : Attribute
	{
		public string Category { get; }
		public string DisplayName { get; }
		public string DefaultPath { get; }

		public InputEventAttribute(string category, string displayName, string defaultPath)
		{
			Category = category;
			DisplayName = displayName;
			DefaultPath = defaultPath;
		}
	}
}
