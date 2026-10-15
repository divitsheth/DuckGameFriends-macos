// Duck Game Friends room relay.
//
// Every player's game sends UDP to this server instead of to each other.
// A room has up to eight slots; slot 0 is the host, who picks the size. Packets are addressed by slot,
// so players never need each other's IP addresses or open router ports.
//
// Client -> relay
//   'J' role(1: 0 host, 1 guest) code(8 ascii) nonce(8) [size(1)]
//                                                         join or create a room;
//                                                         a host's size (2-8) defaults to 4
//   'D' token(8) toSlot(1) payload                        forward payload
//   'K' token(8)                                          keepalive
//   'Q' code(8)                                           does the room have space?
//   'O' token(8) seq(4) [report]                          answer to a ping; the report is the game's own
//                                                         view of the match as JSON (1.5.2 and later)
// Relay -> client
//   'A' slot(1) token(8) nonce(8)                         joined
//   'E' reason(1) nonce(8)                                1 no room, 2 full, 3 code taken
//   'D' fromSlot(1) payload                               forwarded payload
//   'R' status(1) code(8)                                 0 open, 1 no room, 2 full
//   'P' seq(4)                                            ping; older clients ignore it
//
// Match records: while a room has guests, the relay samples every player's connection every
// 10 seconds: its ping to the relay (from 'P'/'O'), packets each way, and the longest silence,
// plus the game's own report. When the room closes, the record is appended as one JSON line to
// $DATA_DIR/matches.jsonl (the Fly volume) and summarised in the log. See relay/matches.py.
const dgram = require('node:dgram');
const crypto = require('node:crypto');

const SLOTS = 8; // the most any room can have
const DEFAULT_SIZE = 4; // rooms opened by 1.1 and 1.2 clients
const MEMBER_TIMEOUT_MS = 20000;
const CODE = /^[0-9A-Z]{8}$/;
const SAMPLE_MS = 10000;
const MAX_SAMPLES = 1080; // three hours
const median = (xs) => { const s = [...xs].sort((a, b) => a - b); return s.length ? s[s.length >> 1] : null; };

