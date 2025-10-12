using System.Collections.Generic;

namespace xyDocumentor.Core.Docs
{
    /// <summary>
    /// Represents detailed information about a single parameter of a method or constructor.
    /// This expanded record provides structured information for robust documentation generation.
    /// </summary>
    public record ParameterDoc
    {
        /// <summary>Name of the parameter</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary> Call .ToString() on the parameter and store the result here </summary>
        public string? Value { get; init; } 

        /// <summary>The full, canonical type name of the parameter (ie., "System.String" or "System.Collections.Generic.List<System.Int32>")</summary>
        public string TypeFullName { get; init; } = string.Empty;

        /// <summary>The display name as it appears in code (ie., "string" or "List<int>")</summary>
        public string TypeDisplayName { get; init; } = string.Empty;

        /// <summary>Indicates if the parameter has the "ref" modifier.</summary>
        public bool IsRef { get; init; } = false;

        /// <summary>Indicates if the parameter has the "ref readonly" modifier.</summary>
        public bool IsRefReadonly { get; init; } = false;

        /// <summary>Indicates if the parameter has the "out" modifier.</summary>
        public bool IsOut { get; init; } = false;

        /// <summary>Indicates if the parameter has the "in" modifier (read-only reference).</summary>
        public bool IsIn { get; init; } = false;

        /// <summary>Indicates if the parameter has the "params" modifier (parameter array).</summary>
        public bool IsParams { get; init; } = false;

        /// <summary>Indicates if the parameter is optional and has a default value.</summary>
        public bool IsOptional { get; init; } = false;

         /// <summary> The modifiers for the parameter </summary>
        public HashSet<string> Modifiers { get; set; } = new();
        
        /// <summary> The default value expression as a string, if the parameter is optional (e.g., "null", "10", "Color.Red").  Is null if no default value is present. </summary>
        public string? DefaultValueExpression { get; init; }

        /// <summary> Documentation extracted from the XML &lt;param name="Name"&gt; comment. </summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>Optional: The explicit type of the default value expression (useful for resolving ambiguity, e.g., "int" for DefaultValueExpression="0").</summary>
        public string? DefaultValueType { get; init; }

        /// <summary>Optional: Indicates if the parameter is a generic type parameter (e.g., the 'T' in a method signature like MyMethod&lt;T&gt;(T item)).</summary>
        public bool IsGenericTypeParam { get; init; } = false;



        private static ParameterDoc SetModifierString(ParameterDoc pd_Parameter_)
        {
            ParameterDoc pd_Parameter = pd_Parameter_;
            if (pd_Parameter_.IsIn)
            {
                pd_Parameter.Modifiers.Add( "in,");
            }
            if (pd_Parameter_.IsRef)
            {
                pd_Parameter.Modifiers.Add("ref,");
            }
            if (pd_Parameter_.IsRefReadonly)
            {
                pd_Parameter.Modifiers.Add("ref readonly,");
            }
            if (pd_Parameter_.IsOut)
            {
                pd_Parameter.Modifiers.Add("out,");   
            }
            if (pd_Parameter_.IsParams)
            {
                pd_Parameter.Modifiers.Add("params,");
            }
            if (pd_Parameter_.IsOptional)
            {
                pd_Parameter.Modifiers.Add("optional,");
            }
            if (pd_Parameter_.IsGenericTypeParam)
            {
                pd_Parameter.Modifiers.Add("T,");
            }
            return pd_Parameter;
        }
    }
}