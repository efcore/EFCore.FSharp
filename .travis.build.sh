#!/usr/bin/env bash

mono .paket/paket.exe restore
# Change directory separator, this curently breaks build on Travis CI, hence the build outside FAKE
sed -i 's#\\#/#g' EFCore.FSharp.sln
MSBuildEmitSolution=1 dotnet build EFCore.FSharp.sln # MSBuildEmitSolution is to output *.fsproj.metaproj files
dotnet test EFCore.FSharp.Test/EFCore.FSharp.Test.fsproj
