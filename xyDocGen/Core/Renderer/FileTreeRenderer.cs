using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xyDocumentor.Core.Docs;
using xyToolz.Filesystem;

namespace xyDocumentor.Core.Renderer
{
    /// <summary>
    /// Renders a directory structure as tree text
    /// </summary>
    public static class FileTreeRenderer
    {
        public static void RenderTree(DirectoryInfo dir, string prefix, bool isLast, StringBuilder sb, HashSet<string> excludeParts)
        {
            if (excludeParts.Contains(dir.Name)) return;

            sb.AppendLine($"{prefix}{(isLast ? "└─" : "├─")}{dir.Name}/");

            var children = dir.GetDirectories()
                              .Where(d => !excludeParts.Contains(d.Name))
                              .OrderBy(d => d.Name)
                              .ToArray();

            var files = dir.GetFiles()
                           .Where(f => !excludeParts.Contains(f.Name))
                           .OrderBy(f => f.Name)
                           .ToArray();

            for (int i = 0; i < children.Length; i++)
                RenderTree(children[i], prefix + (isLast ? "  " : "│ "), i == children.Length - 1, sb, excludeParts);

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                sb.AppendLine($"{prefix}{(children.Length + i == children.Length + files.Length - 1 ? "└─" : "├─")}{file.Name}");
            }
        }

        /// <summary>
        /// Builds both the namespace-based API index (INDEX.md) 
        /// and the project folder structure (PROJECT-STRUCTURE.md).
        /// </summary>
        public static async Task BuildIndexAndTree(IEnumerable<TypeDoc> flattenedTypes, string format, string rootPath, string outPath, HashSet<string> excludedParts, string prefix = "")
        {
            StringBuilder indexBuilder = await BuildProjectIndex(flattenedTypes, format, outPath);
            indexBuilder.Clear();
            StringBuilder projectBuilder = await BuildProjectTree(indexBuilder, format, rootPath, outPath, excludedParts);
            indexBuilder = null;
        }
        /// <summary>
        /// Builds an INDEX.md file listing all documented types, grouped by namespace.
        /// </summary>
        public static async Task<StringBuilder> BuildProjectIndex(IEnumerable<TypeDoc> flattenedtypes, string format, string outpath)
        {
            // Rendering INDEX.md  ordered by namespace
            StringBuilder indexBuilder = new StringBuilder();

            // Adding the headline
            indexBuilder.AppendLine("# API‑Index (by namespace)\n");

            // Bringing everything into the right order
            IEnumerable<IGrouping<string, TypeDoc>> flattenedTypesGroupedAndInOrder = flattenedtypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key);

            foreach (IGrouping<string, TypeDoc> group in flattenedTypesGroupedAndInOrder)
            {
                // Adding subheadline
                indexBuilder.AppendLine($"## `{group.Key}`");
                foreach (TypeDoc tD in group.OrderBy(t => t.DisplayName))
                {
                    // Choosing the file extension
                    string fileExt = format == "pdf" ? "pdf" : format == "html" ? "html" : format == "json" ? "json" : "md";

                    // Building the group data 
                    string rel = $"./{group.Key.Replace('<', '_').Replace('>', '_')}/{tD.DisplayName.Replace(' ', '_')}.{fileExt}";

                    // Appending data
                    indexBuilder.AppendLine($"- [{tD.DisplayName}]({rel})");
                }
                // Adding empty row
                indexBuilder.AppendLine();
            }

            // Setting target path
            string indexPath = Path.Combine(outpath, "INDEX.md");

            // Saving the Index
            await xyFiles.SaveToFile(indexBuilder.ToString(), indexPath);

            return indexBuilder;
        }
        /// <summary>
        /// Builds PROJECT-STRUCTURE.md, a visual tree representation of the file system.
        /// </summary>
        public static async Task<StringBuilder> BuildProjectTree(StringBuilder treeBuilder, string format, string rootPath, string outPath, HashSet<string> excludedParts, string prefix = "")
        {
            string headline = "# Project structure\n";
            string fileName = "PROJECT-STRUCTURE.md";

            // Adding the headline
            treeBuilder.AppendLine(headline);

            // Rendering PROJECT-STRUCTURE.md 
            FileTreeRenderer.RenderTree(new DirectoryInfo(rootPath), prefix, true, treeBuilder, excludedParts);

            // Combining the target path
            string targetPath = Path.Combine(outPath, fileName);

            // Write the tree into the target file
            await xyFiles.SaveToFile(treeBuilder.ToString(), targetPath);

            return treeBuilder;
        }



    }
}
