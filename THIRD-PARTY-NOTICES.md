# Third-party notices

nord-rice is MIT licensed (see `LICENSE`). It includes or uses the following.

## Included in this repository

| Component | Where | License |
|---|---|---|
| Nord theme for Zebar, by Dang Quang | Base of the bar (`src/`), https://github.com/quangbhdang/zebar-nord | MIT (credited in `LICENSE`) |
| Rubik font, by The Rubik Project Authors | `public/fonts/`, `control-center/Fonts/` | SIL Open Font License 1.1, see `licenses/Rubik-OFL.txt` |

## Downloaded by `install.ps1` (not stored in this repository)

| Component | Used for | License |
|---|---|---|
| NAudio 1.10.0, by Mark Heath & Contributors | Audio devices and volume in the control center; `NAudio.dll` is copied next to the helper | https://github.com/naudio/NAudio/blob/master/license.txt |
| Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3 | Build-time reference assemblies only | https://github.com/Microsoft/dotnet/blob/master/LICENSE |
| npm packages in `package.json` (Solid, Tailwind CSS, Zebar client, and others) | The bar's web UI, bundled into `dist/` on your machine | See each package in `node_modules/` |

## Required separately

GlazeWM and Zebar (https://github.com/glzr-io), Node.js, and optionally btop4win and Spicetify are separate projects under their own licenses.
