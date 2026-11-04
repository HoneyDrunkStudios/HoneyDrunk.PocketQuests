import { useCallback, useEffect, useState } from "react";
import { AdminApi, AdminApiError, readSession } from "./api/admin-api";
import type { AdminUserPage, Session } from "./api/admin-api";
import { Masthead } from "./components/Masthead";
import { QuestStudio } from "./features/quests/QuestStudio";
import { UserConsole } from "./features/users/UserConsole";

type AppState =
  | { kind: "checking" }
  | { kind: "signed-out"; message: string }
  | {
      kind: "ready";
      session: Session;
      api: AdminApi;
      page: AdminUserPage;
      notice: string;
    };

export function App() {
  const [state, setState] = useState<AppState>({ kind: "checking" });
  const [view, setView] = useState<"players" | "quests">("players");
  const [attempt, setAttempt] = useState(0);
  const endSession = useCallback((message: string) => {
    setView("players");
    setState({ kind: "signed-out", message });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void (async () => {
      try {
        const session = await readSession(controller.signal);
        const api = new AdminApi(session);
        let page: AdminUserPage = { users: [], nextCursor: null };
        let notice = "Select a traveler to inspect their account.";
        try {
          page = await api.users("", null, controller.signal);
        } catch (error) {
          if (
            error instanceof AdminApiError &&
            (error.status === 401 || error.status === 403)
          )
            throw error;
          notice =
            "The directory could not be loaded. Use Search players to try again.";
        }
        if (!controller.signal.aborted)
          setState({ kind: "ready", session, api, page, notice });
      } catch (error) {
        if (!controller.signal.aborted)
          endSession(
            new URLSearchParams(location.search).has("signin")
              ? "Sign-in was not completed for the configured operator account."
              : error instanceof Error
                ? error.message
                : "The console session could not be verified.",
          );
      }
    })();
    return () => controller.abort();
  }, [attempt, endSession]);

  if (state.kind === "ready" && view === "quests")
    return (
      <QuestStudio
        session={state.session}
        api={state.api}
        onEndSession={endSession}
        onPlayers={() => {
          setView("players");
          setState({ kind: "checking" });
          setAttempt((value) => value + 1);
        }}
      />
    );
  if (state.kind === "ready")
    return (
      <UserConsole
        key={state.session.userId}
        session={state.session}
        api={state.api}
        initialPage={state.page}
        initialNotice={state.notice}
        onEndSession={endSession}
        onOpenQuests={() => setView("quests")}
      />
    );
  return (
    <>
      <Masthead />
      <main>
        <section id="signin" className="welcome">
          <p className="eyebrow">YOUR WORLD, THOUGHTFULLY MANAGED</p>
          <h1>A little care behind every adventure.</h1>
          <p>
            Manage Pocket Quests accounts through your configured studio
            sign-in.
          </p>
          <a className="button" href="/admin/login">
            Sign in to the console <span aria-hidden="true">↗</span>
          </a>
          <p className="fine">
            Only the explicitly configured operator account can access this
            console.
          </p>
          {state.kind === "signed-out" && (
            <button
              className="quiet"
              onClick={() => {
                setState({ kind: "checking" });
                setAttempt((value) => value + 1);
              }}
            >
              Check session again
            </button>
          )}
        </section>
        <p id="auth-notice" className="notice" role="alert">
          {state.kind === "checking"
            ? "Checking your console session…"
            : state.message}
        </p>
      </main>
    </>
  );
}
