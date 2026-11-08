import { useId } from "react";
import { Platform, View } from "react-native";
import Svg, { Defs, FeColorMatrix, Filter, G } from "react-native-svg";
import { decodeAvatar, petiteBuild, scarfColors } from "./avatar-look";
import {
  avatarRig,
  kitBody,
  kitFace,
  kitHair,
  headPlacement,
  type KitSprite,
} from "./avatar-kit";
import { hairColorMatrix, skinColorMatrix } from "./avatar-palette";
import { AvatarSprite } from "./avatar-sprite";

const gearAtlas = {
  source: require("../../../assets/avatar/accessories.png"),
  width: 1774,
  height: 887,
};
const extraAtlas = {
  source: require("../../../assets/avatar/accessories-starters.png"),
  width: 1774,
  height: 887,
};
function SkinSprite({
  sprite,
  filter,
}: Readonly<{ sprite: KitSprite; filter: string }>) {
  return (
    <>
      <AvatarSprite sprite={sprite} />
      <AvatarSprite sprite={sprite} filter={filter} mask={sprite.skinMask} />
    </>
  );
}

/** A single registered kit: clothing already contains its attached neck and hands. */
export function AvatarFigure({
  code,
  portrait = false,
  size = 300,
  pose = "relaxed",
}: Readonly<{
  code?: string | null;
  portrait?: boolean;
  size?: number;
  pose?: "neutral" | "relaxed";
}>) {
  const look = decodeAvatar(code);
  const poseIndex = pose === "neutral" ? 0 : look.pose;
  const body = kitBody(look.outfit, look.build, poseIndex, look.bodyType);
  const face = kitFace(look.face, poseIndex),
    hair = kitHair(look.hairStyle, poseIndex);
  const id = `avatar-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;
  const bodyId = `${id}-skin-body-${look.outfit}-${look.bodyType}-${look.build}-${poseIndex}-${look.skin}`;
  const faceId = `${id}-skin-face-${look.face}-${poseIndex}-${look.skin}`;
  const hairId = `${id}-hair-${look.hairColor}-${poseIndex}`,
    scarfId = `${id}-scarf-${look.scarfColor}`;
  const filterUnits =
    Platform.OS === "android" ? "userSpaceOnUse" : "objectBoundingBox";
  const colorSpace =
    Platform.OS === "web" ? { colorInterpolationFilters: "sRGB" } : {};
  const gearWidths =
    look.bodyType === 1 ? [1, 1.08, 1.19, 0.96] : [1, 1.15, 1.22, 0.94];
  const broad = gearWidths[look.build];
  const { cranialCenterX, soleY } = avatarRig;
  const transform =
    !portrait && look.build === petiteBuild
      ? `translate(${cranialCenterX} ${soleY}) scale(0.94) translate(-${cranialCenterX} -${soleY})`
      : undefined;
  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel="Your customizable traveler avatar"
      style={{
        width: portrait ? size : size * 0.55,
        alignSelf: "center",
        height: size,
        alignItems: "center",
        overflow: "hidden",
        borderRadius: portrait ? size / 2 : 20,
      }}
    >
      <Svg
        width={portrait ? size : size * 0.55}
        height={size}
        viewBox={portrait ? "128 64 144 164" : "0 -20 400 840"}
      >
        <Defs>
          <Filter
            id={bodyId}
            {...colorSpace}
            filterUnits={filterUnits}
            x="-10%"
            y="-10%"
            width="120%"
            height="120%"
          >
            <FeColorMatrix
              type="matrix"
              values={skinColorMatrix(look.skin, body.skinReference!)}
            />
          </Filter>
          <Filter
            id={faceId}
            {...colorSpace}
            filterUnits={filterUnits}
            x="-10%"
            y="-10%"
            width="120%"
            height="120%"
          >
            <FeColorMatrix
              type="matrix"
              values={skinColorMatrix(look.skin, face.skinReference!)}
            />
          </Filter>
          <Filter
            id={hairId}
            {...colorSpace}
            filterUnits={filterUnits}
            x="-10%"
            y="-10%"
            width="120%"
            height="120%"
          >
            <FeColorMatrix
              type="matrix"
              values={hairColorMatrix(look.hairColor)}
            />
          </Filter>
          {look.scarfColor > 0 && (
            <Filter
              id={scarfId}
              {...colorSpace}
              filterUnits={filterUnits}
              x="-10%"
              y="-10%"
              width="120%"
              height="120%"
            >
              <FeColorMatrix
                type="matrix"
                values={scarfColorMatrix(scarfColors[look.scarfColor])}
              />
            </Filter>
          )}
        </Defs>
        <G transform={transform}>
          {look.accessory === 3 && (
            <AvatarSprite
              sprite={{
                atlas: gearAtlas,
                region: [1070, 30, 704, 825],
                placement: [75 - (broad - 1) * 100, 173, 250 * broad, 570],
              }}
              mask="M1110 30H1774V855H1070V590H1110Z"
            />
          )}
          <G id={`${id}-body`}>
            <SkinSprite sprite={body} filter={`url(#${bodyId})`} />
          </G>
          {look.accessory === 1 && (
            <AvatarSprite
              sprite={{
                atlas: gearAtlas,
                region: [45, 65, 490, 720],
                placement: [146 - (broad - 1) * 50, 190, 130 * broad, 191],
              }}
            />
          )}
          {look.accessory === 2 && (
            <AvatarSprite
              sprite={{
                atlas: gearAtlas,
                region: [580, 340, 530, 230],
                placement: [145 - (broad - 1) * 50, 368, 115 * broad, 50],
              }}
            />
          )}
          <G id={`${id}-face`}>
            <SkinSprite sprite={face} filter={`url(#${faceId})`} />
          </G>
          {(look.accessory === 5 || look.extra === 2) && (
            <AvatarSprite
              sprite={{
                atlas: extraAtlas,
                region: [1025, 105, 725, 695],
                placement: [167, 171, 70, 64],
              }}
              filter={look.scarfColor > 0 ? `url(#${scarfId})` : undefined}
            />
          )}
          {(look.accessory === 4 || look.extra === 1) && (
            <AvatarSprite
              sprite={{
                atlas: extraAtlas,
                region: [45, 300, 845, 315],
                placement: headPlacement([165, 112, 70, 26]),
              }}
            />
          )}
          {hair && (
            <G id={`${id}-hair-layer`}>
              <AvatarSprite sprite={hair} filter={`url(#${hairId})`} />
            </G>
          )}
        </G>
      </Svg>
    </View>
  );
}
function scarfColorMatrix(color: string) {
  const channels = [1, 3, 5].map(
    (offset) => parseInt(color.slice(offset, offset + 2), 16) / 255,
  );
  return (
    channels
      .map(
        (channel) =>
          `${0.299 * channel} ${0.587 * channel} ${0.114 * channel} 0 0`,
      )
      .join(" ") + " 0 0 0 1 0"
  );
}
/** The exact worn torso, without substituting an unrelated face or gear. */
export function OutfitPreview({
  code,
  size = 88,
}: Readonly<{ code: string; size?: number }>) {
  const look = decodeAvatar(code),
    body = kitBody(look.outfit, look.build, look.pose, look.bodyType);
  const id = `outfit-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}-${look.skin}-${look.outfit}-${look.bodyType}-${look.build}-${look.pose}`;
  return (
    <View aria-hidden pointerEvents="none">
      <Svg width={size} height={size} viewBox="60 218 280 240">
        <Defs>
          <Filter
            id={id}
            filterUnits={
              Platform.OS === "android" ? "userSpaceOnUse" : "objectBoundingBox"
            }
            {...(Platform.OS === "web"
              ? { colorInterpolationFilters: "sRGB" }
              : {})}
            x="-10%"
            y="-10%"
            width="120%"
            height="120%"
          >
            <FeColorMatrix
              type="matrix"
              values={skinColorMatrix(look.skin, body.skinReference!)}
            />
          </Filter>
        </Defs>
        <SkinSprite sprite={body} filter={`url(#${id})`} />
      </Svg>
    </View>
  );
}
