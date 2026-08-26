import { Client, Room } from "colyseus";
import { PlayerState, SpiderRoomState } from "./schema/PlayerState.js";

interface SpiderRoomOptions {
  state: SpiderRoomState;
}

export class SpiderRoom extends Room<SpiderRoomOptions> {
  maxClients = 4;

  onCreate() {
    this.setState(new SpiderRoomState());
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
}
