namespace PocketQuests.SchemaTests.Architecture.Fixtures;

internal static class ValidDomainRule
{
    internal static int Evaluate(int value)
    {
        using var bytes = new MemoryStream();
        bytes.WriteByte((byte)Math.Clamp(value, 0, byte.MaxValue));
        return bytes.ToArray()[0];
    }
}
