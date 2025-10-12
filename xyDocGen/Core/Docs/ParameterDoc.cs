namespace xyDocumentor.Core.Docs
{
    /// <summary>
    /// Represents detailed information about a single parameter of a method or constructor.
    /// This expanded record provides structured information for robust documentation generation.
    /// </summary>
    public record ParameterDoc
    {
        /// <summary>Name of the parameter (e.g., "inputString")</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>The full, canonical type name of the parameter (e.g., "System.String" or "System.Collections.Generic.List<System.Int32>")</summary>
        public string TypeFullName { get; init; } = string.Empty;

        /// <summary>The display name of the type as it appears in code (e.g., "string" or "List<int>")</summary>
        public string TypeDisplayName { get; init; } = string.Empty;

        /// <summary>Indicates if the parameter has the "ref" modifier.</summary>
        public bool IsRef { get; init; } = false;

        /// <summary>Indicates if the parameter has the "out" modifier.</summary>
        public bool IsOut { get; init; } = false;

        /// <summary>Indicates if the parameter has the "in" modifier (read-only reference).</summary>
        public bool IsIn { get; init; } = false;

        /// <summary>Indicates if the parameter has the "params" modifier (parameter array).</summary>
        public bool IsParams { get; init; } = false;

        /// <summary>Indicates if the parameter is optional and has a default value.</summary>
        public bool IsOptional { get; init; } = false;

        /// <summary>
        /// The default value expression as a string, if the parameter is optional (e.g., "null", "10", "Color.Red"). 
        /// Is null if no default value is present.
        /// </summary>
        public string? DefaultValueExpression { get; init; }

        /// <summary>
        /// Documentation extracted from the XML &lt;param name="Name"&gt; comment.
        /// </summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>
        /// Optional: The explicit type of the default value expression (useful for resolving ambiguity, e.g., "int" for DefaultValueExpression="0").
        /// </summary>
        public string? DefaultValueType { get; init; }

        /// <summary>
        /// Optional: Indicates if the parameter is a generic type parameter (e.g., the 'T' in a method signature like MyMethod&lt;T&gt;(T item)).
        /// </summary>
        public bool IsTypeParameter { get; init; } = false;
    }
}