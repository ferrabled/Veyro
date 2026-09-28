/* Progressive enhancement only. No analytics, storage or remote requests. */
(function () {
  "use strict";
  var date = document.getElementById("ticket-date");
  function updateDate() {
    var now = new Date();
    if (date) date.textContent = new Intl.DateTimeFormat("en-GB", {
      day: "numeric", month: "short", year: "numeric", timeZone: "UTC"
    }).format(now);
    // Refresh just after the UTC date rolls over, even in a long-lived tab.
    var tomorrow = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1);
    window.setTimeout(updateDate, tomorrow - now.getTime() + 1000);
  }
  if (date) updateDate();
  var guideNote = document.querySelector(".how-note");
  if (guideNote) guideNote.hidden = false;

  var descriptions = {
    tilt_01: "Two hands hold a phone upright, with the garden runner on its screen.",
    tilt_02: "The phone tips left and the runner moves to the left side of the garden path.",
    tilt_03: "The phone tips right and the runner moves to the right side of the garden path.",
    tilt_04: "A thumb taps the phone screen and the runner jumps over a gold hurdle.",
    camera_01: "A phone stands on a table facing a clear space for the player.",
    camera_02: "A person faces a phone on a table, standing in the front camera's view.",
    camera_03: "The player steps sideways to their left, staying in view of the phone.",
    camera_04: "The player steps sideways to their right, staying in view of the phone.",
    camera_05: "The player makes a small hop in front of the phone to jump in the game."
  };
  var heroControls = document.querySelector(".hero-mode-controls");
  if (heroControls) {
    var hero = document.querySelector(".hero-art");
    var heroPanels = document.querySelectorAll("[data-hero-panel]");
    var playback = hero.querySelector(".hero-playback");
    var playButton = hero.querySelector("[data-hero-playback]");
    var stepCount = hero.querySelector("[data-hero-step]");
    var motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    var paused = motion.matches;
    var hovering = false;
    var visible = !("IntersectionObserver" in window);
    var ready = false;
    var loading = false;
    var automaticRequest = false;
    var timer;
    var mode = "camera";
    var step = 0;
    var heroRequest = 0;

    function canPlay() {
      return ready && !paused && !hovering && visible && !document.hidden;
    }
    function restartRotation() {
      window.clearTimeout(timer);
      // Pausing or leaving the card also cancels an automatic frame still decoding.
      if (automaticRequest) {
        ++heroRequest;
        loading = false;
        automaticRequest = false;
      }
      playButton.textContent = paused ? "Play" : "Pause";
      playButton.setAttribute("aria-label", paused ? "Play illustrations" : "Pause illustrations");
      if (canPlay() && !loading) {
        timer = window.setTimeout(function () {
          showFrame(mode, (step + 1) % 4, true);
        }, 3000);
      }
    }
    function showFrame(nextMode, nextStep, automatic) {
      window.clearTimeout(timer);
      var current = ++heroRequest;
      var panel = document.getElementById("hero-" + nextMode);
      var captions = panel.querySelectorAll("[data-hero-frame]");
      var frame = captions[nextStep].dataset.heroFrame;
      var next = new Image(1586, 992);
      loading = true;
      automaticRequest = automatic;
      next.alt = descriptions[frame];
      next.sizes = panel.querySelector("img").sizes;
      next.srcset = "/art/" + frame + "-793.webp 793w, /art/" + frame + "-1586.webp 1586w";
      next.src = "/art/" + frame + "-793.webp";
      next.decode().then(function () {
        if (current !== heroRequest) return;
        loading = false;
        automaticRequest = false;
        // Replace a fully decoded image and its caption together, without a blank frame.
        panel.querySelector("img").replaceWith(next);
        captions.forEach(function (item, index) {
          item.setAttribute("aria-hidden", String(index !== nextStep));
        });
        heroPanels.forEach(function (item) {
          var inactive = item !== panel;
          item.setAttribute("aria-hidden", String(inactive));
          item.toggleAttribute("inert", inactive);
        });
        heroControls.querySelectorAll("button").forEach(function (item) {
          item.setAttribute("aria-pressed", String(item.dataset.heroMode === nextMode));
        });
        mode = nextMode;
        step = nextStep;
        ready = true;
        stepCount.textContent = (step + 1) + " / 4";
        restartRotation();
      }).catch(function () {
        if (current !== heroRequest) return;
        loading = false;
        automaticRequest = false;
        paused = true;
        // Keep the previous complete scene; Play or Next lets the visitor retry.
        restartRotation();
      });
    }

    heroControls.hidden = false;
    playback.hidden = false;
    heroControls.addEventListener("click", function (event) {
      var button = event.target.closest("button[data-hero-mode]");
      if (!button || !heroControls.contains(button)) return;
      var nextMode = button.dataset.heroMode;
      if (nextMode !== "camera" && nextMode !== "tilt") return;
      // A newer choice cancels any pending selection, including a return to this mode.
      ++heroRequest;
      loading = false;
      automaticRequest = false;
      if (nextMode === mode) restartRotation();
      else showFrame(nextMode, 0, false);
    });
    playButton.addEventListener("click", function () {
      paused = !paused;
      // Explicit Play works while the pointer or keyboard focus stays on the control.
      hovering = false;
      restartRotation();
    });
    hero.querySelector("[data-hero-next]").addEventListener("click", function () {
      paused = true;
      showFrame(mode, (step + 1) % 4, false);
      restartRotation();
    });
    hero.addEventListener("pointerenter", function (event) {
      if (event.pointerType !== "mouse") return;
      hovering = true;
      restartRotation();
    });
    hero.addEventListener("pointerleave", function () {
      hovering = false;
      restartRotation();
    });
    hero.addEventListener("focusin", function (event) {
      if (event.target === playButton || !event.target.matches(":focus-visible")) return;
      paused = true;
      restartRotation();
    });
    document.addEventListener("visibilitychange", restartRotation);
    motion.addEventListener("change", function () {
      // Preference changes always stop rotation; resuming requires an explicit Play.
      paused = true;
      restartRotation();
    });
    if ("IntersectionObserver" in window) {
      new IntersectionObserver(function (entries) {
        visible = entries[0].isIntersecting && entries[0].intersectionRatio >= 0.2;
        restartRotation();
      }, { threshold: [0, 0.2] }).observe(hero);
    }
    hero.querySelector("#hero-camera img").decode().then(function () {
      ready = true;
      restartRotation();
    }).catch(function () {
      paused = true;
      restartRotation();
    });
    restartRotation();
  }

  document.querySelectorAll("[data-guide]").forEach(function (card) {
    var controls = card.querySelector(".guide-controls");
    var art = card.querySelector(".guide-art img");
    var request = 0;
    controls.hidden = false;
    controls.addEventListener("click", function (event) {
      var button = event.target.closest("button[data-frame]");
      if (!button || !controls.contains(button)) return;
      var frame = button.dataset.frame;
      if (!descriptions[frame]) return;
      var current = ++request;
      var next = new Image();
      next.onload = function () {
        if (current !== request) return;
        art.src = next.src;
        art.alt = descriptions[frame];
        controls.querySelectorAll("button").forEach(function (item) {
          item.setAttribute("aria-pressed", String(item === button));
        });
      };
      // Keep the previous complete frame if an image cannot load.
      next.src = "/art/" + frame + "-793.webp";
    });
  });
})();
