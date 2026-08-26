import { beforeAll, afterAll, beforeEach, describe, expect, it } from "vitest";
import { ColyseusTestServer, boot } from "@colyseus/testing";
import appConfig from "../src/app.config.js";

describe("spider_room foundation", () => {
  let colyseus: ColyseusTestServer;

  beforeAll(async () => {
    colyseus = await boot(appConfig);
  });

  afterAll(async () => {
    await colyseus.shutdown();
  });

  beforeEach(async () => {
    await colyseus.cleanup();
  });

  it("allows a player to join and removes it when it leaves", async () => {
    const room = await colyseus.createRoom("spider_room");
    const client = await colyseus.connectTo(room);

    expect(room.clients).toHaveLength(1);
    expect(room.state.players.size).toBe(1);

    await client.leave();

    expect(room.clients).toHaveLength(0);
    expect(room.state.players.size).toBe(0);
  });

  it("assigns unique player numbers", async () => {
    const room = await colyseus.createRoom("spider_room");
    const clients = await Promise.all([
      colyseus.connectTo(room),
      colyseus.connectTo(room),
      colyseus.connectTo(room),
      colyseus.connectTo(room),
    ]);

    const playerNumbers = Array.from(room.state.players.values())
      .map((player) => player.playerNumber)
      .sort((left, right) => left - right);

    expect(playerNumbers).toEqual([1, 2, 3, 4]);
    await Promise.all(clients.map((client) => client.leave()));
  });

  it("rejects a fifth client", async () => {
    const room = await colyseus.createRoom("spider_room");
    const clients = await Promise.all([
      colyseus.connectTo(room),
      colyseus.connectTo(room),
      colyseus.connectTo(room),
      colyseus.connectTo(room),
    ]);

    await expect(colyseus.connectTo(room)).rejects.toThrow();
    await Promise.all(clients.map((client) => client.leave()));
  });

  it("accepts a valid transform only for the sending player", async () => {
    const room = await colyseus.createRoom("spider_room");
    const sender = await colyseus.connectTo(room);
    const observer = await colyseus.connectTo(room);
    const senderState = room.state.players.get(sender.sessionId)!;
    const observerState = room.state.players.get(observer.sessionId)!;

    sender.send("transform", { x: 12, y: 3, z: -8, yaw: 450 });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.x).toBe(12);
    expect(senderState.y).toBe(3);
    expect(senderState.z).toBe(-8);
    expect(senderState.yaw).toBe(90);
    expect(observerState.x).toBe(2);

    sender.send("transform", { x: 1000, y: 3, z: -8, yaw: 0 });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.x).toBe(12);
    await Promise.all([sender.leave(), observer.leave()]);
  });
});
