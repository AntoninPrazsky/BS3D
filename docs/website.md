# The website

**The game's landing page is a static site in `Web/`, built with Eleventy, for `https://bs3d.winphonew.eu/` on the owner's Webglobe hosting** (#815). It is an advertisement, not documentation: what the game is, pictures of it, and how to download and run it. The page says what the **newest release** does and links to it, so it is published with a release, after the release exists.

## Building and looking at it

```powershell
cd Web
npm ci                      # once: Eleventy, pinned in package.json / package-lock.json
npx eleventy --serve        # http://localhost:8080, rebuilt on every save
npx eleventy                # the plain files in Web\_site, which is all the hosting serves
```

`_site/` and `node_modules/` are not committed. Eleventy only overwrites `_site`; `publish.ps1` deletes it before building, so a removed page does not linger in an upload.

## What is where

| File | Holds |
|---|---|
| `src/index.njk` | The page: the logo and pitch, the big picture, how it plays, the numbers, the gallery, what else is in it, the download steps for Windows and the Raspberry Pi 5 |
| `src/_data/site.json` | The links (the newest release, the repository), the description the search engines and link previews show, the counts the page prints (levels, chapters, scenes) and the video slot |
| `src/_data/gallery.json` | The gallery's pictures in order, each with its level, chapter and caption |
| `src/_includes/base.njk` | The HTML shell, the link-preview tags and the footer |
| `src/_includes/post.njk`, `src/blog/` | The blog, ready but empty (below) |
| `src/css/site.css` | The look: dark like the game's menus, headings in Anton (the game's own display face, served from `src/fonts`) |
| `publish.ps1` | Builds and uploads (below) |

- **The counts and the feature list are typed, not read from the game.** At a release, check them against what ships: `site.json`'s `levels`, `chapters` and `scenes`, and the claims in "What else is in it" and in the download steps (the latter are the release notes' own, in `.github/workflows/release.yml`).
- **The video slot:** `site.json`'s `video` is null and the page says a video is on its way. Set it to the video's address and the page shows a button to it instead. It is a link, not an embed, so the page still makes no request to anyone else.
- **Nothing on the page talks to a third party**: no analytics, no cookies, no web fonts from elsewhere, so it needs no consent banner. Keep it that way; the footer says so.
- **A post for the blog** is a Markdown file in `src/blog/` with `title` and `date` in its front matter: it gets a page of its own, and the landing page grows a "From the workshop" list of the posts, newest first, which is absent while there are none. The journal (`docs/agent-notes.md`) is raw material for posts, never published as it stands.

## The pictures

**Every picture is the game itself at the High tier, and they are retaken for every release**, next to the release's own screenshot (`Tools/release-screenshot.ps1`, `Images/releases/`):

```powershell
.\Tools\web-screenshots.ps1          # Meander (the big picture) and the gallery's eight levels
.\Tools\web-screenshots.ps1 -Levels Reel,Vortex
```

It builds the Game in Release into a folder of its own and plays each level on a scratch profile whose settings turn the tutorial cards, the intro logo and the FPS counter off and hold High (so no first-time card covers the picture, nothing touches your save and nothing reaches the online boards). Each frame is cut to 16:9 round its centre and written to `Web/src/img/shots/<level>-1600.jpg` and `-800.jpg`. A level brings its own scene; the magazine's deal is pinned by `-Seed`. Look at every picture before committing it: a level that opens on a camera cinematic photographs the cinematic (Trefoil does).

The logo is `Images/logo/bs3d-logo-2048.png` scaled to 1000 and 600 px as WebP; the link-preview picture (`img/og-meander.jpg`, 1200×630) is Meander's frame cut to that shape.

**Weight** (measured on the first build): 28 files, 3.7 MB. The first screen on a desktop is about 600 KB (the page, its stylesheet, the Anton font at 171 KB, the logo at 182 KB and the big picture at 226 KB); the gallery's eight 800 px pictures, 690 KB together, load as they scroll into view.

## Publishing a change

```powershell
$env:BS3D_WEB_HOST = '<the hosting server>'
$env:BS3D_WEB_USER = '<the web account>'
$env:BS3D_WEB_ROOT = '<the subdomain document root, e.g. /www/bs3d>'
.\Web\publish.ps1 -Protocol sftp          # or -Protocol ftps with $env:BS3D_WEB_PASSWORD; -DryRun lists the files
```

- **The credentials live in the environment, never in a file**: the repository is public. A GitHub Actions workflow that publishes on a release would keep them in repository secrets.
- **⚠ Upload with the web account, never the off-site backup's.** The Pi's database backup (BS3D-API #6) goes to its own SFTP account on the same hosting, chrooted outside the web root, so nothing a deploy writes can reach the backups and nothing in them is served. The account names stay out of this public repository.
- The script only uploads: a file the site no longer has stays on the server until it is deleted by hand.
- **Cloudflare caches the page.** After a publish, look with a hard reload; purge the cache in the Cloudflare dashboard if the old files persist.

## The one-time setup (the owner's accounts)

- **Webglobe:** the subdomain `bs3d` with its own document root, and HTTPS at the origin (Webglobe's certificate for the subdomain, which Cloudflare's *Full (strict)* mode needs).
- **Cloudflare** (the domain's nameservers since 2026-10-01): a record for `bs3d` pointing at the Webglobe server, and a redirect rule sending `winphonew.eu` and `www.winphonew.eu` to `https://bs3d.winphonew.eu/`. The record for `scores` must keep pointing at the tunnel to the Pi: check `https://scores.winphonew.eu/` still answers after any change.
- **Check from outside, on more than one resolver**: one ISP's resolver followed the domain's old nameservers for a while after the move (#691).
