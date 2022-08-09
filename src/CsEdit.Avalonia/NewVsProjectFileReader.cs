using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;

namespace CsEdit.Avalonia {

    // this class is for reading newer (vs2017-style) ".csproj" files.

    // https://docs.microsoft.com/en-us/aspnet/web-forms/overview/deployment/web-deployment-in-the-enterprise/understanding-the-project-file 
    // https://natemcmaster.com/blog/2017/03/09/vs2015-to-vs2017-upgrade/ 

    public class NewVsProjectFileReader : IProjectReader {

        private LanguageVersion defaultLanguageVersion;
        private bool defaultNullableReferenceTypes;

        private Dictionary<string,string> props = new Dictionary<string,string>();

        public bool TryRead( string dirPathRel, List<ProjectDescriptor> pList, out RuntimeConfig cfg ) {

            cfg = null;

            string langVersionAsString = null;
            string nullableAsString = null;

            FindAndReadDirectoryBuildPropsFile( dirPathRel );

            // check build-props for any RuntimeConfig information.
            // => NOTICE the key-names have been converted to "macro-names".
            foreach ( KeyValuePair<string,string> pItem in props ) {
                if ( pItem.Key == "$(LangVersion)" ) langVersionAsString = pItem.Value;
                if ( pItem.Key == "$(Nullable)" ) nullableAsString = pItem.Value;
            }

            defaultLanguageVersion = LanguageVersion.CSharp7_3;
            defaultNullableReferenceTypes = false;

            ParseLangVersion( langVersionAsString, ref defaultLanguageVersion );
            ParseNullable( nullableAsString, ref defaultNullableReferenceTypes );

            // the properties "LangVersion" and "Nullable" could be set in ".csproj" files as well.
            // => for roslyn "LangVersion" cannot be set project-wise???
            // => for roslyn "Nullable" is defined project-wise.

            bool result = TryRead_p( dirPathRel, pList, 0 );

            if ( result == true && pList.Count > 0 ) {

                // get targetframework from the primary project data.
                // => the primary project is always the last one in the list.

                int lastIndex = pList.Count - 1;
                ProjectDescriptor pp = pList[lastIndex];

                string targetFramework = pp.TargetFramework;

                // the choice of runtime (mono/dotnet) is not directly available.
                // assume that the older TargetFrameworks mean mono and the newer ones dotnet.
                // https://docs.microsoft.com/en-us/dotnet/standard/frameworks 

                string[] monoTargetFrameworks = new string[] {
                    "net11",
                    "net20",
                    "net35",
                    "net40", "net403",
                    "net45", "net451", "net452",
                    "net46", "net461", "net462",
                    "net47", "net471", "net472",
                    "net48",
                    "netcoreapp1.0", "netcoreapp1.1",
                    "netcoreapp2.0", "netcoreapp2.1", "netcoreapp2.2",
                    //"netcoreapp3.0", "netcoreapp3.1" // NOTICE these are still available in dotnetSDK as well.
                };

                string runtime = "dotnet";

                foreach ( string tf in monoTargetFrameworks ) {
                    if ( targetFramework != tf ) continue;
                    runtime = "mono";
                    break;
                }

                // if the primary project is a library, it can target to one of the "netstandard" targets.
                // => in this case, the runtime can be either mono or dotnet...

                if ( targetFramework.StartsWith( "netstandard" ) ) {
                    //runtime = "mono";
                }

                cfg = new RuntimeConfig();
                cfg.Runtime = runtime;
                cfg.TargetFramework = targetFramework;

// languageversion is a bit odd setting, since apparently it cannot be set project-wise.
// => therefore pick up the maximum value from projects, and use that...
                cfg.LanguageVersion = pp.LanguageVersion;
                for ( int i = 0; i < pList.Count; i++ ) {
                    LanguageVersion lv = pList[i].LanguageVersion;
                    if ( lv > cfg.LanguageVersion ) cfg.LanguageVersion = lv;
                }

                AddLibraries( runtime, targetFramework, pList );
            }

            return result;
        }



private void ParseLangVersion( string txt, ref LanguageVersion val ) {
    if ( txt != null ) {
        txt = txt.ToLower();
        // TODO add the pre-7 versions...
        if ( txt == "8.0" ) val = LanguageVersion.CSharp8;
        if ( txt == "9.0" ) val = LanguageVersion.CSharp9;
        if ( txt == "10.0" ) val = LanguageVersion.CSharp10;
        if ( txt == "default" ) val = LanguageVersion.Default;
        if ( txt == "latest" ) val = LanguageVersion.Latest;
        if ( txt == "latestmajor" ) val = LanguageVersion.LatestMajor;
        if ( txt == "preview" ) val = LanguageVersion.Preview;
    }
}

private void ParseNullable( string txt, ref bool val ) {
    if ( txt != null ) {
        txt = txt.ToLower();
        if ( txt == "enable" ) val = true;
        if ( txt == "disable" ) val = false;
    }
}



