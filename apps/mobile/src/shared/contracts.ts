import type * as Api from "../api/generated";
import type { QuestAction } from "../features/quests/commands/quest-actions";
export type {
  Rank,
  Share,
  Balance,
  CustomSkill,
  Experience,
  Cadence,
  PlannedMoment,
  ZonePreview,
} from "../api/generated";
export type Named = Api.NamedItem;
export type Quest = Api.Quest;
export type Definition = Api.QuestDefinition & { pendingSave?: boolean };
export type Series = Api.QuestSeries;
export type OccurrenceView = Api.OccurrenceView & {
  pendingCompletion?: boolean;
  pendingTimingVerification?: boolean;
};
export type State = Omit<Api.QuestState, "occurrences" | "definitions"> & {
  occurrences: OccurrenceView[];
  definitions: Definition[];
};
export type Catalog = Api.CatalogResponse;
// The local queue always retains the full preview terms; the API also accepts
// omitted optional input fields when a different client creates a command.
export type Command = Omit<
  Api.QuestCommand,
  "action" | "definition" | "acceptedQuest" | "recordedTime" | "expectedRevision"
> & {
  action: QuestAction;
  definition?: Quest;
  acceptedQuest?: Quest;
  expectedRevision?: number | null;
  // This device produces numeric clock evidence. Other wire clients may send
  // numeric strings, so retain those in the generated input contract only.
  recordedTime?: Omit<
    Api.RecordedActionTime,
    "ordinal" | "elapsedMilliseconds"
  > & {
    ordinal: number;
    elapsedMilliseconds: number;
  };
};
// Local-only evidence. OS epoch identifiers are never part of the wire proof.
export type ClockSample = {
  kind: "android-elapsed-realtime-v1" | "js-process-monotonic-v1";
  epoch: string;
  elapsedMilliseconds: number;
  deviceUtc: string;
};
export type Anchor = Omit<Api.SyncAnchor, "recordedTimeFloor"> & {
  // Optional only for a previously saved local generation.
  recordedTimeFloor?: string | null;
  monotonic: number;
  ordinal: number;
  clock?: ClockSample;
  lastElapsedMilliseconds?: number;
};
