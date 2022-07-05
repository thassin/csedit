
## one single console project, with a nuget package reference.

$ dotnet new console --framework "netcoreapp3.1" --output testproject03 --name TestProject03 

$ dotnet add testproject03/TestProject03.csproj package Newtonsoft.Json --version 12.0.1 







info : Adding PackageReference for package 'Newtonsoft.Json' into project 'testproject03/TestProject03.csproj'.
info : Restoring packages for /home/tommi/tommih/proj2022/_EDITORI/cseditGIT/src/CsEdit.Avalonia/SampleFiles/xx/testproject03/TestProject03.csproj...
info : Package 'Newtonsoft.Json' is compatible with all the specified frameworks in project 'testproject03/TestProject03.csproj'.
info : PackageReference for package 'Newtonsoft.Json' version '12.0.1' added to file '/home/tommi/tommih/proj2022/_EDITORI/cseditGIT/src/CsEdit.Avalonia/SampleFiles/xx/testproject03/TestProject03.csproj'.
info : Writing assets file to disk. Path: /home/tommi/tommih/proj2022/_EDITORI/cseditGIT/src/CsEdit.Avalonia/SampleFiles/xx/testproject03/obj/project.assets.json
log  : Restored /home/tommi/tommih/proj2022/_EDITORI/cseditGIT/src/CsEdit.Avalonia/SampleFiles/xx/testproject03/TestProject03.csproj (in 165 ms).



$ locate Newtonsoft.Json.dll | grep 12.0.1
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/net20/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/net35/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/net40/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/net45/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/netstandard1.0/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/netstandard1.3/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/netstandard2.0/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/portable-net40+sl5+win8+wp8+wpa81/Newtonsoft.Json.dll
/home/tommi/.nuget/packages/newtonsoft.json/12.0.1/lib/portable-net45+win8+wp8+wpa81/Newtonsoft.Json.dll



