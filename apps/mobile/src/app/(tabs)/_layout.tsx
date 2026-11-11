import { Redirect, Tabs } from "expo-router";
import Ionicons from "@expo/vector-icons/Ionicons";
import { useWindowDimensions, type ColorValue } from "react-native";
import { useSession } from "../../session/session";
import { useAppTheme } from "../../shared/ui";
type TabIconProps = Readonly<{ color: ColorValue; focused: boolean }>;
function HomeIcon({ color, focused }: TabIconProps) {
  const { colors } = useAppTheme();
  return (
    <Ionicons
      name={focused ? "home" : "home-outline"}
      color={typeof color === "string" ? color : colors.muted}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
function QuestsIcon({ color }: TabIconProps) {
  const { colors } = useAppTheme();
  return (
    <Ionicons
      name="checkbox-outline"
      color={typeof color === "string" ? color : colors.muted}
      size={28}
      accessible={false}
      aria-hidden
    />
  );
}
function CalendarIcon({ color }: TabIconProps) {
  const { colors } = useAppTheme();
  return (
    <Ionicons
      name="calendar-outline"
      color={typeof color === "string" ? color : colors.muted}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
function ProgressIcon({ color }: TabIconProps) {
  const { colors } = useAppTheme();
  return (
    <Ionicons
      name="stats-chart-outline"
      color={typeof color === "string" ? color : colors.muted}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
function RoomIcon({ color }: TabIconProps) {
  const { colors } = useAppTheme();
  return (
    <Ionicons
      name="albums-outline"
      color={typeof color === "string" ? color : colors.muted}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
export const unstable_settings = { anchor: "index" };
export default function Layout() {
  const { colors } = useAppTheme();
  const { signedIn } = useSession();
  const { width } = useWindowDimensions();
  if (!signedIn) return <Redirect href="/" />;
  return (
    <Tabs
      backBehavior="initialRoute"
      screenOptions={{
        tabBarActiveTintColor: colors.green,
        tabBarActiveBackgroundColor: colors.sky,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: {
          backgroundColor: colors.paper,
          minHeight: 72,
          borderTopColor: colors.line,
          paddingTop: 6,
        },
        headerStyle: { backgroundColor: colors.paper },
        headerTintColor: colors.ink,
        tabBarLabelPosition: "below-icon",
        tabBarLabelStyle: {
          fontSize: width < 360 ? 11 : 12,
          fontFamily: "QuestSansBold",
        },
        tabBarItemStyle: {
          minHeight: 48,
          borderRadius: 24,
          overflow: "hidden",
          marginHorizontal: width < 360 ? 1 : 4,
          marginBottom: 6,
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: "Home",
          tabBarAccessibilityLabel: "Home",
          headerShown: false,
          tabBarIcon: HomeIcon,
        }}
      />
      <Tabs.Screen
        name="board"
        options={{
          title: "Quests",
          tabBarAccessibilityLabel: "Quests",
          headerShown: false,
          tabBarIcon: QuestsIcon,
        }}
      />
      <Tabs.Screen
        name="calendar"
        options={{
          title: "Calendar",
          tabBarAccessibilityLabel: "Calendar",
          tabBarIcon: CalendarIcon,
        }}
      />
      <Tabs.Screen
        name="room"
        options={{
          title: "Room",
          headerShown: false,
          tabBarAccessibilityLabel: "Room",
          tabBarIcon: RoomIcon,
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: "Progress",
          tabBarAccessibilityLabel: "Progress",
          tabBarIcon: ProgressIcon,
        }}
      />
    </Tabs>
  );
}
