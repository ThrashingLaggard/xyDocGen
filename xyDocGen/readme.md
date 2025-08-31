# xyDocGen

xyDocGen is an open source tool for automatically generating API documentation from C# projects.  
It supports Markdown, HTML, PDF, and JSON, and handles all nested types recursively.

Scans all C# files in the specified root directory.

Extracts all types, methods, properties, events, fields, and nested types.


## Usage example:

#recommended
(dotnet new tool-manifest)
(dotnet tool install --local xyDocGen --version 1.0.14)
dotnet xydocgen --root . --out docs/api --exclude ".git;bin;obj;node_modules;.vs;TestResults"
or
(dotnet tool install --global xyDocGen --version 1.0.14)
xydocgen --root . --out docs/api --exclude ".git;bin;obj;node_modules;.vs;TestResults"




# Author
ThrashingLaggard
https://github.com/ThrashingLaggard