using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace PocketQuests.SchemaTests;

/// <summary>Compares SQL expression trees, preserving operators and literals while accepting SQL Server's IN/BETWEEN expansion.</summary>
internal static class SqlExpression
{
    internal static string Normalize(string expression, bool predicate = true)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(predicate ? "SELECT 1 WHERE " + expression : "SELECT " + expression);
        var script = (TSqlScript)parser.Parse(reader, out var errors);
        Assert.Empty(errors);
        var statement = (SelectStatement)script.Batches[0].Statements[0];
        var query = (QuerySpecification)statement.QueryExpression;
        return Node(predicate ? query.WhereClause.SearchCondition : ((SelectScalarExpression)query.SelectElements[0]).Expression);
    }

    private static string Node(object? value)
    {
        if (value is PrimaryExpression { Collation: not null } scalar)
            return "Collate(" + Core(value) + "," + Node(scalar.Collation) + ")";
        return Core(value);
    }

    private static string Core(object? value)
    {
        return value switch
        {
            null => "null",
            BooleanParenthesisExpression paren => Node(paren.Expression),
            ParenthesisExpression paren => Node(paren.Expression),
            Identifier id => "Identifier:" + id.Value.ToUpperInvariant(),
            IdentifierLiteral id => "IdentifierLiteral:" + id.Value.ToUpperInvariant(),
            BooleanNotExpression not => "Not(" + Node(not.Expression) + ")",
            LikePredicate like => (like.NotDefined ? "Not(" : string.Empty) + "Like(" + Node(like.FirstExpression) + "," + Node(like.SecondExpression) + "," + Node(like.EscapeExpression) + ")" + (like.NotDefined ? ")" : string.Empty),
            BooleanComparisonExpression comparison => Compare(comparison.ComparisonType, Node(comparison.FirstExpression), Node(comparison.SecondExpression)),
            BooleanBinaryExpression binary => Combine(binary.BinaryExpressionType, Flatten(binary, binary.BinaryExpressionType)),
            InPredicate { Subquery: null } inside => inside.NotDefined
                ? Combine(BooleanBinaryExpressionType.And, inside.Values.Select(v => Compare(BooleanComparisonType.NotEqualToBrackets, Node(inside.Expression), Node(v))))
                : Combine(BooleanBinaryExpressionType.Or, inside.Values.Select(v => Compare(BooleanComparisonType.Equals, Node(inside.Expression), Node(v)))),
            BooleanTernaryExpression between => Between(between),
            TSqlFragment fragment => fragment.GetType().Name + "{" + string.Join(',', fragment.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => (p.CanWrite || typeof(IEnumerable).IsAssignableFrom(p.PropertyType)) && p.GetIndexParameters().Length == 0 && p.Name is not ("ScriptTokenStream" or "FirstTokenIndex" or "LastTokenIndex" or "Collation"))
                .OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + ":" + Node(p.GetValue(fragment)))) + "}",
            string text => JsonSerializer.Serialize(text),
            IEnumerable sequence => "[" + string.Join(',', sequence.Cast<object>().Select(Node)) + "]",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };
    }

    private static string Between(BooleanTernaryExpression expression)
    {
        var first = Node(expression.FirstExpression);
        var second = Node(expression.SecondExpression);
        var third = Node(expression.ThirdExpression);
        return expression.TernaryExpressionType == BooleanTernaryExpressionType.Between
            ? Combine(BooleanBinaryExpressionType.And, [Compare(BooleanComparisonType.GreaterThanOrEqualTo, first, second), Compare(BooleanComparisonType.LessThanOrEqualTo, first, third)])
            : Combine(BooleanBinaryExpressionType.Or, [Compare(BooleanComparisonType.LessThan, first, second), Compare(BooleanComparisonType.GreaterThan, first, third)]);
    }

    private static IEnumerable<string> Flatten(BooleanExpression expression, BooleanBinaryExpressionType kind)
    {
        if (expression is BooleanParenthesisExpression paren)
            return Flatten(paren.Expression, kind);
        if (expression is BooleanBinaryExpression binary && binary.BinaryExpressionType == kind)
            return Flatten(binary.FirstExpression, kind).Concat(Flatten(binary.SecondExpression, kind));
        if (expression is InPredicate { Subquery: null } inside && kind == (inside.NotDefined ? BooleanBinaryExpressionType.And : BooleanBinaryExpressionType.Or))
            return inside.Values.Select(v => Compare(inside.NotDefined ? BooleanComparisonType.NotEqualToBrackets : BooleanComparisonType.Equals, Node(inside.Expression), Node(v)));
        if (expression is BooleanTernaryExpression { TernaryExpressionType: BooleanTernaryExpressionType.Between } between && kind == BooleanBinaryExpressionType.And)
            return [Compare(BooleanComparisonType.GreaterThanOrEqualTo, Node(between.FirstExpression), Node(between.SecondExpression)), Compare(BooleanComparisonType.LessThanOrEqualTo, Node(between.FirstExpression), Node(between.ThirdExpression))];
        return [Node(expression)];
    }

    private static string Compare(BooleanComparisonType kind, string left, string right)
    {
        if (kind == BooleanComparisonType.NotEqualToExclamation)
            kind = BooleanComparisonType.NotEqualToBrackets;
        if (kind is BooleanComparisonType.Equals or BooleanComparisonType.NotEqualToBrackets && string.CompareOrdinal(left, right) > 0)
            (left, right) = (right, left);
        return kind + "(" + left + "," + right + ")";
    }

    private static string Combine(BooleanBinaryExpressionType kind, IEnumerable<string> children)
    {
        var values = children.Order(StringComparer.Ordinal).ToArray();
        return values.Length == 1 ? values[0] : kind + "(" + string.Join(',', values) + ")";
    }
}
