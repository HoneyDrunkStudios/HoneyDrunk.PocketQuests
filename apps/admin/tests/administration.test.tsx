import { StrictMode } from "react";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../src/App";
import { AdminApi } from "../src/api/admin-api";
import type { AccessChange, AdminUser } from "../src/api/admin-api";

const operator = "usr_" + "A".repeat(26);
const initial: AdminUser = {
  userId: "usr_" + "B".repeat(26),
  characterName: "River <script>private</script>",
  zone: "Etc/UTC",
  createdAt: "2026-01-01T12:00:00Z",
  onboardingComplete: true,
  suspended: false,
  reason: null,
  revision: 0,
};
let current: AdminUser;
let authenticated: boolean;
let mutationStatus: number;
let searchStatus: number;
let missing: boolean;
let malformed: boolean;
let writes: AccessChange[];

beforeEach(() => {
  current = { ...initial };
  authenticated = true;
  mutationStatus = 200;
  searchStatus = 200;
  missing = false;
  malformed = false;
  writes = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: string, options: RequestInit) => {
      const body = options.body ? JSON.parse(String(options.body)) : null;
      const path = input.split("/").pop();
      const reply = (status: number, value: unknown) =>
        new Response(status === 204 ? null : JSON.stringify(value), {
          status,
          headers: { "Content-Type": "application/json" },
        });
      if (!authenticated) return reply(401, {});
      if (path === "session")
        return reply(200, { userId: operator, csrfToken: "fixture-only" });
      expect(new Headers(options.headers).get("X-CSRF-TOKEN")).toBe(
        "fixture-only",
      );
      if (path === "search")
        return reply(
          searchStatus,
          malformed
            ? { users: [{}] }
            : {
                users: missing
                  ? []
                  : [
                      current,
                      {
                        ...initial,
                        userId: operator,
                        characterName: "Operator",
                      },
                    ],
                nextCursor: body.after ? null : operator,
              },
        );
      if (path === "access") {
        writes.push(body);
        if (mutationStatus !== 200) return reply(mutationStatus, {});
        current = {
          ...current,
          suspended: body.suspended,
          reason: body.reason,
          revision: current.revision + 1,
        };
        return reply(200, malformed ? { ...current, revision: 0 } : current);
      }
      if (path === "logout") return reply(204, null);
      throw new Error("Unexpected fixture route");
    }),
  );
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

async function openPlayer() {
  render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
  fireEvent.click(await screen.findByRole("button", { name: /River/ }));
  return screen.getByRole("textbox", { name: "Private operator reason" });
}

async function review(reason: string, action = "suspension") {
  fireEvent.change(
    screen.getByRole("textbox", { name: "Private operator reason" }),
    { target: { value: reason } },
  );
  fireEvent.click(screen.getByRole("button", { name: `Review ${action}` }));
}

describe("configured-operator console", () => {
  it("reviews and cancels before mutation, preserves a conflicted reason, reloads, and reactivates", async () => {
    await openPlayer();
    expect(document.querySelector("#details script")).toBeNull();
    await review("Private review");
    expect(writes).toHaveLength(0);
    fireEvent.click(
      screen.getByRole("button", { name: "Keep current access" }),
    );
    expect(writes).toHaveLength(0);
    await review("Private review");
    fireEvent.click(screen.getByRole("button", { name: "Suspend access" }));
    await screen.findByText(
      "Access suspended. The profile and progress are preserved.",
    );
    expect(writes).toHaveLength(1);
    await review("Review complete", "reactivation");
    mutationStatus = 409;
    fireEvent.click(screen.getByRole("button", { name: "Reactivate access" }));
    await screen.findByRole("button", { name: "Reload account status" });
    expect(
      (screen.getByRole("button", { name: /River/ }) as HTMLButtonElement)
        .disabled,
    ).toBe(true);
    fireEvent.click(
      screen.getByRole("button", { name: "Reload account status" }),
    );
    await screen.findByText(
      "Status reloaded. Review your reason before making a new change.",
    );
    expect(
      (
        screen.getByRole("textbox", {
          name: "Private operator reason",
        }) as HTMLTextAreaElement
      ).value,
    ).toBe("Review complete");
    mutationStatus = 200;
    fireEvent.click(
      screen.getByRole("button", { name: "Review reactivation" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Reactivate access" }));
    await screen.findByText("Access reactivated. The traveler can reconnect.");
    fireEvent.click(screen.getByRole("button", { name: /Operator/ }));
    expect(
      (
        screen.getByRole("button", {
          name: "Review suspension",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(true);
  });

  it("paginates, searches with literal text, and clears private controls on logout and expiry", async () => {
    await openPlayer();
    fireEvent.click(screen.getByRole("button", { name: "Load more players" }));
    await waitFor(() =>
      expect(
        screen.queryByRole("button", { name: "Load more players" }),
      ).toBeNull(),
    );
    fireEvent.change(screen.getByRole("searchbox"), {
      target: { value: "literal % name" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Search players" }));
    await waitFor(() =>
      expect(
        (
          screen.getByRole("button", {
            name: "Search players",
          }) as HTMLButtonElement
        ).disabled,
      ).toBe(false),
    );
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await screen.findByText("You have signed out of the console.");
    expect(screen.queryByRole("searchbox")).toBeNull();
    fireEvent.click(
      screen.getByRole("button", { name: "Check session again" }),
    );
    await screen.findByRole("searchbox");
    authenticated = false;
    fireEvent.click(screen.getByRole("button", { name: "Search players" }));
    await screen.findByText(/This session cannot access/);
    expect(screen.queryByText(initial.userId)).toBeNull();
    expect(
      screen.queryByRole("textbox", { name: "Private operator reason" }),
    ).toBeNull();
  });

  it("locks an uncertain malformed success and can recover when the profile has been erased", async () => {
    await openPlayer();
    await review("Retained reason");
    malformed = true;
    fireEvent.click(screen.getByRole("button", { name: "Suspend access" }));
    await screen.findByText(/updated account status could not be verified/);
    malformed = false;
    missing = true;
    fireEvent.click(
      screen.getByRole("button", { name: "Reload account status" }),
    );
    await screen.findByText(
      "This profile is no longer available. Search for another traveler.",
    );
    expect(screen.queryByText("Retained reason")).toBeNull();
    expect(
      (
        screen.getByRole("button", {
          name: "Search players",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(false);
  });

  it("recovers directory failures and handles an expired session on a change", async () => {
    searchStatus = 500;
    render(<App />);
    await screen.findByText(
      "The directory could not be loaded. Use Search players to try again.",
    );
    searchStatus = 200;
    fireEvent.click(screen.getByRole("button", { name: "Search players" }));
    fireEvent.click(await screen.findByRole("button", { name: /River/ }));
    await review("Never published");
    authenticated = false;
    fireEvent.click(screen.getByRole("button", { name: "Suspend access" }));
    await screen.findByText(/This session cannot access/);
    expect(screen.queryByText("Never published")).toBeNull();
  });

  it("rejects malformed directory and non-success responses at the API boundary", async () => {
    const api = new AdminApi({ userId: operator, csrfToken: "fixture-only" });
    const signal = new AbortController().signal;
    malformed = true;
    await expect(api.users("", null, signal)).rejects.toThrow(
      /directory response could not be read/,
    );
    malformed = false;
    searchStatus = 503;
    await expect(api.users("", null, signal)).rejects.toThrow(
      /request could not be confirmed/,
    );
  });
});
