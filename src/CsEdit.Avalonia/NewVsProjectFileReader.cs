using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace CsEdit.Avalonia {

    // this class is for reading newer (vs2017-style) ".csproj" files.

    // https://docs.microsoft.com/en-us/aspnet/web-forms/overview/deployment/web-deployment-in-the-enterprise/understanding-the-project-file 
    // https://natemcmaster.com/blog/2017/03/09/vs2015-to-vs2017-upgrade/ 

    public class NewVsProjectFileReader : IProjectReader {

        public bool TryRead( string dirPathRel, List<ProjectDescriptor> pList ) {
            return TryRead_p( dirPathRel, pList, 0 );
        }

        private bool TryRead_p( string dirPathRel, List<ProjectDescriptor> pList, int depth ) {

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine( "NewVsProjectFileReader: STARTING d=" + depth );

            string filename = ProjectsProvider.FindFirstFileWithExtension( dirPathRel, "csproj" );
            if ( filename == null ) return false;

            Console.WriteLine( "dirPathRel: " + dirPathRel );
            Console.WriteLine( "filename:" + filename );

            string pName = dirPathRel + Path.DirectorySeparatorChar + filename;

            // do not create a project, if it is already created.
            // TODO is there need to check/parse the project-file? no?

            bool alreadyExists = false;
            foreach ( ProjectDescriptor pdFindByName in pList ) {
                if ( pdFindByName.ProjectNameUniq != pName ) continue;
                alreadyExists = true;
                break;
            }

            if ( alreadyExists ) {
                // return true now.
                // the project exists, and a reference to it can be added.
                // we just don't want to create a duplicate of it...
                return true;
            }

            string path = ProjectsProvider.WorkingDirectory;
            path += Path.DirectorySeparatorChar + dirPathRel;
            path += Path.DirectorySeparatorChar + filename;

            Console.WriteLine( "NewVsProjectFileReader: READING " + path );

            string sdk = null;
            string targetFramework = null;

            List<string> srcFileNames = new List<string>();
            List<string> libFileNames = new List<string>();

            // list all projectReferences found from projectfile.
            List<string> prList1 = new List<string>();

            Dictionary<string,string> pkgRefs = new Dictionary<string,string>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            using ( var fileStream = File.OpenText( path ) )
            using ( XmlReader reader = XmlReader.Create( fileStream, settings ) ) {
                while (reader.Read()) {

                    if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "Project" ) ) {
                        if ( reader.HasAttributes ) {
                            sdk = reader.GetAttribute("Sdk");
                        }
                    }

                    if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "PropertyGroup" ) ) {
                        Console.WriteLine( "START reading element: PropertyGroup" );
                        while (reader.Read()) {

                            if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "TargetFramework" ) ) {
                                targetFramework = reader.ReadElementContentAsString();
                                Console.WriteLine( "found targetFramework: " + targetFramework );
                            }

                            if ( ( reader.NodeType == XmlNodeType.EndElement ) && ( reader.Name == "PropertyGroup" ) ) {
                                Console.WriteLine( "END reading element: PropertyGroup" );
                                break;
                            }
                        }
                    }

                    if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "ItemGroup" ) ) {
                        Console.WriteLine( "START reading element: ItemGroup" );
                        while (reader.Read()) {

                            if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "ProjectReference" ) ) {
                                if ( reader.HasAttributes ) {
                                    string prInclude = reader.GetAttribute("Include");
                                    Console.WriteLine( "found a PROJECT REFERENCE: " + prInclude );
                                    prList1.Add( prInclude );
                                }
                            }

                            if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "PackageReference" ) ) {
                                if ( reader.HasAttributes ) {
                                    string pkgInclude = reader.GetAttribute("Include");
                                    string pkgVersion = reader.GetAttribute("Version");
                                    Console.WriteLine( "found a PACKAGE REFERENCE: " + pkgInclude + " " + pkgVersion );
                                    pkgRefs.Add( pkgInclude, pkgVersion );
                                }
                            }

                            if ( ( reader.NodeType == XmlNodeType.EndElement ) && ( reader.Name == "ItemGroup" ) ) {
                                Console.WriteLine( "END reading element: ItemGroup" );
                                break;
                            }
                        }
                    }

