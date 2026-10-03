/** Semantic contract: apps own their palette and branding. No platform imports. */
export interface Theme {
  colors: {
    text: string;
    muted: string;
    background: string;
    surface: string;
    primary: string;
    onPrimary: string;
    border: string;
    accent: string;
    danger: string;
    input: string;
  };
  typography: {
    titleSize: number;
    subtitleSize: number;
    bodySize: number;
    captionSize: number;
    bodyLineHeight: number;
    captionLineHeight: number;
    titleWeight: "700" | "800" | "900";
    fontFamily?: string;
  };
  spacing: { panel: number; gap: number; control: number };
  shape: { panelRadius: number; controlRadius: number; borderWidth: number };
  controls: {
    minHeight: number;
    pressedOpacity: number;
    disabledOpacity: number;
  };
}
export const foundation = {
  typography: {
    titleSize: 28,
    subtitleSize: 20,
    bodySize: 16,
    captionSize: 14,
    bodyLineHeight: 24,
    captionLineHeight: 21,
    titleWeight: "700" as const,
  },
  spacing: { panel: 20, gap: 12, control: 12 },
  shape: { panelRadius: 18, controlRadius: 12, borderWidth: 1 },
  controls: { minHeight: 48, pressedOpacity: 0.8, disabledOpacity: 0.6 },
};
