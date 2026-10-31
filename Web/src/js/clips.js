// The looping clips of a post (#1042). A clip is a <video class="clip"> that is muted and looping, with controls, a poster and
// preload="none", and with no autoplay in the markup. So without this script, or under prefers-reduced-motion, nothing moves and
// only the poster loads until the reader presses Play. With the script a clip plays while at least half of it is on the screen
// in a visible tab, and pauses when it leaves. A clip the reader paused stays paused until they play it again. The post layout
// loads this file only on a page that has such a clip.

const clips = [...document.querySelectorAll("video.clip")];
const still = matchMedia("(prefers-reduced-motion: reduce)").matches;

if (clips.length && !still && "IntersectionObserver" in window) {
  const seen = new Set();      // at least half of it on the screen
  const held = new WeakSet();  // paused by the reader
  const ours = new WeakSet();  // a pause this script asked for, not yet reported

  function run(clip) {
    if (seen.has(clip) && !held.has(clip) && !document.hidden) clip.play().catch(() => {});
    else if (!clip.paused) { ours.add(clip); clip.pause(); }
  }

  for (const clip of clips) {
    clip.addEventListener("pause", () => { if (!ours.delete(clip)) held.add(clip); });
    clip.addEventListener("play", () => held.delete(clip));
  }
  const observer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (entry.isIntersecting) seen.add(entry.target); else seen.delete(entry.target);
      run(entry.target);
    }
  }, { threshold: 0.5 });
  for (const clip of clips) observer.observe(clip);
  document.addEventListener("visibilitychange", () => clips.forEach(run));
}
