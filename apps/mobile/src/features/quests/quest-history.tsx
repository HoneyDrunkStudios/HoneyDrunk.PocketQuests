import { SectionList, Text, View } from "react-native";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import type { OccurrenceView } from "../../shared/contracts";
import { colors, styles } from "../../shared/ui";
import { SessionRecovery } from "../../session/session-recovery";
import { QuestCard } from "./quest-card";

export type QuestHistorySection = {
  key: string;
  title?: string;
  data: OccurrenceView[];
  emptyContent?: React.ReactElement;
  footerContent?: React.ReactElement | null;
};
export function QuestHistory({
  sections,
  children,
}: Readonly<{
  sections: QuestHistorySection[];
  children: React.ReactNode;
}>) {
  const insets = useSafeAreaInsets();
  return (
    <SectionList
      sections={sections}
      keyExtractor={(item) => item.occurrence.id}
      renderItem={({ item }) => <QuestCard item={item} />}
      renderSectionHeader={({ section }) =>
        section.title ? (
          <Text accessibilityRole="header" style={styles.subtitle}>
            {section.title}
          </Text>
        ) : null
      }
      stickySectionHeadersEnabled={false}
      renderSectionFooter={({ section }) => (
        <View style={{ gap: 12 }}>
          {section.data.length === 0 ? section.emptyContent : null}
          {section.footerContent}
        </View>
      )}
      ListHeaderComponent={
        <View style={{ gap: 20 }}>
          <SessionRecovery />
          {children}
        </View>
      }
      contentInsetAdjustmentBehavior="automatic"
      style={{ flex: 1, backgroundColor: colors.paper }}
      contentContainerStyle={{
        padding: 20,
        paddingBottom: Math.max(insets.bottom, 20) + 24,
        gap: 20,
        width: "100%",
        maxWidth: 780,
        alignSelf: "center",
      }}
    />
  );
}
