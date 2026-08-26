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

export class SpiderRoom extends Room<SpiderRoomOptions> {
  maxClients = 4;
  private static readonly maxHorizontalCoordinate = 100;
  private static readonly minVerticalCoordinate = -25;
  private static readonly maxVerticalCoordinate = 75;

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
      Math.abs(message.z) <= SpiderRoom.maxHorizontalCoordinate &&
      message.y >= SpiderRoom.minVerticalCoordinate &&
      message.y <= SpiderRoom.maxVerticalCoordinate;
  }
}
