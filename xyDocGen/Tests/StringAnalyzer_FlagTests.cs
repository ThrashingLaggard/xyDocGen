using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
// Make sure the namespace matches where your StringAnalyzer and CliOptions live:
using xyDocumentor.Core.Helpers;
using xyDocumentor.Core.Models;

namespace xyDocumentor.Core.Tests
{
    /// <summary>
    /// Exhaustive flag coverage for StringAnalyzer.TryParseOptions + AnalyzeArgs.
    /// All tests create a temp root dir to satisfy the existence check.
    /// </summary>
    public class StringAnalyzer_FlagTests : IDisposable
    {
        private readonly string _tmpRoot;
        private readonly string _tmpOut;

        public StringAnalyzer_FlagTests()
        {
            _tmpRoot = CreateTempDir("root");
            _tmpOut = CreateTempDir("out");
        }

        public void Dispose()
        {
            TryDelete(_tmpRoot);
            TryDelete(_tmpOut);
        }

        private static string CreateTempDir(string suffix)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"xyDocGen_{suffix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TryDelete(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch { /* ok on CI */ }
        }

        private static string[] A(params string[] xs) => xs;

        // ---------------------------
        // HELP / INFO
        // ---------------------------

        [Fact]
        public void HelpFlag_Sets_Help_And_Parses()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--help"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.Help);
            Assert.False(opt.Info);
        }

        [Fact]
        public void InfoFlag_Sets_Info_And_Parses()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--info"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.Info);
            Assert.False(opt.Help);
        }

        [Fact]
        public void Help_And_Info_Can_Be_Set_Together()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--help", "--info"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.Help);
            Assert.True(opt.Info);
        }

        // ---------------------------
        // SHOW / INDEX / TREE booleans
        // ---------------------------

        [Fact]
        public void Show_Sets_ShowOnly_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--show"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.ShowOnly);
        }

        [Fact]
        public void Index_Sets_BuildIndex_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--index"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.BuildIndex);
        }

        [Fact]
        public void Tree_Sets_BuildTree_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--tree"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.BuildTree);
        }

        [Fact]
        public void Combined_Show_Index_Tree_All_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--show", "--index", "--tree"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.ShowOnly);
            Assert.True(opt.BuildIndex);
            Assert.True(opt.BuildTree);
        }

        // equals-syntax with boolean assignment
        [Theory]
        [InlineData("--show=false", false)]
        [InlineData("--show=true", true)]
        [InlineData("--index=false", false)]
        [InlineData("--index=true", true)]
        [InlineData("--tree=false", false)]
        [InlineData("--tree=true", true)]
        public void Boolean_Equals_Syntax_Respected(string flag, bool expected)
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, flag), out var opt, out var err);
            Assert.True(ok, err);
            if (flag.StartsWith("--show"))
                Assert.Equal(expected, opt.ShowOnly);
            if (flag.StartsWith("--index"))
                Assert.Equal(expected, opt.BuildIndex);
            if (flag.StartsWith("--tree"))
                Assert.Equal(expected, opt.BuildTree);
        }

        // ---------------------------
        // PRIVATE (inverts IncludeNonPublic)
        // ---------------------------

        [Fact]
        public void Private_Sets_IncludeNonPublic_False()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--private"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.False(opt.IncludeNonPublic);
        }

        [Theory]
        [InlineData("--private=false", true)]
        [InlineData("--private=true", false)]
        public void Private_Equals_Syntax_Respected(string flag, bool expectedIncludeNonPublic)
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, flag), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal(expectedIncludeNonPublic, opt.IncludeNonPublic);
        }

        // ---------------------------
        // FORMAT
        // ---------------------------

        [Theory]
        [InlineData("md")]
        [InlineData("html")]
        [InlineData("pdf")]
        [InlineData("json")]
        public void Format_Supported_Values_Succeed(string fmt)
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--format", fmt), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal(fmt, opt.Format);
        }

        [Fact]
        public void Format_Unsupported_Fails()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--format", "xml"), out var opt, out var err);
            Assert.False(ok);
            Assert.Contains("Unsupported --format", err);
            Assert.Null(opt);
        }

        [Fact]
        public void Format_Equals_Syntax_Works()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--format=json"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal("json", opt.Format);
        }

        // ---------------------------
        // ROOT / OUT / FOLDER / SUBFOLDER
        // ---------------------------

        [Fact]
        public void RootAndOut_Are_Used_As_Is()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--out", _tmpOut), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal(Path.GetFullPath(_tmpRoot), opt.RootPath);
            Assert.Equal(Path.GetFullPath(_tmpOut), opt.OutPath);
        }

        [Fact]
        public void MissingValue_After_Out_Fails()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--out"), out var opt, out var err);
            Assert.False(ok);
            Assert.Contains("Missing value after --out", err);
            Assert.Null(opt);
        }

        [Fact]
        public void Folder_Subfolder_Defaults_When_Out_NotProvided()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot), out var opt, out var err);
            Assert.True(ok, err);
            var expected = Path.Combine(opt.RootPath, "docs", "api");
            Assert.Equal(Path.GetFullPath(expected), opt.OutPath);
        }

        [Fact]
        public void Folder_Subfolder_Customize_OutPath_When_Out_NotProvided()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--folder", "DOCS", "--subfolder", "XAPI"), out var opt, out var err);
            Assert.True(ok, err);
            var expected = Path.Combine(opt.RootPath, "DOCS", "XAPI");
            Assert.Equal(Path.GetFullPath(expected), opt.OutPath);
        }

        [Fact]
        public void Out_Overrides_Folder_Subfolder()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--folder", "DOCS", "--subfolder", "XAPI", "--out", _tmpOut), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal(Path.GetFullPath(_tmpOut), opt.OutPath);
        }

        [Fact]
        public void Equals_Syntax_For_Root_And_Out_Works()
        {
            var ok = StringAnalyzer.TryParseOptions(A($"--root={_tmpRoot}", $"--out={_tmpOut}"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal(Path.GetFullPath(_tmpRoot), opt.RootPath);
            Assert.Equal(Path.GetFullPath(_tmpOut), opt.OutPath);
        }

        // ---------------------------
        // EXCLUDES
        // ---------------------------

        [Fact]
        public void Exclude_Parses_Semicolon_And_Comma()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--exclude", ".cache;bin,.artifacts"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Contains(".cache", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bin", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".artifacts", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Exclude_Equals_Syntax_Works()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--exclude=.vs;TestResults"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Contains(".vs", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("TestResults", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void MissingValue_After_Exclude_Fails()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--exclude"), out var opt, out var err);
            Assert.False(ok);
            Assert.Contains("Missing value after --exclude", err);
            Assert.Null(opt);
        }

        // ---------------------------
        // UNKNOWN / CASE-INSENSITIVITY
        // ---------------------------

        [Fact]
        public void Unknown_Option_Fails_With_Helpful_Message()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--schroedinger"), out var opt, out var err);
            Assert.False(ok);
            Assert.Contains("Unknown option", err);
        }

        [Fact]
        public void Flags_Are_Case_Insensitive()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--ROOT", _tmpRoot, "--FoRmAt", "HTML", "--TrEe", "--InDeX", "--ShOw"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal("html", opt.Format);
            Assert.True(opt.BuildTree);
            Assert.True(opt.BuildIndex);
            Assert.True(opt.ShowOnly);
            Assert.Equal(Path.GetFullPath(_tmpRoot), opt.RootPath);
        }

        // ---------------------------
        // AnalyzeArgs shim
        // ---------------------------

        [Fact]
        public void AnalyzeArgs_Returns_Tuple_With_Parsed_Values()
        {
            var (root, outPath, format, includeNonPublic, excludes) =
                StringAnalyzer.AnalyzeArgs(new List<string>(), A("--root", _tmpRoot, "--out", _tmpOut, "--format", "json", "--private", "--exclude", ".cache;.out"));

            Assert.Equal(Path.GetFullPath(_tmpRoot), root);
            Assert.Equal(Path.GetFullPath(_tmpOut), outPath);
            Assert.Equal("json", format);
            Assert.False(includeNonPublic); // because --private
            Assert.Contains(".cache", excludes, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".out", excludes, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnalyzeArgs_Falls_Back_To_Defaults_On_Parse_Fail()
        {
            // Unknown option triggers parse fail; AnalyzeArgs should still return sane defaults
            var (root, outPath, format, includeNonPublic, excludes) =
                StringAnalyzer.AnalyzeArgs(new List<string>(), A("--root", _tmpRoot, "--out", _tmpOut, "--unknownFlag"));

            Assert.False(string.IsNullOrWhiteSpace(root));
            Assert.False(string.IsNullOrWhiteSpace(outPath));
            Assert.Equal("md", format);
            Assert.True(includeNonPublic);
            Assert.NotEmpty(excludes);
        }
    }
}
