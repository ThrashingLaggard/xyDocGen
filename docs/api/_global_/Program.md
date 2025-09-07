# class `Program`

## Metadata
**Namespace:** `<global>`
**Visibility:** `public`
**Source File:** `C:\Users\Leon\source\repos\ThrashingLaggard\xyDocGen\xyDocGen\CLI\Program.cs`

## Description
(No XML-Summary)

## Methods

| Signature | Description |
|----------|--------------|
| `int Main(string[] args)` | (No XML-Summary) |
| `string GetStartingPath(List<string> ExternalArguments, string[] args)` | (No XML-Summary) |
| `string GetOutputPath(List<string> externalarguments, string[] args,string rootpath)` | (No XML-Summary) |
| `string GetFormat(List<string> ExternalArguments, string[] args)` | (No XML-Summary) |
| `bool GetPublicityHandling(List<string> ExternalArguments)` | (No XML-Summary) |
| `HashSet<string> GetIgnorableFiles(List<string> ExternalArguments, string[] args)` | (No XML-Summary) |
| `Task BuildIndexAndTree(IEnumerable<TypeDoc> flattenedTypes, string format, string rootPath, string outPath, HashSet<string> excludedParts)` | (No XML-Summary) |
| `Task<StringBuilder> BuildProjectIndex(IEnumerable<TypeDoc> flattenedtypes, string format,string outpath)` | (No XML-Summary) |
| `Task<StringBuilder> BuildProjectTree(StringBuilder treeBuilder, string format, string rootPath, string outPath, HashSet<string> excludedParts)` | (No XML-Summary) |
| `IEnumerable<string> CollectFiles(List<string> ExternalArguments, string[] args, string rootPath, HashSet<string> excluded)` | (No XML-Summary) |
| `Task<List<TypeDoc>> TryParseDataFromFile(List<string> ExternalArguments, string[] args, IEnumerable<string> relevantFiles, bool includeNonPublic)` | objects. |
| `IEnumerable<TypeDoc> FlattenTypes(List<TypeDoc> types)` | (No XML-Summary) |
| `Task<bool> WriteDataToFilesOrderedByNamespace(IEnumerable<TypeDoc> alltypes, string outpath, string format)` | (No XML-Summary) |