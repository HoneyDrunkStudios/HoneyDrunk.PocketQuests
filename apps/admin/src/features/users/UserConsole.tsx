import { useEffect, useReducer, useRef } from "react";
import { AdminApi, AdminApiError } from "../../api/admin-api";
import type {
  AccessChange,
  AdminUser,
  AdminUserPage,
  Session,
} from "../../api/admin-api";
import { Masthead } from "../../components/Masthead";
import { UserDetails } from "./UserDetails";
import { UserDirectory } from "./UserDirectory";

type State = {
  rows: AdminUser[];
  cursor: string | null;
  query: string;
  input: string;
  selected: AdminUser | null;
  reason: string;
  confirmation: AccessChange | null;
  pending: boolean;
  locked: boolean;
  notice: string;
  error: boolean;
};
type Action =
  | { type: "input" | "reason"; value: string }
  | { type: "pending"; value: boolean }
  | { type: "select"; user: AdminUser }
  | { type: "review"; change: AccessChange }
  | { type: "cancel" }
  | { type: "failure"; message: string; lock: boolean }
  | { type: "page"; page: AdminUserPage; query: string; append: boolean }
  | { type: "updated"; user: AdminUser; reloaded: boolean }
  | { type: "gone"; userId: string };

function accessStatusNotice(reloaded: boolean, suspended: boolean) {
  if (reloaded)
    return "Status reloaded. Review your reason before making a new change.";
  return suspended
    ? "Access suspended. The profile and progress are preserved."
    : "Access reactivated. The traveler can reconnect.";
}

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case "input":
      return { ...state, input: action.value };
    case "reason":
      return { ...state, reason: action.value };
    case "pending":
      return { ...state, pending: action.value };
    case "select":
      return {
        ...state,
        selected: action.user,
        reason: "",
        confirmation: null,
      };
    case "review":
      return { ...state, confirmation: action.change };
    case "cancel":
      return { ...state, confirmation: null };
    case "failure":
      return {
        ...state,
        notice: action.message,
        error: true,
        locked: state.locked || action.lock,
      };
    case "page": {
      const rows = [
        ...new Map(
          [...(action.append ? state.rows : []), ...action.page.users].map(
            (user) => [user.userId, user],
          ),
        ).values(),
      ];
      return {
        ...state,
        rows,
        cursor: action.page.nextCursor,
        query: action.query,
        selected: action.append ? state.selected : null,
        reason: action.append ? state.reason : "",
        confirmation: null,
        notice: rows.length
          ? "Select a traveler to inspect their account."
          : "No players found. Try a different name or user ID.",
        error: false,
      };
    }
    case "updated":
      return {
        ...state,
        rows: state.rows.map((user) =>
          user.userId === action.user.userId ? action.user : user,
        ),
        selected: action.user,
        reason: action.reloaded ? state.reason : "",
        confirmation: null,
        locked: false,
        error: false,
        notice: accessStatusNotice(action.reloaded, action.user.suspended),
      };
    case "gone":
      return {
        ...state,
        rows: state.rows.filter((user) => user.userId !== action.userId),
        selected: null,
        reason: "",
        confirmation: null,
        locked: false,
        error: true,
        notice:
          "This profile is no longer available. Search for another traveler.",
      };
  }
}

