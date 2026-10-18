import { foundation, type Theme } from "@honeydrunk/ui-tokens";
/** Illustrated adventure journal; generic packages own no branding. */
export const pocketQuestsTheme: Theme = {
  ...foundation,
  colors: {
    text: "#FFF5E9",
    muted: "#B4C5E5",
    background: "#091A29",
    surface: "#102232",
    primary: "#F7BB5D",
    onPrimary: "#091A29",
    border: "#71829B",
    accent: "#F7BB5D",
    danger: "#FF9CAD",
    input: "#091A29",
    progressTrack: "#091A29",
  },
  typography: { ...foundation.typography, titleSize: 32, titleWeight: "800" },
  shape: { ...foundation.shape, panelRadius: 14, controlRadius: 10 },
  spacing: { ...foundation.spacing, panel: 16 },
  controls: { ...foundation.controls, progressHeight: 12 },
};
