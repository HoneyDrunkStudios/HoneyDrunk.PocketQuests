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
    /** Keyboard focus outline; defaults to primary. Validate against adjacent surfaces. */
    focus?: string;
    /** Unfilled progress track; defaults to border. */
    progressTrack?: string;
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
    subtitleWeight?: "500" | "600" | "700";
    buttonWeight?: "500" | "600" | "700";
  };
  spacing: {
    panel: number;
    gap: number;
    control: number;
    buttonHorizontal?: number;
  };
  shape: { panelRadius: number; controlRadius: number; borderWidth: number };
  controls: {
    minHeight: number;
    pressedOpacity: number;
    disabledOpacity: number;
    minWidth?: number;
    focusWidth?: number;
    progressHeight?: number;
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
    subtitleWeight: "600" as const,
    buttonWeight: "600" as const,
  },
  spacing: { panel: 20, gap: 12, control: 12, buttonHorizontal: 18 },
  shape: { panelRadius: 18, controlRadius: 12, borderWidth: 1 },
  controls: {
    minHeight: 48,
    minWidth: 48,
    pressedOpacity: 0.8,
    disabledOpacity: 0.6,
    focusWidth: 2,
    progressHeight: 8,
  },
};

/** Resolve additive tokens without requiring existing consumers to rewrite their themes. */
export function resolveTheme(theme: Theme) {
  return {
    ...theme,
    colors: {
      ...theme.colors,
      focus: theme.colors.focus ?? theme.colors.primary,
      progressTrack: theme.colors.progressTrack ?? theme.colors.border,
    },
    typography: {
      ...theme.typography,
      subtitleWeight:
        theme.typography.subtitleWeight ?? foundation.typography.subtitleWeight,
      buttonWeight:
        theme.typography.buttonWeight ?? foundation.typography.buttonWeight,
    },
    spacing: {
      ...theme.spacing,
      buttonHorizontal:
        theme.spacing.buttonHorizontal ?? foundation.spacing.buttonHorizontal,
    },
    controls: {
      ...theme.controls,
      minWidth: theme.controls.minWidth ?? foundation.controls.minWidth,
      focusWidth: theme.controls.focusWidth ?? foundation.controls.focusWidth,
      progressHeight:
        theme.controls.progressHeight ?? foundation.controls.progressHeight,
    },
  };
}
