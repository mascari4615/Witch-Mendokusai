using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	[CustomPropertyDrawer(typeof(NPCPanelInfo))]
	public class NPCPanelInfo_PropertyDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			VisualElement container = new();

			var popup = new UnityEngine.UIElements.PopupWindow();
			popup.Add(new PropertyField(property.FindPropertyRelative($"{nameof(NPCPanelInfo.Type)}"), "Type"));
			popup.Add(new PropertyField(property.FindPropertyRelative($"{nameof(NPCPanelInfo.DataSOs)}"), "DataSOs"));
			container.Add(popup);

			return container;
		}
	}
}