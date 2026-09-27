/* Veyro Run — challenge landing page (T-024).

   A share from the game's result card is one line of text plus a link to this page:
     https://veyro.ferrabled.com/challenge/?s=<seed>&v=<contentVersion>&w=<worldId>&p=<score>&m=<daily|free>&d=<dayLabel>

   This page's whole job is to get that query string into the app. It re-emits it unchanged as
   veyro://challenge?<same query> — the app parses BOTH shapes with the same code
   (MotionRunner.Track.ChallengeMessage.TryParse), so the two can never drift. On Android the
   scheme link is wrapped in an intent:// URL (see intentUrl); the app receives the same
   veyro://challenge?… either way.

   Why a custom scheme at all, when the https link is registered as an Android App Link:
   verification needs /.well-known/assetlinks.json signed with the release certificate, which
   only the owner can produce (OPEN_QUESTIONS 24). Until that file exists, Android opens this
   page in the browser instead of the app — and the button below is what rescues the trip.
   Once assetlinks lands, most taps never reach this page at all; the ones that do (desktop,
   other browsers, "open in browser") still work exactly like this. */
(function () {
  "use strict";

  var PACKAGE = "com.ferrabled.veyro.run";
  var PLAY_URL = "https://play.google.com/store/apps/details?id=" + PACKAGE;
  var SCHEME_URL = "veyro://challenge";

  /* "Request desktop site" drops the Android token; such a visit gets the generic path below. */
  var IS_ANDROID = /Android/i.test(navigator.userAgent);

  /* How long to wait before deciding the app is not installed. 1.5s is long enough for the
     system to hand over on a slow phone, and short enough that a person staring at a page that
     did nothing has not yet given up. */
  var FALLBACK_MS = 1500;

  function el(id) { return document.getElementById(id); }

  function text(id, value) {
    var node = el(id);
    if (node) node.textContent = value;
  }

  /* Android browsers (Chrome, Samsung Internet, Firefox) resolve an intent:// URL in ONE
     navigation: open the app when it is installed, go to S.browser_fallback_url when it is
     not. A plain veyro:// navigation with no app to take it makes Chrome replace this page
     with ERR_UNKNOWN_URL_SCHEME (docs/SHARE_COMPLIANCE.md §3.5), which also kills the timer
     that was meant to rescue it — so on Android the browser owns the fallback, not us. The
     part before '#' becomes the intent's data: exactly veyro://challenge?<query>. */
  function intentUrl(query) {
    return "intent://challenge?" + query +
      "#Intent;scheme=veyro;package=" + PACKAGE +
      ";S.browser_fallback_url=" + encodeURIComponent(PLAY_URL) + ";end";
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
    var query = params.toString();
    var schemeUrl = SCHEME_URL + "?" + query;

    var open = el("ch-open");
    if (open && IS_ANDROID) {
      /* A plain href and no click handler: browsers launch an external app only from a user
         gesture, and the anchor's own click is the most direct one. No timer either — while
         Chrome shows an "open in app?" prompt the page is still visible, and a timer would
         send someone who HAS the game to the Play Store. */
      open.setAttribute("href", intentUrl(query));
    } else if (open) {
      /* Everything else — desktop, and iOS until there is an iOS build (which will want a
         Universal Link and an App Store fallback instead): the bare scheme plus a timer. */
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
