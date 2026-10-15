using System.Reflection;
using System.Reflection.Emit;

namespace PocketQuests.SchemaTests.Architecture;

/// <summary>Reads compiled member references without executing application code.</summary>
internal static class MethodBodyDependencies
{
    private static readonly IReadOnlyDictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)(field.GetValue(null) ?? throw new InvalidOperationException("Missing opcode.")))
        .ToDictionary(code => code.Value);

    internal static IEnumerable<MemberInfo> Read(MethodBase method)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        if (bytes is null)
        {
            yield break;
        }

        var offset = 0;
        while (offset < bytes.Length)
        {
            var value = bytes[offset++];
            var code = Codes[value == 0xfe ? (short)(0xfe00 | bytes[offset++]) : value];
            var size = OperandSize(code.OperandType, bytes, offset);
            if (offset + size > bytes.Length)
            {
                throw new InvalidOperationException($"Invalid IL in {method}.");
            }

            if (code.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType)
            {
                yield return method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset), method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null)
                    ?? throw new InvalidOperationException($"Unresolved dependency in {method}.");
            }

            offset += size;
        }
    }

    private static int OperandSize(OperandType operand, byte[] bytes, int offset)
    {
        if (operand is OperandType.InlineNone)
        {
            return 0;
        }

        if (operand is OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar)
        {
            return 1;
        }

        if (operand is OperandType.InlineVar)
        {
            return 2;
        }

        if (operand is OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR)
        {
            return 4;
        }

        if (operand is OperandType.InlineI8 or OperandType.InlineR)
        {
            return 8;
        }

        if (operand is OperandType.InlineSwitch)
        {
            return checked(4 + (4 * BitConverter.ToInt32(bytes, offset)));
        }

        // Indirect call signatures and the retired InlinePhi are unsupported; never omit their dependencies.
        throw new InvalidOperationException($"Unsupported IL operand {operand}.");
    }
}
