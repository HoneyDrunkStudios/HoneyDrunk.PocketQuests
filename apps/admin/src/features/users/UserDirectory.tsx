import type { AdminUser } from "../../api/admin-api";

export function UserDirectory({
  rows,
  selectedId,
  pending,
  locked,
  more,
  onSelect,
  onMore,
}: Readonly<{
  rows: AdminUser[];
  selectedId?: string;
  pending: boolean;
  locked: boolean;
  more: boolean;
  onSelect: (user: AdminUser) => void;
  onMore: () => void;
}>) {
  return (
    <section className="directory panel" aria-labelledby="directory-heading">
      <div className="panel-heading">
        <h2 id="directory-heading">Player directory</h2>
        <span id="count">{rows.length} shown</span>
      </div>
      <div id="users" className="user-list">
        {rows.map((user) => (
          <button
            key={user.userId}
            className="user-row"
            aria-pressed={selectedId === user.userId}
            disabled={pending || locked}
            onClick={() => onSelect(user)}
          >
            <span>
              <span className="user-name">
                {user.characterName || "Unnamed traveler"}
              </span>
              <span className="user-id">{user.userId}</span>
            </span>
            <span className={`status${user.suspended ? " suspended" : ""}`}>
              {user.suspended ? "Suspended" : "Active"}
            </span>
          </button>
        ))}
      </div>
      {rows.length === 0 && (
        <p id="empty" className="empty">
          No players match this search.
        </p>
      )}
      {more && (
        <button
          id="more"
          className="quiet"
          disabled={pending || locked}
          onClick={onMore}
        >
          Load more players
        </button>
      )}
    </section>
  );
}
