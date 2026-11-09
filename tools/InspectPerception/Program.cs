using Mono.Cecil;
using Mono.Cecil.Cil;
var asm=AssemblyDefinition.ReadAssembly(@"G:\Steam\steamapps\common\ZED ZONE\BepInEx\interop\Assembly-CSharp.dll");
foreach(var type in asm.MainModule.Types.SelectMany(t=>new[]{t}.Concat(t.NestedTypes)).Where(t=>args.Length==0 || args.Contains(t.Name)))
{
    var init=type.Methods.FirstOrDefault(m=>m.Name==".cctor");
    if(init==null)continue;
    int token=0;
    foreach(var op in init.Body.Instructions)
    {
        if(op.OpCode==OpCodes.Ldc_I4)token=(int)op.Operand;
        if(op.OpCode==OpCodes.Stsfld && op.Operand is FieldReference field && field.Name.StartsWith("NativeMethodInfoPtr_"))
            Console.WriteLine($"{type.Name}|{field.Name}|0x{token:X8}");
    }
}
