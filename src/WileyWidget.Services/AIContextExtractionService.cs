using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services;

/// <summary>
/// Basic implementation of AI context extraction using regex patterns.
/// Can be enhanced with NLP/ML models for better accuracy.
/// </summary>
public class AIContextExtractionService : IAIContextExtractionService
{
    private readonly IAIContextRepository _contextRepository;
    private readonly ILogger<AIContextExtractionService> _logger;

    // Regex patterns for entity extraction
    private static readonly Regex DatePattern = new(@"\b(\d{1,2}[-/]\d{1,2}[-/]\d{2,4}|\d{4}[-/]\d{1,2}[-/]\d{1,2}|(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{1,2},?\s+\d{4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccountPattern = new(@"\b(?:account|acct|GL)[\s-]?(\d{3,10}|\d{3}-\d{3}-\d{3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountPattern = new(@"\$\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)\b", RegexOptions.Compiled);
    private static readonly Regex PersonNamePattern = new(@"\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)\b", RegexOptions.Compiled);
    private static readonly Regex EventPattern = new(@"\b(meeting|conference|deadline|workshop|presentation|training|event|appointment)\s+(?:about|for|on|regarding)\s+([^,.!?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AIContextExtractionService(
        IAIContextRepository contextRepository,
        ILogger<AIContextExtractionService> logger)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AIContextEntity>> ExtractEntitiesAsync(string message, string conversationId, CancellationToken ct = default)
    {
        var entities = new List<AIContextEntity>();

        try
        {
            // Extract dates
            var dateMatches = DatePattern.Matches(message);
            foreach (Match match in dateMatches)
            {
                entities.Add(new AIContextEntity
                {
                    ConversationId = conversationId,
                    EntityType = "Date",
                    EntityValue = match.Value,
                    NormalizedValue = NormalizeDateValue(match.Value),
                    Context = ExtractContext(message, match.Index, 100),
                    ImportanceScore = 60
                });
            }

            // Extract account numbers
            var accountMatches = AccountPattern.Matches(message);
            foreach (Match match in accountMatches)
            {
                entities.Add(new AIContextEntity
                {
                    ConversationId = conversationId,
                    EntityType = "Account",
                    EntityValue = match.Value,
                    NormalizedValue = match.Value.ToLower().Replace(" ", "").Replace("-", ""),
                    Context = ExtractContext(message, match.Index, 100),
                    ImportanceScore = 70
                });
            }

            // Extract amounts
            var amountMatches = AmountPattern.Matches(message);
            foreach (Match match in amountMatches)
            {
                entities.Add(new AIContextEntity
                {
                    ConversationId = conversationId,
                    EntityType = "Amount",
                    EntityValue = match.Value,
                    NormalizedValue = match.Groups[1].Value.Replace(",", ""),
                    Context = ExtractContext(message, match.Index, 100),
                    ImportanceScore = 50
                });
            }

            // Extract person names (simple heuristic - can be improved with NER)
            var nameMatches = PersonNamePattern.Matches(message);
            foreach (Match match in nameMatches)
            {
                // Skip common false positives
                if (IsLikelyNotAName(match.Value))
                    continue;

                entities.Add(new AIContextEntity
                {
                    ConversationId = conversationId,
                    EntityType = "Person",
                    EntityValue = match.Value,
                    NormalizedValue = match.Value.ToLower(),
                    Context = ExtractContext(message, match.Index, 100),
                    ImportanceScore = 65
                });
            }

            // Extract events
            var eventMatches = EventPattern.Matches(message);
            foreach (Match match in eventMatches)
            {
                entities.Add(new AIContextEntity
                {
                    ConversationId = conversationId,
                    EntityType = "Event",
                    EntityValue = match.Groups[2].Value.Trim(),
                    NormalizedValue = match.Groups[2].Value.Trim().ToLower(),
                    Context = ExtractContext(message, match.Index, 100),
                    ImportanceScore = 75
                });
            }

            // Save extracted entities to database
            foreach (var entity in entities)
            {
                await _contextRepository.SaveEntityAsync(entity, ct);
            }

            _logger.LogInformation("Extracted {Count} entities from message in conversation {ConversationId}",
                entities.Count, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities from message");
        }

        return entities;
    }

    public async Task<List<AIContextEntity>> ExtractEntitiesFromConversationAsync(List<ChatMessage> messages, string conversationId, CancellationToken ct = default)
    {
        var allEntities = new List<AIContextEntity>();

        foreach (var message in messages)
        {
            var entities = await ExtractEntitiesAsync(message.Message, conversationId, ct);
            allEntities.AddRange(entities);
        }

        return allEntities;
    }

    public string NormalizeEntityValue(string entityValue, string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityValue))
            return string.Empty;

        return entityType.ToLower() switch
        {
            "date" => NormalizeDateValue(entityValue),
            "account" => entityValue.ToLower().Replace(" ", "").Replace("-", ""),
            "person" => entityValue.ToLower().Trim(),
            "event" => entityValue.ToLower().Trim(),
            "amount" => entityValue.Replace("$", "").Replace(",", "").Trim(),
            _ => entityValue.ToLower().Trim()
        };
    }

    public int CalculateImportanceScore(AIContextEntity entity, List<ChatMessage> conversationHistory)
    {
        var baseScore = entity.ImportanceScore;

        // Boost score based on mention count
        baseScore += Math.Min(entity.MentionCount * 5, 20);

        // Boost score for recent mentions
        var hoursSinceLastMention = (DateTime.UtcNow - entity.LastMentionedAt).TotalHours;
        if (hoursSinceLastMention < 24)
            baseScore += 10;
        else if (hoursSinceLastMention < 168) // 7 days
            baseScore += 5;

        // Cap at 100
        return Math.Min(baseScore, 100);
    }

    private string ExtractContext(string message, int position, int windowSize)
    {
        var startIdx = Math.Max(0, position - windowSize / 2);
        var endIdx = Math.Min(message.Length, position + windowSize / 2);
        var context = message.Substring(startIdx, endIdx - startIdx);

        // Add ellipsis if truncated
        if (startIdx > 0)
            context = "..." + context;
        if (endIdx < message.Length)
            context += "...";

        return context.Trim();
    }

    private string NormalizeDateValue(string dateStr)
    {
        // Try to parse and normalize to ISO format
        if (DateTime.TryParse(dateStr, out var date))
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return dateStr.ToLower();
    }

    private bool IsLikelyNotAName(string candidate)
    {
        // Filter out common false positives
        var excludeList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "General Fund", "Budget Entry", "Account Number", "Municipal Account",
            "Revenue Summary", "Fiscal Year", "Tax Revenue", "Quick Books",
            "Enterprise Fund", "Special Revenue", "Conservation Trust", "Public Works"
        };

        return excludeList.Contains(candidate) || candidate.Length < 3;
    }
}
