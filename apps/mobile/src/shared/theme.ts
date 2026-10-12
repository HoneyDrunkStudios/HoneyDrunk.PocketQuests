import { foundation, type Theme } from "@honeydrunk/ui-tokens";
/** Approved Split Profile palette; generic packages own no branding. */
export const pocketQuestsTheme: Theme = {
  ...foundation,
  colors: {
    text: "#F6F0E2",
    muted: "#BAB6AB",
    background: "#171D22",
    surface: "#222A31",
    primary: "#D6B76D",
    onPrimary: "#171D22",
    border: "#8D836A",
    accent: "#D6B76D",
    danger: "#FF9CAD",
    input: "#171D22",
    progressTrack: "#171D22",
  },
  typography: { ...foundation.typography, titleSize: 32, titleWeight: "800" },
  shape: { ...foundation.shape, panelRadius: 14, controlRadius: 10 },
};
