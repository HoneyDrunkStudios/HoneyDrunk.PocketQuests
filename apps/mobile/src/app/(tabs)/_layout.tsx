import { Redirect, Tabs } from "expo-router";
import Ionicons from "@expo/vector-icons/Ionicons";
import type { ColorValue } from "react-native";
import { useSession } from "../../session/session";
import { colors } from "../../shared/ui";
type TabIconProps = Readonly<{ color: ColorValue; focused: boolean }>;
function HomeIcon({ color, focused }: TabIconProps) {
  return (
    <Ionicons
      name={focused ? "home" : "home-outline"}
      color={color}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
function QuestsIcon({ color }: TabIconProps) {
  return (
    <Ionicons
      name="compass-outline"
      color={color}
      size={28}
      accessible={false}
      aria-hidden
    />
  );
}
function CalendarIcon({ color }: TabIconProps) {
  return (
    <Ionicons
      name="calendar-outline"
      color={color}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
function ProgressIcon({ color }: TabIconProps) {
  return (
    <Ionicons
      name="stats-chart-outline"
      color={color}
      size={26}
      accessible={false}
      aria-hidden
    />
  );
}
export default function Layout() {
  const { signedIn } = useSession();
  if (!signedIn) return <Redirect href="/" />;
  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.green,
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
        tabBarLabelStyle: { fontSize: 12 },
        tabBarItemStyle: { minHeight: 48 },
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
