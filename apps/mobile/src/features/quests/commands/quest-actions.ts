// Stable wire names shared by command producers, offline replay, and the API.
export const questActions = {
  accept: "accept",
  complete: "complete",
  undo: "undo",
  saveDefinition: "save-definition",
  archiveDefinition: "archive-definition",
  assessSkill: "assess-skill",
  saveSkill: "save-skill",
  archiveSkill: "archive-skill",
  interests: "interests",
  finishOnboarding: "finish-onboarding",
  plan: "plan",
  zone: "zone",
  expiryWarnings: "expiry-warnings",
  link: "link",
  selectBadge: "select-badge",
  selectFrame: "select-frame",
  saveSeries: "save-series",
  stopSeries: "stop-series",
  pause: "pause",
  resume: "resume",
  resumeOccurrence: "resume-occurrence",
  abandon: "abandon",
  acceptOffer: "accept-offer",
} as const;

export type QuestAction = (typeof questActions)[keyof typeof questActions];

// Only these actions have a local replay projection and trusted-time reconciliation.
export const offlineQuestActions: ReadonlySet<QuestAction> = new Set([
  questActions.saveDefinition,
  questActions.accept,
  questActions.complete,
  questActions.undo,
]);
