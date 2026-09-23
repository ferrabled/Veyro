/* Veyro Run — challenge landing page (T-024).

   A share from the game's result card is one line of text plus a link to this page:
     https://veyro.ferrabled.com/challenge/?s=<seed>&v=<contentVersion>&w=<worldId>&p=<score>&m=<daily|free>&d=<dayLabel>

   This page's whole job is to get that query string into the app. It re-emits it unchanged as
   veyro://challenge?<same query> — the app parses BOTH shapes with the same code
   (MotionRunner.Track.ChallengeMessage.TryParse), so the two can never drift.

   Why a custom scheme at all, when the https link is registered as an Android App Link:
   verification needs /.well-known/assetlinks.json signed with the release certificate, which
   only the owner can produce (OPEN_QUESTIONS 24). Until that file exists, Android opens this
   page in the browser instead of the app — and the button below is what rescues the trip.
   Once assetlinks lands, most taps never reach this page at all; the ones that do (desktop,
   other browsers, "open in browser") still work exactly like this. */
(function () {
  "use strict";

  var PLAY_URL = "https://play.google.com/store/apps/details?id=com.ferrabled.veyro.run";
  var SCHEME_URL = "veyro://challenge";

  /* How long to wait before deciding the app is not installed. 1.5s is long enough for the
     system to hand over on a slow phone, and short enough that a person staring at a page that
     did nothing has not yet given up. */
  var FALLBACK_MS = 1500;

  function el(id) { return document.getElementById(id); }

  function text(id, value) {
    var node = el(id);
    if (node) node.textContent = value;
  }

  /* "4210" -> "4 210", the same grouping the in-game share text uses (ChallengeMessage). */
  function grouped(n) {
    return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, " ");
  }

  var params = new URLSearchParams(window.location.search);
  var seed = params.get("s");
  var score = parseInt(params.get("p"), 10);
  var daily = params.get("m") === "daily";
  var day = params.get("d") || "";
  var world = params.get("w") || "";
  var version = params.get("v") || "";

  var hasChallenge = seed !== null && seed !== "" && /^-?\d+$/.test(seed);
  var hasScore = hasChallenge && !isNaN(score) && score > 0;

  /* No usable parameters: leave the static copy alone. It already reads as an invitation rather
     than an error, which is the right thing for a link a chat app truncated. */
  if (hasChallenge) {
    if (hasScore) {
      text("ch-title", "Beat " + grouped(score));
      text("ch-sub", "A Veyro Run player scored " + grouped(score) +
        (daily ? " on the Daily Run" + (day ? " of " + day : "") : " on a free run") +
        " — and sent you the exact same track.");
    } else {
      text("ch-sub", "A Veyro Run player sent you the exact track they ran. Your turn.");
    }

    var seedLine = "track " + seed + (version ? " · content " + version : "") +
      (world ? " · " + world : "");
    text("ch-seed", seedLine);

    /* The scheme link carries the query VERBATIM — including any parameter this page does not
       understand, so an older page can still hand a newer app everything it was sent. */
    var schemeUrl = SCHEME_URL + "?" + params.toString();

    var open = el("ch-open");
    if (open) {
      open.setAttribute("href", schemeUrl);
      open.addEventListener("click", function (e) {
        e.preventDefault();
        var left = false;

        /* The only reliable "did the app take over?" signal a browser gives: the page is
           backgrounded. Checked at the end of the timer, and also latched here, because some
           browsers fire visibilitychange and then restore the page. */
        function onHide() { if (document.visibilityState === "hidden") left = true; }
        document.addEventListener("visibilitychange", onHide);

        window.location.href = schemeUrl;

        window.setTimeout(function () {
          document.removeEventListener("visibilitychange", onHide);
          if (left || document.visibilityState === "hidden") return;
          window.location.href = PLAY_URL;
        }, FALLBACK_MS);
      });
    }
  }

  var store = el("ch-store");
  if (store) store.setAttribute("href", PLAY_URL);
})();
