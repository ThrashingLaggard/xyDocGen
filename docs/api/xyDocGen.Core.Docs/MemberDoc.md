# record `MemberDoc`

## Metadata
**Namespace:** `xyDocGen.Core.Docs`
**Visibility:** `public`
**Source File:** `C:\Users\Leon\source\repos\ThrashingLaggard\xyDocGen\xyDocGen\Core\Docs\MemberDoc.cs`

## Description
(No XML-Summary)

## Properties

| Signature | Description |
|----------|--------------|
| `string Kind{ get; init; }` | Kind of member: "field", "property", "method", "ctor", "event", "enum-member" |
| `string Signature{ get; init; }` | Signature of the member (name + parameters/type) |
| `string Modifiers{ get; init; }` | Modifiers like "public", "private", "protected internal" |
| `string Summary{ get; init; }` | Optional documentation/summary extracted from XML comments |