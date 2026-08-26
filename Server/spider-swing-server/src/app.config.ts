import { defineRoom, defineServer } from "colyseus";
import { WebSocketTransport } from "@colyseus/ws-transport";
import type { Request, Response } from "express";
import { SpiderRoom } from "./rooms/SpiderRoom.js";

const server = defineServer({
  rooms: {
    spider_room: defineRoom(SpiderRoom),
  },
  transport: new WebSocketTransport({
    pingInterval: 10_000,
  }),
  express: (app) => {
    app.get("/health", (_request: Request, response: Response) => {
      response.json({ status: "ok" });
    });
  },
});

export default server;
