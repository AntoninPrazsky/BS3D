// The landing page in motion (#948, step 1). The owner: "it is a game, the site must be fun and attention-grabbing even for
// someone who cannot read English". The sections' pieces rise and fade in as they come into view, a few apart, and the three
// counts count up; the hero's entrance is the stylesheet's alone. The page's head sets the `motion` class before the first
// paint (and never under prefers-reduced-motion); this script says it has finished (window.bs3dMotion), or the head takes the
// class off again after a moment and with it every hidden piece, so a script that fails to load, or fails halfway, can never
// leave the page hidden.

const root = document.documentElement;

if (root.classList.contains("motion")) {
  // What rises, and its place among its siblings for the stagger
  const pieces = document.querySelectorAll(
    ".section h2, .section .lead, .card, .gallery figure, .features li, .platform, .posts li, .cannon-stage, .numbers-row p");
  for (const el of pieces) {
    const siblings = Array.from(el.parentElement.children).filter((c) => c.matches(el.tagName));
    el.style.setProperty("--i", String(Math.min(siblings.indexOf(el), 6)));
    el.classList.add("rise");
  }

  // Only the counts count up: a figure in a sentence's bold ("A Raspberry Pi 5 version") is words, and counted it read "5"
  const counted = new WeakSet();
  const reveal = (el) => {
    el.classList.add("in");
    if (!el.matches(".numbers-row p")) return;
    for (const n of el.querySelectorAll("strong")) if (!counted.has(n)) { counted.add(n); countUp(n); }
  };

  // Whatever is already on screen shows at once, without a frame hidden; the rest as it is scrolled to
  const seen = new IntersectionObserver((entries) => {
    for (const e of entries) if (e.isIntersecting) { reveal(e.target); seen.unobserve(e.target); }
  }, { rootMargin: "0px 0px -8% 0px" });
  for (const el of pieces) {
    const box = el.getBoundingClientRect();
    if (box.top < innerHeight && box.bottom > 0) reveal(el);
    else seen.observe(el);
  }
}
window.bs3dMotion = true;

// A count from nothing to its own figure, eased out
function countUp(el) {
  const target = Number(el.textContent.replace(/[^\d]/g, ""));
  if (!target) return;
  const start = performance.now(), length = 900;
  const tick = (now) => {
    const t = Math.min((now - start) / length, 1);
    el.textContent = String(Math.round(target * (1 - Math.pow(1 - t, 3))));
    if (t < 1) requestAnimationFrame(tick);
  };
  el.textContent = "0";
  requestAnimationFrame(tick);
}
