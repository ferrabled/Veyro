/* Veyro Run site — daily ticket generator.
   Illustrative only: the ticket regenerates from today's UTC date so the page
   changes daily, like the game's Daily Run does. The REAL seed is computed
   in-game (deterministic C#, pinned by tests) — this is a visual, not the
   algorithm. The page works without JS; this replaces the static fallback. */
(function () {
  "use strict";

  // real chunk ids from the game's track library (docs/TRACK_GENERATION.md)
  var CHUNKS = [
    "straight_coins", "hop_gate", "weave_blocks", "gate_pair", "hop_run",
    "mixed_pattern", "tunnel_dash", "coin_dash", "bridge_span", "bridge_gate"
  ];
  var DAYS = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
  var MONTHS = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

  function fnv1a(str) {
    var h = 0x811c9dc5;
    for (var i = 0; i < str.length; i++) {
      h ^= str.charCodeAt(i);
      h = Math.imul(h, 0x01000193);
    }
    return h >>> 0;
  }

  function mulberry32(a) {
    return function () {
      a |= 0;
      a = (a + 0x6d2b79f5) | 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  var now = new Date();
  var utcDate = now.toISOString().slice(0, 10); // YYYY-MM-DD, same for everyone
  var seed = fnv1a("veyro-daily-" + utcDate);
  var rand = mulberry32(seed);

  var dateEl = document.getElementById("ticket-date");
  if (dateEl) {
    dateEl.textContent =
      DAYS[now.getUTCDay()] + " " + now.getUTCDate() + " " +
      MONTHS[now.getUTCMonth()] + " " + now.getUTCFullYear();
  }

  var seedEl = document.getElementById("ticket-seed");
  if (seedEl) {
    seedEl.textContent =
      "seed 0x" + seed.toString(16).toUpperCase().padStart(8, "0") +
      " · identical on every device";
  }

  var chunksEl = document.getElementById("ticket-chunks");
  if (chunksEl) {
    var pool = CHUNKS.slice();
    var picks = ["start_flat"]; // every real run starts here too
    while (picks.length < 5 && pool.length) {
      picks.push(pool.splice(Math.floor(rand() * pool.length), 1)[0]);
    }
    chunksEl.textContent = "";
    var hot = 1 + Math.floor(rand() * 4); // one highlighted chunk, never start_flat
    picks.forEach(function (name, i) {
      var s = document.createElement("span");
      s.className = "chunk" + (i === hot ? " hot" : "");
      s.textContent = name;
      chunksEl.appendChild(s);
    });
  }

  var barsEl = document.getElementById("ticket-bars");
  if (barsEl) {
    var inks = ["var(--ink)", "var(--pink)", "var(--ink)", "var(--mixed)"];
    var stops = [];
    var x = 0;
    var TILE = 360;
    while (x < TILE) {
      var w = 1 + Math.floor(rand() * 5);
      var gap = 2 + Math.floor(rand() * 6);
      var c = inks[Math.floor(rand() * inks.length)];
      stops.push(c + " " + x + "px " + (x + w) + "px");
      stops.push("transparent " + (x + w) + "px " + (x + w + gap) + "px");
      x += w + gap;
    }
    barsEl.style.backgroundImage = "linear-gradient(90deg, " + stops.join(", ") + ")";
    barsEl.style.backgroundSize = TILE + "px 100%";
  }
})();
