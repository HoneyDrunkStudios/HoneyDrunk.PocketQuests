# Journal typography

`LibreBaskerville.ttf` is the original bundled variable font, licensed under the adjacent SIL Open Font License. `JournalSerifBold.ttf` is a static weight-700 instance produced with fontTools 4.67.0 from that file. Its family and PostScript names are renamed to Pocket Journal Serif to respect the reserved font name; the original copyright and license records are retained.

Expo runtime font loading registers each face in Android's normal style slot. Use the `JournalSerifBold` family with `fontWeight: "normal"`: the file itself contains the bold outlines. Asking for weight 700 with the former runtime family selected an unregistered bold variant and Android fell back to its system sans-serif font. This affected every journal heading despite the web preview looking correct.

The generated static file is bundled directly; fontTools is a local asset preparation utility, not a runtime or build dependency. iOS and web use the same face.
