// The trailer on the landing page (#1228). The video lives on YouTube, and the page still talks to no one else until the
// visitor asks for it: what is drawn is a poster of our own (the moment YouTube shows as the video's thumbnail, taken from the
// 4K master) with a play button, and only the press puts YouTube's player in its place, on its no-cookie domain and playing at
// once. Without this script, or with a click meant for a new tab, the poster is a plain link to the video on YouTube. The
// page's own sound (js/sound.js) hears about it, so the menu music does not play under the trailer's.

for (const stage of document.querySelectorAll(".trailer-stage[data-youtube]")) {
  const poster = stage.querySelector("a.trailer-poster");
  if (!poster) continue;
  poster.addEventListener("click", (e) => {
    if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    e.preventDefault();
    const frame = document.createElement("iframe");
    frame.src = `https://www.youtube-nocookie.com/embed/${encodeURIComponent(stage.dataset.youtube)}?autoplay=1&rel=0&playsinline=1`;
    frame.title = stage.dataset.title || "Trailer";
    frame.allow = "autoplay; encrypted-media; picture-in-picture; fullscreen";
    frame.allowFullscreen = true;
    // YouTube's player refuses to play with no referrer at all (its "error 153"), so the origin is sent whatever the server says
    frame.referrerPolicy = "strict-origin-when-cross-origin";
    stage.replaceChildren(frame);
    stage.classList.add("playing");
    frame.focus();
    window.dispatchEvent(new CustomEvent("bs3d:trailer"));
  });
}
