namespace PocketQuests.SchemaTests;

/// <summary>Proves expression comparison retains semantic differences and accepts catalog formatting.</summary>
public sealed class ExpressionTests
{
    /// <summary>SQL Server may expand/reorder these predicates without changing their meaning.</summary>
    /// <param name="source">Original predicate.</param>
    /// <param name="deployed">Catalog-normalized predicate.</param>
    [Theory]
    [InlineData("[Value] IN (1,2,3)", "(([Value]=(3) OR [Value]=(2)) OR [Value]=(1))")]
    [InlineData("[Value] BETWEEN 0 AND 20", "([Value]>=(0) AND [Value]<=(20))")]
    [InlineData("A=1 AND B BETWEEN 0 AND 20", "B<=(20) AND (A=1 AND B>=(0))")]
    [InlineData("DATEPART(TZOFFSET,CreatedAt)=0", "(datepart(tzoffset,[CreatedAt])=(0))")]
    [InlineData("Value NOT LIKE '%x%' COLLATE Latin1_General_100_BIN2", "NOT Value LIKE ('%x%') COLLATE Latin1_General_100_BIN2")]
    public void EquivalentPredicatesCompareEqual(string source, string deployed) =>
        Assert.Equal(SqlExpression.Normalize(source), SqlExpression.Normalize(deployed));

    /// <summary>Changed function arguments, literals, precedence, offsets or bounds must never be hidden.</summary>
    /// <param name="source">Required predicate.</param>
    /// <param name="changed">Invalid mutation.</param>
    [Theory]
    [InlineData("Value BETWEEN 0 AND 20", "Value BETWEEN 0 AND 21")]
    [InlineData("DATALENGTH(Value)<=65536", "DATALENGTH(Other)<=65536")]
    [InlineData("Value='usr_'", "Value='USR_'")]
    [InlineData("A=1 AND (B=2 OR C=3)", "(A=1 AND B=2) OR C=3")]
    [InlineData("DATEPART(TZOFFSET,CreatedAt)=0", "DATEPART(TZOFFSET,CreatedAt)=60")]
    [InlineData("Value COLLATE Latin1_General_100_BIN2='a'", "Value COLLATE Latin1_General_100_CI_AS='a'")]
    public void ChangedPredicatesRemainDifferent(string source, string changed) =>
        Assert.NotEqual(SqlExpression.Normalize(source), SqlExpression.Normalize(changed));
}
