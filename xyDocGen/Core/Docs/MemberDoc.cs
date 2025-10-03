using System.Collections.Generic;

namespace xyDocumentor.Core.Docs
{
    /// <summary>
    /// Represents a single member of a type (field, property, method, constructor, event, enum-member)
    /// </summary>
    public record MemberDoc
    {
        /// <summary>Kind of member: "field", "property", "method", "ctor", "event", "enum-member"</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>Signature of the member (name + parameters/type)</summary>
        public string Signature { get; init; } = string.Empty;

        /// <summary>Modifiers like "public", "private", "protected internal"</summary>
        public string Modifiers { get; init; } = string.Empty;

        /// <summary>Optional documentation/summary extracted from XML comments</summary>
        public string Summary { get; init; } = string.Empty;
    }
}
