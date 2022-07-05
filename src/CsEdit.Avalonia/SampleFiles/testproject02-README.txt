
## two projects, so that a console project has a reference to classlib project.

$ dotnet new console --framework "netcoreapp3.1" --output testproject02 --name TestProject02 

$ dotnet new classlib --framework "netcoreapp3.1" --output testproject02b --name TestProject02b 

$ dotnet add testproject02/TestProject02.csproj reference testproject02b/TestProject02b.csproj 



