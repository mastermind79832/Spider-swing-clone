import { Client, Room } from "colyseus";
import { PlayerState, SpiderRoomState } from "./schema/PlayerState.js";

interface SpiderRoomOptions {
  state: SpiderRoomState;
}

interface TransformMessage {
  x: number;
  y: number;
  z: number;
  yaw: number;
}

interface SkinMessage {
  skinId: string;
}

interface SwingMessage {
  active: boolean;
  anchorX: number;
  anchorY: number;
  anchorZ: number;
}

interface AnimationMessage {
  state: number;
}

export class SpiderRoom extends Room<SpiderRoomOptions> {
  maxClients = 4;
  private static readonly maxHorizontalCoordinate = 100;
  private static readonly maxNetworkCoordinate = 100000;
  private static readonly minVerticalCoordinate = -25;
  private static readonly maxVerticalCoordinate = 75;
  private static readonly allowedSkinIds = new Set(["Default", "Upgrade01", "Upgrade02", "Upgrade03"]);
  private static readonly minAnimationState = 0;
  private static readonly maxAnimationState = 5;

  onCreate() {
    this.setState(new SpiderRoomState());

    this.onMessage("transform", (client, message: TransformMessage) => {
      if (!SpiderRoom.isValidTransform(message)) {
        return;
      }

      const player = this.state.players.get(client.sessionId);
      if (player === undefined) {
        return;
      }

      player.x = message.x;
      player.y = message.y;
      player.z = message.z;
      player.yaw = ((message.yaw % 360) + 360) % 360;
    });

    this.onMessage("skin", (client, message: SkinMessage) => {
      if (!SpiderRoom.isValidSkin(message?.skinId)) {
        return;
      }

      const player = this.state.players.get(client.sessionId);
      if (player !== undefined) {
        player.skinId = message.skinId;
      }
    });

    this.onMessage("swing", (client, message: SwingMessage) => {
      const player = this.state.players.get(client.sessionId);
      if (player === undefined || !SpiderRoom.isValidSwing(message)) {
        return;
      }

      player.isSwinging = message.active;
      if (message.active) {
        player.swingAnchorX = message.anchorX;
        player.swingAnchorY = message.anchorY;
        player.swingAnchorZ = message.anchorZ;
      }
    });

    this.onMessage("animation", (client, message: AnimationMessage) => {
      if (!SpiderRoom.isValidAnimation(message)) {
        return;
      }

      const player = this.state.players.get(client.sessionId);
      if (player !== undefined) {
        player.animationState = message.state;
      }
    });
  }

  onJoin(client: Client) {
    const player = new PlayerState();
    player.playerNumber = this.findAvailablePlayerNumber();
    player.x = (player.playerNumber - 1) * 2;

    this.state.players.set(client.sessionId, player);
  }

  onLeave(client: Client) {
    this.state.players.delete(client.sessionId);
  }

  private findAvailablePlayerNumber(): number {
    const usedNumbers = new Set<number>();

    this.state.players.forEach((player) => {
      usedNumbers.add(player.playerNumber);
    });

    for (let playerNumber = 1; playerNumber <= this.maxClients; playerNumber += 1) {
      if (!usedNumbers.has(playerNumber)) {
        return playerNumber;
      }
    }

    throw new Error("No player number is available.");
  }

  private static isValidTransform(message: TransformMessage): boolean {
    return Number.isFinite(message?.x) &&
      Number.isFinite(message?.y) &&
      Number.isFinite(message?.z) &&
      Number.isFinite(message?.yaw) &&
      Math.abs(message.x) <= SpiderRoom.maxHorizontalCoordinate &&
      Math.abs(message.z) <= SpiderRoom.maxNetworkCoordinate &&
      message.y >= SpiderRoom.minVerticalCoordinate &&
      message.y <= SpiderRoom.maxVerticalCoordinate;
  }

  private static isValidSkin(skinId: unknown): skinId is string {
    return typeof skinId === "string" && SpiderRoom.allowedSkinIds.has(skinId);
  }

  private static isValidSwing(message: SwingMessage): boolean {
    if (typeof message?.active !== "boolean") {
      return false;
    }

    if (!message.active) {
      return true;
    }

    if (!Number.isFinite(message.anchorX) ||
      !Number.isFinite(message.anchorY) ||
      !Number.isFinite(message.anchorZ)) {
      return false;
    }

    return Math.abs(message.anchorX) <= SpiderRoom.maxNetworkCoordinate &&
      Math.abs(message.anchorY) <= SpiderRoom.maxNetworkCoordinate &&
      Math.abs(message.anchorZ) <= SpiderRoom.maxNetworkCoordinate;
  }

  private static isValidAnimation(message: AnimationMessage): boolean {
    return Number.isInteger(message?.state) &&
      message.state >= SpiderRoom.minAnimationState &&
      message.state <= SpiderRoom.maxAnimationState;
  }
}
