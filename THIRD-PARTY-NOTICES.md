# IFC Toolbox: licence and third-party notices

## IFC Toolbox

Copyright (c) 2021 Yousheng Wang. Modifications from 2026 by their respective contributors.

IFC Toolbox is free software under the GNU General Public License version 3 (`LICENSE.txt`).
It comes with ABSOLUTELY NO WARRANTY. Its source code is published with every release: each
release page on GitHub links a source code archive of exactly that version. The original
project is at https://github.com/youshengCode/IfcToolbox.

## Components included

The desktop app (`IfcToolbox.exe`) and the command line tool (`ifctoolbox.exe`) include the
components below. The licence texts are in the `licenses` folder next to this file.

### IfcOpenShell IfcConvert 0.6.0b0 (`IfcConvert_x64.exe`)

- Licence: GNU Lesser General Public License version 3 (`licenses/LGPL-3.0.txt`), which builds
  on the GNU General Public License version 3 (`LICENSE.txt`).
- Copyright (c) IfcOpenShell contributors.
- Source code: https://github.com/IfcOpenShell/IfcOpenShell (release v0.6.0).
- IfcConvert runs as a separate program and is not modified. You can use your own build or a
  newer version instead: in the desktop app under Settings > IfcConvert, or for the command line
  tool with the `IFCTOOLBOX_IFCCONVERT` environment variable.

### xbim Toolkit, Xbim.Essentials 6.1.605

`Xbim.Common.dll`, `Xbim.Ifc.dll`, `Xbim.Ifc2x3.dll`, `Xbim.Ifc4.dll`, `Xbim.Ifc4x3.dll`,
`Xbim.IO.MemoryModel.dll`, `Xbim.IO.Esent.dll`

- Licence: Common Development and Distribution License 1.0 (`licenses/CDDL-1.0.txt`).
- Copyright (c) xBIM Team and contributors.
- Source code: https://github.com/xBimTeam/XbimEssentials

### Apache License 2.0 (`licenses/Apache-2.0.txt`)

- Serilog 4.4.0 and Serilog.Sinks.Console 6.1.1. Copyright (c) Serilog Contributors.
  https://github.com/serilog/serilog
- CsvHelper 33.1.0, offered under MS-PL or Apache-2.0 and used here under Apache-2.0.
  Copyright (c) Josh Close. https://github.com/JoshClose/CsvHelper

### MIT (`licenses/MIT.txt`)

- .NET 8 runtime and libraries, including Microsoft.Extensions.* 8.0. Copyright (c) .NET Foundation
  and Contributors. https://github.com/dotnet/runtime, whose own third-party notices are at
  https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT
- System.CommandLine 2.0.12 (command line tool). Copyright (c) .NET Foundation and Contributors.
  https://github.com/dotnet/command-line-api
- ManagedEsent 2.0.3 (`Esent.Interop.dll`). Copyright (c) Microsoft Corporation.
  https://github.com/microsoft/ManagedEsent
- C#/WinRT (`WinRT.Runtime.dll`, `Microsoft.Windows.SDK.NET.dll`, desktop app). Copyright (c)
  Microsoft Corporation. https://github.com/microsoft/CsWinRT
- CommunityToolkit.Mvvm 8.4.2 (desktop app). Copyright (c) .NET Foundation and Contributors.
  Licence text as published: `licenses/CommunityToolkit-License.md`.
  https://github.com/CommunityToolkit/dotnet

### Microsoft Windows App SDK 2.x, including WinUI 3 (desktop app)

- Licence: Microsoft Software License Terms for the Windows App SDK
  (`licenses/Microsoft-Windows-App-SDK.txt`), with its own third-party notices in
  `licenses/Microsoft-Windows-App-SDK-NOTICE.txt`.
- Copyright (c) Microsoft Corporation. https://github.com/microsoft/WindowsAppSDK

## Services used, not included

- The buildingSMART Data Dictionary (bSDD), https://search.bsdd.buildingsmart.org, which the IFC
  Validator queries over the internet when its bSDD option is used. Subject to the terms of use
  of buildingSMART International.
