using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace NewTso2Pmx.Core.Infrastructure;

public static class LegacyScriptCompiler
{
    public static T CreateInstance<T>(string scriptFile, string className) where T : class
    {
        var (scriptText, scriptEncoding) = LegacyEncoding.ReadAllTextWithDetection(scriptFile);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            SourceText.From(scriptText, scriptEncoding),
            path: scriptFile);
        var assemblyName = $"{Path.GetFileNameWithoutExtension(scriptFile)}.{Guid.NewGuid():N}";

        var references = BuildMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
        {
            var errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"脚本编译失败: {scriptFile}{Environment.NewLine}{errors}");
        }

        peStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(peStream);
        var type = assembly.GetType(className, throwOnError: true, ignoreCase: false)
                   ?? throw new InvalidOperationException($"未找到脚本类型: {className}");

        return Activator.CreateInstance(type) as T
               ?? throw new InvalidOperationException($"无法创建脚本实例: {className}");
    }

    private static IReadOnlyCollection<MetadataReference> BuildMetadataReferences()
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            TryAddAssemblyLocation(locations, assembly);
        }

        TryAddAssemblyLocation(locations, typeof(object).Assembly);
        TryAddAssemblyLocation(locations, typeof(Enumerable).Assembly);
        TryAddAssemblyLocation(locations, typeof(List<>).Assembly);
        TryAddAssemblyLocation(locations, typeof(Microsoft.DirectX.Vector3).Assembly);
        TryAddAssemblyLocation(locations, typeof(TDCG.IProportion).Assembly);
        TryAddAssemblyLocation(locations, typeof(Tso2Pmd.IPhysObTemplate).Assembly);

        return locations.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    private static void TryAddAssemblyLocation(ISet<string> locations, Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(assembly.Location))
        {
            return;
        }

        if (!File.Exists(assembly.Location))
        {
            return;
        }

        locations.Add(assembly.Location);
    }
}
