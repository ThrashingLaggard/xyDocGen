using System.Collections.Generic;
using System.Linq;

namespace xyDocumentor.Core.Docs
{
    /// <summary>
    /// Helper methods for the TypeDoc record
    /// </summary>
    public static class TypeDocExtensions
    {

        private static readonly Dictionary<TypeDoc, List<TypeDoc>> NestedMapping = new();

        /// <summary>
        /// Returns all nested types stored in the mapping.
        /// </summary>
        /// <param name="param_CallingTypeDoc"> the TypeDoc instance calling the method</param>
        /// <returns></returns>
        public static List<TypeDoc> NestedTypes(this TypeDoc param_CallingTypeDoc) => param_CallingTypeDoc.GetNestedList();
        

        /// <summary>
        /// Returns all nested types stored in the mapping.
        /// </summary>
        /// <param name="param_CallingTypeDoc"></param>
        /// <returns></returns>
        private static List<TypeDoc> GetNestedList(this TypeDoc param_CallingTypeDoc)
        {   
            // If there is no value for the key, return a new empty list.
            if (!NestedMapping.TryGetValue(param_CallingTypeDoc, out List<TypeDoc> list))
            {
                list = [];
                NestedMapping[param_CallingTypeDoc] = list;
            }
            
            // Else return the listed values.
            return list;
        }



       /// <summary>
       /// Recursively yields this type + all nested types
       /// </summary>
       /// <param name="param_CallingTypeDoc"></param>
       /// <returns></returns>
        public static IEnumerable<TypeDoc> FlattenNested(this TypeDoc param_CallingTypeDoc)
        {
            // Add the caller to the output
            yield return param_CallingTypeDoc;

            // For every nested type 
            foreach (TypeDoc td_NestedType in param_CallingTypeDoc.NestedTypes())
            {   
                // For every subtype
                foreach (TypeDoc td_SubType in td_NestedType.FlattenNested())
                {
                    // Add the subtype to the output
                    yield return td_SubType;
                }
            }
        }



        /// <summary>
        /// Get all members (fields, properties, methods, events) including nested types recursively
        /// </summary>
        /// <param name="param_CallingTypeDoc"></param>
        /// <returns></returns>
        public static IEnumerable<MemberDoc> AllMembers(this TypeDoc param_CallingTypeDoc)
        {
            foreach (MemberDoc md_Field in param_CallingTypeDoc.Fields) yield return md_Field;
            foreach (MemberDoc md_Property in param_CallingTypeDoc.Properties) yield return md_Property;
            foreach (MemberDoc md_Method in param_CallingTypeDoc.Methods) yield return md_Method;
            foreach (MemberDoc md_Constructor in param_CallingTypeDoc.Constructors) yield return md_Constructor;
            foreach (MemberDoc md_Event in param_CallingTypeDoc.Events) yield return md_Event;


            foreach (TypeDoc td_NestedType in param_CallingTypeDoc.NestedTypes())
            {
                foreach (MemberDoc md_NestedMember in td_NestedType.AllMembers())
                {
                    yield return md_NestedMember;
                }
            }
        }

        /// <summary>
        /// Add a member to the correct list in the calling TypeDoc
        /// </summary>
        /// <param name="param_CallingTypeDoc"></param>
        /// <param name="param_MemberDoc"></param>
        public static void AddMember(this TypeDoc param_CallingTypeDoc, MemberDoc param_MemberDoc)
        {
            switch (param_MemberDoc.Kind)
            {
                case "ctor": param_CallingTypeDoc.Constructors.Add(param_MemberDoc); break;
                case "method": param_CallingTypeDoc.Methods.Add(param_MemberDoc); break;
                case "property": param_CallingTypeDoc.Properties.Add(param_MemberDoc); break;
                case "event": param_CallingTypeDoc.Events.Add(param_MemberDoc); break;
                case "field": param_CallingTypeDoc.Fields.Add(param_MemberDoc); break;
                case "enum-member": param_CallingTypeDoc.Fields.Add(param_MemberDoc); break;
            }
        }
    }
}
