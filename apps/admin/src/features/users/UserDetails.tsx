import { useEffect, useRef } from "react";
import type { AccessChange, AdminUser } from "../../api/admin-api";

export function UserDetails({
  user,
  operator,
  pending,
  locked,
  reason,
  confirmation,
  onReason,
  onReview,
  onCancel,
  onConfirm,
  onReload,
}: {
  user: AdminUser | null;
  operator: string;
  pending: boolean;
  locked: boolean;
  reason: string;
  confirmation: AccessChange | null;
  onReason: (value: string) => void;
  onReview: () => void;
  onCancel: () => void;
  onConfirm: () => void;
  onReload: () => void;
}) {
  const confirmButton = useRef<HTMLButtonElement>(null);
  const reviewButton = useRef<HTMLButtonElement>(null);
  const reloadButton = useRef<HTMLButtonElement>(null);
  const wasConfirming = useRef(false);
  useEffect(() => {
    if (pending) return;
    if (locked) reloadButton.current?.focus();
    else if (confirmation) confirmButton.current?.focus();
    else if (wasConfirming.current) reviewButton.current?.focus();
    wasConfirming.current = confirmation !== null;
  }, [confirmation, locked, pending]);
  const metadata = user
    ? [
        ["User ID", user.userId],
        ["Time zone", user.zone],
        ["Joined", new Date(user.createdAt).toLocaleDateString()],
        ["Setup", user.onboardingComplete ? "Complete" : "In progress"],
        ["Last reason", user.reason || "No access changes recorded"],
      ]
    : [];
  return (
    <section
      id="details"
      className="details panel"
      aria-labelledby="detail-heading"
    >
      <p className="eyebrow">ACCOUNT DETAILS</p>
      <h2 id="detail-heading">
        {user?.characterName ||
          (user ? "Unnamed traveler" : "Choose a traveler")}
      </h2>
      <p id="detail-intro">
        {!user
          ? "Select a player to see their profile and access status."
          : user.suspended
            ? "Pocket Quests access is suspended."
            : "Pocket Quests access is active."}
      </p>
      {user && (
        <div id="selected">
          <dl id="metadata">
            {metadata.map(([label, value]) => (
              <div className="metadata-row" key={label}>
                <dt>{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
          <div className="divider" />
          <h3>Manage access</h3>
          <p className="fine">
            Suspension blocks connected Pocket Quests access. It preserves the
            profile and progress and does not disable the person's sign-in
            account.
          </p>
          {!confirmation ? (
            <form
              id="moderate"
              onSubmit={(event) => {
                event.preventDefault();
                onReview();
              }}
            >
              <label htmlFor="reason">Private operator reason</label>
              <textarea
                id="reason"
                rows={3}
                maxLength={500}
                required
                value={reason}
                onChange={(event) => onReason(event.target.value)}
                disabled={pending}
                placeholder="Explain why you are changing this account's access."
              />
              {user.userId === operator && (
                <p id="self-note" className="fine">
                  The configured operator cannot change their own access.
                </p>
              )}
              <button
                id="review"
                ref={reviewButton}
                type="submit"
                disabled={pending || locked || user.userId === operator}
              >
                {user.suspended ? "Review reactivation" : "Review suspension"}
              </button>
            </form>
          ) : (
            <div id="confirmation" className="confirmation">
              <h3 id="confirmation-heading">
                {confirmation.suspended
                  ? "Suspend this traveler's access?"
                  : "Reactivate this traveler?"}
              </h3>
              <p id="confirmation-reason">{confirmation.reason}</p>
              <p>No quests, rewards or saved progress will be removed.</p>
              <div className="actions">
                <button
                  id="confirm"
                  ref={confirmButton}
                  disabled={pending || locked}
                  onClick={onConfirm}
                >
                  {confirmation.suspended
                    ? "Suspend access"
                    : "Reactivate access"}
                </button>
                <button
                  id="cancel"
                  className="quiet"
                  disabled={pending}
                  onClick={onCancel}
                >
                  Keep current access
                </button>
              </div>
            </div>
          )}
          {locked && (
            <button
              id="reload"
              ref={reloadButton}
              className="quiet"
              disabled={pending}
              onClick={onReload}
            >
              Reload account status
            </button>
          )}
        </div>
      )}
    </section>
  );
}
