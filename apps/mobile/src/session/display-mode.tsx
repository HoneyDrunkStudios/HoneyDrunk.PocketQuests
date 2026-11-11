import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useColorScheme } from "react-native";
import { ThemeProvider } from "@honeydrunk/ui-native";
import { resolveDisplayTheme, type DisplayMode } from "../shared/theme";
import { displayPreferences } from "./storage";
type DisplayChoice = {
  mode: DisplayMode;
  ready: boolean;
  error: string;
  selectMode(mode: DisplayMode): void;
};
const DisplayContext = createContext<DisplayChoice | null>(null);
/** Device preference; theme changes never remount the session or navigation. */
export function DisplayModeProvider({
  children,
}: Readonly<{ children: ReactNode }>) {
  const system = useColorScheme();
  const [mode, setMode] = useState<DisplayMode>("system");
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const writes = useRef<Promise<void>>(Promise.resolve());
  const changed = useRef(false);
  const generation = useRef(0);
  const active = useRef(0);
  const revision = useRef(0);
  useEffect(() => {
    let current = true;
    const instance = ++generation.current;
    active.current = instance;
    void displayPreferences
      .load()
      .then((saved) => {
        if (current && !changed.current) setMode(saved);
      })
      .catch(() => {
        if (current)
          setError(
            "Your display preference could not be loaded. You can choose it again.",
          );
      })
      .finally(() => {
        if (current) setReady(true);
      });
    return () => {
      current = false;
      if (active.current === instance) active.current = 0;
    };
  }, []);
  const selectMode = useCallback((next: DisplayMode) => {
    changed.current = true;
    setMode(next);
    setError("");
    const instance = active.current,
      selection = ++revision.current;
    writes.current = writes.current
      .catch(() => {})
      .then(() => displayPreferences.save(next))
      .catch(() => {
        if (active.current === instance && revision.current === selection)
          setError(
            "The display changed, but could not be saved. Please choose it again to retry.",
          );
      });
  }, []);
  const value = useMemo(
    () => ({ mode, ready, error, selectMode }),
    [mode, ready, error, selectMode],
  );
  return (
    <DisplayContext.Provider value={value}>
      <ThemeProvider theme={resolveDisplayTheme(mode, system)}>
        {children}
      </ThemeProvider>
    </DisplayContext.Provider>
  );
}
export function useDisplayMode() {
  const value = useContext(DisplayContext);
  if (!value) throw new Error("Display choices require DisplayModeProvider.");
  return value;
}
