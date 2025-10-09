# xyDocumentor

xyDocumentor is an open source CLI tool for generating API documentation from C# projects.  
It supports Markdown, HTML, PDF, and JSON output.
Scans all C# files in the specified root directory and all its subfolders.
Extracts all types and nested types, methods, properties, events, fields, etc.
Collects the XML Comments from the project.
Generates a tree to visualize the project structure.



## Usage example:

(Sample for easiest input to generate everything according to the standard settings)
xydocgen               =====>>>>>               xydocgen      --root [current working directory]     --folder docs     --subfolder api     --exclude .git;bin;obj;node_modules;.vs;TestResults     --format md     

(Use dotnet at the start when installed locally)
dotnet xydocgen --root . --out docs/api --exclude .git;bin;obj;node_modules;.vs;TestResults

(add this keyword to exclude non public)
xydocgen  --privat 

(Choose your output flavour)
xydocgen --format [json/pdf/html/md]

(Another Sample)
xydocgen --root X://User/TestPrograms/TestRoot--out TestFolder/TestSubFolder 

(Output a list of commands, MUST be the first argument if used (others will then be ignored))
xydocgen --help


## Installation

### Local:
(dotnet new tool-manifest)
(dotnet tool install --local xyDocGen --version 1.0.xx)

### Global:
(dotnet tool install --global xyDocGen --version 1.0.xx)



## Planned features & improvements

- Reading and outputting the remarks
- Upgrading the visual result of the outputs
- 
- Format mit als Unterordner hinzufügen.....                                 was soll das heißen?????



# Author
ThrashingLaggard
https://github.com/ThrashingLaggard

## Contact
Ideas or questions:
xytlagg@gmail.com