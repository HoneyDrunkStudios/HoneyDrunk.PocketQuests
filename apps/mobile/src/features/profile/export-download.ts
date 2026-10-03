import { Platform } from "react-native";
import * as Crypto from "expo-crypto";
export async function saveExport(buffer: ArrayBuffer, format: "json" | "csv") {
  const name = `pocket-quests-${Crypto.randomUUID()}.${format === "json" ? "json" : "zip"}`;
  const type = format === "json" ? "application/json" : "application/zip";
  if (Platform.OS === "web") {
    const url = URL.createObjectURL(new Blob([buffer], { type }));
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } else {
    const { File, Paths } = await import("expo-file-system");
    const Sharing = await import("expo-sharing");
    if (!(await Sharing.isAvailableAsync()))
      throw new Error("File saving is unavailable on this device.");
    const file = new File(Paths.cache, name);
    try {
      file.create();
      file.write(new Uint8Array(buffer));
      await Sharing.shareAsync(file.uri, {
        mimeType: type,
        dialogTitle: "Save private Pocket Quests export",
      });
    } finally {
      if (file.exists) file.delete();
    }
  }
}
