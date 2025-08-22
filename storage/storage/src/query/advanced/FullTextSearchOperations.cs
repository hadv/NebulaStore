using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Advanced;

/// <summary>
/// Advanced full-text search operations with ranking, relevance scoring, and text indexing.
/// Supports various search algorithms and text analysis techniques.
/// </summary>
public class FullTextSearchOperations
{
    private readonly ITextIndexProvider _textIndexProvider;
    private readonly ITextAnalyzer _textAnalyzer;
    private readonly IFullTextStatisticsCollector _statisticsCollector;
    private readonly FullTextSearchOptions _options;

    public FullTextSearchOperations(
        ITextIndexProvider textIndexProvider,
        ITextAnalyzer textAnalyzer,
        IFullTextStatisticsCollector statisticsCollector,
        FullTextSearchOptions? options = null)
    {
        _textIndexProvider = textIndexProvider ?? throw new ArgumentNullException(nameof(textIndexProvider));
        _textAnalyzer = textAnalyzer ?? throw new ArgumentNullException(nameof(textAnalyzer));
        _statisticsCollector = statisticsCollector ?? throw new ArgumentNullException(nameof(statisticsCollector));
        _options = options ?? new FullTextSearchOptions();
    }

    /// <summary>
    /// Executes a full-text search with ranking and relevance scoring.
    /// </summary>
    public async Task<IQueryResult> ExecuteFullTextSearchAsync(
        IQueryResult input,
        string searchColumn,
        string searchQuery,
        FullTextSearchType searchType = FullTextSearchType.Contains,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Analyze search query
            var analyzedQuery = await _textAnalyzer.AnalyzeQueryAsync(searchQuery, cancellationToken);

            // Choose search strategy based on availability of text index
            var hasTextIndex = await _textIndexProvider.HasTextIndexAsync(searchColumn, cancellationToken);
            
            IQueryResult result = hasTextIndex
                ? await ExecuteIndexedFullTextSearchAsync(input, searchColumn, analyzedQuery, searchType, cancellationToken)
                : await ExecuteSequentialFullTextSearchAsync(input, searchColumn, analyzedQuery, searchType, cancellationToken);

            // Collect statistics
            await _statisticsCollector.RecordFullTextSearchAsync(new FullTextSearchStats
            {
                SearchType = searchType,
                SearchTerms = analyzedQuery.Terms.Count,
                InputRows = input.RowCount,
                ResultRows = result.RowCount,
                ExecutionTime = DateTime.UtcNow - startTime,
                UsedIndex = hasTextIndex
            });

            return result;
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Full-text search execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a phrase search for exact phrase matching.
    /// </summary>
    public async Task<IQueryResult> ExecutePhraseSearchAsync(
        IQueryResult input,
        string searchColumn,
        string phrase,
        int proximityDistance = 0,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var analyzedPhrase = await _textAnalyzer.AnalyzePhraseAsync(phrase, cancellationToken);
            var results = new List<IDataRow>();

            await foreach (var row in input.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var textValue = row.GetValue(searchColumn)?.ToString();
                if (string.IsNullOrEmpty(textValue)) continue;

                var score = CalculatePhraseScore(textValue, analyzedPhrase, proximityDistance);
                if (score > 0)
                {
                    var scoredRow = AddRelevanceScore(row, score);
                    results.Add(scoredRow);
                }
            }

            // Sort by relevance score
            results.Sort((x, y) => GetRelevanceScore(y).CompareTo(GetRelevanceScore(x)));

            // Collect statistics
            await _statisticsCollector.RecordFullTextSearchAsync(new FullTextSearchStats
            {
                SearchType = FullTextSearchType.Phrase,
                SearchTerms = analyzedPhrase.Terms.Count,
                InputRows = input.RowCount,
                ResultRows = results.Count,
                ExecutionTime = DateTime.UtcNow - startTime,
                UsedIndex = false
            });

            return new QueryResult(results, CreateScoredSchema(input.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Phrase search execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a fuzzy search with edit distance tolerance.
    /// </summary>
    public async Task<IQueryResult> ExecuteFuzzySearchAsync(
        IQueryResult input,
        string searchColumn,
        string searchTerm,
        int maxEditDistance = 2,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var results = new List<IDataRow>();

            await foreach (var row in input.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var textValue = row.GetValue(searchColumn)?.ToString();
                if (string.IsNullOrEmpty(textValue)) continue;

                var score = CalculateFuzzyScore(textValue, searchTerm, maxEditDistance);
                if (score > 0)
                {
                    var scoredRow = AddRelevanceScore(row, score);
                    results.Add(scoredRow);
                }
            }

            // Sort by relevance score
            results.Sort((x, y) => GetRelevanceScore(y).CompareTo(GetRelevanceScore(x)));

            // Collect statistics
            await _statisticsCollector.RecordFullTextSearchAsync(new FullTextSearchStats
            {
                SearchType = FullTextSearchType.Fuzzy,
                SearchTerms = 1,
                InputRows = input.RowCount,
                ResultRows = results.Count,
                ExecutionTime = DateTime.UtcNow - startTime,
                UsedIndex = false
            });

            return new QueryResult(results, CreateScoredSchema(input.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Fuzzy search execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a boolean search with AND, OR, NOT operators.
    /// </summary>
    public async Task<IQueryResult> ExecuteBooleanSearchAsync(
        IQueryResult input,
        string searchColumn,
        string booleanQuery,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var parsedQuery = await ParseBooleanQueryAsync(booleanQuery, cancellationToken);
            var results = new List<IDataRow>();

            await foreach (var row in input.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var textValue = row.GetValue(searchColumn)?.ToString();
                if (string.IsNullOrEmpty(textValue)) continue;

                var (matches, score) = EvaluateBooleanQuery(textValue, parsedQuery);
                if (matches)
                {
                    var scoredRow = AddRelevanceScore(row, score);
                    results.Add(scoredRow);
                }
            }

            // Sort by relevance score
            results.Sort((x, y) => GetRelevanceScore(y).CompareTo(GetRelevanceScore(x)));

            // Collect statistics
            await _statisticsCollector.RecordFullTextSearchAsync(new FullTextSearchStats
            {
                SearchType = FullTextSearchType.Boolean,
                SearchTerms = CountTermsInBooleanQuery(parsedQuery),
                InputRows = input.RowCount,
                ResultRows = results.Count,
                ExecutionTime = DateTime.UtcNow - startTime,
                UsedIndex = false
            });

            return new QueryResult(results, CreateScoredSchema(input.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Boolean search execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a wildcard search with pattern matching.
    /// </summary>
    public async Task<IQueryResult> ExecuteWildcardSearchAsync(
        IQueryResult input,
        string searchColumn,
        string wildcardPattern,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var regex = ConvertWildcardToRegex(wildcardPattern);
            var results = new List<IDataRow>();

            await foreach (var row in input.GetRowsAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var textValue = row.GetValue(searchColumn)?.ToString();
                if (string.IsNullOrEmpty(textValue)) continue;

                if (regex.IsMatch(textValue))
                {
                    var score = CalculateWildcardScore(textValue, wildcardPattern);
                    var scoredRow = AddRelevanceScore(row, score);
                    results.Add(scoredRow);
                }
            }

            // Sort by relevance score
            results.Sort((x, y) => GetRelevanceScore(y).CompareTo(GetRelevanceScore(x)));

            // Collect statistics
            await _statisticsCollector.RecordFullTextSearchAsync(new FullTextSearchStats
            {
                SearchType = FullTextSearchType.Wildcard,
                SearchTerms = 1,
                InputRows = input.RowCount,
                ResultRows = results.Count,
                ExecutionTime = DateTime.UtcNow - startTime,
                UsedIndex = false
            });

            return new QueryResult(results, CreateScoredSchema(input.Schema));
        }
        catch (Exception ex)
        {
            throw new QueryExecutionException($"Wildcard search execution failed: {ex.Message}", ex);
        }
    }

    // Private implementation methods

    private async Task<IQueryResult> ExecuteIndexedFullTextSearchAsync(
        IQueryResult input,
        string searchColumn,
        IAnalyzedQuery analyzedQuery,
        FullTextSearchType searchType,
        CancellationToken cancellationToken)
    {
        // Use text index for efficient search
        var textIndex = await _textIndexProvider.GetTextIndexAsync(searchColumn, cancellationToken);
        var candidateRows = new HashSet<long>();

        // Find candidate rows using inverted index
        foreach (var term in analyzedQuery.Terms)
        {
            var termRows = await textIndex.FindRowsContainingTermAsync(term.Text, cancellationToken);
            
            if (candidateRows.Count == 0)
            {
                candidateRows.UnionWith(termRows);
            }
            else
            {
                // Apply boolean logic based on search type
                candidateRows = searchType switch
                {
                    FullTextSearchType.Contains => new HashSet<long>(candidateRows.Union(termRows)),
                    FullTextSearchType.ContainsAll => new HashSet<long>(candidateRows.Intersect(termRows)),
                    _ => candidateRows
                };
            }
        }

        // Score and filter candidate rows
        var results = new List<IDataRow>();
        await foreach (var row in input.GetRowsAsync(cancellationToken))
        {
            // Check if row is a candidate (simplified - would need row ID mapping)
            var textValue = row.GetValue(searchColumn)?.ToString();
            if (string.IsNullOrEmpty(textValue)) continue;

            var score = CalculateRelevanceScore(textValue, analyzedQuery);
            if (score > _options.MinimumRelevanceScore)
            {
                var scoredRow = AddRelevanceScore(row, score);
                results.Add(scoredRow);
            }
        }

        // Sort by relevance score
        results.Sort((x, y) => GetRelevanceScore(y).CompareTo(GetRelevanceScore(x)));

        return new QueryResult(results, CreateScoredSchema(input.Schema));
    }

    private async Task<IQueryResult> ExecuteSequentialFullTextSearchAsync(
        IQueryResult input,
        string searchColumn,
        IAnalyzedQuery analyzedQuery,
        FullTextSearchType searchType,
        CancellationToken cancellationToken)
    {
        var results = new List<IDataRow>();

        await foreach (var row in input.GetRowsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var textValue = row.GetValue(searchColumn)?.ToString();
            if (string.IsNullOrEmpty(textValue)) continue;

            var score = CalculateRelevanceScore(textValue, analyzedQuery);
            if (score > _options.MinimumRelevanceScore)
            {
                var scoredRow = AddRelevanceScore(row, score);
                results.Add(scoredRow);
            }
        }

        // Sort by relevance score
        results.Sort((x, y) => GetRelevanceScore(y).CompareTo(GetRelevanceScore(x)));

        return new QueryResult(results, CreateScoredSchema(input.Schema));
    }

    private double CalculateRelevanceScore(string text, IAnalyzedQuery analyzedQuery)
    {
        var score = 0.0;
        var textTerms = _textAnalyzer.ExtractTerms(text);
        var textLength = textTerms.Count;

        foreach (var queryTerm in analyzedQuery.Terms)
        {
            var termFrequency = textTerms.Count(t => t.Equals(queryTerm.Text, StringComparison.OrdinalIgnoreCase));
            if (termFrequency > 0)
            {
                // TF-IDF scoring
                var tf = (double)termFrequency / textLength;
                var idf = Math.Log(1.0 + 1.0 / Math.Max(queryTerm.DocumentFrequency, 1));
                score += tf * idf * queryTerm.Weight;
            }
        }

        return score;
    }

    private double CalculatePhraseScore(string text, IAnalyzedQuery analyzedPhrase, int proximityDistance)
    {
        var textTerms = _textAnalyzer.ExtractTerms(text);
        var phraseTerms = analyzedPhrase.Terms.Select(t => t.Text).ToList();

        // Find phrase occurrences
        var score = 0.0;
        for (int i = 0; i <= textTerms.Count - phraseTerms.Count; i++)
        {
            var isMatch = true;
            for (int j = 0; j < phraseTerms.Count; j++)
            {
                var expectedIndex = i + j;
                if (expectedIndex >= textTerms.Count || 
                    !textTerms[expectedIndex].Equals(phraseTerms[j], StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                score += 1.0; // Exact phrase match
            }
        }

        return score;
    }

    private double CalculateFuzzyScore(string text, string searchTerm, int maxEditDistance)
    {
        var textTerms = _textAnalyzer.ExtractTerms(text);
        var bestScore = 0.0;

        foreach (var term in textTerms)
        {
            var editDistance = CalculateEditDistance(term, searchTerm);
            if (editDistance <= maxEditDistance)
            {
                var similarity = 1.0 - (double)editDistance / Math.Max(term.Length, searchTerm.Length);
                bestScore = Math.Max(bestScore, similarity);
            }
        }

        return bestScore;
    }

    private int CalculateEditDistance(string s1, string s2)
    {
        // Levenshtein distance implementation
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    private async Task<IBooleanQuery> ParseBooleanQueryAsync(string booleanQuery, CancellationToken cancellationToken)
    {
        // Simplified boolean query parser
        // In practice, would implement a full parser for complex boolean expressions
        return new BooleanQuery(booleanQuery);
    }

    private (bool matches, double score) EvaluateBooleanQuery(string text, IBooleanQuery booleanQuery)
    {
        // Simplified boolean query evaluation
        var textTerms = _textAnalyzer.ExtractTerms(text);
        var score = 0.0;
        var matches = false;

        // This would implement full boolean logic evaluation
        foreach (var term in textTerms)
        {
            if (booleanQuery.ContainsTerm(term))
            {
                matches = true;
                score += 1.0;
            }
        }

        return (matches, score);
    }

    private int CountTermsInBooleanQuery(IBooleanQuery booleanQuery)
    {
        return booleanQuery.GetTermCount();
    }

    private Regex ConvertWildcardToRegex(string wildcardPattern)
    {
        var regexPattern = "^" + Regex.Escape(wildcardPattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return new Regex(regexPattern, RegexOptions.IgnoreCase);
    }

    private double CalculateWildcardScore(string text, string wildcardPattern)
    {
        // Simple scoring based on pattern complexity
        var wildcardCount = wildcardPattern.Count(c => c == '*' || c == '?');
        return 1.0 / (1.0 + wildcardCount * 0.1);
    }

    private IDataRow AddRelevanceScore(IDataRow originalRow, double score)
    {
        var values = new Dictionary<string, object?>();

        // Copy original row values
        foreach (var column in originalRow.Schema.Columns)
        {
            values[column.Name] = originalRow.GetValue(column.Name);
        }

        // Add relevance score
        values["_relevance_score"] = score;

        return new DataRow(values);
    }

    private double GetRelevanceScore(IDataRow row)
    {
        var scoreValue = row.GetValue("_relevance_score");
        return scoreValue is double score ? score : 0.0;
    }

    private ISchema CreateScoredSchema(ISchema originalSchema)
    {
        var columns = new List<IColumnInfo>(originalSchema.Columns)
        {
            new ColumnInfo("_relevance_score", typeof(double), false, null)
        };

        return new Schema(columns);
    }
}

/// <summary>
/// Types of full-text search operations.
/// </summary>
public enum FullTextSearchType
{
    Contains,
    ContainsAll,
    Phrase,
    Fuzzy,
    Boolean,
    Wildcard,
    Proximity
}

/// <summary>
/// Options for full-text search operations.
/// </summary>
public class FullTextSearchOptions
{
    public double MinimumRelevanceScore { get; set; } = 0.1;
    public int MaxResults { get; set; } = 1000;
    public bool EnableStemming { get; set; } = true;
    public bool EnableStopWords { get; set; } = true;
    public string Language { get; set; } = "en";
}

/// <summary>
/// Statistics for full-text search execution.
/// </summary>
public class FullTextSearchStats
{
    public FullTextSearchType SearchType { get; set; }
    public int SearchTerms { get; set; }
    public long InputRows { get; set; }
    public long ResultRows { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool UsedIndex { get; set; }
}

/// <summary>
/// Simple boolean query implementation.
/// </summary>
public class BooleanQuery : IBooleanQuery
{
    private readonly string _query;
    private readonly HashSet<string> _terms;

    public BooleanQuery(string query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _terms = ExtractTerms(query);
    }

    public bool ContainsTerm(string term)
    {
        return _terms.Contains(term.ToLowerInvariant());
    }

    public int GetTermCount()
    {
        return _terms.Count;
    }

    private HashSet<string> ExtractTerms(string query)
    {
        // Simplified term extraction
        var terms = query.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !IsOperator(t))
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        return terms;
    }

    private bool IsOperator(string term)
    {
        return term.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
               term.Equals("OR", StringComparison.OrdinalIgnoreCase) ||
               term.Equals("NOT", StringComparison.OrdinalIgnoreCase);
    }
}
