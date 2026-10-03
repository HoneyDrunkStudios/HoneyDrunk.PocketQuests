import { AccessibilityInfo, Modal, ScrollView, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { useSession } from "../../session/session";
import { CompletionCelebration } from "./completion-celebration";
import { completionAnnouncement } from "./completion-feedback";

/** A single root overlay keeps reward feedback visible from every scroll position. */
export function CompletionOverlay() {
  const { recentCompletion, dismissCompletion, state, command, busy, pending } =
    useSession();
  const insets = useSafeAreaInsets();
  if (!recentCompletion) return null;
  return (
    <Modal
      key={recentCompletion.completionId}
      visible
      transparent
      animationType="none"
      onRequestClose={dismissCompletion}
      onShow={() =>
        AccessibilityInfo.announceForAccessibility(
          completionAnnouncement(recentCompletion),
        )
      }
    >
      <View
        accessibilityViewIsModal
        style={{
          flex: 1,
          backgroundColor: "rgba(39,37,31,0.65)",
          justifyContent: "center",
          paddingTop: insets.top + 20,
          paddingBottom: insets.bottom + 20,
        }}
      >
        <ScrollView
          contentContainerStyle={{
            flexGrow: 1,
            justifyContent: "center",
            padding: 20,
            width: "100%",
            maxWidth: 600,
            alignSelf: "center",
          }}
        >
          <CompletionCelebration
            key={recentCompletion.completionId}
            feedback={recentCompletion}
            canUndo={
              !busy &&
              !pending &&
              !!state?.occurrences.some(
                (o) =>
                  o.canUndo &&
                  o.completion?.id === recentCompletion.completionId,
              )
            }
            onDismiss={dismissCompletion}
            onUndo={() =>
              void command({
                action: "undo",
                occurrenceId: recentCompletion.occurrenceId,
                completionId: recentCompletion.completionId,
              })
            }
          />
        </ScrollView>
      </View>
    </Modal>
  );
}
