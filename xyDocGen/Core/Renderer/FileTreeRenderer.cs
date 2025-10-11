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
        /// <summary>
        /// Recursiie method to render the tree structure for better representation
        /// </summary>
        /// <param name="di_Directory_">Target folder</param>
        /// <param name="prefix_"> Prefix for this level of the tree</param>
        /// <param name="isLast_">Last folder in this directory?</param>
        /// <param name="sb_TreeBuilder_">Stores the tree</param>
        /// <param name="hs_ExcludeTheseParts_"></param>
        public static void RenderTree(DirectoryInfo di_Directory_, string prefix_, bool isLast_, StringBuilder sb_TreeBuilder_, HashSet<string> hs_ExcludeTheseParts_)
        {
            // Ignore unwanted folders
            if (hs_ExcludeTheseParts_.Contains(di_Directory_.Name))
            {
                return;
            }                

            // Build the current level of the tree
            sb_TreeBuilder_.AppendLine($"{prefix_}{(isLast_ ?"└─" : "├─")}{di_Directory_.Name}/");

            var children = di_Directory_.GetDirectories().Where(d => !hs_ExcludeTheseParts_.Contains(d.Name)).OrderBy(d => d.Name).ToArray();

            var files = di_Directory_.GetFiles().Where(f => !hs_ExcludeTheseParts_.Contains(f.Name)).OrderBy(f => f.Name).ToArray();

            for (int i = 0; i < children.Length; i++)
            {
                //
                RenderTree(children[i], prefix_ + (isLast_ ? "  " : "│ "), i == children.Length - 1, sb_TreeBuilder_, hs_ExcludeTheseParts_);
            }

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                sb_TreeBuilder_.AppendLine($"{prefix_}{(children.Length + i == children.Length + files.Length - 1 ? "└─" : "├─")}{file.Name}");
            }
        }



        /// <summary>
        /// Builds PROJECT-STRUCTURE.md, a visual tree representation of the file system.
        /// </summary>
        /// <param name="treeBuilder"></param>
        /// <param name="format"></param>
        /// <param name="rootPath"></param>
        /// <param name="outPath"></param>
        /// <param name="excludedParts"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
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
        /// Builds both the namespace-based API index (INDEX.md) 
        /// and the project folder structure (PROJECT-STRUCTURE.md).
        /// </summary>
        /// <param name="flattenedTypes"></param>
        /// <param name="format"></param>
        /// <param name="rootPath"></param>
        /// <param name="outPath"></param>
        /// <param name="excludedParts"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public static async Task BuildIndexAndTree(IEnumerable<TypeDoc> flattenedTypes, string format, string rootPath, string outPath, HashSet<string> excludedParts, string prefix = "")
        {
            StringBuilder indexBuilder = await BuildProjectIndex(flattenedTypes, format, outPath);
            indexBuilder.Clear();
            StringBuilder projectBuilder = await BuildProjectTree(indexBuilder, format, rootPath, outPath, excludedParts);
            indexBuilder = null;
        }

    }
}
