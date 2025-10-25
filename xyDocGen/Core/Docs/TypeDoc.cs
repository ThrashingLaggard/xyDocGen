using System.Collections.Generic;

namespace xyDocumentor.Core.Docs
{
    /// <summary>
    /// Represents a type (class, struct, interface, record, enum) and its members
    /// </summary>
    public record TypeDoc
    {
        /// <summary>Kind of type: "class", "struct", "interface", "record", "enum"</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>Type name including generic parameters</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary> Types within  </summary>
        public List<TypeDoc> NestedTypes { get; set; } = new();

        /// <summary>Namespace the type belongs to, "<global>" if none</summary>
        public string Namespace { get; init; } = string.Empty;

        /// <summary>Modifiers like "public", "internal", "abstract"</summary>
        public string Modifiers { get; init; } = string.Empty;

        /// <summary>List of attribute names applied to the type</summary>
        public List<string> Attributes { get; init; } = new();

        /// <summary>List of base classes or implemented interfaces</summary>
        public List<string> BaseTypes { get; init; } = new();

        /// <summary>Summary extracted from XML doc comments</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>File path where the type is defined</summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>If nested type, parent type name</summary>
        public string Parent { get; init; }

        // Collections for type members
        public List<MemberDoc> Constructors { get; } = new();
        public List<MemberDoc> Properties { get; } = new();
        public List<MemberDoc> Methods { get; } = new();
        public List<MemberDoc> Events { get; } = new();
        public List<MemberDoc> Fields { get; } = new();

        ///<summary> Display name including parent if nested </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Parent) ? Name : $"{Parent}.{Name}";

        public string Signature { get; internal set; }
    }
}
