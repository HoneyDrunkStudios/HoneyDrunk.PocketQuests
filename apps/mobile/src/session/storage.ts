import { Platform } from "react-native";
import * as SecureStore from "expo-secure-store";
import type { Command } from "../shared/contracts";
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
async function read<T>(key: string): Promise<T | null> {
  const value =
    Platform.OS === "web"
      ? webStorage.get(key)
      : await SecureStore.getItemAsync(key);
  return value ? (JSON.parse(value) as T) : null;
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
  load: () => read<Session>("pocketquests.session"),
  save: (session: Session | null) => write("pocketquests.session", session),
  // Retain only the private-cache owner across interrupted logout. This is not
  // authentication and must never authorize a request.
  loadCleanupOwner: async () => {
    await cleanupWrites.catch(() => {});
    const owner = await read<unknown>("pocketquests.cleanup-owner");
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
  loadPending: (userId: string) =>
    read<Pending>(`pocketquests.pending.${userId}`),
  savePending: (userId: string, pending: Pending | null) =>
    write(`pocketquests.pending.${userId}`, pending),
};
