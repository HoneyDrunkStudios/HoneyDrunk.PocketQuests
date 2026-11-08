import { useEffect, useState } from "react";

import { Text, View } from "react-native";

import { useGlobalSearchParams, usePathname, useRouter } from "expo-router";

import { useSafeAreaInsets } from "react-native-safe-area-context";

import { useAccountSnapshot, useSessionStatus } from "../../session/session";

import { Button, useAppTheme } from "../../shared/ui";

export const journalTour = [
  {
    route: "/",

    title: "Welcome to your journal",

    text: "Home holds your traveler and today's plan. Start small: one thing you want to do is enough.",

    next: "Tap Quests below to find your first adventure.",

    tab: 1,
  },

  {
    route: "/board",

    title: "Find something that fits",

    text: "Available quests are ideas you can choose from. Open a card to read its steps and rewards. Active holds the quests you've accepted; Completed holds your history.",

    next: "Tap Calendar below to see where plans belong.",

    tab: 2,
  },

  {
    route: "/calendar",

    title: "Give it a little space",

    text: "Choose a day, then choose or create a quest. A planned time helps organize your day; you can also leave a quest unscheduled.",

    next: "Tap Room below to visit your space.",

    tab: 3,
  },

  {
    route: "/room",

    title: "Make yourself at home",

    text: "Your room is a home for your traveler and earned keepsakes. Tap Decorate, then a shelf or table to place a reward. Rooms holds your spaces and their quest unlocks.",

    next: "Tap Progress below to see how you grow.",

    tab: 4,
  },

  {
    route: "/profile",

    title: "Small steps add up",

    text: "Progress shows your confirmed XP, skills and milestones. Complete a quest after doing it in the real world. Undo remains available in its details when eligible.",

    next: "Next, see where your preferences live.",

    tab: -1,
  },

  {
    route: "/settings",

    title: "Keep it your own",

    text: "Settings groups your traveler, appearance, reminders and private account controls. You can replay this tour here at any time.",

    next: "You're ready. Choose a quest when it fits your day.",

    tab: -1,
  },
] as const;

/** Guidance only: moving through the tour never accepts/completes a quest or grants XP. */

export function AppTour() {
  const { colors, styles } = useAppTheme();

  const { state } = useAccountSnapshot();

  const { signedIn } = useSessionStatus();

  const { tour } = useGlobalSearchParams<{ tour?: string }>();

  const pathname = usePathname();

  const router = useRouter();

  const insets = useSafeAreaInsets();

  const [step, setStep] = useState<number | null>(null);
  const [lastStart, setLastStart] = useState(tour);
  const [requested, setRequested] = useState(tour === "start");
  const [starting, setStarting] = useState(false);
  if (tour !== lastStart) {
    setLastStart(tour);
    if (tour === "start") setRequested(true);
  }
  if (!signedIn && (step !== null || requested || starting)) {
    setStep(null);
    setRequested(false);
    setStarting(false);
  }
  if (signedIn && requested && state?.profile.onboardingComplete) {
    setRequested(false);
    setStep(0);
    setStarting(true);
  }
  if (starting && pathname === "/") setStarting(false);
  if (
    !starting &&
    step !== null &&
    step < journalTour.length - 1 &&
    pathname === journalTour[step + 1].route
  )
    setStep(step + 1);
  useEffect(() => {
    if (tour === "start") router.setParams({ tour: undefined });
    if (starting) router.replace("/(tabs)");
  }, [tour, starting, router]);
  if (step === null || !signedIn || !state?.profile.onboardingComplete)
    return null;

  const page = journalTour[step];

  const inTabs =
    pathname !== "/settings" &&
    ["/", "/board", "/calendar", "/room", "/profile"].includes(pathname);

  if (!inTabs && pathname !== "/settings") return null;

  return (
    <View pointerEvents="box-none" style={{ position: "absolute", inset: 0 }}>
      {inTabs && page.tab >= 0 && (
        <View
          pointerEvents="none"

          style={{
            position: "absolute",

            bottom: 4 + insets.bottom,

            left: `${page.tab * 20}%`,

            width: "20%",

            height: 64,

            borderWidth: 3,

            borderRadius: 18,

            borderColor: colors.gold,
          }}
        />
      )}

      <View
        accessibilityLiveRegion="polite"

        style={{
          position: "absolute",

          bottom: inTabs ? 86 + insets.bottom : 20 + insets.bottom,

          left: 16,

          right: 16,

          maxWidth: 560,

          alignSelf: "center",

          padding: 18,

          borderRadius: 20,

          backgroundColor: colors.card,

          borderWidth: 2,

          borderColor: colors.gold,

          gap: 9,
        }}
      >
        <Text style={[styles.muted, { color: colors.gold }]}>
          YOUR FIRST ADVENTURE · {step + 1} OF {journalTour.length}
        </Text>

        <Text
          accessibilityRole="header"

          style={[styles.subtitle, { fontSize: 23 }]}
        >
          {page.title}
        </Text>

        <Text style={styles.text}>{page.text}</Text>

        <Text style={[styles.text, { color: colors.gold }]}>{page.next}</Text>

        <View style={{ flexDirection: "row", gap: 10 }}>
          <View style={{ flex: 1 }}>
            <Button
              title={
                step === 5
                  ? "Choose a quest"
                  : step === 4
                    ? "Open settings"
                    : "Next"
              }

              onPress={() => {
                if (step === 5) {
                  setStep(null);

                  router.replace({
                    pathname: "/(tabs)/board",

                    params: { view: "Available" },
                  });
                } else {
                  const next = step + 1;

                  setStep(next);

                  router.navigate(journalTour[next].route);
                }
              }}
            />
          </View>

          <Button title="Skip tour" secondary onPress={() => setStep(null)} />
        </View>
      </View>
    </View>
  );
}
