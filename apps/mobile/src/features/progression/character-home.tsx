import {
  ImageBackground,
  Pressable,
  Text,
  View,
  useWindowDimensions,
} from "react-native";
import { Link, Redirect } from "expo-router";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { Page } from "../../session/session-page";
import { useAccountSnapshot, useSessionStatus } from "../../session/session";
import { Label, useAppTheme } from "../../shared/ui";
import { playfulArt } from "../../shared/playful-art";
import { AppBrand } from "../../shared/app-brand";
import { RankInsignia } from "../../shared/rank-insignia";
import {
  CharacterPortrait,
  TravelerBackdrop,
} from "../profile/character-portrait";
import { TodayQuests } from "../quests/today-quests";
import { activityRecap } from "./activity-recap";
import { levelProgress } from "./level-progress";

export function journalGreeting(now: Date, zone: string) {
  const hour = Number(
    new Intl.DateTimeFormat("en-US", {
      hour: "numeric",
      hourCycle: "h23",
      timeZone: zone,
    }).format(now),
  );
  return hour < 12
    ? "Good morning"
    : hour < 18
      ? "Good afternoon"
      : "Good evening";
}
/** Real traveler and confirmed progression in the selected illustrated quest-log layout. */
export function CharacterHome() {
  const { colors, styles, isDark } = useAppTheme();
  const { state, catalog } = useAccountSnapshot();
  const { pending } = useSessionStatus();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  if (!state)
    return (
      <Page>
        <Label>Opening your quest log...</Label>
      </Page>
    );
  if (!state.profile.onboardingComplete) return <Redirect href="/onboarding" />;
  const recap = activityRecap(state, 7);
  const progress = levelProgress(
    "overall",
    state.overallXp,
    state.overallLevel,
  );
  const homeDate = new Intl.DateTimeFormat("en-US", {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(state.today + "T00:00:00Z"));
  return (
    <Page
      header={
        <View
          style={{
            paddingTop: insets.top + 8,
            paddingBottom: 4,
            flexDirection: "row",
            alignItems: "center",
            gap: 8,
          }}
        >
          <View style={{ flex: 1 }}>
            <AppBrand />
          </View>
          <Link href="/settings" asChild>
            <Pressable
              accessibilityRole="link"
              accessibilityLabel="Settings and profile"
              style={{
                minHeight: 48,
                minWidth: 48,
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <Ionicons
                name="settings-outline"
                size={28}
                color={colors.ink}
                accessible={false}
              />
            </Pressable>
          </Link>
        </View>
      }
    >
      <View style={{ gap: 12 }}>
        <View testID="character-sheet" style={{ gap: 12 }}>
          <View style={{ gap: 3 }}>
            <Text
              accessibilityRole="header"
              style={[styles.title, { fontSize: 32, lineHeight: 40 }]}
            >
              Hey, {state.profile.characterName ?? "traveler"}!
            </Text>
            <Text style={[styles.muted, { fontSize: 15, lineHeight: 21 }]}>
              {homeDate}
            </Text>
          </View>
          <View
            testID="traveler-scene"
            style={{
              height: 360,
              marginHorizontal: -16,
              alignItems: "center",
              justifyContent: "center",
              overflow: "hidden",
            }}
          >
            <TravelerBackdrop
              avatarCode={state.profile.avatarCode}
              width={Math.min(width, 780)}
              height={360}
            />
            <CharacterPortrait
              avatarCode={state.profile.avatarCode}
              portraitId={state.profile.portraitId}
              size={360}
              pose="relaxed"
            />
          </View>
          <View
            style={{
              width: "100%",
              paddingBottom: 8,
              gap: 12,
            }}
          >
            <View
              style={{
                flexDirection: "row",
                alignItems: "center",
                gap: 5,
                flexWrap: "wrap",
                justifyContent: "center",
              }}
            >
              <View style={{ width: 88 }}>
                <RankInsignia
                  rank={state.rank.current}
                  size={86}
                  label={false}
                />
              </View>
              <View style={{ gap: 6 }}>
                <Text style={[styles.subtitle, { fontSize: 23 }]}>
                  Rank {state.rank.current}
                </Text>
                <Text
                  style={[
                    styles.text,
                    {
                      backgroundColor: colors.sky,
                      borderRadius: 18,
                      paddingHorizontal: 12,
                      paddingVertical: 3,
                      textAlign: "center",
                      fontSize: 16,
                      fontFamily: "QuestSansBold",
                    },
                  ]}
                >
                  Level {state.overallLevel}
                </Text>
              </View>
            </View>
            {progress ? (
              <View style={{ gap: 8 }}>
                <Text
                  style={[
                    styles.text,
                    {
                      fontFamily: "QuestSansBold",
                      textAlign: "center",
                      fontSize: 16,
                    },
                  ]}
                >
                  {progress.earned} / {progress.required} XP
                </Text>
                <View
                  accessible
                  accessibilityRole="progressbar"
                  accessibilityLabel="Overall level progress"
                  aria-valuemin={0}
                  aria-valuemax={progress.required}
                  aria-valuenow={progress.earned}
                  aria-valuetext={`${progress.earned}/${progress.required} XP toward level ${progress.nextLevel}`}
                  accessibilityValue={{
                    min: 0,
                    max: progress.required,
                    now: progress.earned,
                    text: `${progress.earned}/${progress.required} XP toward level ${progress.nextLevel}`,
                  }}
                  style={[
                    styles.progressTrack,
                    {
                      height: 14,
                      borderRadius: 10,
                      borderWidth: 1,
                      borderColor: colors.line,
                    },
                  ]}
                >
                  <View
                    style={[
                      styles.progressFill,
                      {
                        width: `${(progress.earned / progress.required) * 100}%`,
                        height: "100%",
                        borderRadius: 10,
                      },
                    ]}
                  />
                </View>
              </View>
            ) : (
              <Label>Level progress unavailable.</Label>
            )}
            <Link href="/wardrobe" asChild>
              <Pressable
                accessibilityRole="link"
                accessibilityLabel="Open your wardrobe"
                style={{
                  flexDirection: "row",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: 8,
                  minHeight: 48,
                  paddingHorizontal: 10,
                  paddingVertical: 8,
                  backgroundColor: colors.sky,
                  borderRadius: 16,
                }}
              >
                <Ionicons
                  name="shirt-outline"
                  size={24}
                  color={colors.green}
                  accessible={false}
                />
                <Text
                  style={[
                    styles.text,
                    {
                      fontFamily: "QuestSansBold",
                      color: colors.ink,
                      flexShrink: 1,
                    },
                  ]}
                >
                  Wardrobe
                </Text>
                <Ionicons
                  name="chevron-forward"
                  size={18}
                  color={colors.green}
                  accessible={false}
                />
              </Pressable>
            </Link>
          </View>
        </View>
        <ImageBackground
          source={playfulArt.firstAdventure}
          resizeMode="cover"
          imageStyle={{
            width: "100%",
            height: "100%",
            opacity: isDark ? 0.55 : 0.82,
            borderRadius: 18,
          }}
          style={{
            minHeight: 124,
            marginHorizontal: -8,
            borderRadius: 18,
            overflow: "hidden",
            backgroundColor: "#5836A2",
            paddingHorizontal: 20,
            paddingVertical: 12,
            gap: 2,
          }}
        >
          <Text
            accessibilityRole="header"
            style={[
              styles.subtitle,
              { color: "#FFFFFF", fontSize: 22, lineHeight: 28 },
            ]}
          >
            Your first adventure
          </Text>
          <Text
            style={[
              styles.text,
              { color: "#FFFFFF", fontSize: 15, lineHeight: 20 },
            ]}
          >
            Get to know your journal.
          </Text>
          <Link
            href={{ pathname: "/(tabs)", params: { tour: "start" } }}
            asChild
          >
            <Pressable
              accessibilityRole="link"
              accessibilityLabel="Start your first adventure"
              style={{
                alignSelf: "flex-start",
                minHeight: 48,
                paddingHorizontal: 22,
                paddingVertical: 10,
                backgroundColor: "#FFFFFF",
                borderRadius: 24,
                flexDirection: "row",
                alignItems: "center",
                gap: 10,
                marginTop: 0,
              }}
            >
              <Text
                style={[
                  styles.text,
                  {
                    color: "#6536BB",
                    fontFamily: "QuestSansBold",
                    fontSize: 18,
                  },
                ]}
              >
                Start the tour
              </Text>
              <Ionicons
                name="arrow-forward"
                size={22}
                color="#6536BB"
                accessible={false}
              />
            </Pressable>
          </Link>
        </ImageBackground>
        <TodayQuests
          compact
          state={state}
          catalog={catalog}
          completedToday={recap?.days.at(-1)?.count ?? 0}
          pending={pending}
        />
        <Link
          href="/(tabs)/profile"
          style={[
            styles.muted,
            {
              minHeight: 48,
              paddingVertical: 12,
              textAlign: "center",
              fontSize: 13,
            },
          ]}
        >
          See your progress · {state.overallXp.toLocaleString()} overall XP
        </Link>
      </View>
    </Page>
  );
}
