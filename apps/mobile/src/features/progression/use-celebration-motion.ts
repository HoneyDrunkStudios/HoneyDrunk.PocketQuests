import { useEffect, useState } from "react";
import { AccessibilityInfo } from "react-native";

/** One subscription per celebration, shared by all of its reward bars. */
export function useCelebrationMotion() {
  const [reducedMotion, setReducedMotion] = useState<boolean | null>(null);
  useEffect(() => {
    let active = true;
    let changed = false;
    const subscription = AccessibilityInfo.addEventListener(
      "reduceMotionChanged",
      (enabled) => {
        changed = true;
        if (active) setReducedMotion(enabled);
      },
    );
    void AccessibilityInfo.isReduceMotionEnabled()
      .then((enabled) => {
        if (active && !changed) setReducedMotion(enabled);
      })
      .catch(() => {
        if (active) setReducedMotion(true);
      });
    return () => {
      active = false;
      subscription?.remove();
    };
  }, []);
  return reducedMotion;
}
