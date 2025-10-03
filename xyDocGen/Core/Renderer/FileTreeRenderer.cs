using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

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
    }
}
