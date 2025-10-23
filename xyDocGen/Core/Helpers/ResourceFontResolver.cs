namespace xyDocumentor.Core.Fonts;

using PdfSharpCore.Fonts;
using System.IO;
using System.Reflection;

    public class ResourceFontResolver : IFontResolver
    {
        public string DefaultFontName => "Inter";

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

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            familyName = familyName?.ToLowerInvariant() ?? "";
            if (familyName.Contains("consolas") || familyName.Contains("cascadia"))
                return new FontResolverInfo("Cascadia#Regular"); // monospaced

            if (isBold) return new FontResolverInfo("Inter#Bold");
            return new FontResolverInfo("Inter#Regular");
        }
    }
