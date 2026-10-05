import { useEffect, useState } from "react";
import { syncWarnings } from "../features/quests/notifications";
import type { State } from "../shared/contracts";
export function useExpiryWarnings(state: State | null) {
  const [status, setStatus] = useState("Expiry warnings are off.");
  useEffect(() => {
    let current = true;
    void syncWarnings(state)
      .then((message) => {
        if (current) setStatus(message);
      })
      .catch(() => {
        if (current)
          setStatus(
            "Warnings could not be scheduled. Quests remain usable; check phone notification settings.",
          );
      });
    return () => {
      current = false;
    };
  }, [state]);
  return status;
}
