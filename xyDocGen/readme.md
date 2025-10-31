# xyDocumentor

xyDocumentor is an open source CLI tool for generating API documentation from C# projects.  
It supports Markdown, HTML, PDF, and JSON output.
Scans all C# files in the specified root directory and all its subfolders.
Extracts all types and nested types, methods, properties, events, fields, etc.
Collects the XML Comments from the project.
Generates a tree to visualize the project structure.

Each format is written into its **own subfolder**, and optional **index** and **tree** files visualize your project's structure.



## Usage example:

(Sample for easiest input to generate everything according to the standard settings)
xydocgen               =====>>>>>               xydocgen      --root [current working directory]     --folder docs     --subfolder api     --exclude .git;bin;obj;node_modules;.vs;TestResults     --format md     

(Use dotnet at the start when installed locally)
dotnet xydocgen --root . --out docs/api --exclude .git;bin;obj;node_modules;.vs;TestResults

(add this keyword to exclude non public)
xydocgen  --private 

(Choose your output flavour)
xydocgen --format [json/pdf/html/md]

(Another Sample)
xydocgen --root X://User/TestPrograms/TestRoot--out TestFolder/TestSubFolder 

(Output a list of commands, discard other commands written with it)
xydocgen --help

## Changes

- Now every format has its own folder
- Generate multiple formats in one go

## Planned features & improvements

- Removing the Projectname from output folders 
 
+ Upgrades 
	- Reading and outputting the remarks
	- Upgrading the visual result of the outputs


  
## Installation

### Local:
dotnet new tool-manifest
dotnet tool install --local xyDocGen --version 1.0.xx

### Global:
dotnet tool install --global xyDocGen --version 1.0.xx


## Update

### Checking the Version of installed dotnet tools
dotnet list tool [--local/--global]

### Updating the Version
dotnet tool update xydocgen [--local/--global]


## Problems

There is currently no format subfolder being used and the pdf folders lie in the docs folder
The api folder is useless and needs to be removed, if there is no replacement from the input


# Author
ThrashingLaggard
https://github.com/ThrashingLaggard

