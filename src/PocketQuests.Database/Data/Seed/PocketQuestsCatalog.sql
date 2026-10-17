-- Versioned public catalog only. Repeat publication preserves unchanged rows and timestamps.
SET NOCOUNT ON;
UPDATE target SET [Name]=source.[Name], [CatalogVersion]=source.[CatalogVersion], [SortOrder]=source.[SortOrder], [ModifiedAt]=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')
FROM [pocketquests].[Category] AS target JOIN (VALUES
    (N'c01', N'Health & Fitness', N'PQ-CAT-0.5', 1),
    (N'c02', N'Learning', N'PQ-CAT-0.5', 2),
    (N'c03', N'Creativity', N'PQ-CAT-0.5', 3),
    (N'c04', N'Work & Purpose', N'PQ-CAT-0.5', 4),
    (N'c05', N'Relationships & Community', N'PQ-CAT-0.5', 5),
    (N'c06', N'Everyday Life', N'PQ-CAT-0.5', 6),
    (N'c07', N'Rest & Recreation', N'PQ-CAT-0.5', 7),
    (N'c08', N'Reflection & Spirituality', N'PQ-CAT-0.5', 8),
    (N'c09', N'Finances', N'PQ-CAT-0.5', 9),
    (N'c10', N'Exploration & Adventure', N'PQ-CAT-0.5', 10)) AS source ([Id], [Name], [CatalogVersion], [SortOrder]) ON target.Id=source.Id
WHERE target.[Name] <> source.[Name] OR target.[CatalogVersion] <> source.[CatalogVersion] OR target.[SortOrder] <> source.[SortOrder];
INSERT [pocketquests].[Category] ([Id], [Name], [CatalogVersion], [SortOrder])
SELECT source.[Id], source.[Name], source.[CatalogVersion], source.[SortOrder]
FROM (VALUES
    (N'c01', N'Health & Fitness', N'PQ-CAT-0.5', 1),
    (N'c02', N'Learning', N'PQ-CAT-0.5', 2),
    (N'c03', N'Creativity', N'PQ-CAT-0.5', 3),
    (N'c04', N'Work & Purpose', N'PQ-CAT-0.5', 4),
    (N'c05', N'Relationships & Community', N'PQ-CAT-0.5', 5),
    (N'c06', N'Everyday Life', N'PQ-CAT-0.5', 6),
    (N'c07', N'Rest & Recreation', N'PQ-CAT-0.5', 7),
    (N'c08', N'Reflection & Spirituality', N'PQ-CAT-0.5', 8),
    (N'c09', N'Finances', N'PQ-CAT-0.5', 9),
    (N'c10', N'Exploration & Adventure', N'PQ-CAT-0.5', 10)) AS source ([Id], [Name], [CatalogVersion], [SortOrder])
WHERE NOT EXISTS (SELECT 1 FROM [pocketquests].[Category] AS target WHERE target.Id=source.Id);

UPDATE target SET [Name]=source.[Name], [CatalogVersion]=source.[CatalogVersion], [SortOrder]=source.[SortOrder], [ModifiedAt]=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')
FROM [pocketquests].[Attribute] AS target JOIN (VALUES
    (N'a01', N'Strength', N'PQ-CAT-0.5', 1),
    (N'a02', N'Endurance', N'PQ-CAT-0.5', 2),
    (N'a03', N'Flexibility', N'PQ-CAT-0.5', 3),
    (N'a04', N'Dexterity', N'PQ-CAT-0.5', 4),
    (N'a05', N'Intelligence', N'PQ-CAT-0.5', 5),
    (N'a06', N'Wisdom', N'PQ-CAT-0.5', 6),
    (N'a07', N'Creativity', N'PQ-CAT-0.5', 7),
    (N'a08', N'Charisma', N'PQ-CAT-0.5', 8)) AS source ([Id], [Name], [CatalogVersion], [SortOrder]) ON target.Id=source.Id
WHERE target.[Name] <> source.[Name] OR target.[CatalogVersion] <> source.[CatalogVersion] OR target.[SortOrder] <> source.[SortOrder];
INSERT [pocketquests].[Attribute] ([Id], [Name], [CatalogVersion], [SortOrder])
SELECT source.[Id], source.[Name], source.[CatalogVersion], source.[SortOrder]
FROM (VALUES
    (N'a01', N'Strength', N'PQ-CAT-0.5', 1),
    (N'a02', N'Endurance', N'PQ-CAT-0.5', 2),
    (N'a03', N'Flexibility', N'PQ-CAT-0.5', 3),
    (N'a04', N'Dexterity', N'PQ-CAT-0.5', 4),
    (N'a05', N'Intelligence', N'PQ-CAT-0.5', 5),
    (N'a06', N'Wisdom', N'PQ-CAT-0.5', 6),
    (N'a07', N'Creativity', N'PQ-CAT-0.5', 7),
    (N'a08', N'Charisma', N'PQ-CAT-0.5', 8)) AS source ([Id], [Name], [CatalogVersion], [SortOrder])
