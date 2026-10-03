import type { QuestAction } from "../features/quests/commands/quest-actions";
export type Rank = "F" | "E" | "D" | "C" | "B" | "A" | "S";
export type Named = { id: string; name: string };
export type Share = { id: string; basisPoints: number };
export type Quest = {
  id: string;
  title: string;
  criterion: string;
  categoryId: string;
  rank: Rank;
  effort: string;
  attributes: Share[];
  skills: Share[];
  baseXp: number;
  isCustom: boolean;
  description?: string | null;
  penaltyPercent?: number;
};
export type Balance = Named & { xp: number; level: number };
export type OccurrenceView = {
  pendingCompletion?: boolean;
  occurrence: {
    id: string;
    quest: Quest;
    dueDate: string | null;
    deadline: string | null;
    acceptedAt: string;
    plannedTime: string | null;
    parentId: string | null;
    lifecycle: {
      seriesId: string | null;
      frozenAt: string | null;
      individuallyFrozen: boolean;
      abandonedAt: string | null;
      lockedLoss: number | null;
      lossCategoryId: string | null;
      unaccepted: boolean;
    } | null;
  };
  status:
    "Active" | "Completed" | "Missed" | "Frozen" | "Abandoned" | "Offered";
  completion: { id: string; recordedAt: string } | null;
  canUndo: boolean;
  planned?: PlannedMoment | null;
};
export type CustomSkill = Named & { revision: number; archived: boolean };
export type Experience = "New" | "Practiced" | "Experienced" | "Expert";
export type Definition = {
  quest: Quest;
  revision: number;
  archived: boolean;
  pendingSave?: boolean;
};
export type Cadence = "Days" | "Weeks" | "Months" | "Years";
export type Series = {
  id: string;
  quest: Quest;
  anchor: string;
  cadence: Cadence;
  interval: number;
  version: number;
  nextSequence: number;
  pauseDays: number;
  stopped: boolean;
  plannedTime: string | null;
  autoAcceptPenalty: boolean;
};
export type PlannedMoment = {
  requested: string;
  resolved: string;
  instant: string;
  adjusted: boolean;
  repeated: boolean;
};
export type ZonePreview = {
  previousZone: string;
  zone: string;
  deadlines: {
    occurrenceId: string;
    title: string;
    deadline: string;
    becomesMissed: boolean;
  }[];
};
export type State = {
  completionOutcome?: {
    completionId: string;
    occurrenceId: string;
    levelUps: {
      track: string;
      trackId: string;
      name: string;
      from: number;
      to: number;
    }[];
    rankUp: Rank | null;
    unlocks: { id: string; name: string }[];
  } | null;
  ledger?: {
    eventId: string;
    occurrenceId: string;
    at: string;
    track: "Overall" | "Category" | "Attribute" | "Skill";
    trackId: string;
    amount: number;
  }[];
  futureWarnings?: string[] | null;
  schedule: {
    series: Series[];
    pauses: { categoryId: string; startedAt: string; endedAt: string | null }[];
    pausedCategories: string[];
    accountPaused: boolean;
  };
  penalties: {
    occurrenceId: string;
    categoryId: string;
    lockedLoss: number;
    actualLoss: number;
    at: string;
  }[];
  definitions: Definition[];
  profile: {
    customSkills?: CustomSkill[] | null;
    assessmentHistory?:
      { skillId: string; experience: Experience; at: string }[] | null;
    interests: string[];
    assessments: Record<string, Experience>;
    onboardingComplete: boolean;
    expiryWarnings?: boolean;
    zoneHistory?: { from: string; to: string; at: string }[] | null;
    badgeId: string | null;
    frameId: string | null;
  };
  zone: string;
  today: string;
  occurrences: OccurrenceView[];
  overallXp: number;
  overallLevel: number;
  categories: Balance[];
  attributes: Balance[];
  skills: Balance[];
  rank: {
    current: Rank;
    requirement: { rank: Rank; count: number; floor: number; total: number };
    qualifyingCategories: number;
    total: number;
  };
  streaks: {
    categoryId: string;
    days: number;
    qualifiedToday: boolean;
    rate: number;
  }[];
  entitlements: {
    id: string;
    name: string;
    kind: string;
    count: number;
    requiredCount: number;
    requiredRank: Rank;
    earned: boolean;
  }[];
};
export type Catalog = {
  categories: Named[];
  attributes: Named[];
  skills: Named[];
  quests: Quest[];
};
export type Anchor = {
  id: string;
  deviceId: string;
  bootId: string;
  serverUtc: string;
  // Server-owned issuance floor; elapsed time remains relative to serverUtc.
  recordedTimeFloor?: string | null;
  deviceUtc: string;
  monotonic: number;
  ordinal: number;
};
export type Command = {
  recordedTime?: {
    anchorId: string;
    bootId: string;
    ordinal: number;
    elapsedMilliseconds: number;
    deviceUtc: string;
  };
  operationId: string;
  action: QuestAction;
  seriesId?: string;
  cadence?: Cadence;
  interval?: number;
  categoryId?: string | null;
  confirmPenalty?: boolean;
  acceptedLoss?: number;
  acceptedQuest?: Quest;
  newZone?: string;
  expectedZone?: string;
  confirmZoneChange?: boolean;
  expiryWarnings?: boolean;
  definition?: Quest;
  expectedRevision?: number;
  skillId?: string;
  skillName?: string;
  experience?: Experience;
  interests?: string[];
  plannedTime?: string | null;
  parentId?: string;
  rewardId?: string | null;
  occurrenceId?: string;
  questId?: string;
  dueDate?: string | null;
  completionId?: string;
};
