// Generated from contracts/pocketquests-v1.openapi.json. Run npm run generate:api.
export type AnchorRequest = {
  "deviceId": string;
  "bootId": string;
  "deviceUtc": string;
};

export type Balance = {
  "id": string;
  "name": string;
  "xp": number;
  "level": number;
};

export type Cadence = "Days" | "Weeks" | "Months" | "Years";

export type CatalogResponse = {
  "categories": (NamedItem)[];
  "attributes": (NamedItem)[];
  "skills": (NamedItem)[];
  "quests": (Quest)[];
  "rules": (RankRule)[];
};

export type Completion = {
  "id": string;
  "occurrenceId": string;
  "recordedAt": string;
  "snapshot": null | Quest;
};

export type CompletionLevelUp = {
  "track": string;
  "trackId": string;
  "name": string;
  "from": number;
  "to": number;
};

export type CompletionOutcome = {
  "completionId": string;
  "occurrenceId": string;
  "levelUps": (CompletionLevelUp)[];
  "rankUp": null | Rank;
  "unlocks": (Entitlement)[];
};

export type CustomSkill = {
  "id": string;
  "name": string;
  "revision": number;
  "archived": boolean;
};

export type DeadlineChange = {
  "occurrenceId": string;
  "title": string;
  "deadline": string;
  "becomesMissed": boolean;
};

export type Effort = "Small" | "Medium" | "Large";

export type Entitlement = {
  "id": string;
  "kind": string;
  "name": string;
  "count": number;
  "requiredCount": number;
  "requiredRank": Rank;
  "earned": boolean;
};

export type Experience = "New" | "Practiced" | "Experienced" | "Expert";

export type InitializeProfile = {
  "zone": string;
};

export type NamedItem = {
  "id": string;
  "name": string;
};

export type Occurrence = {
  "id": string;
  "quest": Quest;
  "dueDate": string | null;
  "deadline": string | null;
  "acceptedAt": string;
  "plannedTime": string | null;
  "parentId": string | null;
  "lifecycle": null | OccurrenceLifecycle;
};

export type OccurrenceLifecycle = {
  "seriesId": string | null;
  "sequence": number | string | null | null;
  "scheduleVersion": number | string | null | null;
  "frozenAt": string | null;
  "individuallyFrozen": boolean;
  "abandonedAt": string | null;
  "lockedLoss": number | string | null | null;
  "lossCategoryId": string | null;
  "unaccepted": boolean;
  "sourceAnchorId": string | null;
  "deadlineZone": string | null;
};

export type OccurrenceView = {
  "occurrence": Occurrence;
  "status": QuestStatus;
  "completion": null | Completion;
  "canUndo": boolean;
  "planned": null | PlannedMoment;
};

export type PauseWindow = {
  "categoryId": string;
  "startedAt": string;
  "endedAt": string | null;
};

export type PenaltyAssessment = {
  "occurrenceId": string;
  "categoryId": string;
  "lockedLoss": number;
  "actualLoss": number;
  "at": string;
};

export type PlannedMoment = {
  "requested": string;
  "resolved": string;
  "instant": string;
  "adjusted": boolean;
  "repeated": boolean;
};

export type PlayerProfile = {
  "interests": (string)[];
  "assessments": Record<string, Experience>;
  "onboardingComplete": boolean;
  "badgeId": string | null;
  "frameId": string | null;
  "customSkills": (CustomSkill)[] | null;
  "assessmentHistory": (SkillAssessment)[] | null;
  "zoneHistory": (ZoneChange)[] | null;
  "expiryWarnings": boolean;
};

export type ProblemDetails = {
  "type"?: string | null;
  "title"?: string | null;
  "status"?: number | string | null | null;
  "detail"?: string | null;
  "instance"?: string | null;
};

export type Quest = {
  "id": string;
  "title": string;
  "criterion": string;
  "categoryId": string;
  "rank": Rank;
  "effort": Effort;
  "attributes": (Share)[];
  "skills": (Share)[];
  "isCustom": boolean;
  "description": string | null;
  "penaltyPercent": number;
  "baseXp": number;
};

export type QuestCommand = {
  "operationId": string;
  "action": string;
  "occurrenceId"?: string | null;
  "questId"?: string | null;
  "dueDate"?: string | null;
  "completionId"?: string | null;
  "definition"?: null | QuestInput;
  "expectedRevision"?: number | string | null | null;
  "skillId"?: string | null;
  "experience"?: null | Experience;
  "interests"?: (string)[] | null;
  "plannedTime"?: string | null;
  "parentId"?: string | null;
  "rewardId"?: string | null;
  "seriesId"?: string | null;
  "cadence"?: null | Cadence;
  "interval"?: number | string | null | null;
  "categoryId"?: string | null;
  "confirmPenalty"?: boolean;
  "acceptedLoss"?: number | string | null | null;
  "recordedTime"?: null | RecordedActionTime;
  "skillName"?: string | null;
  "acceptedQuest"?: null | QuestInput;
  "newZone"?: string | null;
  "expectedZone"?: string | null;
  "confirmZoneChange"?: boolean;
  "expiryWarnings"?: boolean | null;
};