WHERE NOT EXISTS (SELECT 1 FROM [pocketquests].[Attribute] AS target WHERE target.Id=source.Id);

UPDATE target SET [Name]=source.[Name], [CatalogVersion]=source.[CatalogVersion], [SortOrder]=source.[SortOrder], [NormalizedName]=source.[NormalizedName], [ModifiedAt]=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')
FROM [pocketquests].[Skill] AS target JOIN (VALUES
    (N's01', N'Strength Training', N'PQ-CAT-0.5', 1, N'STRENGTH TRAINING'),
    (N's02', N'Running', N'PQ-CAT-0.5', 2, N'RUNNING'),
    (N's03', N'Language Learning', N'PQ-CAT-0.5', 3, N'LANGUAGE LEARNING'),
    (N's04', N'Research', N'PQ-CAT-0.5', 4, N'RESEARCH'),
    (N's05', N'Drawing', N'PQ-CAT-0.5', 5, N'DRAWING'),
    (N's06', N'Creative Writing', N'PQ-CAT-0.5', 6, N'CREATIVE WRITING'),
    (N's07', N'Programming', N'PQ-CAT-0.5', 7, N'PROGRAMMING'),
    (N's08', N'Project Planning', N'PQ-CAT-0.5', 8, N'PROJECT PLANNING'),
    (N's09', N'Communication', N'PQ-CAT-0.5', 9, N'COMMUNICATION'),
    (N's10', N'Conflict Resolution', N'PQ-CAT-0.5', 10, N'CONFLICT RESOLUTION'),
    (N's11', N'Cooking', N'PQ-CAT-0.5', 11, N'COOKING'),
    (N's12', N'Home Maintenance', N'PQ-CAT-0.5', 12, N'HOME MAINTENANCE'),
    (N's13', N'Meditation', N'PQ-CAT-0.5', 13, N'MEDITATION'),
    (N's14', N'Journaling', N'PQ-CAT-0.5', 14, N'JOURNALING'),
    (N's15', N'Budgeting', N'PQ-CAT-0.5', 15, N'BUDGETING'),
    (N's16', N'Financial Planning', N'PQ-CAT-0.5', 16, N'FINANCIAL PLANNING'),
    (N's17', N'Navigation', N'PQ-CAT-0.5', 17, N'NAVIGATION'),
    (N's18', N'Trip Planning', N'PQ-CAT-0.5', 18, N'TRIP PLANNING')) AS source ([Id], [Name], [CatalogVersion], [SortOrder], [NormalizedName]) ON target.Id=source.Id
WHERE target.[Name] <> source.[Name] OR target.[CatalogVersion] <> source.[CatalogVersion] OR target.[SortOrder] <> source.[SortOrder] OR target.[NormalizedName] <> source.[NormalizedName];
INSERT [pocketquests].[Skill] ([Id], [Name], [CatalogVersion], [SortOrder], [NormalizedName])
SELECT source.[Id], source.[Name], source.[CatalogVersion], source.[SortOrder], source.[NormalizedName]
FROM (VALUES
    (N's01', N'Strength Training', N'PQ-CAT-0.5', 1, N'STRENGTH TRAINING'),
    (N's02', N'Running', N'PQ-CAT-0.5', 2, N'RUNNING'),
    (N's03', N'Language Learning', N'PQ-CAT-0.5', 3, N'LANGUAGE LEARNING'),
    (N's04', N'Research', N'PQ-CAT-0.5', 4, N'RESEARCH'),
    (N's05', N'Drawing', N'PQ-CAT-0.5', 5, N'DRAWING'),
    (N's06', N'Creative Writing', N'PQ-CAT-0.5', 6, N'CREATIVE WRITING'),
    (N's07', N'Programming', N'PQ-CAT-0.5', 7, N'PROGRAMMING'),
    (N's08', N'Project Planning', N'PQ-CAT-0.5', 8, N'PROJECT PLANNING'),
    (N's09', N'Communication', N'PQ-CAT-0.5', 9, N'COMMUNICATION'),
    (N's10', N'Conflict Resolution', N'PQ-CAT-0.5', 10, N'CONFLICT RESOLUTION'),
    (N's11', N'Cooking', N'PQ-CAT-0.5', 11, N'COOKING'),
    (N's12', N'Home Maintenance', N'PQ-CAT-0.5', 12, N'HOME MAINTENANCE'),
    (N's13', N'Meditation', N'PQ-CAT-0.5', 13, N'MEDITATION'),
    (N's14', N'Journaling', N'PQ-CAT-0.5', 14, N'JOURNALING'),
    (N's15', N'Budgeting', N'PQ-CAT-0.5', 15, N'BUDGETING'),
    (N's16', N'Financial Planning', N'PQ-CAT-0.5', 16, N'FINANCIAL PLANNING'),
    (N's17', N'Navigation', N'PQ-CAT-0.5', 17, N'NAVIGATION'),
    (N's18', N'Trip Planning', N'PQ-CAT-0.5', 18, N'TRIP PLANNING')) AS source ([Id], [Name], [CatalogVersion], [SortOrder], [NormalizedName])