        private void FindAndReadDirectoryBuildPropsFile( string dirPathRel ) {

            // scan the parent directories, and look for a "Directory.Build.props" -file.
            // https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2022

            string propsFile = null;

            string path = ProjectsProvider.WorkingDirectory;
            path += Path.DirectorySeparatorChar + dirPathRel;

            while ( true ) {
                path = Path.GetFullPath( path );

                string[] items = Directory.GetFiles( path, "Directory.Build.props" );
                if ( items.Length > 0 ) {
                    propsFile = items[0];
                    break;
                }

                if ( path == "/" ) break;
                path += Path.DirectorySeparatorChar + "..";
            }

            if ( propsFile == null ) {
                Console.WriteLine( "Directory.Build.props -file not found." );
                return;
            }

            Console.WriteLine( "FOUND: " + propsFile );

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            using ( var fileStream = File.OpenText( propsFile ) )
            using ( XmlReader reader = XmlReader.Create( fileStream, settings ) ) {
                while (reader.Read()) {

                    if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "PropertyGroup" ) ) {
                        Console.WriteLine( "START reading element: PropertyGroup" );
                        while (reader.Read()) {

                            if ( ( reader.NodeType == XmlNodeType.Element ) ) {

                                if ( reader.HasAttributes == false ) {
                                    string key = "$(" + reader.Name + ")";
                                    string value = reader.ReadString();

                                    Console.WriteLine( "FOUND PROP: " + key + " => " + value );

                                    props.Add( key, value );
                                }
                            }

// WARNING might skip some elements quite easily??? see this:
// https://stackoverflow.com/questions/24991218/c-sharp-xmlreader-skips-nodes-after-using-readelementcontentas 

                            if ( ( reader.NodeType == XmlNodeType.EndElement ) && ( reader.Name == "PropertyGroup" ) ) {
                                Console.WriteLine( "END reading element: PropertyGroup" );
                                break;
                            }
                        }
                    }

                    /* TODO not yet in use...
                    if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "ItemGroup" ) ) {
                        Console.WriteLine( "START reading element: ItemGroup" );
                        while (reader.Read()) {
                            if ( ( reader.NodeType == XmlNodeType.EndElement ) && ( reader.Name == "ItemGroup" ) ) {
                                Console.WriteLine( "END reading element: ItemGroup" );
                                break;
                            }
                        }
                    } */
                }
            }
        }

        private bool TryRead_p( string dirPathRel, List<ProjectDescriptor> pList, int depth ) {

            LanguageVersion languageVersion = defaultLanguageVersion;
            bool nullableReferenceTypes = defaultNullableReferenceTypes;

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

            string langVersion = null;
            string nullable = null;

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
                                targetFramework = reader.ReadString();
                                Console.WriteLine( "found TargetFramework: " + targetFramework );
                            }

                            if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "LangVersion" ) ) {
                                langVersion = reader.ReadString();
                                Console.WriteLine( "found LangVersion: " + langVersion );
                            }

                            if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "Nullable" ) ) {
                                nullable = reader.ReadString();
                                Console.WriteLine( "found Nullable: " + nullable );
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

                                    // check if pkgVersion is one of the macro values, and convert it...
                                    if ( pkgVersion.StartsWith( "$(" ) ) {
                                        if ( props.TryGetValue( pkgVersion, out string value ) ) pkgVersion = value;
                                    }

                                    Console.WriteLine( "found a PACKAGE REFERENCE: " + pkgInclude + " " + pkgVersion );
                                    pkgRefs.Add( pkgInclude, pkgVersion );
                                }
                            }



