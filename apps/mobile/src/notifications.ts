import { Platform } from "react-native";
import type { State } from "./contracts";
import { warningTimes } from "./notification-plan";
let serial: Promise<string> = Promise.resolve("");
export async function requestWarningPermission(): Promise<boolean> {
  if (Platform.OS === "web") return false;
  const notifications = await import("expo-notifications");
  if (Platform.OS === "android")
    await notifications.setNotificationChannelAsync("quest-expiry", {
      name: "Quest expiry warnings",
      importance: notifications.AndroidImportance.DEFAULT,
    });
  return (await notifications.requestPermissionsAsync()).granted;
}
export function syncWarnings(state: State | null): Promise<string> {
  serial = serial
    .catch(() => "")
    .then(async () => {
      if (Platform.OS === "web")
        return "Phone notifications are available in the installed iOS or Android app.";
      const notifications = await import("expo-notifications");
      await notifications.cancelAllScheduledNotificationsAsync();
      if (!state?.profile.expiryWarnings) {
        await notifications.dismissAllNotificationsAsync();
        return "Expiry warnings are off.";
      }
      if (!(await notifications.getPermissionsAsync()).granted)
        return "Notification permission is unavailable. Quests remain usable; enable permission in phone settings to receive warnings.";
      notifications.setNotificationHandler({
        handleNotification: async () => ({
          shouldShowBanner: true,
          shouldShowList: true,
          shouldPlaySound: false,
          shouldSetBadge: false,
        }),
      });
      for (const at of warningTimes(state, Date.now()))
        await notifications.scheduleNotificationAsync({
          identifier: `quest-expiry-${at}`,
          content: {
            title: "Quests are due today",
            body: "Check your quests before the end of the day.",
          },
          trigger: {
            type: notifications.SchedulableTriggerInputTypes.DATE,
            date: new Date(at),
            channelId: "quest-expiry",
          },
        });
      return "One grouped warning is scheduled 60 minutes before each eligible due-day midnight. Delivery depends on your phone; warnings refresh when the app connects.";
    });
  return serial;
}