function createRelay({ port = 7777, host = '0.0.0.0', log = console.log, probeMs = 2000, memberTimeoutMs = MEMBER_TIMEOUT_MS, dataDir = process.env.DATA_DIR } = {}) {
  const socket = dgram.createSocket('udp4');
  const rooms = new Map(); // code -> Array(room size) of member | null
  const members = new Map(); // token hex -> member
  const records = new Map(); // code -> match record
  let loopLag = 0; // the relay's own worst delay in the current sample, in ms

  const send = (buf, m) => socket.send(buf, m.port, m.address);
  const reply = (buf, rinfo) => socket.send(buf, rinfo.port, rinfo.address);

  function touch(member, rinfo) {
    const now = Date.now();
    member.address = rinfo.address;
    member.port = rinfo.port;
    if (member.seen) member.stats.gap = Math.max(member.stats.gap, now - member.seen);
    member.seen = now;
  }
  const freshStats = () => ({ rtts: [], probes: 0, in: 0, out: 0, bytesIn: 0, gap: 0, report: null });

  function sample(code, room) {
    const record = records.get(code);
    if (!record || record.samples.length >= MAX_SAMPLES) return;
    const slots = {};
    for (const m of room) {
      if (!m) continue;
      const s = m.stats;
      slots[m.slot] = { rtt: s.rtts.length ? [Math.min(...s.rtts), median(s.rtts), Math.max(...s.rtts)] : null,
        probes: s.probes, answers: s.rtts.length, in: s.in, out: s.out, kbIn: Math.round(s.bytesIn / 1024), gap: s.gap, game: s.report };
      m.stats = freshStats();
    }
    record.samples.push({ t: Date.now() - record.opened, lag: loopLag, slots });
  }

  function finish(code) {
    const record = records.get(code);
    records.delete(code);
    if (!record || !record.players.some((p) => p.slot > 0)) return;
    record.closed = Date.now();
    const parts = record.players.map((p) => {
      const rtts = record.samples.map((x) => x.slots[p.slot]?.rtt?.[1]).filter((x) => x != null);
      const gaps = record.samples.map((x) => x.slots[p.slot]?.gap ?? 0);
      return `slot ${p.slot}${p.name ? ` ${p.name}` : ''}: ping ${rtts.length ? median(rtts) + 'ms' : 'n/a'}, longest silence ${Math.max(0, ...gaps)}ms`;
    });
    log(`room ${code} report (${Math.round((record.closed - record.opened) / 60000)} min): ${parts.join('; ')}`);
    if (!dataDir) return;
    try {
      require('node:fs').appendFileSync(require('node:path').join(dataDir, 'matches.jsonl'), JSON.stringify({ ...record, players: record.players.map(({ token, ...p }) => p) }) + '\n');
    } catch (e) { log(`could not save room ${code}: ${e.message}`); }
  }

  function remove(member) {
    members.delete(member.token.toString('hex'));
    const room = rooms.get(member.code);
    if (!room || room[member.slot] !== member) return;
    room[member.slot] = null;
    const player = records.get(member.code)?.players.find((p) => p.token === member.token);
    if (player) player.left = Date.now() - records.get(member.code).opened;
    if (member.slot === 0) {
      // The match lives in the host's game; guests cannot continue without it.
      for (const m of room) if (m) members.delete(m.token.toString('hex'));
      rooms.delete(member.code);
      log(`room ${member.code} closed`);
      finish(member.code);
    }
  }

  function join(msg, rinfo) {
    if (msg.length !== 18 && msg.length !== 19) return;
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
      const size = msg.length === 19 ? Math.min(SLOTS, Math.max(2, msg[18])) : DEFAULT_SIZE;
      room = new Array(size).fill(null);
      rooms.set(code, room);
      records.set(code, { room: code, opened: Date.now(), size, players: [], samples: [] });
      slot = 0;
      log(`room ${code} opened`);
    } else {
      if (!room) return reply(Buffer.concat([Buffer.from([0x45, 1]), nonce]), rinfo);
      slot = room.findIndex((m, i) => i > 0 && m === null);
      if (slot < 0) return reply(Buffer.concat([Buffer.from([0x45, 2]), nonce]), rinfo);
    }
    const member = { code, slot, nonce: Buffer.from(nonce), token: crypto.randomBytes(8), stats: freshStats(), probes: new Map() };
    touch(member, rinfo);
    const record = records.get(code);
    if (record) record.players.push({ slot, token: member.token, joined: Date.now() - record.opened });
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
    member.stats.in++;
    member.stats.bytesIn += msg.length;
    if (type === 0x4f && msg.length >= 13) return pong(member, msg);
    if (type !== 0x44 || msg.length < 10) return;
    const to = msg[9];
    const target = to !== member.slot ? rooms.get(member.code)?.[to] : null;
    if (!target) return;
    const out = Buffer.allocUnsafe(msg.length - 8);
    out[0] = 0x44;
    out[1] = member.slot;
    msg.copy(out, 2, 10);
    send(out, target);
    target.stats.out++;
  });

  function pong(member, msg) {
    const seq = msg.readUInt32BE(9);
    const sent = member.probes.get(seq);
    if (sent === undefined) return;
    member.probes.delete(seq);
    member.stats.rtts.push(Date.now() - sent);
    if (msg.length > 13) {
      try {
        const report = JSON.parse(msg.toString('utf8', 13));
        member.stats.report = report;
        const player = records.get(member.code)?.players.find((p) => p.token === member.token);
        if (player && typeof report.name === 'string') { player.name = report.name.slice(0, 32); player.os = String(report.os || '').slice(0, 16); }
      } catch { /* a malformed report still counts as a ping */ }
    }
  }

  let seq = 0;
  const probe = probeMs > 0 && setInterval(() => {
    const now = Date.now();
    for (const m of members.values()) {
      for (const [s, t] of m.probes) if (now - t > 10000) m.probes.delete(s);
      const buf = Buffer.alloc(5);
      buf[0] = 0x50;
      buf.writeUInt32BE(seq, 1);
      m.probes.set(seq, now);
      m.stats.probes++;
      seq = (seq + 1) >>> 0;
      send(buf, m);
    }
  }, probeMs);

  let lagCheck = Date.now();
  const lag = setInterval(() => {
    const now = Date.now();
    loopLag = Math.max(loopLag, now - lagCheck - 100);
    lagCheck = now;
  }, 100);

  const sampler = setInterval(() => {
    for (const [code, room] of rooms) if (room.filter(Boolean).length > 1) sample(code, room);
    loopLag = 0;
  }, SAMPLE_MS);

  const sweep = setInterval(() => {
    const cutoff = Date.now() - memberTimeoutMs;
    for (const m of [...members.values()]) if (m.seen < cutoff) remove(m);
  }, Math.min(2000, memberTimeoutMs));

  socket.on('close', () => { clearInterval(sweep); clearInterval(probe); clearInterval(lag); clearInterval(sampler); });

  return new Promise((resolve) => {
    socket.bind(port, host, () => {
      log(`relay listening on ${host}:${socket.address().port}`);
      resolve({ socket, rooms, records, sample: () => { for (const [c, r] of rooms) sample(c, r); }, port: socket.address().port, close: () => socket.close() });
    });
  });
}

module.exports = { createRelay };

if (require.main === module) {
  // Fly.io only routes UDP to processes bound to fly-global-services.
  const host = process.env.FLY_APP_NAME ? 'fly-global-services' : process.env.RELAY_HOST || '0.0.0.0';
  createRelay({ port: Number(process.env.PORT || 7777), host });
}
