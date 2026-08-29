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

    sender.send("transform", { x: 12, y: 3, z: 1000, yaw: 0 });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(senderState.z).toBe(1000);

    sender.send("transform", { x: 1000, y: 3, z: -8, yaw: 0 });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.x).toBe(12);
    await Promise.all([sender.leave(), observer.leave()]);
  });

  it("synchronizes only approved skin ids for the sending player", async () => {
    const room = await colyseus.createRoom("spider_room");
    const sender = await colyseus.connectTo(room);
    const observer = await colyseus.connectTo(room);
    const senderState = room.state.players.get(sender.sessionId)!;
    const observerState = room.state.players.get(observer.sessionId)!;

    sender.send("skin", { skinId: "Upgrade02" });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.skinId).toBe("Upgrade02");
    expect(observerState.skinId).toBe("Default");

    sender.send("skin", { skinId: "AnythingElse" });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.skinId).toBe("Upgrade02");
    await Promise.all([sender.leave(), observer.leave()]);
  });

  it("synchronizes swing state only for the sending player", async () => {
    const room = await colyseus.createRoom("spider_room");
    const sender = await colyseus.connectTo(room);
    const observer = await colyseus.connectTo(room);
    const senderState = room.state.players.get(sender.sessionId)!;
    const observerState = room.state.players.get(observer.sessionId)!;

    sender.send("swing", {
      active: true,
      anchorX: 0,
      anchorY: 10,
      anchorZ: 6,
    });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.isSwinging).toBe(true);
    expect(senderState.swingAnchorY).toBe(10);
    expect(observerState.isSwinging).toBe(false);

    sender.send("swing", {
      active: true,
      anchorX: 0,
      anchorY: 28,
      anchorZ: 40,
    });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(senderState.swingAnchorY).toBe(28);
    expect(senderState.swingAnchorZ).toBe(40);

    sender.send("swing", {
      active: true,
      anchorX: 0,
      anchorY: 28,
      anchorZ: 100001,
    });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(senderState.swingAnchorZ).toBe(40);

    sender.send("swing", {
      active: false,
      anchorX: 0,
      anchorY: 0,
      anchorZ: 0,
    });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(senderState.isSwinging).toBe(false);

    await Promise.all([sender.leave(), observer.leave()]);
  });

  it("synchronizes valid animation states only for the sending player", async () => {
    const room = await colyseus.createRoom("spider_room");
    const sender = await colyseus.connectTo(room);
    const observer = await colyseus.connectTo(room);
    const senderState = room.state.players.get(sender.sessionId)!;
    const observerState = room.state.players.get(observer.sessionId)!;

    expect(senderState.animationState).toBe(0);
    sender.send("animation", { state: 3 });
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(senderState.animationState).toBe(3);
    expect(observerState.animationState).toBe(0);

    sender.send("animation", { state: 6 });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(senderState.animationState).toBe(3);

    sender.send("animation", { state: 2.5 });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(senderState.animationState).toBe(3);

    await Promise.all([sender.leave(), observer.leave()]);
  });
});
