// A tier slider in a post (#1208). The markup is a figure.tiers holding a .tiers-frame of two or more .tiers-pane, each a picture,
// its label (data-label), a .tiers-tag and a .tiers-note saying what that detail level draws. Without this script the page shows
// every pane, one above the next, each with its tag and its note. With it the panes lie on one another in one grid cell, a stepped
// slider under the picture says which is shown (a range input, so the arrow keys, Home and End work, and a finger can drag it),
// the tier names under the slider are buttons that jump to one, a click on the picture steps to the next, and the notes stand
// in one place under the slider, the one for the shown pane visible. Nothing is fetched here: every picture is in the page already
// (visibility, not display, hides one, so a lazy picture still loads as the figure nears the screen). The post layout loads this
// file only on a page that has such a figure. The figure's data-slider-label names what the slider picks for a screen reader
// ("Detail level" without it), and a figure of more than MANY_STEPS panes is marked .tiers-many: on a phone its names cannot stand
// under their steps, and the stylesheet wraps them into rows under the slider instead (#1238, the ten ball materials).

const MANY_STEPS = 6;

for (const figure of document.querySelectorAll("figure.tiers")) setUp(figure);

function setUp(figure) {
  const frame = figure.querySelector(".tiers-frame");
  const panes = [...figure.querySelectorAll(".tiers-pane")];
  if (!frame || panes.length < 2) return;
  const last = panes.length - 1;
  const names = panes.map((pane, i) => pane.dataset.label || `Level ${i + 1}`);

  const controls = document.createElement("div");
  controls.className = "tiers-controls";

  const slider = document.createElement("input");
  slider.type = "range";
  slider.className = "tiers-slider";
  slider.min = "0";
  slider.max = String(last);
  slider.step = "1";
  slider.value = "0";
  slider.setAttribute("aria-label", figure.dataset.sliderLabel || "Detail level");
  slider.addEventListener("input", () => show(Number(slider.value)));

  // The names are a convenience for a mouse or a finger: the slider is the control a keyboard and a screen reader meet
  const ticks = document.createElement("div");
  ticks.className = "tiers-ticks";
  ticks.setAttribute("aria-hidden", "true");
  ticks.style.setProperty("--last", String(last));
  const buttons = names.map((name, i) => {
    const button = document.createElement("button");
    button.type = "button";
    button.tabIndex = -1;
    button.textContent = name;
    button.style.setProperty("--i", String(i));
    button.addEventListener("click", () => show(i));
    ticks.append(button);
    return button;
  });

  // The notes move out from under the pictures into one grid cell, so the readout is as tall as the longest and the page
  // does not jump as the slider moves
  const notes = document.createElement("div");
  notes.className = "tiers-notes";
  notes.setAttribute("aria-live", "polite");
  const noteOf = panes.map((pane) => {
    const note = pane.querySelector(".tiers-note");
    if (note) notes.append(note);
    return note;
  });

  controls.append(slider, ticks, notes);
  frame.after(controls);

  let shown = -1;
  function show(i) {
    i = Math.max(0, Math.min(last, i));
    if (i === shown) return;
    shown = i;
    slider.value = String(i);
    slider.setAttribute("aria-valuetext", names[i]);
    panes.forEach((pane, j) => pane.classList.toggle("is-shown", j === i));
    buttons.forEach((button, j) => button.setAttribute("aria-pressed", j === i ? "true" : "false"));
    noteOf.forEach((note, j) => note && note.classList.toggle("is-shown", j === i));
  }

  // The picture is a button for a mouse or a finger: the next level, round to the first after the last
  frame.addEventListener("click", () => show(shown === last ? 0 : shown + 1));
  show(0);
  figure.classList.toggle("tiers-many", panes.length > MANY_STEPS);
  figure.classList.add("is-live");
}
