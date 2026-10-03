export type FocusClock = { remainingMs: number; startedAt: number | null };
export function remainingFocus(clock: FocusClock, now: number) {
  return Math.max(
    0,
    clock.remainingMs -
      (clock.startedAt === null ? 0 : Math.max(0, now - clock.startedAt)),
  );
}
export function pauseFocus(clock: FocusClock, now: number): FocusClock {
  return { remainingMs: remainingFocus(clock, now), startedAt: null };
}
