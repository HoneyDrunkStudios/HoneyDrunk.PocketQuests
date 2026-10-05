import { useEffect } from "react";
import { AppState, Platform } from "react-native";
import type { createSessionRuntime } from "./session-runtime";
export function useSessionLifecycle(
  runtime: ReturnType<typeof createSessionRuntime>,
) {
  useEffect(() => {
    let current = true;
    const release = runtime.mount();
    void runtime.restore(() => current);
    return () => {
      current = false;
      release();
    };
  }, [runtime]);
  useEffect(() => {
    const refresh = () => {
      void runtime.actions.refresh();
    };
    const subscription = AppState.addEventListener("change", (status) => {
      if (status === "active") refresh();
    });
    if (Platform.OS === "web") window.addEventListener("online", refresh);
    return () => {
      subscription.remove();
      if (Platform.OS === "web") window.removeEventListener("online", refresh);
    };
  }, [runtime]);
}
