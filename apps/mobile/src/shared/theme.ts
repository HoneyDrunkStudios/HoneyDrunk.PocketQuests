import { foundation, type Theme } from "@honeydrunk/ui-tokens";
/** Playful Quest Log: indigo ink, violet actions, sky-blue surfaces and honey rewards. */
export const pocketQuestsTheme: Theme = {
  ...foundation,
  colors: {
    text: "#242153",
    muted: "#5D5D88",
    background: "#FAFAFF",
    surface: "#F0EEFC",
    primary: "#6536BB",
    onPrimary: "#FFFFFF",
    border: "#8580A6",
    accent: "#985900",
    danger: "#AD254C",
    input: "#FFFFFF",
    progressTrack: "#DDDFF1",
  },
  typography: {
    ...foundation.typography,
    fontFamily: "QuestSans",
    titleSize: 32,
    titleWeight: "800",
  },
  shape: { ...foundation.shape, panelRadius: 18, controlRadius: 14 },
  spacing: { ...foundation.spacing, panel: 16 },
  controls: { ...foundation.controls, progressHeight: 8 },
};
export const pocketQuestsDarkTheme: Theme = {
  ...pocketQuestsTheme,
  colors: {
    text: "#F1EEFF",
    muted: "#B9B4D4",
    background: "#17162C",
    surface: "#25233F",
    primary: "#C8AEFF",
    onPrimary: "#24124B",
    border: "#8780AC",
    accent: "#FFC778",
    danger: "#FF9BB5",
    input: "#1D1C35",
    progressTrack: "#423B63",
  },
};
export type DisplayMode = "light" | "dark" | "system";
export function resolveDisplayTheme(
  mode: DisplayMode,
  system: string | null | undefined,
) {
  return mode === "dark" || (mode === "system" && system === "dark")
    ? pocketQuestsDarkTheme
    : pocketQuestsTheme;
}
