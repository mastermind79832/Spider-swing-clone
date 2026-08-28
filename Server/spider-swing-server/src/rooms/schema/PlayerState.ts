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
}

export class SpiderRoomState extends Schema {
  @type({ map: PlayerState })
  players = new MapSchema<PlayerState>();
}
