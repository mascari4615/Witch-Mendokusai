using System.Collections.Generic;
using Appearance.Serialization;
using UnityEngine;

namespace Appearance.Unity
{
    public sealed class AppearancePalette : MonoBehaviour, IAppearanceTarget
    {
        [SerializeField] private TextAsset document;
        private IReadOnlyList<SurfaceColor> colors = new List<SurfaceColor>();

        public IReadOnlyList<SurfaceColor> Colors => colors;

        public void Load(string json)
        {
            AppearanceApplication.Apply(AppearanceJson.Read(json), this);
        }

        public void Apply(IReadOnlyList<SurfaceColor> values)
        {
            List<SurfaceColor> snapshot = new List<SurfaceColor>();
            foreach (SurfaceColor value in values)
            {
                snapshot.Add(new SurfaceColor
                {
                    Surface = value.Surface,
                    Channel = value.Channel,
                    ColorSpace = value.ColorSpace,
                    Rgba = (float[])value.Rgba.Clone()
                });
            }
            colors = snapshot;
        }

        private void Awake()
        {
            if (document != null)
            {
                Load(document.text);
            }
        }
    }
}
