// Duck Game Friends room relay.
//
// Every player's game sends UDP to this server instead of to each other.
// A room has four slots; slot 0 is the host. Packets are addressed by slot,
// so players never need each other's IP addresses or open router ports.
//
// Client -> relay
//   'J' role(1: 0 host, 1 guest) code(8 ascii) nonce(8)   join or create a room
//   'D' token(8) toSlot(1) payload                        forward payload
//   'K' token(8)                                          keepalive
//   'Q' code(8)                                           does the room have space?
// Relay -> client
//   'A' slot(1) token(8) nonce(8)                         joined
//   'E' reason(1) nonce(8)                                1 no room, 2 full, 3 code taken
//   'D' fromSlot(1) payload                               forwarded payload
//   'R' status(1) code(8)                                 0 open, 1 no room, 2 full
const dgram = require('node:dgram');
const crypto = require('node:crypto');

const SLOTS = 4;
const MEMBER_TIMEOUT_MS = 20000;
const CODE = /^[0-9A-Z]{8}$/;

function createRelay({ port = 7777, host = '0.0.0.0', log = console.log } = {}) {
  const socket = dgram.createSocket('udp4');
  const rooms = new Map(); // code -> Array(SLOTS) of member | null
  const members = new Map(); // token hex -> member

  const send = (buf, m) => socket.send(buf, m.port, m.address);
  const reply = (buf, rinfo) => socket.send(buf, rinfo.port, rinfo.address);

  function touch(member, rinfo) {
    member.address = rinfo.address;
    member.port = rinfo.port;
    member.seen = Date.now();
  }

  function remove(member) {
    members.delete(member.token.toString('hex'));
    const room = rooms.get(member.code);
    if (!room || room[member.slot] !== member) return;
    room[member.slot] = null;
    if (member.slot === 0) {
      // The match lives in the host's game; guests cannot continue without it.
      for (const m of room) if (m) members.delete(m.token.toString('hex'));
      rooms.delete(member.code);
      log(`room ${member.code} closed`);
    }
  }

  function join(msg, rinfo) {
    if (msg.length !== 18) return;
    const role = msg[1];
    const code = msg.toString('ascii', 2, 10);
    const nonce = msg.subarray(10, 18);
    if (!CODE.test(code)) return reply(Buffer.concat([Buffer.from([0x45, 1]), nonce]), rinfo);
    let room = rooms.get(code);

    // A retried join from the same request gets the same answer.
    if (room) {
      for (const m of room) {
        if (m && m.nonce.equals(nonce)) {
          touch(m, rinfo);
          return reply(Buffer.concat([Buffer.from([0x41, m.slot]), m.token, nonce]), rinfo);
        }
      }
    }

    let slot;
    if (role === 0) {
      if (room) return reply(Buffer.concat([Buffer.from([0x45, 3]), nonce]), rinfo);
      room = new Array(SLOTS).fill(null);
      rooms.set(code, room);
      slot = 0;
      log(`room ${code} opened`);
    } else {
      if (!room) return reply(Buffer.concat([Buffer.from([0x45, 1]), nonce]), rinfo);
      slot = room.findIndex((m, i) => i > 0 && m === null);
      if (slot < 0) return reply(Buffer.concat([Buffer.from([0x45, 2]), nonce]), rinfo);
    }
    const member = { code, slot, nonce: Buffer.from(nonce), token: crypto.randomBytes(8) };
    touch(member, rinfo);
    room[slot] = member;
    members.set(member.token.toString('hex'), member);
    reply(Buffer.concat([Buffer.from([0x41, slot]), member.token, nonce]), rinfo);
  }

  function query(msg, rinfo) {
    if (msg.length !== 9) return;
    const code = msg.toString('ascii', 1, 9);
    const room = rooms.get(code);
    const status = !room ? 1 : room.some((m, i) => i > 0 && m === null) ? 0 : 2;
    reply(Buffer.concat([Buffer.from([0x52, status]), msg.subarray(1, 9)]), rinfo);
  }

  socket.on('message', (msg, rinfo) => {
    if (msg.length < 1) return;
    const type = msg[0];
    if (type === 0x4a) return join(msg, rinfo);
    if (type === 0x51) return query(msg, rinfo);
    if (msg.length < 9) return;
    const member = members.get(msg.toString('hex', 1, 9));
    if (!member) return;
    touch(member, rinfo);
    if (type !== 0x44 || msg.length < 10) return;
    const to = msg[9];
    const target = to < SLOTS && to !== member.slot ? rooms.get(member.code)?.[to] : null;
    if (!target) return;
    const out = Buffer.allocUnsafe(msg.length - 8);
    out[0] = 0x44;
    out[1] = member.slot;
    msg.copy(out, 2, 10);
    send(out, target);
  });

  const sweep = setInterval(() => {
    const cutoff = Date.now() - MEMBER_TIMEOUT_MS;
    for (const m of [...members.values()]) if (m.seen < cutoff) remove(m);
  }, 2000);

  socket.on('close', () => clearInterval(sweep));

  return new Promise((resolve) => {
    socket.bind(port, host, () => {
      log(`relay listening on ${host}:${socket.address().port}`);
      resolve({ socket, rooms, port: socket.address().port, close: () => socket.close() });
    });
  });
}

module.exports = { createRelay };

if (require.main === module) {
  // Fly.io only routes UDP to processes bound to fly-global-services.
  const host = process.env.FLY_APP_NAME ? 'fly-global-services' : process.env.RELAY_HOST || '0.0.0.0';
  createRelay({ port: Number(process.env.PORT || 7777), host });
}
