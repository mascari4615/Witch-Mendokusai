using System;
using System.Collections.Generic;

namespace Appearance
{
    [Serializable]
    public sealed class AppearanceDocument
    {
        public string Format = "appearance";
        public int Version = 1;
        public string Id;
        public string DisplayName;
        public List<SurfaceColor> Colors = new List<SurfaceColor>();
    }

    [Serializable]
    public sealed class SurfaceColor
    {
        public string Surface;
        public string Channel;
        public string ColorSpace;
        public float[] Rgba;
    }

    public interface IAppearanceTarget
    {
        void Apply(IReadOnlyList<SurfaceColor> colors);
    }

    public static class AppearanceApplication
    {
        public static IReadOnlyList<string> Validate(AppearanceDocument document)
        {
            List<string> errors = new List<string>();
            if (document == null)
            {
                errors.Add("Document is required.");
                return errors;
            }
            if (document.Format != "appearance" || document.Version != 1)
            {
                errors.Add("Unsupported appearance format or version.");
            }
            if (string.IsNullOrWhiteSpace(document.Id))
            {
                errors.Add("Id is required.");
            }
            if (document.Colors == null || document.Colors.Count == 0)
            {
                errors.Add("At least one surface color is required.");
                return errors;
            }
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (SurfaceColor color in document.Colors)
            {
                if (color == null || string.IsNullOrWhiteSpace(color.Surface) ||
                    string.IsNullOrWhiteSpace(color.Channel))
                {
                    errors.Add("Surface and channel are required.");
                    continue;
                }
                if (keys.Add(color.Surface.Length + ":" + color.Surface + color.Channel) == false)
                {
                    errors.Add("Duplicate surface channel: " + color.Surface + "/" + color.Channel);
                }
                if (color.ColorSpace != "srgb" && color.ColorSpace != "linear" &&
                    color.ColorSpace != "unspecified")
                {
                    errors.Add("Unknown color space: " + color.Surface);
                }
                if (color.Rgba == null || color.Rgba.Length != 4)
                {
                    errors.Add("RGBA requires four components: " + color.Surface);
                    continue;
                }
                foreach (float component in color.Rgba)
                {
                    if (float.IsNaN(component) || float.IsInfinity(component) || component < 0 || component > 1)
                    {
                        errors.Add("RGBA components must be finite and normalized: " + color.Surface);
                        break;
                    }
                }
            }
            return errors;
        }

        public static void Apply(AppearanceDocument document, IAppearanceTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            IReadOnlyList<string> errors = Validate(document);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join("\n", errors), nameof(document));
            }
            target.Apply(document.Colors);
        }
    }
}
