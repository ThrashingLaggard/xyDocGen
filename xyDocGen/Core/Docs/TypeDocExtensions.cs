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
        /// <param name="CallingTypeDoc_"> the TypeDoc instance calling the method</param>
        /// <returns></returns>
        public static List<TypeDoc> NestedTypes(this TypeDoc CallingTypeDoc_) => CallingTypeDoc_.GetNestedList();
        

        /// <summary>
        /// Returns all nested types stored in the mapping.
        /// </summary>
        /// <param name="CallingTypeDoc_"></param>
        /// <returns></returns>
        private static List<TypeDoc> GetNestedList(this TypeDoc CallingTypeDoc_)
        {   
            // If there is no value for the key, return a new empty list.
            if (!NestedMapping.TryGetValue(CallingTypeDoc_, out List<TypeDoc> list))
            {
                list = [];
                NestedMapping[CallingTypeDoc_] = list;
            }
            
            // Else return the listed values.
            return list;
        }



       /// <summary>
       /// Recursively yields this type + all nested types
       /// </summary>
       /// <param name="CallingTypeDoc_"></param>
       /// <returns></returns>
        public static IEnumerable<TypeDoc> FlattenNested(this TypeDoc CallingTypeDoc_)
        {
            // Add the caller to the output
            yield return CallingTypeDoc_;

            // For every nested type 
            foreach (TypeDoc td_NestedType in CallingTypeDoc_.NestedTypes())
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
        /// <param name="CallingTypeDoc_"></param>
        /// <returns></returns>
        public static IEnumerable<MemberDoc> AllMembers(this TypeDoc CallingTypeDoc_)
        {
            foreach (MemberDoc md_Field in CallingTypeDoc_.Fields)
            {
                yield return md_Field;
            }
            foreach (MemberDoc md_Property in CallingTypeDoc_.Properties) 
            {
                yield return md_Property;
            }
            foreach (MemberDoc md_Method in CallingTypeDoc_.Methods) 
            {
                yield return md_Method;
            }
            foreach (MemberDoc md_Constructor in CallingTypeDoc_.Constructors)
            {
                yield return md_Constructor;
            }
            foreach (MemberDoc md_Event in CallingTypeDoc_.Events) 
            {
                yield return md_Event;
            }
            foreach (TypeDoc td_NestedType in CallingTypeDoc_.NestedTypes())
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
        /// <param name="CallingTypeDoc_"></param>
        /// <param name="MemberDoc_"></param>
        public static void AddMember(this TypeDoc CallingTypeDoc_, MemberDoc MemberDoc_)
        {
            switch (MemberDoc_.Kind)
            {
                case "ctor": CallingTypeDoc_.Constructors.Add(MemberDoc_); break;
                case "method": CallingTypeDoc_.Methods.Add(MemberDoc_); break;
                case "property": CallingTypeDoc_.Properties.Add(MemberDoc_); break;
                case "event": CallingTypeDoc_.Events.Add(MemberDoc_); break;
                case "field": CallingTypeDoc_.Fields.Add(MemberDoc_); break;
                case "enum-member": CallingTypeDoc_.Fields.Add(MemberDoc_); break;
            }
        }
    }
}