//xxxx compile Include ja Remove:
//     <Compile Remove="SampleFiles\**" />
//     <Compile Include="..\RoslynPad.Editor.Shared\**\*.cs">
//jatka

                            if ( ( reader.NodeType == XmlNodeType.Element ) && ( reader.Name == "Compile" ) ) {
                                if ( reader.HasAttributes ) {
                                    string compInclude = reader.GetAttribute("Include");
                                    string compRemove = reader.GetAttribute("Remove");

                                    if ( string.IsNullOrWhiteSpace( compInclude ) == false ) {
                                        Console.WriteLine( "found COMPILE Include : " + compInclude );
                                    }

                                    if ( string.IsNullOrWhiteSpace( compRemove ) == false ) {
                                        Console.WriteLine( "found COMPILE Remove : " + compRemove );
                                    }
                                }
                            }






                            if ( ( reader.NodeType == XmlNodeType.EndElement ) && ( reader.Name == "ItemGroup" ) ) {
                                Console.WriteLine( "END reading element: ItemGroup" );
                                break;
                            }
                        }
                    }

/* TODO a plain library reference like this still missing:

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

            ParseLangVersion( langVersion, ref languageVersion );
            ParseNullable( nullable, ref nullableReferenceTypes );

            // need to include all local .cs files?
            // by default, unless told otherwise?

            string srcFileSarchPath = ProjectsProvider.WorkingDirectory;
            srcFileSarchPath += Path.DirectorySeparatorChar + dirPathRel;

            string[] items = Directory.GetFiles( srcFileSarchPath, "*.cs", SearchOption.AllDirectories );

            foreach ( string resultPath in items ) {

                if ( File.Exists( resultPath ) == false ) continue; // ignore directories etc.

                string filePath = Path.GetRelativePath( srcFileSarchPath, resultPath );

                // ignore results from "bin" and "obj" -folders.
                if ( filePath.StartsWith( "bin" + Path.DirectorySeparatorChar ) ) continue;
                if ( filePath.StartsWith( "obj" + Path.DirectorySeparatorChar ) ) continue;



// TODO need to ignore other folders as well... from csproj properties...
// TODO need to ignore other folders as well... from csproj properties...
// TODO need to ignore other folders as well... from csproj properties...
                if ( filePath.StartsWith( "SampleFiles" + Path.DirectorySeparatorChar ) ) continue;



                Console.WriteLine( "addsrc GOT FILEPATH: " + filePath );
                Console.WriteLine( "addsrc RELATIVE PATH: " + dirPathRel );

                srcFileNames.Add( dirPathRel + Path.DirectorySeparatorChar + filePath );
            }

            // do not create a project, if it is an empty one.
            // => TODO is there any need to include empty projects???

            if ( srcFileNames.Count < 1 ) return false;

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

            ProjectDescriptor pd = new ProjectDescriptor( pName, targetFramework, srcFileNames.ToArray(), prList2.ToArray(), pkgRefs, languageVersion, nullableReferenceTypes );
            pList.Add( pd );

            return true;
        }



        private void AddLibraries( string runtime, string targetFramework, List<ProjectDescriptor> pList ) {

// https://docs.microsoft.com/en-us/nuget/concepts/dependency-resolution 
// https://docs.microsoft.com/en-us/nuget/concepts/dependency-resolution 
// https://docs.microsoft.com/en-us/nuget/concepts/dependency-resolution 

            // now we need to convert all package-references to library(dll)-references.
            // => linux : the files are in /home/<username>/.nuget/packages/<packagename>/<packageversion>/lib/<??>/Newtonsoft.Json.dll

            string username = Environment.UserName;

            foreach ( ProjectDescriptor pd in pList ) {



                // the project directory is not stored separately,
                // but it can be obtained from the project unique name.

                string dirPathRel = Path.GetDirectoryName( pd.ProjectNameUniq );

                Dictionary<string,string> pkgRefs = pd.PackageReferences;



                // now find the complete set of nugets needed in this project.
                // => nugets may reference other nugets, so traverse the full dependency tree and collect all packages.
                // => the file "obj/project.assets.json" is contains this information.

                string projectAssetsPath = ProjectsProvider.WorkingDirectory;
                projectAssetsPath += Path.DirectorySeparatorChar + dirPathRel;
                projectAssetsPath += Path.DirectorySeparatorChar + "obj";
                projectAssetsPath += Path.DirectorySeparatorChar + "project.assets.json";

                projectAssetsPath = Path.GetFullPath( projectAssetsPath );
                if ( File.Exists( projectAssetsPath ) == false ) {
                    Console.WriteLine();
                    Console.WriteLine( "ERROR: file not found: " + projectAssetsPath );
                    Console.WriteLine( "The file is required to resolve Nuget-package dependencies." );
                    Console.WriteLine( "Try restoring the Nuget packages." );
                    Console.WriteLine();
                }

                ResolveNugetDependencies( projectAssetsPath, pkgRefs );



// TODO rename to libFiles???
// TODO rename to libFiles???
// TODO rename to libFiles???
                List<string> libFileNames = new List<string>();

                // the netstandard.dll is needed if:
                // => the project target is a netstandard one?
                // => any of the package-reference dlls is a netstandard one?

                bool needToAddNetstandardDll = false;

                // the mscorlib.dll is needed if:
                // => the project target is an old .net-framework one?
                // => any of the package-reference dlls is an old .net-framework one?

                bool needToAddMscorlibDll = false;



                foreach( KeyValuePair<string,string> pkg in pd.PackageReferences ) {

                    string pkgName = pkg.Key;
                    string pkgName_lower = pkgName.ToLower();

                    string pkgVersion = pkg.Value;



                    // often project data suggests that many of the "System.X.Y" -type packages are needed,
                    // but (at least with net6.0) those same libraries are found from the system directory.
                    // => then why should one try to read some ancient netstandard etc versions for those?
                    // => so replace the dependency with a system library, if there seems to be one.

                    string _dllPath = GetDllPath( runtime, targetFramework, pkgName + ".dll", true );

                    if ( _dllPath != null && File.Exists( _dllPath ) ) {
// TODO no checking here is made for pkgVersion...
// TODO no checking here is made for pkgVersion... need to add the versionnumber param as well.
// TODO no checking here is made for pkgVersion...
                        Console.WriteLine( "PKG-search : REPLACING library for " + pkgName + " from the system folder." );
                        libFileNames.Add( _dllPath );

                        continue;
                    }



                    // TODO detect platform: linux/mac/windows and adjust search path accordingly.

                    string pkgSearchPath = "/home/" + username + "/.nuget/packages/" + pkgName_lower;

                    // when we enter here, "pkgVersion" can be either a fixed version number (like "X.Y.Z") or a version range.
                    // fixed version numbers are like "X.Y.Z" but ranges contain characters '[','(','*',etc...

                    pkgVersion = ResolveVersionRangeToFixedVersion( pkgSearchPath, pkgVersion );

                    string libFileSearchPath = pkgSearchPath + "/" + pkgVersion + "/lib";



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

// https://michaelscodingspot.com/assemblies-load-in-dotnet/ 

                        if ( dirName.StartsWith( "netstandard" ) ) {
                            // these seem to require "netstandard" assemblies...
                            needToAddNetstandardDll = true;
                            isOk = true;
                        }

                        //if ( dirName.StartsWith( "net" ) && dirName.Length == 5 ) {
                        if ( dirName.StartsWith( "net4" ) || dirName.StartsWith( "net3" ) || dirName.StartsWith( "net2" ) ) {
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

/*	THIS IS COVERED ALREADY, USING A PRE-CHECK...
// TODO here are some "System.*" libraries, which one would expect to be in the system folder, not in nuget folders???
// TODO here are some "System.*" libraries, which one would expect to be in the system folder, not in nuget folders???
// TODO here are some "System.*" libraries, which one would expect to be in the system folder, not in nuget folders???

                        string dllPath = GetDllPath( runtime, targetFramework, pkgName + ".dll", true );

                        if ( dllPath == null || File.Exists( dllPath ) == false ) {
                            Console.WriteLine( "PKG-search : ERROR no library found for " + pkgName + " " + pkgVersion );
                        } else {
// TODO no checking here is made for pkgVersion...
// TODO no checking here is made for pkgVersion...
// TODO no checking here is made for pkgVersion...
                            Console.WriteLine( "PKG-search : recovered library for " + pkgName + " from the system folder." );
                            libFileNames.Add( dllPath );
                        } */

                    }
                }

                // add other dependencies, which are not explicitly listed in project files.

                List<string> extraLibs = new List<string>();

                if ( runtime == "mono" || needToAddMscorlibDll ) {

// try adding the mono/4.5/mscorlib.dll always...
//libFileNames.Add( "/usr/lib/mono/4.5/mscorlib.dll" ); // no effect on anything???

                    extraLibs.Add( "mscorlib.dll" );
                }

                if ( runtime == "dotnet" ) {

                    extraLibs.Add( "System.Private.CoreLib.dll" );

                    extraLibs.Add( "System.Runtime.dll" );
                    extraLibs.Add( "System.Console.dll" );
                }

                if ( needToAddNetstandardDll ) {
                    extraLibs.Add( "netstandard.dll" );
                }

                foreach ( string libName in extraLibs ) {
                    string dllPath = GetDllPath( runtime, targetFramework, libName );

                    if ( dllPath == null || File.Exists( dllPath ) == false ) {
                        throw new Exception( "ERROR: " + libName + " not found for: " + runtime + " / " + targetFramework );
                    }

                    libFileNames.Add( dllPath );
                }

                pd.SetLibraries( libFileNames.ToArray() );
            }
        }

        private string GetDllPath( string runtime, string targetFramework, string dllName, bool silent = false ) {

//            string username = Environment.UserName;

            string dllPath = null;
//            string version = null;

            // this probably works only for Linux at the moment (with some fixed versionnumbers etc).

            // TODO how to detect the variable parts (versionnumbers) in these paths?
            // TODO need to detect if mono runtime used (it has different paths).

// https://docs.microsoft.com/en-us/dotnet/standard/frameworks 
// https://docs.microsoft.com/en-us/dotnet/standard/frameworks 
// https://docs.microsoft.com/en-us/dotnet/standard/frameworks 

            if ( runtime == "mono" ) {

/* TODO there are multiple versions of mscorlib.dll in mono:

$ find /usr | grep mscorlib.dll | xargs ls -l 
-rw-r--r-- 1 root root   627200 Jun 15 01:06 /usr/lib/mono/2.0-api/mscorlib.dll
-rw-r--r-- 1 root root   752128 Jun 15 01:06 /usr/lib/mono/4.0-api/mscorlib.dll
lrwxrwxrwx 1 root root       23 Jun 15 01:06 /usr/lib/mono/4.0/mscorlib.dll -> ../4.0-api/mscorlib.dll
-rw-r--r-- 1 root root   831488 Jun 15 01:06 /usr/lib/mono/4.5.1-api/mscorlib.dll
-rw-r--r-- 1 root root   831488 Jun 15 01:06 /usr/lib/mono/4.5.2-api/mscorlib.dll
-rw-r--r-- 1 root root   829440 Jun 15 01:06 /usr/lib/mono/4.5-api/mscorlib.dll
-rw-r--r-- 1 root root  4632064 Jun 15 01:06 /usr/lib/mono/4.5/mscorlib.dll
-rw-r--r-- 1 root root   844800 Jun 15 01:06 /usr/lib/mono/4.6.1-api/mscorlib.dll
-rw-r--r-- 1 root root   845824 Jun 15 01:06 /usr/lib/mono/4.6.2-api/mscorlib.dll
-rw-r--r-- 1 root root   844800 Jun 15 01:06 /usr/lib/mono/4.6-api/mscorlib.dll
-rw-r--r-- 1 root root   901120 Jun 15 01:06 /usr/lib/mono/4.7.1-api/mscorlib.dll
-rw-r--r-- 1 root root   899072 Jun 15 01:06 /usr/lib/mono/4.7.2-api/mscorlib.dll
-rw-r--r-- 1 root root   897024 Jun 15 01:06 /usr/lib/mono/4.7-api/mscorlib.dll
-rw-r--r-- 1 root root   899072 Jun 15 01:06 /usr/lib/mono/4.8-api/mscorlib.dll

=> what is the point having these separate "-api" libs?
=> why mono/4.5/mscorlib.dll is so much bigger than the others?
=> need to add multiple mscorlib.dll-files? apparently NO since everything works fine with just "-api" libs?

*/

                //dllPath = "/usr/lib/mono/4.5/" + dllName;
                dllPath = "/usr/lib/mono/4.6.1-api/" + dllName;

                if ( dllName == "netstandard.dll" ) dllPath = "/usr/lib/mono/4.5/Facades/" + dllName;



            } else if ( runtime == "dotnet" ) {

                string libPath = null;

                // if the primary project is a library, it can target to one of the "netstandard" targets.
                // => in this case, we must select one of the "actual runtimes" here (to get System.Private.CoreLib.dll etc).
                if ( targetFramework == "netstandard2.0" ) targetFramework = "netcoreapp3.1";

                if ( targetFramework == "netcoreapp3.1" ) {
                    libPath = "/usr/share/dotnet/shared/Microsoft.NETCore.App/3.1.27";
                }

                if ( targetFramework == "net6.0" ) {
                    libPath = "/usr/share/dotnet/shared/Microsoft.NETCore.App/6.0.7";
                }

                if ( libPath != null ) {
                    dllPath = libPath + Path.DirectorySeparatorChar + dllName;
                }

            } else {
                throw new Exception( "unknown runtime: " + runtime );
            }

            if ( silent == false && ( dllPath == null || File.Exists( dllPath ) == false ) ) {
                Console.WriteLine();
                Console.WriteLine( "ERROR: NewVsProjectFileReader.GetDllPath() failed." );
                Console.WriteLine( "  =>  could not find dll " + dllName + " for " + runtime + " / " + targetFramework + "." );
                Console.WriteLine( "  =>  see NewVsProjectFileReader.cs around line 700 and check the paths in your local system." );
                Console.WriteLine();
                throw new Exception( "dllPath not found" );
            }

            return dllPath;
        }





        public class NugetDependency_p {

            public string PkgName { get; private set; }
            public string PkgVersion { get; private set; }

            public Dictionary<string,string> Deps { get; private set; }

            public NugetDependency_p( string name, string version ) {
                PkgName = name;
                PkgVersion = version;
                Deps = new Dictionary<string,string>();
            }
        }

