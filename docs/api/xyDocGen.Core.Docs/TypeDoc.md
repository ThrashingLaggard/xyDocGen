# record `TypeDoc`

## Metadata
**Namespace:** `xyDocGen.Core.Docs`
**Visibility:** `public`
**Source File:** `C:\Users\Leon\source\repos\ThrashingLaggard\xyDocGen\xyDocGen\Core\Docs\TypeDoc.cs`

## Description
(No XML-Summary)

## Properties

| Signature | Description |
|----------|--------------|
| `string Kind{ get; init; }` | Kind of type: "class", "struct", "interface", "record", "enum" |
| `string Name{ get; init; }` | Type name including gen |
| `List<TypeDoc> NestedTypes{ get; set; }` | (No XML-Summary) |
| `string Namespace{ get; init; }` | Namespace the type belongs to, "" if none |
| `string Modifiers{ get; init; }` | Modifiers like "public", "internal", "abstract" |
| `List<string> Attributes{ get; init; }` | List of attribute names applied to the type |
| `List<string> BaseTypes{ get; init; }` | List of base classes or implemented interfaces |
| `string Summary{ get; init; }` | Summary extracted from XML doc comments |
| `string FilePath{ get; init; }` | File path where the type is defined |
| `string Parent{ get; init; }` | If nested type, parent type name |
| `List<MemberDoc> Constructors{ get; }` | (No XML-Summary) |
| `List<MemberDoc> Properties{ get; }` | (No XML-Summary) |
| `List<MemberDoc> Methods{ get; }` | (No XML-Summary) |
| `List<MemberDoc> Events{ get; }` | (No XML-Summary) |
| `List<MemberDoc> Fields{ get; }` | (No XML-Summary) |
| `string DisplayName` | (No XML-Summary) |