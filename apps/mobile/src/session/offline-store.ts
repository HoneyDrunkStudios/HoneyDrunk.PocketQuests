import { Platform } from "react-native";
import * as SecureStore from "expo-secure-store";
import * as Crypto from "expo-crypto";
import type { Anchor, Catalog, Command, State } from "../shared/contracts";
import type { RejectedCommand } from "./sync-recovery";
export type LocalAccount = {
  userId: string;
  state: State;
  catalog: Catalog;
  queue: Command[];
  // Optional when reading caches written before individual sync recovery existed.
  rejected?: RejectedCommand[];
  anchor: Anchor | null;
};
const memory = new Map<string, LocalAccount>();
type Pointer = { filename: string; key: string };
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
  if (Platform.OS === "web") return memory.get(userId) ?? null;
  const pointer = await SecureStore.getItemAsync(await pointerKey(userId));
  if (!pointer) return null;
  const value = JSON.parse(pointer) as Pointer;
  if (!/^pq-[a-f0-9-]+\.encrypted$/.test(value.filename))
    throw new Error("Invalid private cache pointer.");
  const { File, Paths } = await import("expo-file-system");
  const file = new File(Paths.document, value.filename);
  const key = await Crypto.AESEncryptionKey.import(value.key, "hex");
  const sealed = Crypto.AESSealedData.fromCombined(await file.bytes());
  const bytes = await Crypto.aesDecryptAsync(sealed, key, {
    additionalData: new TextEncoder().encode(userId),
  });
  const account = JSON.parse(new TextDecoder().decode(bytes)) as LocalAccount;
  if (account.userId !== userId)
    throw new Error("Cached data belongs to another account.");
  return account;
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
    new TextEncoder().encode(JSON.stringify(account)),
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
    file.delete();
    throw error;
  }
  if (previous) {
    try {
      const old = new File(
        Paths.document,
        (JSON.parse(previous) as Pointer).filename,
      );
      if (old.exists) old.delete();
    } catch {
      /* Old ciphertext is no longer decryptable after the key commit. */
    }
  }
}
export async function clearAccount(userId: string) {
  memory.delete(userId);
  if (Platform.OS === "web") return;
  const name = await pointerKey(userId);
  const old = await SecureStore.getItemAsync(name);
  // Delete the key before best-effort ciphertext cleanup, including orphan safety.
  await SecureStore.deleteItemAsync(name);
  if (old) {
    try {
      const { File, Paths } = await import("expo-file-system");
      const file = new File(
        Paths.document,
        (JSON.parse(old) as Pointer).filename,
      );
      if (file.exists) file.delete();
    } catch {
      /* Key has already been erased. */
    }
  }
}
export async function deviceId() {
  const existing =
    Platform.OS === "web" ? null : await SecureStore.getItemAsync("pq.device");
  if (existing) return existing;
  const id = Crypto.randomUUID();
  if (Platform.OS !== "web") await SecureStore.setItemAsync("pq.device", id);
  return id;
}
