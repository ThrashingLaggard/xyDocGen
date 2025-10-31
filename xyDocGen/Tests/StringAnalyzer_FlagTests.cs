using System;
using System.IO;
using System.Collections.Generic;
using Xunit;
using xyDocumentor.Helpers;

namespace xyDocumentor.Tests
{
    /// <summary>
    /// Exhaustive flag coverage for StringAnalyzer.TryParseOptions + AnalyzeArgs.
    /// All tests create a temp root dir to satisfy the existence check.
    /// </summary>
    public class StringAnalyzer_FlagTests : IDisposable
    {
        private readonly string _tmpRoot;
        private readonly string _tmpOut;

        /// <summary>
        /// Construct the test class
        /// </summary>
        public StringAnalyzer_FlagTests()
        {
            _tmpRoot = CreateTempDir("root");
            _tmpOut = CreateTempDir("out");
        }

        /// <summary>
        /// Dispose this
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
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

        /// <summary>
        /// Does help flag work
        /// </summary>
        [Fact]
        public void HelpFlag_Sets_Help_And_Parses()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--help"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.Help);
            Assert.False(opt.Info);
        }

        /// <summary>
        /// Check if the info flag works
        /// </summary>
        [Fact]
        public void InfoFlag_Sets_Info_And_Parses()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--info"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.Info);
            Assert.False(opt.Help);
        }

        /// <summary>
        /// Check if help and info flags work together
        /// </summary>
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

        /// <summary>
        /// Check if show flag works
        /// </summary>
        [Fact]
        public void Show_Sets_ShowOnly_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--show"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.ShowOnly);
        }

        /// <summary>
        /// Check if index flag works
        /// </summary>
        [Fact]
        public void Index_Sets_BuildIndex_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--index"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.BuildIndex);
        }

        /// <summary>
        /// Check if tree flag works
        /// </summary>
        [Fact]
        public void Tree_Sets_BuildTree_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--tree"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.BuildTree);
        }

        /// <summary>
        /// Check if index and tree flags work together
        /// </summary>
        [Fact]
        public void Combined_Show_Index_Tree_All_True()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--show", "--index", "--tree"), out var opt, out var err);
            Assert.True(ok, err);
            Assert.True(opt.ShowOnly);
            Assert.True(opt.BuildIndex);
            Assert.True(opt.BuildTree);
        }

        /// <summary>
        /// Checks the equals-syntax with boolean assignment
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="expected"></param>
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


        /// <summary>
        /// Check  if format values get parsed corrctly
        /// </summary>
        /// <param name="fmt"></param>
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


        /// <summary>
        /// Check the case of unsopported fornats
        /// </summary>
        [Fact]
        public void Format_Unsupported_Fails()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--format", "xml"), out var opt, out var err);
            Assert.False(ok);
            Assert.Contains("Unsupported --format", err);
            Assert.Null(opt);
        }


        /// <summary>
        /// Check default fallback
        /// </summary>
        [Fact]
        public void Folder_Subfolder_Defaults_When_Out_NotProvided()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot), out var opt, out var err);
            Assert.True(ok, err);
            var expected = Path.Combine(opt.RootPath, "docs", "api");
            Assert.Equal(Path.GetFullPath(expected), opt.OutPath);
        }

        /// <summary>
        /// Check behaviour if no outpath is provided
        /// </summary>
        [Fact]
        public void Folder_Subfolder_Customize_OutPath_When_Out_NotProvided()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--folder", "DOCS", "--subfolder", "XAPI"), out var opt, out var err);
            Assert.True(ok, err);
            var expected = Path.Combine(opt.RootPath, "DOCS", "XAPI");
            Assert.Equal(Path.GetFullPath(expected), opt.OutPath);
        }

        /// <summary>
        /// Check override 
        /// </summary>
        [Fact]
        public void Out_Overrides_Folder_Subfolder()
        {
            var ok = StringAnalyzer.TryParseOptions(A("--root", _tmpRoot, "--folder", "DOCS", "--subfolder", "XAPI", "--out", _tmpOut), out var opt, out var err);
            Assert.True(ok, err);
            Assert.Equal(Path.GetFullPath(_tmpOut), opt.OutPath);
        }

        /// <summary>
        ///  Check the flags for case sensitivity
        /// </summary>
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

        /// <summary>
        /// Check for a returned tuple
        /// </summary>
        [Fact]
        public void AnalyzeArgs_Returns_Tuple_With_Parsed_Values()
        {
            var (root, outPath, format, includeNonPublic, excludes) =
                StringAnalyzer.AnalyzeArgs(["--root", _tmpRoot, "--out", _tmpOut, "--format", "json", "--private", "--exclude", ".cache;.out"]);

            Assert.Equal(Path.GetFullPath(_tmpRoot), root);
            Assert.Equal(Path.GetFullPath(_tmpOut), outPath);
            Assert.Equal("json", format);
            Assert.False(includeNonPublic); // because --private
            Assert.Contains(".cache", excludes, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".out", excludes, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check fallback
        /// </summary>
        [Fact]
        public void AnalyzeArgs_Falls_Back_To_Defaults_On_Parse_Fail()
        {
            // Unknown option triggers parse fail; AnalyzeArgs should still return sane defaults
            var (root, outPath, format, includeNonPublic, excludes) =
                StringAnalyzer.AnalyzeArgs(["--root", _tmpRoot, "--out", _tmpOut, "--unknownFlag"]);

            Assert.False(string.IsNullOrWhiteSpace(root));
            Assert.False(string.IsNullOrWhiteSpace(outPath));
            Assert.Equal("md", format);
            Assert.True(includeNonPublic);
            Assert.NotEmpty(excludes);
        }
    }
}
