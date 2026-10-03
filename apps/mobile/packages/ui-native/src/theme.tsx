import React, { createContext, useContext } from "react";
import type { Theme } from "@honeydrunk/ui-tokens";

const ThemeContext = createContext<Theme | null>(null);

/** Every app supplies its own complete theme. Changing the value updates consumers. */
export function ThemeProvider({
  theme,
  children,
}: {
  theme: Theme;
  children: React.ReactNode;
}) {
  return (
    <ThemeContext.Provider value={theme}>{children}</ThemeContext.Provider>
  );
}

export function useTheme(): Theme {
  const theme = useContext(ThemeContext);
  if (!theme)
    throw new Error("HoneyDrunk UI requires an app-supplied ThemeProvider.");
  return theme;
}
