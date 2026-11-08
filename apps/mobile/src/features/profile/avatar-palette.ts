export function hairColorMatrix(index: number): number[] {
  // Keep the painted strand range: dark colors still need visible midtones
  // and highlights. A small base tint keeps roots colored rather than gray.
  const palettes = [
    [0.6, 0.52, 0.47, 0.025, 0.023, 0.025], // Midnight
    [0.95, 0.65, 0.42, 0.05, 0.025, 0.008], // Chestnut
    [1.03, 0.86, 0.56, 0.15, 0.09, 0.025], // Honey
    [0.85, 0.89, 0.95, 0.13, 0.14, 0.16], // Silver
    [1.12, 0.55, 0.28, 0.08, 0.03, 0.01], // Copper
    [0.48, 0.49, 0.54, 0.008, 0.012, 0.018], // Raven
    [0.75, 0.43, 0.73, 0.045, 0.02, 0.045], // Plum
    [0.35, 0.7, 0.72, 0.018, 0.04, 0.045], // Teal
  ];

  const [r, g, b, ro, go, bo] = palettes[index] ?? palettes[0];

  return [r, 0, 0, 0, ro, 0, g, 0, 0, go, 0, 0, b, 0, bo, 0, 0, 0, 1, 0];
}

/** Palette changes source skin only; clothing and eye whites retain their pigments. */
export function skinColorMatrix(
  tone: number,
  reference: readonly number[],
): number[] {
  const targets = [
    [226, 187, 156],
    [212, 164, 123],
    [195, 133, 92],
    [165, 105, 69],
    [127, 78, 53],
    [94, 59, 44],
  ];
  const target = targets[tone];
  const gains = target.map((value, i) =>
    Math.min(tone >= 4 ? 0.7 : 0.85, (250 - value) / (255 - reference[i])),
  );
  const offsets = target.map(
    (value, i) => (value - gains[i] * reference[i]) / 255,
  );
  return [
    gains[0],
    0,
    0,
    0,
    offsets[0],
    0,
    gains[1],
    0,
    0,
    offsets[1],
    0,
    0,
    gains[2],
    0,
    offsets[2],
    0,
    0,
    0,
    1,
    0,
  ];
}
