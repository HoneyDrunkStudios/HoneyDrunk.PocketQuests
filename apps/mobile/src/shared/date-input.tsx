import { useState, type ComponentProps } from "react";
import { Input } from "./ui";
import { calendarDateInput, displayDate } from "./date-format";

type Props = Omit<ComponentProps<typeof Input>, "value" | "onChangeText"> & {
  value: string;
  onChangeText(value: string): void;
};

export function DateInput({
  value,
  onChangeText,
  placeholder,
  ...props
}: Props) {
  const [draft, setDraft] = useState<{ text: string; value: string } | null>(
    null,
  );
  return (
    <Input
      {...props}
      placeholder={placeholder ? displayDate(placeholder) : "MM/DD/YYYY"}
      value={draft?.value === value ? draft.text : displayDate(value)}
      autoCapitalize="none"
      autoCorrect={false}
      keyboardType="numbers-and-punctuation"
      maxLength={10}
      onChangeText={(text) => {
        const next = calendarDateInput(text);
        setDraft({ text, value: next });
        onChangeText(next);
      }}
    />
  );
}
