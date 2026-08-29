import { MapSchema, Schema, type } from "@colyseus/schema";

export class PlayerState extends Schema {
  @type("number")
  playerNumber = 0;

  @type("number")
  x = 0;

  @type("number")
  y = 1;

  @type("number")
  z = 0;

  @type("number")
  yaw = 0;

  @type("string")
  skinId = "Default";

  @type("boolean")
  isSwinging = false;

  @type("number")
  swingAnchorX = 0;

  @type("number")
  swingAnchorY = 0;

  @type("number")
  swingAnchorZ = 0;

  @type("number")
  animationState = 0;
}

export class SpiderRoomState extends Schema {
  @type({ map: PlayerState })
  players = new MapSchema<PlayerState>();
}
