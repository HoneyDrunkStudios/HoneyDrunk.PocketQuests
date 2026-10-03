import { Platform } from "react-native";
import * as SecureStore from "expo-secure-store";
import type { Command } from "./contracts";
export type Session = { token: string; userId: string };
export type Pending = { userId: string; command: Command };
const webStorage = new Map<string, string>();
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
  else await SecureStore.setItemAsync(key, JSON.stringify(value));
}
export const sessionStorage = {
  load: () => read<Session>("pocketquests.session"),
  save: (session: Session | null) => write("pocketquests.session", session),
  loadPending: (userId: string) =>
    read<Pending>(`pocketquests.pending.${userId}`),
  savePending: (userId: string, pending: Pending | null) =>
    write(`pocketquests.pending.${userId}`, pending),
};
