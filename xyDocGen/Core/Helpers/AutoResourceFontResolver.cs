namespace xyDocumentor.Core.Fonts;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PdfSharpCore.Fonts;

/// <summary>
/// PdfSharpCore font resolver that auto-detects embedded font resources (EmbeddedResource).
/// It scans the assembly for .ttf/.otf resources and picks:
///   - SansRegular  : first match from a "sans" candidate list (Inter, Roboto, OpenSans, Noto Sans, DejaVu Sans, Source Sans, Montserrat, Lato, Arial, Helvetica, ...),
///   - SansBold     : a matching bold face for the chosen sans (if available), otherwise falls back to regular,
///   - MonoRegular  : first match from a "mono" candidate list (Cascadia, FiraMono, DejaVu Sans Mono, Noto Sans Mono, Courier, Consolas, Source Code, Menlo, ...).
/// Use the family names below with XFont:
///   FamilySans = "XY Sans", FamilyMono = "XY Mono".
/// </summary>
public sealed class AutoResourceFontResolver : IFontResolver
{
    public string Description { get; set; }

    private readonly Assembly _asm;

    public const string FamilySans = "XY Sans";
    public const string FamilyMono = "XY Mono";

    private const string FaceSansRegular = "XY_SANS_REG";
    private const string FaceSansBold = "XY_SANS_BOLD";
    private const string FaceMonoRegular = "XY_MONO_REG";

    private readonly string? _resSansReg;
    private readonly string? _resSansBold;
    private readonly string? _resMonoReg;

    public string? SansRegularResourceName => _resSansReg;
    public string? SansBoldResourceName => _resSansBold;
    public string? MonoRegularResourceName => _resMonoReg;

    private byte[]? _bufSansReg, _bufSansBold, _bufMonoReg;

    public string DefaultFontName => FamilySans;

    public AutoResourceFontResolver()
    {
        // List all embedded resources in the executing assembly
         _asm = typeof(AutoResourceFontResolver).Assembly;
        var names = _asm.GetManifestResourceNames();

        foreach (var n in typeof(AutoResourceFontResolver).Assembly.GetManifestResourceNames().Take(10))
            System.Diagnostics.Debug.WriteLine("RES: " + n);




        // Consider only .ttf/.otf
        var fontRes = names
            .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Heuristics for picking sans/mono/bold faces
        var sansRegex = new Regex("(inter|roboto|open.?sans|noto.?sans(?!.*mono)|dejavu.?sans(?!.*mono)|source.?sans|montserrat|lato|arial|helvetica|liberation.?sans)",
                                  RegexOptions.IgnoreCase);
        var monoRegex = new Regex("(comic.?mono|comic.?sans|comic|monospace|cascadia|fira.?mono|dejavu.?sans.?mono|noto.?sans.?mono|inconsolata|source.?code|courier|consolas|menlo|mono|code)",
                                  RegexOptions.IgnoreCase);
        var boldRegex = new Regex("(bold|semi.?bold|demi|black)", RegexOptions.IgnoreCase);

        // Pick a sans regular
        _resSansReg = fontRes.FirstOrDefault(n => sansRegex.IsMatch(n))
                   ?? fontRes.FirstOrDefault(); // fallback: any font if no sans candidate is found

        // Try to find a matching bold for the chosen sans (same stem + bold marker)
        if (_resSansReg != null)
        {
            var stem = Stem(_resSansReg);
            _resSansBold = fontRes.FirstOrDefault(n =>
                n != _resSansReg &&
                Stem(n) == stem && boldRegex.IsMatch(n))
                ?? fontRes.FirstOrDefault(n => boldRegex.IsMatch(n)); // generic bold as a fallback
        }

        // Pick a mono regular
        // _resMonoReg = fontRes.FirstOrDefault(n => monoRegex.IsMatch(n));

        // Pick a mono regular (Comic bevorzugen)
        _resMonoReg = fontRes.Where(n => monoRegex.IsMatch(n)).OrderByDescending(n => Regex.IsMatch(n, "comic", RegexOptions.IgnoreCase) ? 
            2 : Regex.IsMatch(n, "monospace", RegexOptions.IgnoreCase) ? 1 : 0).FirstOrDefault();



        // Optional diagnostics: set XYDOCGEN_LOG_FONTS=1 to log selected resources
        if (Environment.GetEnvironmentVariable("XYDOCGEN_LOG_FONTS") == "1")
        {
            Console.WriteLine("[AutoResourceFontResolver] Embedded fonts:");
            foreach (var r in fontRes) Console.WriteLine("  - " + r);
            Console.WriteLine($"Chosen SansReg : {_resSansReg ?? "(none)"}");
            Console.WriteLine($"Chosen SansBold: {_resSansBold ?? "(none)"}");
            Console.WriteLine($"Chosen MonoReg : {_resMonoReg ?? "(none)"}");
        }

        if (_resSansReg == null)
            throw new FileNotFoundException("No embedded fonts found. Add .ttf/.otf under Resources/Fonts and mark them as <EmbeddedResource>.");
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var fam = (familyName ?? "").Trim().ToLowerInvariant();

        // Mono family? (+Comic & Monospace)
        if (fam.Contains("comic") || fam.Contains("monospace") ||
            fam.Contains("mono") || fam.Contains("cascadia") ||
            fam.Contains("consolas") || fam.Contains("courier") || fam.Contains("code")) 
            return new FontResolverInfo(FaceMonoRegular);

        // Sans family
        if (isBold && _resSansBold != null)
            return new FontResolverInfo(FaceSansBold);

        return new FontResolverInfo(FaceSansRegular);
    }

    public byte[] GetFont(string faceName)
    {
  
        return faceName switch
        {
            FaceSansRegular => _bufSansReg ??= LoadBytes(_asm, _resSansReg!),
            FaceSansBold => _bufSansBold ??= LoadBytes(_asm, _resSansBold ?? _resSansReg!),
            FaceMonoRegular => _bufMonoReg ??= LoadBytes(_asm, _resMonoReg ?? _resSansReg!),
            // Comic Sans MS is missing here
            _ => throw new ArgumentException($"Unknown face name: {faceName}", nameof(faceName))
        };
    }

    private static byte[] LoadBytes(Assembly asm, string resourceName)
    {
        using var s = asm.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded font not found: {resourceName}\n" +
                "Check <EmbeddedResource> items and the project's default namespace.");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static string Stem(string resourceName)
    {
        // Extract a family stem (remove style tokens like Regular/Bold/Italic/Medium/etc.)
        var file = resourceName.Split('.').Reverse().Skip(1).FirstOrDefault() ?? resourceName; // filename without extension
        return Regex.Replace(file, "(regular|bold|italic|oblique|medium|semi.?bold|black|light|thin|extra|ultra)",
                             "", RegexOptions.IgnoreCase)
                    .Replace("_", "").Replace("-", "").ToLowerInvariant();
    }
}