export type QuestDefinition = {
  "quest": Quest;
  "revision": number;
  "archived": boolean;
};

export type QuestExport = {
  "schemaVersion": number;
  "generatedAt": string;
  "snapshotCutoff": string;
  "accountId": string;
  "state": QuestState;
  "definitionRevisions": (QuestDefinition)[];
  "completions": (Completion)[];
  "undos": (UndoEvent)[];
  "notice": string;
};

export type QuestInput = {
  "id": string;
  "title": string;
  "criterion": string;
  "categoryId": string;
  "rank": Rank;
  "effort": Effort;
  "attributes": (ShareInput)[];
  "skills": (ShareInput)[];
  "isCustom"?: boolean;
  "description"?: string | null;
  "penaltyPercent"?: number | string;
  "baseXp"?: number | string;
};

export type QuestSeries = {
  "id": string;
  "quest": Quest;
  "anchor": string;
  "cadence": Cadence;
  "interval": number;
  "version": number;
  "nextSequence": number;
  "pauseDays": number;
  "stopped": boolean;
  "plannedTime": string | null;
  "autoAcceptPenalty": boolean;
  "effectiveAt": string | null;
};

export type QuestState = {
  "zone": string;
  "today": string;
  "occurrences": (OccurrenceView)[];
  "overallXp": number;
  "overallLevel": number;
  "categories": (Balance)[];
  "attributes": (Balance)[];
  "skills": (Balance)[];
  "rank": RankProgress;
  "streaks": (Streak)[];
  "entitlements": (Entitlement)[];
  "definitions": (QuestDefinition)[];
  "profile": PlayerProfile;
  "schedule": ScheduleState;
  "penalties": (PenaltyAssessment)[];
  "ledger": (XpEntry)[];
  "futureWarnings": (string)[] | null;
  "completionOutcome": null | CompletionOutcome;
};

export type QuestStatus = "Active" | "Completed" | "Missed" | "Frozen" | "Abandoned" | "Offered";

export type Rank = "F" | "E" | "D" | "C" | "B" | "A" | "S";

export type RankProgress = {
  "current": Rank;
  "requirement": RankRule;
  "qualifyingCategories": number;
  "total": number;
};

export type RankRule = {
  "rank": Rank;
  "count": number;
  "floor": number;
  "total": number;
};

export type RecordedActionTime = {
  "anchorId": string;
  "bootId": string;
  "ordinal": number | string;
  "elapsedMilliseconds": number | string;
  "deviceUtc": string;
};

export type ScheduleState = {
  "series": (QuestSeries)[];
  "pauses": (PauseWindow)[];
  "pausedCategories": (string)[];
  "accountPaused": boolean;
};

export type Share = {
  "id": string;
  "basisPoints": number;
};

export type ShareInput = {
  "id": string;
  "basisPoints": number | string;
};

export type SkillAssessment = {
  "skillId": string;
  "experience": Experience;
  "at": string;
};

export type Streak = {
  "categoryId": string;
  "days": number;
  "qualifiedToday": boolean;
  "rate": number;
};

export type SyncAnchor = {
  "id": string;
  "deviceId": string;
  "bootId": string;
  "serverUtc": string;
  "deviceUtc": string;
  "recordedTimeFloor": string | null;
};

export type UndoEvent = {
  "id": string;
  "completionId": string;
  "recordedAt": string;
};

export type XpEntry = {
  "eventId": string;
  "occurrenceId": string;
  "at": string;
  "track": string;
  "trackId": string;
  "amount": number;
};

export type ZoneChange = {
  "from": string;
  "to": string;
  "at": string;
};

export type ZonePreview = {
  "previousZone": string;
  "zone": string;
  "deadlines": (DeadlineChange)[];
};

export type ApiOperations = {
  "POST /api/sync-anchor": { body: AnchorRequest; query: undefined; response: SyncAnchor };
  "GET /api/catalog": { body: undefined; query: undefined; response: CatalogResponse };
  "POST /api/profile": { body: InitializeProfile; query: undefined; response: QuestState };
  "GET /api/state": { body: undefined; query: undefined; response: QuestState };
  "POST /api/commands": { body: QuestCommand; query: undefined; response: QuestState };
  "GET /api/planning/clock": { body: undefined; query: { "date": string; "time": string; "zone": string }; response: PlannedMoment };
  "GET /api/planning/zone": { body: undefined; query: { "zone": string }; response: ZonePreview };
};
