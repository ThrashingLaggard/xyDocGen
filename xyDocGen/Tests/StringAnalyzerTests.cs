using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using xyDocumentor.Helpers;

namespace xyDocumentor.Tests
{
    /// <summary>
    /// Tests for the robust CLI parser in StringAnalyzer.TryParseOptions.
    /// Each test creates a temp root directory to satisfy root existence checks.
    /// </summary>
    public class StringAnalyzerTests : IDisposable
    {
        private readonly string _tmpRoot;
        private readonly string _tmpOut;

        /// <summary>
        /// Shitty constructor for this test-class?....
        /// </summary>
        public StringAnalyzerTests()
        {
            // Create temp folders for predictable, isolated tests
            _tmpRoot = CreateTempDir("root");
            _tmpOut = CreateTempDir("out");
        }

        /// <summary>
        /// Dispose this
        /// </summary>
        public void Dispose()
        {
            TryDelete(_tmpRoot);
            TryDelete(_tmpOut);
            GC.SuppressFinalize(this);
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
            catch { /* ignore on CI */ }
        }

        private static string[] A(params string[] args) => args;

        /// <summary>
        /// Check for parsing success
        /// </summary>
        [Fact]
        public void Parse_Minimal_WithRootOut_ShouldSucceed()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--out", _tmpOut, "--format", "md"),
                out var opt, out var err);

            Assert.True(ok, err);
            Assert.NotNull(opt);
            Assert.Equal(Path.GetFullPath(_tmpRoot), opt.RootPath);
            Assert.Equal(Path.GetFullPath(_tmpOut), opt.OutPath);
            Assert.Equal("md", opt.Format);
            Assert.True(opt.IncludeNonPublic);     // default is true (unless --private)
            Assert.False(opt.ShowOnly);
            Assert.False(opt.BuildIndex);
            Assert.False(opt.BuildTree);
        }

        /// <summary>
        /// Check if output goes into the default output directory
        /// </summary>
        [Fact]
        public void Parse_DefaultOut_WhenFolderSubfolder_NotGiven_ShouldBeRootDocsApi()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot), out var opt, out var err);

            Assert.True(ok, err);
            var exp = Path.Combine(opt.RootPath, "docs", "api");
            Assert.Equal(Path.GetFullPath(exp), opt.OutPath);
            Assert.Equal("md", opt.Format);
        }

        /// <summary>
        /// Crys in case of unknown format string
        /// </summary>
        [Fact]
        public void Parse_FormatValidation_ShouldFail_OnUnsupported()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--format", "txt"), out var opt, out var err);

            Assert.False(ok);
            Assert.Contains("Unsupported --format", err);
            Assert.Null(opt);
        }

        /// <summary>
        /// Checks if semicolon and commas get rendered
        /// </summary>
        [Fact]
        public void Parse_Exclude_Semicolon_And_Comma()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--exclude", ".git;bin,CustomFolder"),
                out var opt, out var err);

            Assert.True(ok, err);
            // Defaults + added:
            Assert.Contains(".git", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bin", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("CustomFolder", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if the private flag disables private content
        /// </summary>
        [Fact]
        public void Parse_Private_Flag_ShouldDisable_NonPublic()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--private"), out var opt, out var err);

            Assert.True(ok, err);
            Assert.False(opt.IncludeNonPublic);
        }

        /// <summary>
        /// Check for flags
        /// </summary>
        [Fact]
        public void Parse_Show_Index_Tree_Flags()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--show", "--index", "--tree"),
                out var opt, out var err);

            Assert.True(ok, err);
            Assert.True(opt.ShowOnly);
            Assert.True(opt.BuildIndex);
            Assert.True(opt.BuildTree);
        }

        /// <summary>
        /// Test = syntax
        /// </summary>
        [Fact]
        public void Parse_EqualsSyntax_ShouldWork()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A($"--root={_tmpRoot}", $"--out={_tmpOut}", "--format=json", "--exclude=.cache,.artifacts"),
                out var opt, out var err);

            Assert.True(ok, err);
            Assert.Equal("json", opt.Format);
            Assert.Contains(".cache", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(".artifacts", opt.ExcludedParts, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check for error after missing argument
        /// </summary>
        [Fact]
        public void Parse_MissingValue_After_Flag_ShouldFail()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--out"), out var opt, out var err);

            Assert.False(ok);
            Assert.Contains("Missing value after --out", err);
            Assert.Null(opt);
        }


        /// <summary>
        /// Check bahaviour with unknown option
        /// </summary>
        [Fact]
        public void Parse_UnknownOption_ShouldFail_WithHelpfulMessage()
        {
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--unknown"), out var opt, out var err);

            Assert.False(ok);
            Assert.Contains("Unknown option", err);
            Assert.Null(opt);
        }



        /// <summary>
        /// Check the priority of parameter flags
        /// </summary>
        [Fact]
        public void Parse_Folder_Subfolder_And_Out_Priority()
        {
            // --out should override folder/subfolder
            var customOut = _tmpOut;
            var ok = StringAnalyzer.TryParseOptions(
                A("--root", _tmpRoot, "--folder", "docsX", "--subfolder", "apiY", "--out", customOut),
                out var opt, out var err);

            Assert.True(ok, err);
            Assert.Equal(Path.GetFullPath(customOut), opt.OutPath);
        }
    }
}
