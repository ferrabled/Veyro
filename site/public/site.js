/* Progressive enhancement only. No analytics, storage, autoplay or remote requests. */
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
