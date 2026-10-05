import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";
import type { Command } from "../shared/contracts";
import { isRecord, isStoredCommand } from "../api/decode";
import type { Credentials } from "./auth-session";
export type Session = Credentials & {
  userId: string;
  // Empty while an exchange is unresolved; isolate its replacement until the
  // original account is verified. Both forms belong only in secure storage.
  renewal?: { credentials?: Credentials };
};
export type Pending = { userId: string; command: Command };
const webStorage = new Map<string, string>();
let cleanupWrites: Promise<void> = Promise.resolve();
async function read(key: string): Promise<unknown> {
  const value =
    Platform.OS === "web"
      ? webStorage.get(key)
      : await SecureStore.getItemAsync(key);
  return value ? JSON.parse(value) : null;
}
function isCredentials(
  value: unknown,
): value is Credentials & Record<string, unknown> {
  return (
    isRecord(value) &&
    typeof value.token === "string" &&
    ["refreshToken", "authority", "clientId"].every(
      (key) => value[key] === undefined || typeof value[key] === "string",
    ) &&
    (value.expiresAt === undefined ||
      (typeof value.expiresAt === "number" && Number.isFinite(value.expiresAt)))
  );
}
function decodeSession(value: unknown): Session | null {
  if (value === null) return null;
  if (
    !isRecord(value) ||
    !isCredentials(value) ||
    typeof value.userId !== "string" ||
    !value.userId.trim() ||
    (value.renewal !== undefined &&
      (!isRecord(value.renewal) ||
        (value.renewal.credentials !== undefined &&
          !isCredentials(value.renewal.credentials))))
  )
    throw new Error(
      "Saved sign-in could not be read. Private pending work is retained.",
    );
  return value as Session;
}
async function write(key: string, value: unknown | null) {
  if (Platform.OS === "web") {
    if (value === null) webStorage.delete(key);
    else webStorage.set(key, JSON.stringify(value));
  } else if (value === null) await SecureStore.deleteItemAsync(key);
  else
    await SecureStore.setItemAsync(key, JSON.stringify(value), {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
}
export const sessionStorage = {
  load: async () => decodeSession(await read("pocketquests.session")),
  save: (session: Session | null) => write("pocketquests.session", session),
  // Retain only the private-cache owner across interrupted logout. This is not
  // authentication and must never authorize a request.
  loadCleanupOwner: async () => {
    await cleanupWrites.catch(() => {});
    const owner = await read("pocketquests.cleanup-owner");
    if (owner !== null && (typeof owner !== "string" || !owner.trim()))
      throw new Error("Private cleanup owner could not be read.");
    return owner as string | null;
  },
  saveCleanupOwner: (owner: string | null) => {
    const next = cleanupWrites
      .catch(() => {})
      .then(() => write("pocketquests.cleanup-owner", owner));
    cleanupWrites = next;
    return next;
  },
  loadPending: async (userId: string): Promise<Pending | null> => {
    const value = await read(`pocketquests.pending.${userId}`);
    if (value === null) return null;
    if (
      !isRecord(value) ||
      value.userId !== userId ||
      !isStoredCommand(value.command)
    )
      throw new Error(
        "Saved pending work could not be read. It remains on this device.",
      );
    return value as Pending;
  },
  savePending: (userId: string, pending: Pending | null) =>
    write(`pocketquests.pending.${userId}`, pending),
};
