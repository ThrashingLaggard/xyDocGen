using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xyDocumentor.Docs;

namespace xyDocumentor.Interfaces
{

    /// <summary>
    /// Abstraction for a document renderer.
    /// Since all the renderers are         STATIC             and thus cant partake in this luxurious idea 
    /// I implemented WRAPPERS (to wrap static renderer classes with small adapter types).
    /// </summary>
    public interface IDocRenderer
    {
        /// <summary>
        /// Add usefull information here
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Render it! Now!
        /// Render the given type into the renderer's target format.
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

