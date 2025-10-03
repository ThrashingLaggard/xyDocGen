using System.Collections.Generic;
using System.Linq;

namespace xyDocumentor.Core.Docs
{
    public static class TypeDocExtensions
    {
        private static readonly Dictionary<TypeDoc, List<TypeDoc>> NestedMapping = new();

        // Returns all nested types stored in the mapping
        public static List<TypeDoc> NestedTypes(this TypeDoc t)
        {
            return t.GetNestedList();
        }

        private static List<TypeDoc> GetNestedList(this TypeDoc t)
        {
            if (!NestedMapping.TryGetValue(t, out var list))
            {
                list = new List<TypeDoc>();
                NestedMapping[t] = list;
            }
            return list;
        }

        // Recursively yields all nested types (flattened)
        public static IEnumerable<TypeDoc> AllNestedTypesRecursive(this TypeDoc t)
        {
            foreach (var nt in t.NestedTypes())
            {
                yield return nt;
                foreach (var sub in nt.AllNestedTypesRecursive())
                    yield return sub;
            }
        }

        // Recursively yields this type + all nested types
        public static IEnumerable<TypeDoc> FlattenNested(this TypeDoc type)
        {
            yield return type;

            foreach (var nested in type.NestedTypes())
            {
                foreach (var n in nested.FlattenNested())
                    yield return n;
            }
        }

        // Get all members (fields, properties, methods, events) including nested types recursively
        public static IEnumerable<MemberDoc> AllMembers(this TypeDoc type)
        {
            foreach (var m in type.Fields) yield return m;
            foreach (var m in type.Properties) yield return m;
            foreach (var m in type.Methods) yield return m;
            foreach (var m in type.Constructors) yield return m;
            foreach (var m in type.Events) yield return m;

            foreach (var nested in type.NestedTypes())
            {
                foreach (var nm in nested.AllMembers())
                    yield return nm;
            }
        }

        // Add a member to the correct list in TypeDoc
        public static void AddMember(this TypeDoc t, MemberDoc m)
        {
            switch (m.Kind)
            {
                case "ctor": t.Constructors.Add(m); break;
                case "method": t.Methods.Add(m); break;
                case "property": t.Properties.Add(m); break;
                case "event": t.Events.Add(m); break;
                case "field": t.Fields.Add(m); break;
                case "enum-member": t.Fields.Add(m); break;
            }
        }
    }
}
