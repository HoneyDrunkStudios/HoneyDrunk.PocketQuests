import { useId } from "react";
import { View } from "react-native";
import Svg, {
  Defs,
  ClipPath,
  Ellipse,
  FeColorMatrix,
  Filter,
  G,
  Image,
  Path,
  Rect,
} from "react-native-svg";
import { decodeAvatar } from "./avatar-look";
import {
  avatarArt,
  faceArtwork,
  hairColorMatrix,
  hairArtwork,
  outfitArtwork,
  type ArtRect,
  type AvatarAtlas,
} from "./avatar-art";

function ArtLayer({
  atlas,
  region,
  placement,
  filter,
  cutout,
}: Readonly<{
  atlas: AvatarAtlas;
  region: ArtRect;
  placement: ArtRect;
  filter?: string;
  cutout?: string;
}>) {
  const [x, y, width, height] = placement;
  const [sourceX, sourceY, sourceWidth, sourceHeight] = region;
  const clipId = `avatar-cutout-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const boundsId = `${clipId}-bounds`;
  const scaleX = width / sourceWidth;
  const scaleY = height / sourceHeight;
  return (
    <>
      <Defs>
        <ClipPath id={boundsId}>
          <Rect
            x={sourceX}
            y={sourceY}
            width={sourceWidth}
            height={sourceHeight}
          />
        </ClipPath>
        {cutout && (
          <ClipPath id={clipId}>
            <Path d={cutout} />
          </ClipPath>
        )}
      </Defs>
      {/* One SVG root keeps native filter/clip lookup in the same definition scope. */}
      <G
        transform={`translate(${x - sourceX * scaleX} ${y - sourceY * scaleY}) scale(${scaleX} ${scaleY})`}
        clipPath={`url(#${boundsId})`}
      >
        <Image
          href={atlas.source}
          x={0}
          y={0}
          width={atlas.width}
          height={atlas.height}
          filter={filter}
          clipPath={cutout ? `url(#${clipId})` : undefined}
        />
      </G>
    </>
  );
}

/** Front-facing painted paper doll. Saved v1 slots retain their original meaning. */
export function AvatarFigure({
  code,
  portrait = false,
  size = 300,
}: Readonly<{
  code?: string | null;
  portrait?: boolean;
  size?: number;
}>) {
  const look = decodeAvatar(code);
  const face = faceArtwork(look.face, look.skin);
  const hair = hairArtwork(look.hairStyle);
  const outfit = outfitArtwork(look.build, look.outfit);
  const [faceX, faceY, faceWidth, faceHeight] = face.region;
  const neckRegion: ArtRect = [
    faceX,
    faceY + face.split,
    faceWidth,
    faceHeight - face.split,
  ];
  const filterId = `avatar-hair-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const neckId = `${filterId}-neck`;
  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel="Your customizable traveler avatar"
      style={{
        width: portrait ? size : "100%",
        height: size,
        alignItems: "center",
        overflow: "hidden",
        borderRadius: portrait ? size / 2 : 20,
      }}
    >
      <Svg
        width={portrait ? size : size * 0.55}
        height={size}
        viewBox={portrait ? "85 -20 230 240" : "0 -20 400 840"}
      >
        <Defs>
          <ClipPath id={neckId}>
            <Path d="M180 147 H220 V169 L211 186 H189 L180 169 Z" />
          </ClipPath>
          <Filter id={filterId} x="0%" y="0%" width="100%" height="100%">
            <FeColorMatrix
              type="matrix"
              values={hairColorMatrix(look.hairColor)}
            />
          </Filter>
        </Defs>
        <Ellipse
          cx={200}
          cy={787}
          rx={133}
          ry={12}
          fill="#071522"
          opacity={0.65}
        />
        <ArtLayer
          atlas={face.atlas}
          region={[faceX, faceY, faceWidth, face.split]}
          placement={[154, 36, 92, 111]}
        />
        <ArtLayer
          atlas={face.atlas}
          region={neckRegion}
          placement={[154, 147, 92, 75]}
        />
        {look.accessory === 3 && (
          <ArtLayer
            atlas={avatarArt.accessories}
            region={[1070, 30, 704, 825]}
            placement={
              look.build === 0 ? [13, 145, 374, 602] : [2, 145, 396, 602]
            }
            cutout="M1110 30 H1774 V855 H1070 V590 H1110 Z"
          />
        )}
        <ArtLayer
          atlas={outfit.atlas}
          region={outfit.region}
          placement={outfit.placement}
        />
        {look.accessory === 1 && (
          <ArtLayer
            atlas={avatarArt.accessories}
            region={[45, 65, 490, 720]}
            placement={[
              look.build === 0 ? 130 : 116,
              176,
              look.build === 0 ? 159 : 180,
              outfit.waist + 90 - 176,
            ]}
          />
        )}
        {look.accessory === 2 && (
          <ArtLayer
            atlas={avatarArt.accessories}
            region={[580, 340, 530, 230]}
            placement={[
              look.build === 0 ? 136 : 119,
              outfit.waist - 19,
              look.build === 0 ? 128 : 162,
              64,
            ]}
          />
        )}
        {/* The back of a collar passes behind the neck, not across its front. */}
        <G clipPath={`url(#${neckId})`}>
          <ArtLayer
            atlas={face.atlas}
            region={neckRegion}
            placement={[154, 147, 92, 75]}
          />
        </G>
        {look.accessory === 5 && (
          <ArtLayer
            atlas={avatarArt.starterAccessories}
            region={[1025, 105, 725, 695]}
            placement={[160, 152, 80, 82]}
          />
        )}
        {look.accessory === 4 && (
          <ArtLayer
            atlas={avatarArt.starterAccessories}
            region={[45, 300, 845, 315]}
            placement={[165, 86, 70, 26]}
          />
        )}
        {/* Hair sits over collars; the long style remains independent of garments. */}
        {hair && (
          <G>
            <ArtLayer
              atlas={hair.atlas}
              region={hair.region}
              placement={hair.placement}
              filter={`url(#${filterId})`}
            />
          </G>
        )}
      </Svg>
    </View>
  );
}
