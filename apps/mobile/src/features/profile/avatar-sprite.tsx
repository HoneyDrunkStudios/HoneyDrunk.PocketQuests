import { useId } from "react";
import { ClipPath, Defs, G, Image, Path, Rect } from "react-native-svg";
import type { KitSprite } from "./avatar-kit";

/** Crop the unchanged source artwork inside the avatar's single SVG scope. */
export function AvatarSprite({
  sprite,
  filter,
  mask,
}: Readonly<{
  sprite: KitSprite;
  filter?: string;
  mask?: string;
}>) {
  const [x, y, w, h] = sprite.placement;
  const [sx, sy, sw, sh] = sprite.region;
  const id = `sprite-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const bounds = `${id}-${sprite.region.join("-")}`;
  const shape = `${id}-skin-${Array.from(mask ?? "").reduce((sum, char) => (sum * 31 + char.codePointAt(0)!) >>> 0, 0)}`;
  const scaleX = w / sw,
    scaleY = h / sh;
  return (
    <>
      <Defs>
        <ClipPath id={bounds}>
          <Rect x={sx} y={sy} width={sw} height={sh} />
        </ClipPath>
        {mask && (
          <ClipPath id={shape}>
            <Path d={mask} clipRule="evenodd" fillRule="evenodd" />
          </ClipPath>
        )}
      </Defs>
      <G
        transform={`translate(${x - sx * scaleX} ${y - sy * scaleY}) scale(${scaleX} ${scaleY})`}
        clipPath={`url(#${bounds})`}
      >
        <Image
          href={sprite.atlas.source}
          x={0}
          y={0}
          width={sprite.atlas.width}
          height={sprite.atlas.height}
          filter={filter}
          clipPath={mask ? `url(#${shape})` : undefined}
        />
      </G>
    </>
  );
}
