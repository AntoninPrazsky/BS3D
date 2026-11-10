# BS3D in Pi-Apps

[Pi-Apps](https://github.com/Botspot/pi-apps) is the app store most Raspberry Pi OS users install apps from. This
folder holds BS3D's app for it (#1078, part of #858). The files live here so they change together with the release
they install. They are copied into a fork of Botspot/pi-apps only when the owner says go on #858. The fork, its
`test_build.yml` run and the pull request are public, so they wait for that go.

## What goes where in a Pi-Apps fork

| Here | In the fork |
|---|---|
| `BS3D/` (`install-64`, `uninstall`, `description`, `credits`, `website`, `icon-24.png`, `icon-64.png`) | `apps/BS3D/` |
| `updates/BS3D.sh` | `.github/workflows/updates/BS3D.sh` |
| (one line) | `BS3D\|Games` in `etc/categories` |

`install-64` and `uninstall` must keep mode 775.

## What install-64 does

1. It refuses a Raspberry Pi older than the Pi 5, and a Jetson, until #1080 has measured them. A computer it cannot
   identify gets a warning and installs anyway: Pi-Apps' own CI runs in a chroot that reports no model.
2. It makes ICU a dependency through `install_packages` (`libicu78`, `77`, `76`, `74`, `72` or `70`: Ubuntu 26.04 Resolute has 78, and the first test_build.yml run failed there without it). .NET needs ICU, and the
   desktop images of Raspberry Pi OS already carry it.
3. It downloads `BS3D-v<version>-linux-arm64.tar.gz` and the release's `SHA256SUMS` into `/var/tmp`, which is on
   disk; Trixie keeps `/tmp` in memory. It checks the tarball against its line in `SHA256SUMS`, and the download
   folder is removed however the script ends.
4. It unpacks the tarball into `/opt/bs3d`. Not into `~/BS3D`, which is the folder a clone of this repository takes.
5. It links `/usr/local/bin/bs3d` and writes the menu entry `/usr/local/share/applications/bs3d.desktop`.

`uninstall` removes those three paths and nothing else. The player's own files (the save, the settings, the online
identity and the outbox) live in `~/.local/share/BS3D` and stay. They have to, because Pi-Apps updates an app by
uninstalling it and installing it again.

## The contract with our release

Once the app is in Pi-Apps, their updater (`updates/BS3D.sh`, run on their schedule) moves `version=` in
`install-64` to our newest release by itself. Every tag therefore reaches Pi players, and these names are fixed:

- the asset `BS3D-v<version>-linux-arm64.tar.gz`, from a tag `v<version>`;
- its line in the release's `SHA256SUMS`;
- one top folder inside the tarball.

Rename any of them and every Pi that installed the game fails its next update. `release.yml` says the same thing
beside `BUILD_NAME`.

## Testing it on a Pi

Without Pi-Apps' own CI, on a Pi 5 that has Pi-Apps:

```bash
cp -r Tools/pi-apps/BS3D ~/pi-apps/apps/BS3D
~/pi-apps/manage install BS3D      # asks for the sudo password
bs3d                               # or Menu -> Games -> BS3D
~/pi-apps/manage uninstall BS3D
rm -r ~/pi-apps/apps/BS3D
```

To test an update, set `version=` in the copied `install-64` to an older tag, install it, play a level, set
`version=` back to the newest tag, and run `~/pi-apps/manage uninstall BS3D` and then `~/pi-apps/manage install BS3D`.
The save in `~/.local/share/BS3D` must still be there.

`shellcheck BS3D/install-64 BS3D/uninstall` must print nothing. Pi-Apps' CI fails a pull request on any shellcheck
error.