export function UserConsole({
  session,
  api,
  initialPage,
  initialNotice,
  onEndSession,
}: Readonly<{
  session: Session;
  api: AdminApi;
  initialPage: AdminUserPage;
  initialNotice: string;
  onEndSession: (message: string) => void;
}>) {
  const [state, dispatch] = useReducer(reducer, {
    rows: initialPage.users,
    cursor: initialPage.nextCursor,
    query: "",
    input: "",
    selected: null,
    reason: "",
    confirmation: null,
    pending: false,
    locked: false,
    notice: initialNotice,
    error: false,
  });
  const busy = useRef(false);
  const requests = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    requests.current = controller;
    return () => controller.abort();
  }, []);

  async function perform<T>(
    operation: (signal: AbortSignal) => Promise<T>,
    success: (value: T) => void,
    uncertainChange = false,
  ) {
    const signal = requests.current?.signal;
    if (busy.current || !signal || signal.aborted) return;
    busy.current = true;
    dispatch({ type: "pending", value: true });
    try {
      const value = await operation(signal);
      if (!signal.aborted) success(value);
    } catch (error) {
      if (signal.aborted) return;
      if (
        error instanceof AdminApiError &&
        (error.status === 401 || error.status === 403)
      )
        onEndSession(error.message);
      else
        dispatch({
          type: "failure",
          message:
            error instanceof Error
              ? error.message
              : "The request could not be confirmed. Reload before retrying.",
          lock: uncertainChange,
        });
    } finally {
      if (!signal.aborted) {
        busy.current = false;
        dispatch({ type: "pending", value: false });
      }
    }
  }

  function search(append = false) {
    if (state.locked) return;
    const query = append ? state.query : state.input.trim();
    void perform(
      (signal) => api.users(query, append ? state.cursor : null, signal),
      (page) => dispatch({ type: "page", page, query, append }),
    );
  }

  function review() {
    if (
      busy.current ||
      state.locked ||
      !state.selected ||
      state.selected.userId === session.userId ||
      !state.reason.trim()
    )
      return;
    dispatch({
      type: "review",
      change: {
        userId: state.selected.userId,
        suspended: !state.selected.suspended,
        reason: state.reason.trim(),
        expectedRevision: state.selected.revision,
      },
    });
  }

  function confirm() {
    const change = state.confirmation;
    if (!change || state.locked) return;
    void perform(
      (signal) => api.setAccess(change, signal),
      (user) => dispatch({ type: "updated", user, reloaded: false }),
      true,
    );
  }

  function reload() {
    const userId = state.selected?.userId;
    if (!userId) return;
    void perform(
      (signal) => api.users(userId, null, signal),
      (page) => {
        const user = page.users.find((row) => row.userId === userId);
        dispatch(
          user
            ? { type: "updated", user, reloaded: true }
            : { type: "gone", userId },
        );
      },
    );
  }

  return (
    <>
      <Masthead
        userId={session.userId}
        pending={state.pending}
        onSignOut={() =>
          void perform(
            (signal) => api.logout(signal),
            () => onEndSession("You have signed out of the console."),
          )
        }
      />
      <main>
        <section id="console">
          <nav className="section-nav" aria-label="Administration">
            <span aria-current="page">Players</span>
          </nav>
          <div className="page-heading">
            <div>
              <p className="eyebrow">PLAYER CARE</p>
              <h1>Your travelers</h1>
              <p>
                Find an account, inspect its profile, and manage access with a
                clear record.
              </p>
            </div>
            <span className="scope">Pocket Quests access only</span>
          </div>
          <form
            id="search"
            className="search-bar"
            onSubmit={(event) => {
              event.preventDefault();
              search();
            }}
          >
            <label htmlFor="query">Find a traveler</label>
            <div>
              <input
                id="query"
                type="search"
                maxLength={120}
                autoComplete="off"
                placeholder="Character name or user ID"
                value={state.input}
                disabled={state.locked}
                onChange={(event) =>
                  dispatch({ type: "input", value: event.target.value })
                }
              />
              <button type="submit" disabled={state.pending || state.locked}>
                Search players
              </button>
            </div>
          </form>
          <p
            id="notice"
            className={`notice${state.error ? " error" : ""}`}
            role="status"
            aria-live="polite"
          >
            {state.pending ? "Loading…" : state.notice}
          </p>
          <div className="workspace" aria-busy={state.pending}>
            <UserDirectory
              rows={state.rows}
              selectedId={state.selected?.userId}
              pending={state.pending}
              locked={state.locked}
              more={state.cursor !== null}
              onSelect={(user) => {
                if (!busy.current && !state.locked)
                  dispatch({ type: "select", user });
              }}
              onMore={() => search(true)}
            />
            <UserDetails
              user={state.selected}
              operator={session.userId}
              pending={state.pending}
              locked={state.locked}
              reason={state.reason}
              confirmation={state.confirmation}
              onReason={(value) => dispatch({ type: "reason", value })}
              onReview={review}
              onConfirm={confirm}
              onReload={reload}
              onCancel={() => {
                if (!busy.current) dispatch({ type: "cancel" });
              }}
            />
          </div>
          <footer>
            Private studio tooling · Explicit changes, preserved progress.
          </footer>
        </section>
      </main>
    </>
  );
}
