#!/bin/bash
# Builds Launcher.exe with mono's C# compiler. The result runs on the .NET Framework 4.8 built into Windows 10/11.
cd "$(dirname "$0")"
mcs -target:winexe -out:Launcher.exe -win32icon:launcher.ico -optimize+ \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.IO.Compression.dll \
  -r:System.IO.Compression.FileSystem.dll -r:System.Web.Extensions.dll \
  Launcher.cs
