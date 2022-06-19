using System;
﻿using System.Collections.Generic;
﻿using System.IO;
using System.Reflection;
﻿using System.Threading;

using Microsoft.CodeAnalysis; // SourceCodeKind etc
using Microsoft.CodeAnalysis.CSharp; // LanguageVersion etc
using Microsoft.CodeAnalysis.CSharp.Syntax; // CompilationUnitSyntax

using AvaloniaEdit.Document; // TextDocument

using RoslynPad.Editor; // AvalonEditTextContainer
using RoslynPad.Roslyn; // RoslynHost
using RoslynPad.Roslyn.Diagnostics; // DiagnosticsUpdatedArgs

namespace RoslynHostSample
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("starting");
            var inst = CsEditWorkspace.Instance;
        }
    }
}
