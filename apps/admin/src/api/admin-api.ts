// DTO types come from the existing endpoint-derived generator; this import has
// no mobile runtime dependency and cannot grant administrative access.
import type {
  AdminUser,
  AdminUserPage,
} from "../../../mobile/src/api/generated";

export type { AdminUser, AdminUserPage };
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
    const message = accessFailureMessage(response.status);
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
