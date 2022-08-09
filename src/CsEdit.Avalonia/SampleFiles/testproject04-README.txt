
## two projects, so that a console project has a reference to classlib project.
## the classlib project has a nuget package reference.

$ dotnet new console --framework "netcoreapp3.1" --output testproject04 --name TestProject04 

$ dotnet new classlib --framework "netcoreapp3.1" --output testproject04b --name TestProject04b 

$ dotnet add testproject04/TestProject04.csproj reference testproject04b/TestProject04b.csproj 

$ dotnet add testproject04b/TestProject04b.csproj package Newtonsoft.Json --version 12.0.1 



