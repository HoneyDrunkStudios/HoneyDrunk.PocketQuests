import { type CompletionFeedback } from "../features/progression/completion-feedback";
import type {
  Catalog,
  Command,
  PlannedMoment,
  State,
  ZonePreview,
} from "../shared/contracts";
import { type Credentials } from "./auth-session";
import { type RejectedCommand } from "./sync-recovery";
import { type UnverifiedAction } from "./unverified-actions";
export type AccountStatus = {
  userId: string;
  state: string;
  requestedAt: string | null;
  recoveryDeadline: string | null;
  version: number;
};
export type SessionContext = {
  inactiveAccount: AccountStatus | null;
  verifyOwner(token: string): Promise<void>;
  accountAction(
    action: "deletion" | "recovery" | "link" | "unlink",
    token: string,
    additionalToken?: string,
  ): Promise<void>;
  state: State | null;
  catalog: Catalog | null;
  signedIn: boolean;
  busy: boolean;
  error: string | null;
  pending: boolean;
  offline: boolean;
  queuedCount: number;
  recoveryRequired: boolean;
  rejected: (RejectedCommand & { label: string })[];
  discardRejected(operationId: string): Promise<void>;
  unverified: (UnverifiedAction & { label: string })[];
  discardUnverified(operationId: string): Promise<void>;
  recentCompletion: (CompletionFeedback & { until: number }) | null;
  dismissCompletion(): void;
  connect(token: string | Credentials): Promise<void>;
  refresh(): Promise<void>;
  command(command: Omit<Command, "operationId">): Promise<void>;
  retry(): Promise<void>;
  signOut(discard?: boolean): Promise<void>;
  discardPending(): Promise<void>;
  exportData(format: "json" | "csv"): Promise<void>;
  notificationStatus: string;
  previewZone(zone: string): Promise<ZonePreview>;
  previewClock(
    date: string,
    time: string,
    zone: string,
  ): Promise<PlannedMoment>;
};
