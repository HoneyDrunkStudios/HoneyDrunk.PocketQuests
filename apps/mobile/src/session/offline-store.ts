import * as Crypto from "expo-crypto";
import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";
import type { Anchor, Catalog, Command, State } from "../shared/contracts";
import { decodeAccount, encodeAccount } from "./cache-format";
import type { RejectedCommand } from "./sync-recovery";
import type { UnverifiedAction } from "./unverified-actions";
export type LocalAccount = {
  userId: string;
  queue: Command[];
  // Optional when reading caches written before individual sync recovery existed.
  rejected?: RejectedCommand[];
  unverified?: UnverifiedAction[];
  anchor: Anchor | null;
  // A readable pending journal can survive a damaged display snapshot.
} & (
  | { requiresReload?: false; state: State; catalog: Catalog }
  | { requiresReload: true; state: null; catalog: null }
);
const memory = new Map<string, LocalAccount>();
const recoveryNotices = new Map<string, string>();
export const cacheRecoveryNotice = (userId: string) =>
  recoveryNotices.get(userId) ?? null;
export async function discardCacheRecovery(userId: string) {
  if (Platform.OS !== "web")
    await SecureStore.deleteItemAsync(`${await pointerKey(userId)}.recovery`);
  recoveryNotices.delete(userId);
}
let webDeviceId: string | undefined;
type Pointer = { filename: string; key: string };
const cacheFilename = (value: unknown): value is string =>
  typeof value === "string" && /^pq-[a-f0-9-]+\.encrypted$/.test(value);
async function pointerKey(userId: string) {
  return (
    "pq.cache." +
    (await Crypto.digestStringAsync(
      Crypto.CryptoDigestAlgorithm.SHA256,
      userId,
    ))
  );
}
export async function loadAccount(
  userId: string,
): Promise<LocalAccount | null> {
  if (Platform.OS === "web") return decodeAccount(memory.get(userId), userId);
  const name = await pointerKey(userId);
  const pointer = await SecureStore.getItemAsync(name);
  if (await SecureStore.getItemAsync(`${name}.recovery`))
    recoveryNotices.set(
      userId,
      "An unreadable saved copy is retained privately. Its local actions have not been synchronized. Discard pending changes only if you want to remove this recovery copy.",
    );
  if (!pointer) return null;
  async function recover() {
    // Retain the pointer/key and ciphertext, rather than destroying potentially
    // recoverable pending work. Only explicit sign-out/discard erases them.
    const quarantine = `${name}.recovery`;
    const raw = await SecureStore.getItemAsync(quarantine);
    let saved: string[] = [];
    if (raw) {
      try {
        const parsed: unknown = JSON.parse(raw);
        saved =
          Array.isArray(parsed) &&
          parsed.every((item) => typeof item === "string")
            ? parsed
            : [raw];
      } catch {
        // Preserve damaged recovery metadata as well as the current pointer.
        saved = [raw];
      }
    }
    if (!saved.includes(pointer!)) saved.push(pointer!);
    await SecureStore.setItemAsync(quarantine, JSON.stringify(saved), {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
    await SecureStore.deleteItemAsync(name);
    recoveryNotices.set(
      userId,
      "The saved copy could not be opened. Reconnect to reload online history. Unreadable local actions were retained privately for recovery and have not been synchronized.",
    );
    return null;
  }
  let value: Pointer;
  try {
    value = JSON.parse(pointer) as Pointer;
    if (
      !value ||
      !cacheFilename(value.filename) ||
      typeof value.key !== "string"
    )
      return await recover();
  } catch {
    return recover();
  }
  const { File, Paths } = await import("expo-file-system");
  const file = new File(Paths.document, value.filename);
  if (!file.exists) return recover();
  // A transient read/locked-device error must not reset the active pointer.
  const ciphertext = await file.bytes();
  try {
    const key = await Crypto.AESEncryptionKey.import(value.key, "hex");
    const sealed = Crypto.AESSealedData.fromCombined(ciphertext);
    const bytes = await Crypto.aesDecryptAsync(sealed, key, {
      additionalData: new TextEncoder().encode(userId),
    });
    const decoded: unknown = JSON.parse(new TextDecoder().decode(bytes));
    const account = decodeAccount(decoded, userId);
    return account ?? recover();
  } catch {
    return recover();
  }
}
export async function saveAccount(account: LocalAccount) {
  if (Platform.OS === "web") {
    memory.set(
      account.userId,
      JSON.parse(JSON.stringify(account)) as LocalAccount,
    );
    return;
  }
  const name = await pointerKey(account.userId);
  const previous = await SecureStore.getItemAsync(name);
  const { File, Paths } = await import("expo-file-system");
  const filename = `pq-${Crypto.randomUUID()}.encrypted`;
  const file = new File(Paths.document, filename);
  const key = await Crypto.AESEncryptionKey.generate();
  const sealed = await Crypto.aesEncryptAsync(
    new TextEncoder().encode(JSON.stringify(encodeAccount(account))),
    key,
    { additionalData: new TextEncoder().encode(account.userId) },
  );
  file.create();
  file.write(await sealed.combined());
  try {
    // The key/pointer is the commit point. A crash before it leaves the previous
    // complete generation; a crash afterward leaves the new complete generation.
    await SecureStore.setItemAsync(
      name,
      JSON.stringify({ filename, key: await key.encoded("hex") }),
      { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY },
    );
  } catch (error) {
    // A native pointer write may commit and then fail to return successfully.
    // Keep this ciphertext until the pointer can be read again; deleting it here
    // could destroy a committed generation. Unreferenced ciphertext is harmless.
    throw error;
  }
  if (previous) {
    try {
      const previousFilename = (JSON.parse(previous) as Pointer)?.filename;
      if (!cacheFilename(previousFilename)) return;
      const old = new File(Paths.document, previousFilename);
      if (old.exists) old.delete();
    } catch {
      /* Old ciphertext is no longer decryptable after the key commit. */
    }
  }
}
export async function clearAccount(userId: string) {
  memory.delete(userId);
  recoveryNotices.delete(userId);
  if (Platform.OS === "web") return;
  const name = await pointerKey(userId);
  const old = await SecureStore.getItemAsync(name);
  // Delete the key before best-effort ciphertext cleanup, including orphan safety.
  await SecureStore.deleteItemAsync(name);
  // Explicit sign-out revokes access to quarantined generations too.
  await SecureStore.deleteItemAsync(`${name}.recovery`);
  if (old) {
    try {
      const oldFilename = (JSON.parse(old) as Pointer)?.filename;
      if (!cacheFilename(oldFilename)) return;
      const { File, Paths } = await import("expo-file-system");
      const file = new File(Paths.document, oldFilename);
      if (file.exists) file.delete();
    } catch {
      /* Key has already been erased. */
    }
  }
}
export async function deviceId() {
  // Web cache and identity share the current page lifetime. A fresh page must
  // reconnect for a new anchor; each command within it keeps the same identity.
  if (Platform.OS === "web") return (webDeviceId ??= Crypto.randomUUID());
  const existing = await SecureStore.getItemAsync("pq.device");
  if (existing) return existing;
  const id = Crypto.randomUUID();
  await SecureStore.setItemAsync("pq.device", id);
  return id;
}
