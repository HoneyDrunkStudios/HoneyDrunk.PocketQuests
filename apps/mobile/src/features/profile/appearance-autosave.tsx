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
import type { State } from "../../shared/contracts";
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
const appearanceFields = ["avatarCode", "badgeId", "frameId"] as const;

function remainingEdits(
  edits: Edits | null,
  owner: string | null | undefined,
  profile: State["profile"] | undefined,
  submittedKeys: Partial<Record<Field, string>>,
) {
  if (!edits || edits.owner !== owner) return null;
  if (!profile) return edits;
  const remaining = { ...edits };
  let acknowledged = false;
  for (const field of appearanceFields) {
    const value = remaining[field];
    if (
      value !== undefined &&
      value === (profile[field] ?? null) &&
      submittedKeys[field] === JSON.stringify([owner, field, value])
    ) {
      delete remaining[field];
      acknowledged = true;
    }
  }
  if (!acknowledged) return edits;
  return Object.keys(remaining).length === 1 ? null : remaining;
}

function nextAppearanceChange(
  draft: Edits | null,
  profile: State["profile"] | undefined,
) {
  if (!profile) return null;
  for (const field of appearanceFields) {
    const value = draft?.[field];
    if (value !== undefined && value !== (profile[field] ?? null))
      return { field, value };
    if (
      field === "avatarCode" &&
      !profile.onboardingComplete &&
      profile.avatarCode == null
    )
      return { field, value: starterAvatar };
  }
  return null;
}

function canSaveAppearance(
  state: State | null,
  field: Field | undefined,
  value: string | null,
) {
  if (!state) return false;
  if (field === "avatarCode")
    return canWear(decodeAvatar(value), state.entitlements);
  if (value === null) return true;
  const kind = field === "badgeId" ? "Badge" : "Frame";
  return state.entitlements.some(
    (item) => item.id === value && item.earned && item.kind === kind,
  );
}

function appearanceNotice(
  key: string | null,
  wearable: boolean,
  offline: boolean,
  failed: boolean,
) {
  if (!key) return "";
  if (!wearable)
    return "This item is no longer available. Choose something you own.";
  if (offline)
    return "Appearance changes are previewed on this device. Reconnect to save them.";
  if (failed) return "Your appearance changes have not saved. Please retry.";
  return "";
}

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
  const [submittedKeys, setSubmittedKeys] = useState<
    Partial<Record<Field, string>>
  >({});
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
  const draft = remainingEdits(edits, owner, state?.profile, submittedKeys);
  if (draft !== edits) setEdits(draft);
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
  const change = nextAppearanceChange(draft, state?.profile);
  const field = change?.field;
  const value = change?.value ?? null;
  const key = field && owner ? JSON.stringify([owner, field, value]) : null;
  const wearable = canSaveAppearance(state, field, value);
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
      setSubmittedKeys((previous) => ({ ...previous, [field]: key }));
      const rewardAction =
        field === "badgeId" ? questActions.selectBadge : questActions.selectFrame;
      const input =
        field === "avatarCode"
          ? { action: questActions.saveAvatar, avatarCode: value! }
          : { action: rewardAction, rewardId: value };
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
  const notice = appearanceNotice(key, wearable, offline, failed);
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
