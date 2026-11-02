import { useRef, useState } from "react";
import { Redirect, useRouter } from "expo-router";
import { Pressable, ScrollView, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useSession } from "../../session/session";
import { SessionRecovery } from "../../session/session-recovery";
import { JournalHeading } from "../../shared/journal-heading";
import { Button, Card, Label, colors, styles } from "../../shared/ui";
import { questActions } from "../quests/commands/quest-actions";
import { CharacterEditor } from "./character-editor";
import { CharacterPortrait } from "./character-portrait";
import { AvatarEditor } from "./avatar-editor";
import { StartingSkills } from "./starting-skills";

const steps = [
  "Your name",
  "Your traveler",
  "Your interests",
  "Starting skills",
  "Your first quest",
] as const;
const icons = [
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
function sameInterests(left: string[], right: string[]) {
  return left.length === right.length && left.every((id) => right.includes(id));
}

export function Onboarding({
  editExisting = false,
}: Readonly<{ editExisting?: boolean }>) {
  const {
    state,
    catalog,
    busy,
    pending,
    offline,
    queuedCount,
    command,
    signOut,
  } = useSession();
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(0);
  const [interests, setInterests] = useState<string[] | null>(null);
  const [interestSave, setInterestSave] = useState<{
    interests: string[];
    finished: boolean;
  } | null>(null);
  const submitting = useRef(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const insets = useSafeAreaInsets();
  const isSavingInterests = !!interestSave && !interestSave.finished;
  const interestsConfirmed =
    currentStep === 2 &&
    interestSave?.finished &&
    state &&
    !busy &&
    !pending &&
    queuedCount === 0 &&
    sameInterests(interestSave.interests, state.profile.interests);
  if (interestsConfirmed) {
    setCurrentStep(3);
    setInterestSave(null);
  }
  const step = currentStep;
  if (state?.profile.onboardingComplete && !editExisting)
    return <Redirect href="/(tabs)/board" />;
  const disabled =
    busy || pending || offline || !state || !catalog || isSavingInterests;
  const choices = interests ?? state?.profile.interests ?? [];
  async function continueWithInterests() {
    if (disabled || submitting.current || !state) return;
    if (sameInterests(choices, state.profile.interests)) {
      setCurrentStep(3);
      return;
    }
    submitting.current = true;
    setSaveError(null);
    setInterestSave({ interests: choices, finished: false });
    try {
      await command({ action: questActions.interests, interests: choices });
    } catch {
      setSaveError("We couldn’t save your choices. Please try again.");
    } finally {
      setInterestSave({ interests: choices, finished: true });
      submitting.current = false;
    }
  }
  return (
    <View style={{ flex: 1, backgroundColor: colors.paper }}>
      <ScrollView
        key={step}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
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
            title="Create your character"
            description="A name. A face. A story that’s yours."
          />
        ) : (
          <View style={{ paddingTop: insets.top + 24, gap: 10 }}>
            <Text
              style={[styles.muted, { color: colors.gold, letterSpacing: 2 }]}
            >
              POCKET QUESTS
            </Text>
            <Text accessibilityRole="header" style={styles.title}>
              {steps[step]}
            </Text>
          </View>
        )}
        <View
          accessibilityLabel={`Step ${step + 1} of 5: ${steps[step]}`}
          style={{ flexDirection: "row", alignItems: "center", gap: 12 }}
        >
          <Text style={[styles.muted, { color: colors.gold }]}>
            0{step + 1} / 05
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
              <CharacterEditor onContinue={() => setCurrentStep(1)} />
            )}
            {step === 1 && (
              <AvatarEditor onContinue={() => setCurrentStep(2)} />
            )}
            {step === 2 && (
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
                    return (
                      <Pressable
                        key={category.id}
                        accessibilityRole="checkbox"
                        accessibilityLabel={category.name}
                        accessibilityState={{
                          checked: selected,
                          disabled: isSavingInterests,
                        }}
                        aria-checked={selected}
                        disabled={isSavingInterests}
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
                          opacity: isSavingInterests ? 0.6 : pressed ? 0.8 : 1,
                        })}
                      >
                        <Ionicons
                          name={icons[index % icons.length]}
                          size={24}
                          color={colors.gold}
                          accessible={false}
                        />
                        <Text
                          style={[
                            styles.text,
                            { fontSize: 15, lineHeight: 21 },
                          ]}
                        >
                          {category.name}
                          {selected ? " ✓" : ""}
                        </Text>
                      </Pressable>
                    );
                  })}
                </View>
                <Label style={styles.muted}>
                  Optional. Your choices stay here until you continue. You can
                  change them later.
                </Label>
              </>
            )}
            {step === 3 && (
              <StartingSkills onContinue={() => setCurrentStep(4)} />
            )}
            {step === 4 && (
              <Card style={{ borderColor: colors.gold, padding: 22, gap: 18 }}>
                <CharacterPortrait
                  portraitId={state.profile.portraitId}
                  avatarCode={state.profile.avatarCode}
                />
                <Text accessibilityRole="header" style={styles.title}>
                  {state.profile.characterName
                    ? `Ready, ${state.profile.characterName}?`
                    : "Your journal is ready"}
                </Text>
                <Label>
                  Choose a quest that fits today. Open its card, plan your next
                  step, and start when you’re ready.
                </Label>
                <Label>
                  Do it in the real world, then mark it complete to watch your
                  character grow.
                </Label>
              </Card>
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
        {step === 2 && (
          <Button
            title={isSavingInterests ? "Saving choices…" : "Continue"}
            disabled={disabled}
            onPress={() => void continueWithInterests()}
          />
        )}
        {step === 4 && (
          <Button
            title={editExisting ? "Return to Home" : "Choose my first quest"}
            disabled={disabled}
            loading={busy}
            onPress={() =>
              editExisting
                ? router.replace("/(tabs)")
                : void command({ action: questActions.finishOnboarding })
            }
          />
        )}
        {step > 0 ? (
          <Button
            title="Back"
            secondary
            disabled={busy || pending || isSavingInterests}
            onPress={() => {
              setInterestSave(null);
              setCurrentStep(step - 1);
            }}
          />
        ) : (
          <Button
            title={editExisting ? "Return to Home" : "Sign out"}
            secondary
            disabled={busy || pending}
            onPress={() =>
              editExisting ? router.replace("/(tabs)") : void signOut()
            }
          />
        )}
      </View>
    </View>
  );
}
