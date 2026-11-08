import { TravelerRoom } from "../../features/profile/room-screen";
import { SessionRecovery } from "../../session/session-recovery";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { useRouter } from "expo-router";
export default function Room() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  return (
    <TravelerRoom
      topInset={insets.top}
      bottomInset={insets.bottom}
      recovery={<SessionRecovery />}
      onFindQuests={() => router.push("/(tabs)/board")}
    />
  );
}
