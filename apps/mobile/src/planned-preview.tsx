import { useEffect, useState } from "react";
import { useSession } from "./session";
import { Label } from "./ui";
export function PlannedPreview({ date, time }: { date: string; time: string }) {
  const { state, previewClock, offline } = useSession();
  const [result, setResult] = useState({ key: "", message: "" });
  const zone = state?.zone;
  const key = `${date}/${time}/${zone}`;
  const valid = !!zone && /^\d{4}-\d{2}-\d{2}$/.test(date) && /^\d{2}:\d{2}$/.test(time);
  useEffect(() => {
    let active = true;
    if (
      !zone ||
      !time ||
      !/^\d{4}-\d{2}-\d{2}$/.test(date) ||
      !/^\d{2}:\d{2}$/.test(time)
    )
      return;
    if (offline) return;
    const timer = setTimeout(() => {
      void previewClock(date, time, zone)
        .then((p) => {
          if (active)
            setResult({ key, message: `${p.adjusted ? "Clock gap: next valid time is" : p.repeated ? "Repeated time: first occurrence is" : "Planned for"} ${p.resolved}, ${p.instant}. The due-day deadline is unchanged.` });
        })
        .catch((error) => {
          if (active) setResult({ key, message: error.message });
        });
    }, 300);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [date, time, zone, offline, previewClock, key]);
  const message = !valid ? "" : offline ? "Connect to verify daylight-saving adjustments for this planned time." : result.key === key ? result.message : "Checking planned clock time…";
  return message ? <Label>{message}</Label> : null;
}
