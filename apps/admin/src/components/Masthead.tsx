export function Masthead({
  userId,
  pending,
  onSignOut,
}: Readonly<{
  userId?: string;
  pending?: boolean;
  onSignOut?: () => void;
}>) {
  return (
    <header className="masthead">
      <a className="brand" href="/admin/index.html">
        <span className="mark" aria-hidden="true">
          PQ
        </span>
        <span>
          Pocket Quests<small>STUDIO CONSOLE</small>
        </span>
      </a>
      <div className="account">
        <span id="operator">{userId ?? "Private administration"}</span>
        {onSignOut && (
          <button
            id="signout"
            className="quiet"
            disabled={pending}
            onClick={onSignOut}
          >
            Sign out
          </button>
        )}
      </div>
    </header>
  );
}
