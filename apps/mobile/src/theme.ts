import { foundation, type Theme } from "@honeydrunk/ui-tokens";
/** Pocket Quests' original anime RPG palette; reusable packages own no product branding. */
export const pocketQuestsTheme: Theme = {
  ...foundation,
  colors: {
    text: "#EDF1FF",
    muted: "#B7C3E0",
    background: "#0D1225",
    surface: "#18213B",
    primary: "#8BE9E1",
    onPrimary: "#0D1225",
    border: "#52618B",
    accent: "#FFD780",
    danger: "#FFB4C2",
    input: "#10182E",
  },
  typography: { ...foundation.typography, titleSize: 34, titleWeight: "900" },
  shape: { ...foundation.shape, panelRadius: 14, controlRadius: 10 },
};
