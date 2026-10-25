// A before/after figure in a post (#942). The markup is a figure.compare holding two .compare-pane, each a picture and its
// label, and without this script the page shows both, the first above the second, each labelled. With it the two panes are
// laid on one another, a Before / After pair of buttons says which is shown (aria-pressed), and a click on the picture
// switches to the other, so the change is seen in the same place. Nothing is fetched here: both pictures are in the page
// already (visibility, not display, hides one, so a lazy picture still loads as the figure nears the screen). The post
// layout loads this file only on a page that has such a figure.

for (const figure of document.querySelectorAll("figure.compare")) setUp(figure);

function setUp(figure) {
  const frame = figure.querySelector(".compare-frame");
  const panes = [...figure.querySelectorAll(".compare-pane")];
  if (!frame || panes.length !== 2) return;

  const group = document.createElement("div");
  group.className = "compare-controls";
  group.setAttribute("role", "group");
  group.setAttribute("aria-label", "Show the picture before or after");
  const buttons = panes.map((pane, i) => {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = pane.dataset.label || (i === 0 ? "Before" : "After");
    button.addEventListener("click", () => show(i));
    group.append(button);
    return button;
  });
  frame.before(group);

  let shown = -1;
  function show(i) {
    shown = i;
    panes.forEach((pane, j) => pane.classList.toggle("is-shown", j === i));
    buttons.forEach((button, j) => button.setAttribute("aria-pressed", j === i ? "true" : "false"));
  }

  // The picture is a button for a mouse or a finger; a keyboard has the two real buttons above it
  frame.addEventListener("click", () => show(1 - shown));
  show(0);
  figure.classList.add("is-live");
}
