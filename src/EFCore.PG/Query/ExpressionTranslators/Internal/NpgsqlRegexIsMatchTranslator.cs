using System.Text.RegularExpressions;
using ExpressionExtensions = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Query.ExpressionTranslators.Internal;

/// <summary>
/// Translates Regex.IsMatch calls into PostgreSQL regex expressions for database-side processing.
/// </summary>
/// <remarks>
/// http://www.postgresql.org/docs/current/static/functions-matching.html
/// </remarks>
public class NpgsqlRegexIsMatchTranslator : IMethodCallTranslator
{
    private static readonly MethodInfo IsMatch =
        typeof(Regex).GetRuntimeMethod(nameof(Regex.IsMatch), new[] { typeof(string), typeof(string) })!;

    private static readonly MethodInfo IsMatchWithRegexOptions =
        typeof(Regex).GetRuntimeMethod(nameof(Regex.IsMatch), new[] { typeof(string), typeof(string), typeof(RegexOptions) })!;

    private const RegexOptions UnsupportedRegexOptions = RegexOptions.RightToLeft | RegexOptions.ECMAScript;

    private readonly NpgsqlSqlExpressionFactory _sqlExpressionFactory;

    public NpgsqlRegexIsMatchTranslator(NpgsqlSqlExpressionFactory sqlExpressionFactory)
        => _sqlExpressionFactory = sqlExpressionFactory;

    /// <inheritdoc />
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method != IsMatch && method != IsMatchWithRegexOptions)
        {
            return null;
        }

        var (input, pattern) = (arguments[0], arguments[1]);
        var typeMapping = ExpressionExtensions.InferTypeMapping(input, pattern);

        RegexOptions options;

        if (method == IsMatch)
        {
            options = RegexOptions.None;
        }
        else if (arguments[2] is SqlConstantExpression { Value: RegexOptions regexOptions })
        {
            options = regexOptions;
        }
        else
        {
            return null;  // We don't support non-constant regex options
        }

        return (options & UnsupportedRegexOptions) == 0
            ? _sqlExpressionFactory.RegexMatch(
                _sqlExpressionFactory.ApplyTypeMapping(input, typeMapping),
                _sqlExpressionFactory.ApplyTypeMapping(pattern, typeMapping),
                options)
            : null;
    }
}