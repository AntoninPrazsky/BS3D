// The game's sound on the landing page (#950). The owner: "the website could also have the game's sounds and music - then
// there needs to be an always-visible mute button at the bottom of the site". The page opens silent and fetches nothing:
// the first press of the toggle loads the menu's own loop and the shot (about 1.2 MB), and from then on the toggle fades the
// loop in and out as the game's front end does (MENU_FADE_SECONDS, 0.5 s), at the game's level for it (MusicLevels.MENU_LEVEL
// times DECODE_HEADROOM: the recordings overshoot full scale once decoded). The live cannon's shots (#949) are heard with the
// game's own shot, panned by where the gun is. Web Audio, so the loop wraps sample-exactly; suspended in a hidden tab. Nothing
// is remembered: no cookie, no storage.

const button = document.querySelector("button.sound-toggle");
if (button && window.AudioContext) {
  button.hidden = false;
  document.body.classList.add("has-sound");
  setUp(button);
}

function setUp(button) {
  const MENU_LEVEL = 0.29, DECODE_HEADROOM = 0.84139514, FADE = 0.5, SHOT_LEVEL = 0.55, SHOT_PAN = 0.7, LOAD_TIMEOUT = 20000;
  const label = button.querySelector(".sound-label");
  let context = null, music = null, shot = null, loop = null, on = false, loading = false;

  const show = (text, pressed) => {
    label.textContent = text;
    button.setAttribute("aria-pressed", pressed ? "true" : "false");
    button.classList.toggle("on", pressed);
  };

  // Decoded through an offline context, which needs no output device (a playing context that cannot reach one never
  // finished decoding in a headless browser); an AudioBuffer belongs to no context, so the playing one takes it as it is
  async function load() {
    const decoder = new OfflineAudioContext(2, 1, 48000);
    const fetchBuffer = (url) => fetch(url).then((r) => r.arrayBuffer()).then((b) => decoder.decodeAudioData(b));
    const [menu, shoot] = await Promise.race([
      Promise.all([fetchBuffer("/audio/menu.ogg"), fetchBuffer("/audio/shoot.ogg")]),
      new Promise((_, fail) => setTimeout(() => fail(new Error("the sound took too long to load")), LOAD_TIMEOUT)),
    ]);
    context = new AudioContext();
    music = context.createGain();
    music.gain.value = 0;
    music.connect(context.destination);
    loop = context.createBufferSource();
    loop.buffer = menu;
    loop.loop = true;
    loop.connect(music);
    loop.start();
    shot = shoot;
  }

  button.addEventListener("click", async () => {
    if (loading) return;
    if (!music) {
      loading = true;
      show("Loading…", false);
      try {
        await load();
      } catch (e) {
        console.warn("[sound]", e);
        loading = false;
        show("No sound in this browser", false);
        button.disabled = true;
        return;
      }
      loading = false;
    }
    on = !on;
    if (on) context.resume().catch(() => {});
    const now = context.currentTime;
    music.gain.cancelScheduledValues(now);
    music.gain.setValueAtTime(music.gain.value, now);
    music.gain.linearRampToValueAtTime(on ? MENU_LEVEL * DECODE_HEADROOM : 0, now + FADE);
    show(on ? "Sound on" : "Sound off", on);
  });

  // The cannon's shot, from where it is across the canvas (-1 left to 1 right)
  window.addEventListener("bs3d:shot", (e) => {
    if (!on || !shot || document.hidden) return;
    const source = context.createBufferSource();
    source.buffer = shot;
    source.playbackRate.value = 0.94 + Math.random() * 0.12;
    const gain = context.createGain();
    gain.gain.value = SHOT_LEVEL * DECODE_HEADROOM;
    const pan = context.createStereoPanner();
    pan.pan.value = Math.max(-1, Math.min(1, (e.detail?.x ?? 0) * SHOT_PAN));
    source.connect(gain).connect(pan).connect(context.destination);
    source.start();
  });

  document.addEventListener("visibilitychange", () => {
    if (!context) return;
    if (document.hidden) context.suspend();
    else if (on) context.resume();
  });
}
