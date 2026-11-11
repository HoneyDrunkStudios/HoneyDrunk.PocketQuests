export const roomArt = [
  require("../../../assets/room/room-0.png"),
  require("../../../assets/room/room-1.png"),
  require("../../../assets/room/room-2.png"),
] as const;
export const portraitRoomArt = [
  require("../../../assets/room/room-portrait-0.png"),
  require("../../../assets/room/room-portrait-1.png"),
  require("../../../assets/room/room-portrait-2.png"),
] as const;
export const decorationArt = require("../../../assets/room/decorations.png");
export const decorationBounds = [
  [84, 32, 356, 448],
  [530, 104, 475, 373],
  [1103, 48, 393, 422],
  [97, 534, 348, 445],
  [521, 596, 486, 363],
  [1140, 528, 279, 447],
] as const;
export const roomAnchors = [
  [378 / 1312, 412 / 1199],
  [942 / 1312, 412 / 1199],
  [319 / 1312, 684 / 1199],
  [995 / 1312, 684 / 1199],
] as const;

/** Cover tall phones while keeping the original full composition on wide screens. */
export function roomSceneLayout(width: number, height: number, room: number) {
  const portrait = width / height < 0.85;
  const sourceWidth = portrait ? 948 : 1312;
  const sourceHeight = portrait ? [1659, 1659, 1660][room] : 1199;
  const scale = portrait
    ? Math.max(width / sourceWidth, height / sourceHeight)
    : Math.min(width / sourceWidth, height / sourceHeight);
  const sceneWidth = sourceWidth * scale,
    sceneHeight = sourceHeight * scale;
  const x = (width - sceneWidth) / 2,
    y = height - sceneHeight;
  const anchors = portrait
    ? [
        [269, 653],
        [681, 653],
        [230, 945],
        [716, 945],
      ]
    : roomAnchors.map(([ax, ay]) => [ax * sourceWidth, ay * sourceHeight]);
  const floor = portrait ? [474, 1538] : [656, 1115];
  return {
    portrait,
    x,
    y,
    width: sceneWidth,
    height: sceneHeight,
    anchors: anchors.map(([ax, ay]) => [x + ax * scale, y + ay * scale]),
    floor: [x + floor[0] * scale, y + floor[1] * scale],
  };
}
