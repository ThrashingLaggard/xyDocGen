namespace xyDocumentor.Core.Fonts;

using PdfSharpCore.Fonts;
using System.IO;
using System.Reflection;

    /// <summary>
    /// Resolves font ressources from the project's resource folder...eventually... when i add them
    /// </summary>
    public class ResourceFontResolver : IFontResolver
    {
        public string DefaultFontName => "Inter";

        /// <summary>
        /// Retrieves the font data as a byte array for the specified font face name.
        /// </summary>
        /// <remarks>This method accesses embedded resources within the assembly to retrieve font data.
        /// Ensure that the specified font face name corresponds to an available resource.</remarks>
        /// <param name="faceName">The name of the font face to retrieve. Supported values include "Inter#Regular", "Inter#Bold", and
        /// "Cascadia#Regular". If an unsupported face name is provided, "Inter#Regular" is used by default.</param>
        /// <returns>A byte array containing the font data for the specified font face.</returns>
        public byte[] GetFont(string faceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            var resName = faceName switch
            {
                "Inter#Regular" => "YourAssembly.Resources.Inter-Regular.ttf",
                "Inter#Bold" => "YourAssembly.Resources.Inter-Bold.ttf",
                "Cascadia#Regular" => "YourAssembly.Resources.CascadiaCode.ttf",
                _ => "YourAssembly.Resources.Inter-Regular.ttf"
            };
            using var s = asm.GetManifestResourceStream(resName)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Decide something based on these parameters, the last one doesnt éven get used... nice
        /// </summary>
        /// <param name="familyName"></param>
        /// <param name="isBold"></param>
        /// <param name="isItalic"></param>
        /// <returns>Yes yes.</returns>
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            familyName = familyName?.ToLowerInvariant() ?? "";
            if (familyName.Contains("consolas") || familyName.Contains("cascadia"))
                return new FontResolverInfo("Cascadia#Regular"); // monospaced

            if (isBold) return new FontResolverInfo("Inter#Bold");
            return new FontResolverInfo("Inter#Regular");
        }
    }
