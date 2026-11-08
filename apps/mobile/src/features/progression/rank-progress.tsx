import { useState } from "react";
import { Text } from "react-native";
import { RankSummary } from "./rank-summary";
import { RankRequirements } from "./rank-requirements";
import type { Catalog, State } from "../../shared/contracts";
import { Card, useAppTheme } from "../../shared/ui";

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
  const { colors, styles } = useAppTheme();
  const [showProgress, setShowProgress] = useState(false);
  return (
    <Card
      style={{ gap: 12, borderColor: colors.line, padding: compact ? 6 : 16 }}
    >
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
