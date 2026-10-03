import { foundation, type Theme } from "@honeydrunk/ui-tokens";
/** Product-owned cream, gold and charcoal; generic packages own no branding. */
export const pocketQuestsTheme: Theme = {
  ...foundation,
  colors: {
    text: "#27251F",
    muted: "#625A49",
    background: "#F6F0E2",
    surface: "#FFFBF3",
    primary: "#765718",
    onPrimary: "#FFFFFF",
    border: "#89764F",
    accent: "#765718",
    danger: "#A12336",
    input: "#FFFFFF",
  },
  typography: { ...foundation.typography, titleSize: 32, titleWeight: "800" },
  shape: { ...foundation.shape, panelRadius: 14, controlRadius: 10 },
};
