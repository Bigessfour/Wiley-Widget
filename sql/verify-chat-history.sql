-- Verify AI Chat History Retention
-- Run this query after using the Jarvis chat to verify history is being saved

-- 1. Check if any conversations exist
SELECT
    COUNT(*) AS TotalConversations,
    SUM(MessageCount) AS TotalMessages,
    MIN(CreatedAt) AS OldestConversation,
    MAX(UpdatedAt) AS LatestUpdate
FROM dbo.ConversationHistories;

-- 2. View recent conversations (detailed)
SELECT TOP 10
    ConversationId,
    Title,
    MessageCount,
    CreatedAt,
    UpdatedAt,
    LEN(Content) AS ContentLength,
    LEN(MessagesJson) AS MessagesJsonLength
FROM dbo.ConversationHistories
ORDER BY UpdatedAt DESC;

-- 3. View a sample conversation content (shows actual chat messages)
SELECT TOP 1
    ConversationId,
    Title,
    Content, -- Human-readable conversation
    MessagesJson -- JSON format for parsing
FROM dbo.ConversationHistories
ORDER BY UpdatedAt DESC;

-- 4. Check conversation breakdown by date
SELECT
    CAST(CreatedAt AS DATE) AS ConversationDate,
    COUNT(*) AS ConversationsCreated,
    SUM(MessageCount) AS TotalMessages,
    AVG(MessageCount) AS AvgMessagesPerConversation
FROM dbo.ConversationHistories
GROUP BY CAST(CreatedAt AS DATE)
ORDER BY ConversationDate DESC;
