namespace xyDocumentor.Core.Renderer.Pdf
{
    using PdfSharpCore.Pdf;

    /// <summary>
    /// Value object for anchor destinations inside the PDF.
    /// </summary>
    internal readonly record struct AnchorTarget(PdfPage Page, double Y);
}
