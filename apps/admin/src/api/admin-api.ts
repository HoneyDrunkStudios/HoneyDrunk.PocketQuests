// Generated contracts and a pure schema matcher are shared with the mobile client;
// the administrative bundle has no Expo or native-platform dependency.
import { createSchemaMatcher } from "../../../mobile/src/api/schema/matcher";
import schemas from "../../../mobile/src/api/runtime-schemas.json";
import type {
  ApiOperations,
  AdminSystemQuest,
  SystemQuestStudio,
  SaveSystemQuestRequest,
  PublishSystemQuestRequest,
  Quest,
  AdminUser,
  AdminUserPage,
} from "../../../mobile/src/api/generated";
export type {
  AdminSystemQuest,
  SystemQuestStudio,
  SaveSystemQuestRequest,
  PublishSystemQuestRequest,
  Quest,
  AdminUser,
  AdminUserPage,
};
const responseMatcher = createSchemaMatcher(schemas);
function decodeResponse<K extends keyof ApiOperations>(
  operation: K,
  value: unknown,
): ApiOperations[K]["response"] {
  if (!responseMatcher.matchesResponse(operation, value))
    throw new Error(
      "The studio response could not be verified. Reload the saved version before another change.",
    );
  return value as ApiOperations[K]["response"];
}
export type Session = { userId: string; csrfToken: string };
export type AccessChange = {
  userId: string;
  suspended: boolean;
  reason: string;
  expectedRevision: number;
};

export class AdminApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

function accessFailureMessage(status: number) {
  if (status === 401 || status === 403)
    return "This session cannot access the console. Sign in with the configured operator account.";
  if (status === 409)
    return "This account changed. Reload its status before making another change. Your reason is kept.";
  return "The request could not be confirmed. Reload the account status before retrying a change.";
}

async function serverFailureMessage(response: Response) {
  let message = accessFailureMessage(response.status);
  if (response.status === 400) {
    try {
      const problem: unknown = await response.json();
      if (
        isRecord(problem) &&
        typeof problem.detail === "string" &&
        problem.detail.length <= 2000
      )
        message = problem.detail;
    } catch {
      /* Retain the generic message for an unreadable problem. */
    }
  }
  return message;
}

async function request(
  path: string,
  csrfToken: string,
  signal: AbortSignal,
  body?: unknown,
): Promise<unknown> {
  const response = await fetch(`/api/admin/${path}`, {
    method: body === undefined ? "GET" : "POST",
    credentials: "same-origin",
    headers:
      body === undefined
        ? {}
        : { "Content-Type": "application/json", "X-CSRF-TOKEN": csrfToken },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.any([signal, AbortSignal.timeout(15000)]),
  });
  if (!response.ok) {
    const message = await serverFailureMessage(response);
    throw new AdminApiError(response.status, message);
  }
  return response.status === 204 ? null : response.json();
}

export async function readSession(signal: AbortSignal): Promise<Session> {
  const value = await request("session", "", signal);
  if (
    !isRecord(value) ||
    !validUserId(value.userId) ||
    typeof value.csrfToken !== "string" ||
    !value.csrfToken
  )
    throw new Error("The console session could not be verified.");
  return { userId: value.userId, csrfToken: value.csrfToken };
}

export class AdminApi {
  constructor(private readonly session: Session) {}

  async studio(signal: AbortSignal): Promise<SystemQuestStudio> {
    const value = await request(
      "quests/list",
      this.session.csrfToken,
      signal,
      {},
    );
    return decodeResponse("POST /api/admin/quests/list", value);
  }

  async previewQuest(
    body: SaveSystemQuestRequest,
    signal: AbortSignal,
  ): Promise<Quest> {
    const value = await request(
      "quests/preview",
      this.session.csrfToken,
      signal,
      body,
    );
    return decodeResponse("POST /api/admin/quests/preview", value);
  }

  async saveQuest(
    body: SaveSystemQuestRequest,
    signal: AbortSignal,
  ): Promise<AdminSystemQuest> {
    const value = await request(
      "quests/draft",
      this.session.csrfToken,
      signal,
      body,
    );
    const saved = decodeResponse("POST /api/admin/quests/draft", value);
    if (saved.id !== body.id || saved.draft.id !== body.quest.id)
      throw new Error(
        "The saved draft identity could not be verified. Reload before another save.",
      );
    return saved;
  }

  async users(
    query: string,
    after: string | null,
    signal: AbortSignal,
  ): Promise<AdminUserPage> {
    const page = await request("users/search", this.session.csrfToken, signal, {
      query,
      after,
    });
    if (
      !isRecord(page) ||
      !Array.isArray(page.users) ||
      page.users.length > 50 ||
      !page.users.every(validUser) ||
      (page.nextCursor !== null && !validUserId(page.nextCursor))
    )
      throw new Error(
        "The directory response could not be read. Try loading it again.",
      );
    return { users: page.users, nextCursor: page.nextCursor };
  }

  async setAccess(
    change: AccessChange,
    signal: AbortSignal,
  ): Promise<AdminUser> {
    const user = await request(
      "users/access",
      this.session.csrfToken,
      signal,
      change,
    );
    if (
      !validUser(user) ||
      user.userId !== change.userId ||
      user.suspended !== change.suspended ||
      user.revision !== change.expectedRevision + 1
    )
      throw new Error(
        "The updated account status could not be verified. Reload it before another change.",
      );
    return user;
  }

  async publishQuest(
    body: PublishSystemQuestRequest,
    signal: AbortSignal,
  ): Promise<AdminSystemQuest> {
    const value = decodeResponse(
      "POST /api/admin/quests/publish",
      await request("quests/publish", this.session.csrfToken, signal, body),
    );
    if (
      value.id !== body.id ||
      (body.published
        ? value.publishedSystemQuestRevisionId !== body.expectedDraftRevisionId
        : value.publishedSystemQuestRevisionId !== null)
    )
      throw new Error(
        "The publication result could not be verified. Reload the saved version before another change.",
      );
    return value;
  }

  async logout(signal: AbortSignal): Promise<void> {
    await request("logout", this.session.csrfToken, signal, {});
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function validUserId(value: unknown): value is string {
  return (
    typeof value === "string" && /^usr_[0-9A-HJKMNP-TV-Z]{26}$/.test(value)
  );
}

function validUser(value: unknown): value is AdminUser {
  return (
    isRecord(value) &&
    validUserId(value.userId) &&
    (value.characterName === null || typeof value.characterName === "string") &&
    typeof value.zone === "string" &&
    typeof value.suspended === "boolean" &&
    typeof value.onboardingComplete === "boolean" &&
    typeof value.revision === "number" &&
    Number.isInteger(value.revision) &&
    value.revision >= 0 &&
    typeof value.createdAt === "string" &&
    Number.isFinite(Date.parse(value.createdAt)) &&
    (value.reason === null || typeof value.reason === "string")
  );
}
