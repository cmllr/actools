using System.Drawing;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public class PaintShopSource {
        public static readonly PaintShopSource InputSource = new PaintShopSource();
        public static readonly PaintShopSource White = new PaintShopSource(System.Drawing.Color.White);

        public readonly bool UseInput;

        public readonly Color? Color;

        [CanBeNull]
        public readonly string Name;

        [CanBeNull]
        public readonly byte[] Data;

        public PaintShopSource() {
            UseInput = true;
        }

        public PaintShopSource(Color baseColor) {
            Color = baseColor;
        }

        public PaintShopSource([NotNull] string name) {
            Name = name;
        }

        public PaintShopSource([NotNull] byte[] data) {
            Data = data;
        }

        public override int GetHashCode() {
            return UseInput ? -1 : Color?.GetHashCode() ?? Name?.GetHashCode() ?? Data?.GetHashCode() ?? 0;
        }

        public override string ToString() {
            if (UseInput) return "( PaintShopSource: use input )";
            if (Color != null) return $"( PaintShopSource: color={Color.Value} )";
            if (Name != null) return $"( PaintShopSource: name={Name} )";
            if (Data != null) return $"( PaintShopSource: {Data} bytes )";
            return "( PaintShopSource: nothing )";
        }
    }

    public interface IPaintShopRenderer {
        /// <summary>
        /// Override texture.
        /// </summary>
        /// <param name="textureName">Name of a texture being replaced.</param>
        /// <param name="source">If null, reset to its original state.</param>
        /// <returns>True if successfull</returns>
        bool OverrideTexture(string textureName, [CanBeNull] PaintShopSource source);

        bool OverrideTexture(string textureName, Color color, double alpha);

        bool OverrideTextureFlakes(string textureName, Color color, double flakes);

        bool OverrideTexturePattern(string textureName, [NotNull] PaintShopSource ao, bool autoAdjustLevels, [NotNull] PaintShopSource pattern,
                [CanBeNull] PaintShopSource overlay, [NotNull] Color[] colors);

        bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular, bool autoAdjustLevels, bool fixGloss,
                [NotNull] PaintShopSource source);

        bool OverrideTextureTint(string textureName, Color color, bool autoAdjustLevels, double alphaAdd, [NotNull] PaintShopSource source);

        Task SaveTextureAsync(string filename, [NotNull] PaintShopSource source);

        Task SaveTextureAsync(string filename, Color color, double alpha);

        Task SaveTextureFlakesAsync(string filename, Color color, double flakes);

        Task SaveTexturePatternAsync(string filename, [NotNull] PaintShopSource ao, bool autoAdjustLevels, [NotNull] PaintShopSource pattern,
                [CanBeNull] PaintShopSource overlay, [NotNull] Color[] colors);

        Task SaveTextureMapsAsync(string filename, double reflection, double gloss, double specular, bool autoAdjustLevels, bool fixGloss,
                [NotNull] PaintShopSource source);

        Task SaveTextureTintAsync(string filename, Color color, bool autoAdjustLevels, double alphaAdd, [NotNull] PaintShopSource source);
    }
}