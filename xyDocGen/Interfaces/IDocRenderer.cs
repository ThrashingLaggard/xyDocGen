using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xyDocumentor.Core.Docs;

namespace xyDocumentor.Interfaces
{

    /// <summary>
    /// Useless for now, since all the renderers are         STATIC             and thus cant parttake in this luxurious idea
    /// </summary>
    internal interface IDocRenderer
    {
        public string Description { get; set; }
        /// <summary>
        /// Render it! Now!
        /// </summary>
        /// <param name="td_Type"></param>
        /// <returns>A rendered string from the target</returns>
        public string Render(TypeDoc td_Type);

        /// <summary>
        /// Stores the actual file extension used by the implementing renderer
        /// </summary>
        string FileExtension { get; }
    }
}
