import { Text, View } from "react-native";
import { Button, useAppTheme } from "../../shared/ui";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  useAccountSnapshot,
  useSessionActions,
  useSessionStatus,
} from "../../session/session";
import { questActions } from "../quests/commands/quest-actions";
import { canWear, decodeAvatar, starterAvatar } from "./avatar-look";

type Field = "avatarCode" | "badgeId" | "frameId";
type Edits = {
  owner: string;
  avatarCode?: string;
  badgeId?: string | null;
  frameId?: string | null;
};
type Appearance = {
  previewAvatar?: string;
  avatarCode: string;
  badgeId: string | null;
  frameId: string | null;
  notice: string;
  failed: boolean;
  changeAvatar(code: string): void;
  changeReward(kind: "Badge" | "Frame", id: string | null): void;
  retry(): void;
};
const AppearanceContext = createContext<Appearance | null>(null);

/** Three cosmetic fields, one quiet single-flight writer. Drafts survive screen navigation. */
export function AppearanceAutosaveProvider({
  children,
}: Readonly<{ children: ReactNode }>) {
  const { state } = useAccountSnapshot();
  const { command } = useSessionActions();
  const {
    appearanceOwnerId: owner,
    signedIn,
    offline,
    busy,
    pending,
    queuedCount,
  } = useSessionStatus();
  const [edits, setEdits] = useState<Edits | null>(null);
  const [attempted, setAttempted] = useState<string | null>(null);
  const [failure, setFailure] = useState<{ owner: string; key: string } | null>(
    null,
  );
  const [flight, setFlight] = useState<{ owner: string; key: string } | null>(
    null,
  );
  const lock = useRef(false);
  const currentOwner = useRef(owner);
  const alive = useRef(0);
  const epoch = useRef(0);
  useEffect(() => {
    currentOwner.current = owner;
  }, [owner]);
  useEffect(() => {
    const instance = ++epoch.current;
    alive.current = instance;
    return () => {
      if (alive.current === instance) alive.current = 0;
    };
  }, []);
  if (edits && edits.owner !== owner) setEdits(null);
  else if (edits && state) {
    const confirmed = { ...edits };
    for (const field of ["avatarCode", "badgeId", "frameId"] as const) {
      if (
        confirmed[field] !== undefined &&
        confirmed[field] === (state.profile[field] ?? null) &&
        attempted === JSON.stringify([owner, field, confirmed[field]])
      )
        delete confirmed[field];
    }
    if (Object.keys(confirmed).length !== Object.keys(edits).length) {
      setEdits(Object.keys(confirmed).length === 1 ? null : confirmed);
    }
  }
  const draft = edits?.owner === owner ? edits : null;
  const avatarCode =
    draft?.avatarCode ?? state?.profile.avatarCode ?? starterAvatar;
  const badgeId =
    draft?.badgeId === undefined
      ? (state?.profile.badgeId ?? null)
      : draft.badgeId;
  const frameId =
    draft?.frameId === undefined
      ? (state?.profile.frameId ?? null)
      : draft.frameId;
  let field: Field | null = null;
  let value: string | null = null;
  if (
    state &&
    (draft?.avatarCode !== undefined ||
      (!state.profile.onboardingComplete &&
        state.profile.avatarCode == null)) &&
    avatarCode !== state.profile.avatarCode
  ) {
    field = "avatarCode";
    value = avatarCode;
  } else if (
    state &&
    draft?.badgeId !== undefined &&
    badgeId !== (state.profile.badgeId ?? null)
  ) {
    field = "badgeId";
    value = badgeId;
  } else if (
    state &&
    draft?.frameId !== undefined &&
    frameId !== (state.profile.frameId ?? null)
  ) {
    field = "frameId";
    value = frameId;
  }
  const key = field && owner ? JSON.stringify([owner, field, value]) : null;
  const wearable =
    !!state &&
    (field === "avatarCode"
      ? canWear(decodeAvatar(value), state.entitlements)
      : value === null ||
        state.entitlements.some(
          (item) =>
            item.id === value &&
            item.earned &&
            item.kind === (field === "badgeId" ? "Badge" : "Frame"),
        ));
  useEffect(() => {
    if (
      !state ||
      !owner ||
      !signedIn ||
      !field ||
      !key ||
      !wearable ||
      offline ||
      busy ||
      pending ||
      queuedCount > 0 ||
      flight ||
      attempted === key
    )
      return;
    const instance = alive.current;
    const timer = setTimeout(() => {
      if (currentOwner.current !== owner || !alive.current || lock.current)
        return;
      lock.current = true;
      setFlight({ owner, key });
      setAttempted(key);
      const input =
        field === "avatarCode"
          ? { action: questActions.saveAvatar, avatarCode: value! }
          : {
              action:
                field === "badgeId"
                  ? questActions.selectBadge
                  : questActions.selectFrame,
              rewardId: value,
            };
      void command(input, owner)
        .catch(() => {
          if (alive.current === instance && currentOwner.current === owner)
            setFailure({ owner, key });
        })
        .finally(() => {
          lock.current = false;
          if (alive.current === instance) setFlight(null);
        });
    }, 650);
    return () => clearTimeout(timer);
  }, [
    state,
    owner,
    signedIn,
    field,
    key,
    value,
    wearable,
    offline,
    busy,
    pending,
    queuedCount,
    flight,
    attempted,
    command,
  ]);
  const edit = useCallback(
    (change: Partial<Omit<Edits, "owner">>) => {
      if (!owner || currentOwner.current !== owner) return;
      setEdits((previous) => ({
        ...(previous?.owner === owner ? previous : {}),
        owner,
        ...change,
      }));
      setAttempted(null);
      setFailure(null);
    },
    [owner],
  );
  const changeAvatar = useCallback(
    (code: string) => edit({ avatarCode: code }),
    [edit],
  );
  const changeReward = useCallback(
    (kind: "Badge" | "Frame", id: string | null) => {
      if (id === (kind === "Badge" ? badgeId : frameId)) return;
      edit(kind === "Badge" ? { badgeId: id } : { frameId: id });
    },
    [edit, badgeId, frameId],
  );
  const retry = useCallback(() => {
    setAttempted(null);
    setFailure(null);
  }, []);
  const failed =
    !!key &&
    !!owner &&
    !flight &&
    !busy &&
    !pending &&
    queuedCount === 0 &&
    ((failure?.owner === owner && failure.key === key) || attempted === key);
  const notice = !key
    ? ""
    : !wearable
      ? "This item is no longer available. Choose something you own."
      : offline
        ? "Appearance changes are previewed on this device. Reconnect to save them."
        : failed
          ? "Your appearance changes have not saved. Please retry."
          : "";
  const context = useMemo(
    () => ({
      previewAvatar: draft?.avatarCode,
      avatarCode,
      badgeId,
      frameId,
      notice,
      failed,
      changeAvatar,
      changeReward,
      retry,
    }),
    [
      draft?.avatarCode,
      avatarCode,
      badgeId,
      frameId,
      notice,
      failed,
      changeAvatar,
      changeReward,
      retry,
    ],
  );
  return (
    <AppearanceContext.Provider value={context}>
      {children}
    </AppearanceContext.Provider>
  );
}

export function useAppearanceAutosave() {
  const value = useContext(AppearanceContext);
  if (!value)
    throw new Error("Appearance editing requires AppearanceAutosaveProvider.");
  return value;
}

/** A portrait can show the same local cosmetic choice while the quiet write settles. */
export function usePreviewAvatar(saved: string | null | undefined) {
  return useContext(AppearanceContext)?.previewAvatar ?? saved;
}

/** Only exceptional states get visible feedback; normal writes have no loading UI. */
export function AppearanceSaveNotice() {
  const appearance = useAppearanceAutosave();
  const { colors, styles } = useAppTheme();
  if (!appearance.notice) return null;
  return (
    <View
      style={{
        position: "absolute",
        left: 16,
        right: 16,
        bottom: 88,
        backgroundColor: colors.card,
        padding: 14,
        borderRadius: 12,
        borderWidth: 1,
        borderColor: colors.line,
        gap: 8,
      }}
    >
      <Text accessibilityRole="alert" style={styles.text}>
        {appearance.notice}
      </Text>
      {appearance.failed && (
        <Button
          title="Retry appearance save"
          secondary
          onPress={appearance.retry}
        />
      )}
    </View>
  );
}
