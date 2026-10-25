import { useRef, useState } from "react";
import { Redirect } from "expo-router";
import { Pressable, ScrollView, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useSession } from "../../session/session";
import { SessionRecovery } from "../../session/session-recovery";
import { JournalHeading } from "../../shared/journal-heading";
import { Button, Card, Label, colors, styles } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";

const steps = ["Welcome", "Your interests", "Your first quest"] as const;
const categoryIcons = [
  "fitness-outline",
  "book-outline",
  "color-palette-outline",
  "briefcase-outline",
  "people-outline",
  "home-outline",
  "sunny-outline",
  "leaf-outline",
  "wallet-outline",
  "compass-outline",
] as const;

/** Setup has its own route; only a confirmed account update unlocks the app. */
export function Onboarding() {
  const { state, catalog, busy, pending, offline, command, signOut } =
    useSession();
  const [currentStep, setStep] = useState(0);
  const [interests, setInterests] = useState<string[] | null>(null);
  const [interestSave, setInterestSave] = useState<{
    interests: string[];
    finished: boolean;
  } | null>(null);
  const savingInterests = useRef(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const insets = useSafeAreaInsets();
  const isSavingInterests = !!interestSave && !interestSave.finished;
  // Interest commands do not project onto the profile before confirmation.
  const step =
    currentStep === 1 &&
    interestSave?.finished &&
    state &&
    sameInterests(interestSave.interests, state.profile.interests)
      ? 2
      : currentStep;
  if (state?.profile.onboardingComplete)
    return <Redirect href="/(tabs)/board" />;
  const disabled =
    busy || pending || offline || !state || !catalog || isSavingInterests;
  const choices = interests ?? state?.profile.interests ?? [];
  async function continueWithInterests() {
    if (disabled || savingInterests.current || !state) return;
    if (sameInterests(choices, state.profile.interests)) {
      setStep(2);
      return;
    }
    savingInterests.current = true;
    setSaveError(null);
    setInterestSave({ interests: choices, finished: false });
    try {
      await command({ action: questActions.interests, interests: choices });
    } catch {
      setSaveError("We couldn’t save your choices. Please try again.");
    } finally {
      setInterestSave({ interests: choices, finished: true });
      savingInterests.current = false;
    }
  }
  return (
    <View style={{ flex: 1, backgroundColor: colors.paper }}>
      <ScrollView
        key={step}
        showsVerticalScrollIndicator={false}
        contentInsetAdjustmentBehavior="never"
        contentContainerStyle={{
          flexGrow: 1,
          width: "100%",
          maxWidth: 780,
          alignSelf: "center",
          paddingHorizontal: 16,
          paddingBottom: 24,
          gap: 20,
        }}
      >
        {step === 0 ? (
          <JournalHeading
            showSettings={false}
            title="Your adventure starts here"
            description="Small moments. Real progress. A story that’s yours."
          />
        ) : (
          <View style={{ paddingTop: insets.top + 24, gap: 10 }}>
            <Text
              style={[styles.muted, { color: colors.gold, letterSpacing: 2 }]}
            >
              POCKET QUESTS
            </Text>
            <Text accessibilityRole="header" style={styles.title}>
              {step === 1
                ? "What matters to you?"
                : "Start with one small quest"}
            </Text>
          </View>
        )}
        <View
          accessibilityLabel={`Step ${step + 1} of 3: ${steps[step]}`}
          style={{ flexDirection: "row", alignItems: "center", gap: 12 }}
        >
          <Text style={[styles.muted, { color: colors.gold }]}>
            0{step + 1} / 03
          </Text>
          <Text style={styles.muted}>{steps[step]}</Text>
        </View>
        <SessionRecovery />
        {saveError && <Label>{saveError}</Label>}
        {!state || !catalog ? (
          <Label>Loading your journal…</Label>
        ) : (
          <>
            {step === 0 && (
              <>
                <Card
                  style={{ borderColor: colors.gold, gap: 18, padding: 20 }}
                >
                  <Intro
                    icon="compass-outline"
                    title="Choose a little adventure"
                    description="Make space for learning, creativity, movement or rest."
                  />
                  <Intro
                    icon="sparkles-outline"
                    title="See yourself grow"
                    description="Finish real-world quests to earn XP and grow your character."
                  />
                  <Intro
                    icon="time-outline"
                    title="Make it your own"
                    description="Start small. Choose your pace. Change your interests any time."
                  />
                </Card>
                <Label style={styles.muted}>
                  Let’s find a starting point. No skill assessments needed.
                </Label>
              </>
            )}
            {step === 1 && (
              <>
                <Label>
                  Pick the paths you’d like to explore. We’ll show those
                  suggestions first; every category stays open.
                </Label>
                <View
                  style={{ flexDirection: "row", flexWrap: "wrap", gap: 10 }}
                >
                  {catalog.categories.map((category, index) => {
                    const selected = choices.includes(category.id);
                    const choiceDisabled = isSavingInterests;
                    return (
                      <Pressable
                        key={category.id}
                        accessibilityRole="checkbox"
                        accessibilityLabel={category.name}
                        accessibilityState={{
                          checked: selected,
                          disabled: choiceDisabled,
                        }}
                        aria-checked={selected}
                        disabled={choiceDisabled}
                        onPress={() => {
                          setInterestSave(null);
                          setInterests((draft) => {
                            const current = draft ?? state.profile.interests;
                            return current.includes(category.id)
                              ? current.filter((id) => id !== category.id)
                              : [...current, category.id];
                          });
                        }}
                        style={({ pressed }) => ({
                          flexBasis: "46%",
                          flexGrow: 1,
                          minHeight: 100,
                          padding: 14,
                          gap: 10,
                          borderWidth: 1,
                          borderRadius: 14,
                          borderColor: selected ? colors.gold : colors.line,
                          backgroundColor: selected ? "#243444" : colors.card,
                          opacity: choiceDisabled ? 0.6 : pressed ? 0.8 : 1,
                        })}
                      >
                        <View
                          style={{
                            flexDirection: "row",
                            justifyContent: "space-between",
                          }}
                        >
                          <Ionicons
                            name={categoryIcons[index % categoryIcons.length]}
                            size={24}
                            color={colors.gold}
                            accessible={false}
                          />
                          {selected && (
                            <Ionicons
                              name="checkmark-circle"
                              size={22}
                              color={colors.gold}
                              accessible={false}
                            />
                          )}
                        </View>
                        <Text
                          style={[
                            styles.text,
                            { fontSize: 15, lineHeight: 21 },
                          ]}
                        >
                          {category.name}
                        </Text>
                      </Pressable>
                    );
                  })}
                </View>
                <Label style={styles.muted}>
                  Optional. You can continue without choosing, and update these
                  later in your profile. Your choices are saved when you
                  continue.
                </Label>
              </>
            )}
            {step === 2 && (
              <>
                <Card
                  style={{ borderColor: colors.gold, padding: 22, gap: 20 }}
                >
                  <Intro
                    icon="compass-outline"
                    title="Find something that fits today"
                    description="Explore Available quests and open one to see what it involves."
                  />
                  <Intro
                    icon="add-circle-outline"
                    title="Start when you’re ready"
                    description="Starting a quest adds it to Active. Nothing repeats automatically."
                  />
                  <Intro
                    icon="checkmark-circle-outline"
                    title="Do it in the real world"
                    description="Mark it complete when you’re done. Your journal records your progress."
                  />
                </Card>
                <Label style={styles.muted}>
                  Your character starts at level 1. You can add skills and
                  experience in your profile whenever you like.
                </Label>
              </>
            )}
          </>
        )}
      </ScrollView>
      <View
        style={{
          width: "100%",
          maxWidth: 780,
          alignSelf: "center",
          paddingHorizontal: 20,
          paddingTop: 14,
          paddingBottom: Math.max(insets.bottom, 16),
          gap: 10,
          borderTopWidth: 1,
          borderTopColor: colors.line,
          backgroundColor: colors.paper,
        }}
      >
        <Button
          title={
            isSavingInterests
              ? "Saving choices…"
              : step === 2
                ? "Choose my first quest"
                : step === 0
                  ? "Let’s begin"
                  : "Continue"
          }
          disabled={disabled}
          onPress={() =>
            step === 0
              ? setStep(1)
              : step === 1
                ? void continueWithInterests()
                : void command({ action: questActions.finishOnboarding })
          }
        />
        {step > 0 ? (
          <Button
            title="Back"
            secondary
            disabled={busy || pending || isSavingInterests}
            onPress={() => {
              setInterestSave(null);
              setStep(step - 1);
            }}
          />
        ) : (
          <Button
            title="Sign out"
            secondary
            disabled={busy || pending}
            onPress={() => void signOut()}
          />
        )}
      </View>
    </View>
  );
}

function sameInterests(left: string[], right: string[]) {
  return left.length === right.length && left.every((id) => right.includes(id));
}

function Intro({
  icon,
  title,
  description,
}: Readonly<{
  icon: React.ComponentProps<typeof Ionicons>["name"];
  title: string;
  description: string;
}>) {
  return (
    <View style={{ flexDirection: "row", gap: 14 }}>
      <Ionicons name={icon} size={26} color={colors.gold} accessible={false} />
      <View style={{ flex: 1, gap: 6 }}>
        <Text style={[styles.subtitle, { fontSize: 17, lineHeight: 24 }]}>
          {title}
        </Text>
        <Text style={[styles.muted, { fontSize: 14, lineHeight: 21 }]}>
          {description}
        </Text>
      </View>
    </View>
  );
}
