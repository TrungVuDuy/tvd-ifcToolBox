# IFC Toolbox

Tools for processing IFC files: validate against IDS, convert, split, optimise, anonymise and georeference.
Supports IFC2X3, IFC4 and IFC 4.3 (IFC4X3_ADD2, ISO 16739-1:2024). Built on [xbim](https://docs.xbim.net/) and [IfcOpenShell](https://ifcopenshell.org/).

## Packages

| Package | What it is |
|---|---|
| `IfcToolbox.Cli` | The `ifctoolbox` command line tool |
| `IfcToolbox.Tools` | One processor per tool, for use from .NET code |
| `IfcToolbox.Core` | The IFC building blocks the tools are made of |

## Command line

```
dotnet tool install -g IfcToolbox.Cli --prerelease

ifctoolbox info model.ifc
ifctoolbox validate model.ifc --ids requirements.ids --report findings.csv
ifctoolbox convert model.ifc --format glb
ifctoolbox split road.ifc --by facility --list
ifctoolbox split road.ifc --by part --item "#146"
ifctoolbox optimize model.ifc --precision 4
ifctoolbox anonymize model.ifc
ifctoolbox georef model.ifc --crs EPSG:25832 --eastings 333000 --northings 5800000
```

Exit codes: `0` success, `1` the model failed validation, `2` usage error, `3` error.
`validate` returning `1` is what lets it gate a CI pipeline.

## IDS validation

Reads and writes IDS 1.0 and checks the entity, attribute, property, classification, material and partOf
facets with restrictions, cardinality and unit conversion. Verified against every runnable case of the
official buildingSMART IDS test suite.

```csharp
var result = IdsProcessor.ProcessToCsv("model.ifc", "requirements.ids");
```

## Converting

Conversion runs IfcConvert. The bundled build is IfcOpenShell 0.6, which writes GLB, OBJ, DAE, STEP,
IGES, SVG, XML and IFC but cannot read IFC 4.3 models. Point the `IFCTOOLBOX_IFCCONVERT` environment
variable at an IfcConvert 0.8 executable for IFC 4.3 and for the JSON, TTL and H5 formats.
IfcConvert is Windows x64 only as shipped.

## Licence

GPL-3.0. IfcOpenShell (IfcConvert) is LGPL-3.0, xbim is CDDL.