WHERE NOT EXISTS (SELECT 1 FROM [pocketquests].[Skill] AS target WHERE target.Id=source.Id);

UPDATE target SET [KindCode]=source.[KindCode], [Name]=source.[Name], [RequiredCount]=source.[RequiredCount], [RequiredRank]=source.[RequiredRank], [RulesetVersion]=source.[RulesetVersion], [ModifiedAt]=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')
FROM [pocketquests].[ProfileReward] AS target JOIN (VALUES
    (N'PQ-CAT-A01', N'Achievement', N'A Quest of My Own', 1, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-A02', N'Achievement', N'Room to Recharge', 5, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-B01', N'Badge', N'Curious Explorer', 3, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-B02', N'Badge', N'Everyday Care', 10, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-F01', N'Frame', N'Open Notebook', 10, N'E', N'PQ-MVP-0.5'),
    (N'PQ-CAT-F02', N'Frame', N'Quiet Horizon', 10, N'D', N'PQ-MVP-0.5')) AS source ([Id], [KindCode], [Name], [RequiredCount], [RequiredRank], [RulesetVersion]) ON target.Id=source.Id
WHERE target.[KindCode] <> source.[KindCode] OR target.[Name] <> source.[Name] OR target.[RequiredCount] <> source.[RequiredCount] OR target.[RequiredRank] <> source.[RequiredRank] OR target.[RulesetVersion] <> source.[RulesetVersion];
INSERT [pocketquests].[ProfileReward] ([Id], [KindCode], [Name], [RequiredCount], [RequiredRank], [RulesetVersion])
SELECT source.[Id], source.[KindCode], source.[Name], source.[RequiredCount], source.[RequiredRank], source.[RulesetVersion]
FROM (VALUES
    (N'PQ-CAT-A01', N'Achievement', N'A Quest of My Own', 1, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-A02', N'Achievement', N'Room to Recharge', 5, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-B01', N'Badge', N'Curious Explorer', 3, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-B02', N'Badge', N'Everyday Care', 10, N'F', N'PQ-MVP-0.5'),
    (N'PQ-CAT-F01', N'Frame', N'Open Notebook', 10, N'E', N'PQ-MVP-0.5'),
    (N'PQ-CAT-F02', N'Frame', N'Quiet Horizon', 10, N'D', N'PQ-MVP-0.5')) AS source ([Id], [KindCode], [Name], [RequiredCount], [RequiredRank], [RulesetVersion])
WHERE NOT EXISTS (SELECT 1 FROM [pocketquests].[ProfileReward] AS target WHERE target.Id=source.Id);

UPDATE target SET [CategoryId]=source.[CategoryId], [Title]=source.[Title], [CatalogVersion]=source.[CatalogVersion], [ModifiedAt]=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')
FROM [pocketquests].[SystemQuest] AS target JOIN (VALUES
    (N'PQ-CAT-Q01', N'c01', N'Practice a familiar strength routine', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q02', N'c02', N'Return to a language', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q03', N'c03', N'Make a sketch', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q04', N'c04', N'Plan the next step', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q05', N'c05', N'Make time to listen', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q06', N'c06', N'Reset a household space', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q07', N'c07', N'Enjoy some downtime', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q08', N'c08', N'Make space for reflection', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q09', N'c09', N'Review your spending record', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q10', N'c10', N'Discover somewhere local', N'PQ-CAT-0.5')) AS source ([Id], [CategoryId], [Title], [CatalogVersion]) ON target.Id=source.Id
WHERE target.[CategoryId] <> source.[CategoryId] OR target.[Title] <> source.[Title] OR target.[CatalogVersion] <> source.[CatalogVersion];
INSERT [pocketquests].[SystemQuest] ([Id], [CategoryId], [Title], [CatalogVersion])
SELECT source.[Id], source.[CategoryId], source.[Title], source.[CatalogVersion]
FROM (VALUES
    (N'PQ-CAT-Q01', N'c01', N'Practice a familiar strength routine', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q02', N'c02', N'Return to a language', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q03', N'c03', N'Make a sketch', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q04', N'c04', N'Plan the next step', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q05', N'c05', N'Make time to listen', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q06', N'c06', N'Reset a household space', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q07', N'c07', N'Enjoy some downtime', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q08', N'c08', N'Make space for reflection', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q09', N'c09', N'Review your spending record', N'PQ-CAT-0.5'),
    (N'PQ-CAT-Q10', N'c10', N'Discover somewhere local', N'PQ-CAT-0.5')) AS source ([Id], [CategoryId], [Title], [CatalogVersion])
WHERE NOT EXISTS (SELECT 1 FROM [pocketquests].[SystemQuest] AS target WHERE target.Id=source.Id);
