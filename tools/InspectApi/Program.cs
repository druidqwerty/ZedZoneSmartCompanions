using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

class Program
{
    static void Main(string[] args)
    {
        var dllPath = @"G:\Steam\steamapps\common\ZED ZONE\BepInEx\interop\Assembly-CSharp.dll";
        var asm = AssemblyDefinition.ReadAssembly(dllPath);
        var allTypes = asm.MainModule.Types.SelectMany(t => new[] { t }.Concat(t.NestedTypes)).Where(t => !t.Name.StartsWith("<") && !t.Name.StartsWith("__") && !t.Name.StartsWith("CameraFilterPack")).OrderBy(t => t.FullName).ToList();

        if (args.Length == 0 || args[0] == "list")
        {
            var filters = args.Length > 1 ? args[1].Split('|', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
            foreach (var t in allTypes)
            {
                if (filters.Length == 0 || filters.Any(f => t.FullName.Contains(f, StringComparison.OrdinalIgnoreCase)))
                {
                    var bt = t.BaseType != null ? " : " + t.BaseType.Name : "";
                    Console.WriteLine($"{t.FullName}{bt} (fields={t.Fields.Count}, props={t.Properties.Count}, methods={t.Methods.Count})");
                }
            }
        }
        else if (args[0] == "type")
        {
            var target = args[1];
            var memberFilter = args.Length > 2 ? args[2] : "";
            foreach (var t in allTypes.Where(x => x.FullName.Equals(target, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(target, StringComparison.OrdinalIgnoreCase)))
            {
                DumpType(t, memberFilter);
            }
        }
        else if (args[0] == "search-member")
        {
            var kw = args[1];
            foreach (var t in allTypes)
            {
                foreach (var f in t.Fields.Where(f => !f.Name.StartsWith("Native") && f.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    Console.WriteLine($"[F] {t.FullName}.{f.Name} : {f.FieldType.Name}");
                foreach (var p in t.Properties.Where(p => p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    Console.WriteLine($"[P] {t.FullName}.{p.Name} : {p.PropertyType.Name}");
                foreach (var m in t.Methods.Where(m => !m.IsGetter && !m.IsSetter && !m.Name.StartsWith("Native") && m.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    var ps = string.Join(", ", m.Parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"[M] {t.FullName}.{m.Name}({ps}) : {m.ReturnType.Name}");
                }
            }
        }
    }

    static void DumpType(TypeDefinition t, string filter = "")
    {
        Console.WriteLine($"=== TYPE: {t.FullName} : {t.BaseType?.FullName} ===");
        if (t.IsEnum)
        {
            foreach (var f in t.Fields.Where(x => x.IsStatic))
                Console.WriteLine($"  {f.Name} = {f.Constant}");
            return;
        }
        var parts = string.IsNullOrEmpty(filter) ? Array.Empty<string>() : filter.Split('|', StringSplitOptions.RemoveEmptyEntries);
        bool Match(string s) => parts.Length == 0 || parts.Any(p => s.Contains(p, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine("-- Fields --");
        foreach (var f in t.Fields.Where(f => !f.Name.StartsWith("NativeFieldInfoPtr_") && !f.Name.StartsWith("NativeMethodInfoPtr_") && Match(f.Name + " " + f.FieldType.Name)))
            Console.WriteLine($"  {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");
        Console.WriteLine("-- Properties --");
        foreach (var p in t.Properties.Where(p => Match(p.Name + " " + p.PropertyType.Name)))
        {
            var isStatic = (p.GetMethod?.IsStatic ?? p.SetMethod?.IsStatic ?? false) ? "static " : "";
            Console.WriteLine($"  {isStatic}{p.PropertyType.Name} {p.Name} {{ {(p.GetMethod != null ? "get; " : "")}{(p.SetMethod != null ? "set; " : "")}}}");
        }
        Console.WriteLine("-- Methods --");
        foreach (var m in t.Methods.Where(m => !m.IsGetter && !m.IsSetter && !m.Name.StartsWith("Method_Private_Static_") && !m.Name.StartsWith("_") && Match(m.Name)))
        {
            var isStatic = m.IsStatic ? "static " : "";
            var ps = string.Join(", ", m.Parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"  {isStatic}{m.ReturnType.Name} {m.Name}({ps})");
        }
        foreach (var nt in t.NestedTypes)
        {
            if (Match(nt.Name)) Console.WriteLine($"  [Nested] {nt.Name}");
        }
    }
}
