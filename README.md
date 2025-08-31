# xyDocGen

xyDocGen is a tool for automatically generating API documentation from C# projects.  
It supports Markdown, HTML, PDF, and JSON, and handles all nested types recursively.

Scans all C# files in the specified root directory.

Extracts all types, methods, properties, events, fields, and nested types.

---

## Usage example:

xydocgen --root . --out docs/api --exclude ".git;bin;obj;node_modules;.vs;TestResults"

