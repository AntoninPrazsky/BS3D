// The looping clips of a post (#1042). A clip is a <video class="clip"> that is muted and looping, with controls, a poster and
// preload="none", and with no autoplay in the markup. So without this script, or under prefers-reduced-motion, nothing moves and
// only the poster loads until the reader presses Play. With the script a clip plays while at least half of it is on the screen
// in a visible tab, and pauses when it leaves. A clip the reader paused stays paused until they play it again. The post layout
// loads this file only on a page that has such a clip.
//
// The clips of one <figure data-together> (a before beside its after, of one length, #1237) play as one: all of them while half
// of any is on the screen, the reader's pause or play on one does the same to the others, a seek on one takes the others along,
// and when one runs out of step with the first (it loaded later, or stalled), the second is slowed or hurried by up to SPEEDUP
// while they are less than JUMP apart, and beyond that the one ahead is moved back to the one behind.

const clips = [...document.querySelectorAll("video.clip")];
const still = matchMedia("(prefers-reduced-motion: reduce)").matches;

if (clips.length && !still && "IntersectionObserver" in window) {
  const seen = new Set();      // at least half of it on the screen
  const held = new WeakSet();  // paused by the reader
  const ours = new WeakSet();  // a pause this script asked for, not yet reported
  const moved = new WeakSet(); // a seek this script made to keep a group in step, not yet reported

  const STEP = 1 / 30;   // a frame of the clips: closer than half of one, two clips are in step
  const JUMP = 0.25;     // seconds off beyond which a clip is moved rather than hurried
  const SPEEDUP = 0.25;  // the most a clip's rate is changed by while it catches up

  // Each clip's group: the clips of its figure[data-together], or itself alone
  const groups = new Map(clips.map((clip) => {
    const figure = clip.closest("figure[data-together]");
    return [clip, figure ? [...figure.querySelectorAll("video.clip")] : [clip]];
  }));

  function run(clip) {
    const group = groups.get(clip);
    const go = group.some((c) => seen.has(c)) && !group.some((c) => held.has(c)) && !document.hidden;
    for (const c of group) {
      if (go) c.play().catch(() => {});
      else if (!c.paused) { ours.add(c); c.pause(); }
    }
  }

  // How far `other` runs behind `clip`, in seconds (ahead is negative), the short way round the loop's seam
  function behind(clip, other) {
    const length = clip.duration || 0;
    let off = clip.currentTime - other.currentTime;
    if (length) { if (off > length / 2) off -= length; else if (off < -length / 2) off += length; }
    return off;
  }

  // Puts `other` at `clip`'s time
  function moveTo(clip, other) {
    moved.add(other);
    other.playbackRate = 1;
    other.currentTime = clip.currentTime;
  }

  for (const clip of clips) {
    const group = groups.get(clip);
    clip.addEventListener("pause", () => {
      if (ours.delete(clip)) return;
      held.add(clip);
      if (group.length > 1) run(clip);
    });
    clip.addEventListener("play", () => {
      held.delete(clip);
      if (group.length > 1) for (const c of group) if (c.paused) c.play().catch(() => {});
    });
    if (group.length < 2) continue;

    // A seek the reader made (or a loop's return to the start) takes the others along, unless they are already there
    clip.addEventListener("seeking", () => {
      if (moved.delete(clip)) return;
      for (const c of group) if (c !== clip && c.readyState > 0 && Math.abs(behind(clip, c)) > STEP / 2) moveTo(clip, c);
    });

    // The first clip of the group keeps the time; the others follow it
    if (clip !== group[0]) continue;
    clip.addEventListener("timeupdate", () => {
      const ready = !clip.paused && !clip.seeking && clip.readyState > 2;
      for (const c of group) {
        if (c === clip) continue;
        if (!ready || c.paused || c.seeking || c.readyState < 3) { c.playbackRate = 1; continue; }
        const off = behind(clip, c);
        // Far off, the one ahead goes back to the one behind: what it has played is loaded, so the seek is quick, where a seek
        // ahead into what is still downloading stalls, and the one moved falls further behind
        if (off > JUMP) moveTo(c, clip);
        else if (off < -JUMP) moveTo(clip, c);
        else c.playbackRate = Math.abs(off) < STEP / 2 ? 1 : 1 + Math.max(-SPEEDUP, Math.min(SPEEDUP, 2 * off));
      }
    });
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
