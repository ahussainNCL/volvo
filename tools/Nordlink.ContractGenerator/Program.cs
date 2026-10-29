using System.Reflection;
using System.Text;
using System.Text.Json;
using Nordlink.Contracts;

var assembly = typeof(VehicleProfile).Assembly;
var types = assembly.GetExportedTypes().Where(t => t.IsEnum || t.IsClass && t != typeof(WireJson) && t != typeof(PipeProtocol)).OrderBy(t => t.Name);
var nullability = new NullabilityInfoContext();
var output = new StringBuilder("// Generated from Nordlink.Contracts. Do not edit; run scripts/generate-contracts.sh.\n\n");
foreach (var type in types)
{
    if (type.IsEnum)
    {
        output.Append("export type ").Append(type.Name).Append(" = ").Append(string.Join(" | ", Enum.GetNames(type).Select(n => JsonSerializer.Serialize(JsonNamingPolicy.CamelCase.ConvertName(n))))).Append(";\n\n");
        continue;
    }
    output.Append("export interface ").Append(type.Name).Append(" {\n");
    foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
    {
        var ts = Map(p.PropertyType);
        if (!p.PropertyType.IsValueType && nullability.Create(p).ReadState == NullabilityState.Nullable) ts += " | null";
        output.Append("  ").Append(JsonNamingPolicy.CamelCase.ConvertName(p.Name)).Append(": ").Append(ts).Append(";\n");
    }
    output.Append("}\n\n");
}
if (args.Length != 1) throw new ArgumentException("Supply the output path.");
await File.WriteAllTextAsync(args[0], output.ToString().TrimEnd() + "\n");
static string Map(Type t)
{
    if (Nullable.GetUnderlyingType(t) is Type inner) return Map(inner) + " | null";
    if (t == typeof(string) || t == typeof(DateTimeOffset)) return "string";
    if (t == typeof(bool)) return "boolean";
    if (t.IsPrimitive || t == typeof(decimal)) return "number";
    if (t.IsArray) return Map(t.GetElementType()!) + "[]";
    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)) return "Record<string, " + Map(t.GenericTypeArguments[1]) + ">";
    return t.Name;
}