/* TODO a plain library reference still missing:

  <ItemGroup>
    <Reference Include="ClassLibrary1">
      <HintPath>..\ClassLibrary1.dll</HintPath>
    </Reference>
  </ItemGroup>

*/

                }
            }

            Console.WriteLine( "AT THE END: SDK=" + sdk );

            if ( sdk == null ) return false;
            if ( sdk != "Microsoft.NET.Sdk" ) return false;

            Console.WriteLine( "CONTINUE READING..." );

            // need to include all local .cs files?
            // by default, unless told otherwise?

            string srcFileSarchPath = ProjectsProvider.WorkingDirectory;
            srcFileSarchPath += Path.DirectorySeparatorChar + dirPathRel;

            string[] items = Directory.GetFiles( srcFileSarchPath, "*.cs" );

            foreach ( string resultPath in items ) {
                if ( File.Exists( resultPath ) == false ) continue; // ignore directories etc.
                string fileName = Path.GetFileName( resultPath );

                //Console.WriteLine( "GOT FILENAME: " + fileName );
                //Console.WriteLine( "RELATIVE PATH: " + dirPathRel );

                srcFileNames.Add( dirPathRel + Path.DirectorySeparatorChar + fileName );
            }

            // do not create a project, if it is an empty one.
            // => TODO is there any need to include empty projects???

            if ( srcFileNames.Count < 1 ) return false;

            // the netstandard.dll is needed if:
            // => the project target is a netstandard one?
            // => any of the package-reference dlls is a netstandard one?

            bool needToAddNetstandardDll = false;

            // the mscorlib.dll is needed if:
            // => the project target is an old .net-framework one?
            // => any of the package-reference dlls is an old .net-framework one?

            bool needToAddMscorlibDll = false;

            // for any sub-projects, call this same method to read it.
            // => BUT ONLY ONCE for each project, do not create duplicates.

            // list all projectReferences for which the project has been successfully added.
            List<string> prList2 = new List<string>();

            foreach ( string projectReference in prList1 ) {

                // assume that projectReference is always a relative path.
                // it seems that windows-style directory separators are always used?

                string prPathRel = projectReference;
                if ( Path.DirectorySeparatorChar != '\\' ) {
                    prPathRel = prPathRel.Replace( '\\', Path.DirectorySeparatorChar );
                }

                string nextProjectPath = ProjectsProvider.WorkingDirectory;
                nextProjectPath += Path.DirectorySeparatorChar + dirPathRel;
                nextProjectPath += Path.DirectorySeparatorChar + prPathRel;

                string projectPathAbs = Path.GetFullPath( nextProjectPath );

                Console.WriteLine( "ProjectsProvider.WorkingDirectory: " + ProjectsProvider.WorkingDirectory );
                Console.WriteLine( "SUBPROJECT ABS PATH: " + projectPathAbs );

                string projectPathRel = Path.GetRelativePath( ProjectsProvider.WorkingDirectory, projectPathAbs );

                Console.WriteLine( "SUBPROJECT REL PATH: " + projectPathRel );

                string projectPathRel_withName = projectPathRel;

                // drop the filename, because an IProjectReader will try to read in a project folder.
                // => the projectfile is searched/recognized by IProjectReader using it's own internal logic.
                projectPathRel = Path.GetDirectoryName( projectPathRel );

                if ( TryRead_p( projectPathRel, pList, depth + 1) ) {

                    Console.WriteLine( "SUBPROJECT read was succesful..." );

                    prList2.Add( projectPathRel_withName );
                } else {
                    Console.WriteLine( "ERROR: failed to read project: " + projectPathRel_withName );
                }
            }

            // now we need to convert all package-references to library(dll)-references.
            // => linux : the files are in /home/<username>/.nuget/packages/<packagename>/<packageversion>/lib/<??>/Newtonsoft.Json.dll

            string username = Environment.UserName;

            foreach( KeyValuePair<string,string> pkg in pkgRefs ) {
                string pkgName = pkg.Key.ToLower();
                string pkgVersion = pkg.Value;

                // TODO detect platform: linux/mac/windows and adjust search path accordingly.

                string libFileSearchPath = "/home/" + username + "/.nuget/packages/" + pkgName + "/" + pkgVersion + "/lib";

                Console.WriteLine( "PKG-searchpath : " + libFileSearchPath );

                if ( Directory.Exists( libFileSearchPath ) == false ) {
                    // this happens at least for ignoresaccesscheckstogenerator/0.5.0 which has no libraries at all?
                    Console.WriteLine( "WARNING no such package directory : " + libFileSearchPath );
                    continue;
                }

                string[] libFiles = Directory.GetFiles( libFileSearchPath, "*.dll", SearchOption.AllDirectories );

                // TODO here we just do a simple selection from multiple .dll versions.
                // => just select "netstandardX.Y" over "netXX" versions (simply reverse-sort the names).
                // => do not select "portable-*" versions.

                Array.Reverse( libFiles );

                // the .dll files are in separate subdirectories => get and analyze the directory names.

                string selectedLibFile = null;

                foreach ( string libFile in libFiles ) {
                    string dirName = new DirectoryInfo( libFile ).Parent.Name;

                    //Console.WriteLine( "PKG-search RESULT : " + libFile + " dirname " + dirName );

                    bool isOk = false;

/*

$ locate mscorlib.dll | more
/home/username/.nuget/packages/microsoft.aspnetcore.components.webassembly.runtime/3.2.0/tools/dotnetwasm/bcl/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app.ref/3.1.0/ref/netcoreapp3.1/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app.ref/5.0.0/ref/net5.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.linux-x64/6.0.5/runtimes/linux-x64/lib/net6.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.osx-x64/6.0.5/runtimes/osx-x64/lib/net6.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.win-x64/5.0.14/runtimes/win-x64/lib/net5.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.win-x64/6.0.5/runtimes/win-x64/lib/net6.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app/2.0.0/ref/netcoreapp2.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app/2.1.0/ref/netcoreapp2.1/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.app/2.2.0/ref/netcoreapp2.2/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.portable.compatibility/1.0.1/ref/netcore50/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.portable.compatibility/1.0.1/ref/netstandard1.0/mscorlib.dll
/home/username/.nuget/packages/microsoft.netcore.portable.compatibility/1.0.1/runtimes/aot/lib/netcore50/mscorlib.dll
/home/username/.nuget/packages/microsoft.netframework.referenceassemblies.net461/1.0.0/build/.NETFramework/v4.6.1/mscorlib.dll
/home/username/.nuget/packages/microsoft.netframework.referenceassemblies.net461/1.0.2/build/.NETFramework/v4.6.1/mscorlib.dll
/home/username/.nuget/packages/microsoft.netframework.referenceassemblies.net472/1.0.0/build/.NETFramework/v4.7.2/mscorlib.dll
/home/username/.nuget/packages/microsoft.netframework.referenceassemblies.net472/1.0.2/build/.NETFramework/v4.7.2/mscorlib.dll
/home/username/.nuget/packages/mono.webassembly.framework/0.2.2/wasm-assemblies/mscorlib.dll
/home/username/.nuget/packages/netstandard.library/2.0.0/build/netstandard2.0/ref/mscorlib.dll
/home/username/.nuget/packages/netstandard.library/2.0.3/build/netstandard2.0/ref/mscorlib.dll
/home/username/.nuget/packages/runtime.linux-x64.microsoft.netcore.app/2.2.0/runtimes/linux-x64/lib/netcoreapp2.2/mscorlib.dll
/home/username/.nuget/packages/runtime.osx-x64.microsoft.netcore.app/2.2.0/runtimes/osx-x64/lib/netcoreapp2.2/mscorlib.dll
/home/username/.nuget/packages/runtime.osx.10.10-x64.microsoft.netcore.runtime.coreclr/1.1.2/runtimes/osx.10.10-x64/lib/netstandard1.0/mscorlib.dll
/home/username/.nuget/packages/runtime.ubuntu.14.04-x64.microsoft.netcore.runtime.coreclr/1.1.2/runtimes/ubuntu.14.04-x64/lib/netstandard1.0/mscorlib.dll
/home/username/.nuget/packages/runtime.win-x64.microsoft.netcore.app/2.2.0/runtimes/win-x64/lib/netcoreapp2.2/mscorlib.dll
/home/username/.nuget/packages/runtime.win7-x64.microsoft.netcore.runtime.coreclr/1.1.2/runtimes/win7-x64/lib/netstandard1.0/mscorlib.dll

$ locate netstandard.dll | more
/home/username/.nuget/packages/microsoft.aspnetcore.components.webassembly.runtime/3.2.0/tools/dotnetwasm/bcl/Facades/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app.ref/3.1.0/ref/netcoreapp3.1/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app.ref/5.0.0/ref/net5.0/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.linux-x64/6.0.5/runtimes/linux-x64/lib/net6.0/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.osx-x64/6.0.5/runtimes/osx-x64/lib/net6.0/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.win-x64/5.0.14/runtimes/win-x64/lib/net5.0/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app.runtime.win-x64/6.0.5/runtimes/win-x64/lib/net6.0/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app/2.0.0/ref/netcoreapp2.0/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app/2.1.0/ref/netcoreapp2.1/netstandard.dll
/home/username/.nuget/packages/microsoft.netcore.app/2.2.0/ref/netcoreapp2.2/netstandard.dll
/home/username/.nuget/packages/microsoft.netframework.referenceassemblies.net472/1.0.0/build/.NETFramework/v4.7.2/Facades/netstandard.dll
/home/username/.nuget/packages/microsoft.netframework.referenceassemblies.net472/1.0.2/build/.NETFramework/v4.7.2/Facades/netstandard.dll
/home/username/.nuget/packages/mono.webassembly.framework/0.2.2/wasm-assemblies/Facades/netstandard.dll
/home/username/.nuget/packages/netstandard.library/2.0.0/build/netstandard2.0/ref/netstandard.dll
/home/username/.nuget/packages/netstandard.library/2.0.3/build/netstandard2.0/ref/netstandard.dll
/home/username/.nuget/packages/runtime.linux-x64.microsoft.netcore.app/2.2.0/runtimes/linux-x64/lib/netcoreapp2.2/netstandard.dll
/home/username/.nuget/packages/runtime.osx-x64.microsoft.netcore.app/2.2.0/runtimes/osx-x64/lib/netcoreapp2.2/netstandard.dll
/home/username/.nuget/packages/runtime.win-x64.microsoft.netcore.app/2.2.0/runtimes/win-x64/lib/netcoreapp2.2/netstandard.dll

// https://michaelscodingspot.com/assemblies-load-in-dotnet/ 

*/

                    if ( dirName.StartsWith( "netstandard" ) ) {
                        // these seem to require "netstandard" assemblies...
                        needToAddNetstandardDll = true;
                        isOk = true;
                    }

                    if ( dirName.StartsWith( "net" ) && dirName.Length == 5 ) {
                        // these seem to require "mscorlib" assemblies...
                        needToAddMscorlibDll = true;
                        isOk = true;
                    }

                    // TODO need to improve this later, and observe the target frameword used.
                    // perhaps have a method: string[] GetCompatibleTargetFrameworks( string targetFramework )
                    // https://dotnet.microsoft.com/en-us/platform/dotnet-standard#versions 
                    // https://docs.microsoft.com/en-us/dotnet/standard/net-standard?tabs=net-standard-2-1 

                    if ( isOk ) {
                        selectedLibFile = libFile;
                        break;
                    }
                }

                if ( selectedLibFile != null ) {
                    Console.WriteLine( "PKG-search : using library " + selectedLibFile );
                    libFileNames.Add( selectedLibFile );
                } else {
                    Console.WriteLine( "PKG-search : WARNING no library found for " + pkgName + " " + pkgVersion );
                }
            }

            // TODO should these be added with full path here, or dealt with later???
            // see CsEditWorkspace.cs around line 490 (ProjectInfo_p constructor params).

            if ( libFileNames.Contains( "System.Runtime.dll" ) == false ) {
                libFileNames.Add( "System.Runtime.dll" );
            }

            if ( libFileNames.Contains( "System.Console.dll" ) == false ) {
                libFileNames.Add( "System.Console.dll" );
            }

            if ( needToAddNetstandardDll ) {
                string dllPath = GetDllPath( targetFramework, "netstandard.dll" );

                if ( dllPath == null || File.Exists( dllPath ) == false ) {
                    throw new Exception( "ERROR: netstandard.dll not found for: " + targetFramework );
                }

                libFileNames.Add( dllPath );
            }

            if ( needToAddMscorlibDll ) {
                string dllPath = GetDllPath( targetFramework, "mscorlib.dll" );

                if ( dllPath == null || File.Exists( dllPath ) == false ) {
                    throw new Exception( "ERROR: mscorlib.dll not found for: " + targetFramework );
                }

                libFileNames.Add( dllPath );
            }

            ProjectDescriptor pd = new ProjectDescriptor( pName, targetFramework, srcFileNames.ToArray(), libFileNames.ToArray(), prList2.ToArray() );
            pList.Add( pd );

            return true;
        }

        private string GetDllPath( string targetFramework, string dllName ) {

            string username = Environment.UserName;

            string dllPath = null;
            string version = null;

            // this only works for Linux and dotnetSDK at the moment (with some fixed versionnumbers).

            // TODO how to detect the variable parts (versionnumbers) in these paths?
            // TODO need to detect if mono runtime used (it has different paths).

            if ( targetFramework == "netcoreapp3.1" ) {
                version = "3.1.0";
                dllPath = "/home/" + username + "/.nuget/packages/microsoft.netcore.app.ref/" + version + "/ref/netcoreapp3.1/" + dllName;
            }

            if ( dllPath == null || File.Exists( dllPath ) == false ) {
                Console.WriteLine();
                Console.WriteLine( "ERROR: NewVsProjectFileReader.GetDllPath() failed." );
                Console.WriteLine( "  =>  could not find dll '" + dllName + "' for TargetFramework '" + targetFramework + "'." );
                Console.WriteLine( "  =>  see NewVsProjectFileReader.cs around line 440 and check the paths in your local system." );
                Console.WriteLine();
                throw new Exception( "dllPath not found" );
            }

            return dllPath;
        }
    }
}
