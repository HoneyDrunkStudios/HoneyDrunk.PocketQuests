import { useState } from "react";
import { ImageBackground, Text, View } from "react-native";
import { journalArt } from "../../shared/journal-art";
import { RankSummary } from "./rank-summary";
import { RankRequirements } from "./rank-requirements";
import type { Catalog, State } from "../../shared/contracts";
import { Card, colors, styles } from "../../shared/ui";

/** Display API requirements and balances; promotion remains server-owned. */
export function RankProgress({
  rank,
  categories,
  rules,
  compact = false,
}: Readonly<{
  rank: State["rank"];
  categories: State["categories"];
  rules: Catalog["rules"];
  compact?: boolean;
}>) {
  const [showProgress, setShowProgress] = useState(false);
  return (
    <Card
      style={{ gap: 12, borderColor: colors.line, padding: compact ? 10 : 16 }}
    >
      {compact && (
        <View
          pointerEvents="none"
          style={{ position: "absolute", top: 0, bottom: 0, left: 0, right: 0 }}
        >
          <ImageBackground
            testID="journal-background-rank"
            source={journalArt.landscape}
            imageStyle={{
              width: "100%",
              height: "100%",
              opacity: 0.25,
              borderRadius: 14,
            }}
            style={{ flex: 1 }}
          />
        </View>
      )}
      {compact ? (
        <RankSummary
          rank={rank.current}
          expanded={showProgress}
          onPress={() => setShowProgress((value) => !value)}
        />
      ) : (
        <Text accessibilityRole="header" style={styles.subtitle}>
          Global rank {rank.current}
        </Text>
      )}
      {(!compact || showProgress) && (
        <RankRequirements rank={rank} categories={categories} rules={rules} />
      )}
    </Card>
  );
}