// https://docs.microsoft.com/en-us/nuget/concepts/dependency-resolution 
// https://docs.microsoft.com/en-us/nuget/concepts/dependency-resolution 
// https://docs.microsoft.com/en-us/nuget/concepts/dependency-resolution 
// https://stackoverflow.com/questions/3142495/deserialize-json-into-c-sharp-dynamic-object 
// https://stackoverflow.com/questions/3142495/deserialize-json-into-c-sharp-dynamic-object 
// https://stackoverflow.com/questions/3142495/deserialize-json-into-c-sharp-dynamic-object 

        private void ResolveNugetDependencies( string projectAssetsPath, Dictionary<string,string> pkgRefs ) {

            Console.WriteLine( "RND starting for path " + projectAssetsPath );

            // first read in the data from "project.assets.json" file.

            List<NugetDependency_p> deps = new List<NugetDependency_p>();
            try {
                string json = File.ReadAllText( projectAssetsPath );

                DictionaryConverter conv = new DictionaryConverter();
                Dictionary<string,object> data = JsonConvert.DeserializeObject<Dictionary<string,object>>( json, conv );

                // get the item "targets", and further it's first (and supposedly only) subitem.

		Dictionary<string,object> targets = (Dictionary<string,object>) data["targets"];
                if ( targets.Count != 1 ) throw new Exception( "ERROR: targets has unexpected itemcount: " + targets.Count );

                KeyValuePair<string,object> targetsItem = targets.First();
                Console.WriteLine( "RND targets item is: " + targetsItem.Key );

		Dictionary<string,object> pkgDict = (Dictionary<string,object>) targetsItem.Value;
                foreach ( KeyValuePair<string,object> p in pkgDict ) {

                    string[] keyParts = p.Key.Split( '/' );
                    if ( keyParts.Length != 2 ) throw new Exception( "ERROR: pkg-name parse failed for: " + p.Key );
                    string pkgName = keyParts[0];
                    string pkgVersion = keyParts[1];

                    Console.WriteLine( "RND pkg name and version: " + pkgName + " " + pkgVersion );

		    Dictionary<string,object> valueDict = (Dictionary<string,object>) p.Value;

                    if ( valueDict.ContainsKey( "dependencies" ) == false ) {
                        // no dependencies exists for this pkg => we can skip this record.
                        continue;
                    }

                    NugetDependency_p nd = new NugetDependency_p( pkgName, pkgVersion );

		    Dictionary<string,object> depDict = (Dictionary<string,object>) valueDict["dependencies"];
                    foreach ( KeyValuePair<string,object> item in depDict ) {
                        string depName = item.Key;
                        string depVersion = item.Value.ToString();

                        // 20220708 notice that "depVersion" here may be a fixed version number, or a version range.
                        // fixed version numbers are like "X.Y.Z" but ranges contain characters '[','(','*',etc...
                        // => at this stage, just read in the version code as it is.
                        // => only later, when we try to find the actual .dll files, we must resove the ranges to a fixed version number.
                        // => but the version information may come from multiple sóurces ("Directory.Build.props" file being one example).

                        // https://docs.microsoft.com/en-us/nuget/concepts/package-versioning 

                        Console.WriteLine( "RND     DEPENDENCY :: " + depName + " " + depVersion );

                        if ( nd.Deps.ContainsKey( depName ) == false ) {
                            nd.Deps.Add( depName, depVersion );
                        }
                    }

                    deps.Add( nd );
                }
            } catch ( Exception e ) {
                Console.WriteLine();
                Console.WriteLine( "EXCEPTION when reading file: " + projectAssetsPath );
                Console.WriteLine( e.ToString() );
                Console.WriteLine();
                throw new Exception( "EXCEPTION when reading file: " + projectAssetsPath );
            }

            //throw new Exception( "KESKEN_asset_homma" );

            // then loop over packages, and add dependencies for each of them.
            // do this in multiple stages, until no more new dependencies are found.

            int cycle = 0;
            int newItems = 0;

            do {
                Console.WriteLine( "RND cycle=" + cycle++ + " pkgRefs=" + pkgRefs.Count );

                Dictionary<string,string> newRefs = new Dictionary<string,string>();
                foreach ( KeyValuePair<string,string> pkg in pkgRefs ) {
                    foreach ( NugetDependency_p dep in deps ) {
                        if ( pkg.Key != dep.PkgName ) continue;
                        if ( pkg.Value != dep.PkgVersion ) continue;
                        // now add the dependencies into newRefs (no duplicates though).
                        foreach ( KeyValuePair<string,string> d in dep.Deps ) {
                            string name = d.Key;
                            string version = d.Value;
                            if ( pkgRefs.ContainsKey( name ) ) continue;
                            if ( newRefs.ContainsKey( name ) ) continue;
                            newRefs.Add( name, version );
                        }
                    }
                }

                newItems = newRefs.Count;
                foreach ( KeyValuePair<string,string> pkg in newRefs ) {
                    pkgRefs.Add( pkg.Key, pkg.Value );
                }
            } while ( newItems > 0 );
        }





        private string ResolveVersionRangeToFixedVersion( string pkgSearchPath, string pkgVersion ) {

            if ( string.IsNullOrWhiteSpace( pkgVersion ) ) {
                throw new Exception( "pkgVersion is null or whitespace." );
            }

            // https://docs.microsoft.com/en-us/nuget/concepts/package-versioning 

            bool isRange = false;
            if ( pkgVersion.IndexOf( '(' ) != -1 ) isRange = true;
            if ( pkgVersion.IndexOf( '[' ) != -1 ) isRange = true;

            bool hasWildcard = false;
            if ( pkgVersion.IndexOf( '*' ) != -1 ) hasWildcard = true;

            Console.WriteLine( "ResolveVersionRange : " + pkgVersion + "  =>  isRange=" + isRange.ToString() + " hasWildcard=" + hasWildcard.ToString() );

            if ( isRange && hasWildcard ) {
                // TODO is this unnecessarily complicated?
                // TODO need to find a reasonable real-word use-case...
                throw new NotImplementedException();
            }

            string minLimit = null;
            //bool minLimitIsInclusive = false;

            //string maxLimit = null;
            //bool maxLimitIsInclusive = false;

            if ( isRange == false ) {

                if ( hasWildcard ) {
                    // TODO need to find a reasonable real-word use-case...
                    throw new NotImplementedException();
                }

                // this is not an actual "range", but according to the above document,
                // this belongs to category "minimum version, inclusive".

                if ( Directory.Exists( pkgSearchPath + Path.DirectorySeparatorChar + pkgVersion ) ) {
                    return pkgVersion;
                }

                // an exact match was not found, so setup the minLimit.

                minLimit = pkgVersion;
                //minLimitIsInclusive = true;
// HUOM!!! KORJAA...
// HUOM!!! KORJAA...
// HUOM!!! KORJAA...

            } else {

                // the first and last characters must be either brackets or parenthesis.

                char startChar = pkgVersion[0];
                if ( startChar != '[' && startChar != '(' ) {
                    throw new Exception( "startChar is not valid: " + startChar );
                }

                char endChar = pkgVersion[pkgVersion.Length - 1];
                if ( endChar != ']' && endChar != ')' ) {
                    throw new Exception( "endChar is not valid: " + endChar );
                }

                bool hasComma = false;
                if ( pkgVersion.IndexOf( ',' ) != -1 ) hasComma = true;

                if ( startChar == '[' && endChar == ']' && hasComma == false ) {
                    // this is an "exact version match" rule => just return the stuff inside brackets.
                    return pkgVersion.Substring( 1, pkgVersion.Length - 2 );
                }

                // new setup the minLimit and maxLimit according to the rule...



            }

            throw new NotImplementedException();

            //string result = "dummy";
            //return result;
        }





    }
}

