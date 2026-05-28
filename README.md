# 💩 Poop — ModSharp plugin

Spawn poops on dead players (or yourself), build leaderboards, customize colors / sounds. A goofy showcase plugin for the [ModSharp](https://github.com/Kxnrl/modsharp-public) framework — also a decent reference for laying out a multi-project ModSharp plugin with a public Shared API, central database module, and example consumer.

## ✨ Features

- **Spawning** — `!poop` drops a poop on the nearest dead player or at your feet. 7-tier weighted size system (Normal 40% → Rare 2%, with Massive / Legendary / Ultra Legendary sub-tiers).
- **Colors** — 17 presets + per-player preference menu, optional rainbow cycle and random mode.
- **Sounds** — configurable spawn / taunt sound events, per-event volume override.
- **Leaderboards** — `!toppoopers`, `!toppoop` for placed / received counts.
- **Persistence** — LiteDB by default, MySQL / PostgreSQL via SqlSugar — same `IDatabaseProvider` interface, switched in `poop.database.json`.
- **Localization** — `LocalizerManager` key-first JSON, ships `en-US` + `zh-CN`, fully extensible.
- **Public API** — `IPoopShared` for other plugins (force-spawn, stats lookup, spawn / command events). See `Poop.Example`.

## 📁 Solution layout

| Project | Output | Purpose |
|---|---|---|
| `Poop.Core` | `.build/modules/Poop.Core/Poop.dll` | Main plugin — commands, lifecycle, color menu, spawner |
| `Poop.Database` | `.build/modules/Poop.Database/Poop.Database.dll` | Database module — publishes `IDatabaseProvider` |
| `Poop.Database.Shared` | `.build/shared/Poop.Database.Shared/…` | DB API surface (LiteDB / SqlSugar abstraction) |
| `Poop.Shared` | `.build/shared/Poop.Shared/…` | Public plugin API (`IPoopShared`, events, models) |
| `Poop.Example` | `.build/modules/Poop.Example/PoopExample.dll` | Reference consumer of `IPoopShared` |

## 🛠️ Build

Requires **.NET 10 SDK** and the ModSharp NuGet feed (configured via the NuGet packages in `Directory.Build.props`).

```bash
dotnet build -c Release
```

Output lands under `.build/`:

```
.build/
├── configs/poop.json
├── configs/poop.database.json
├── locales/poop.json
├── modules/Poop.Core/Poop.dll
├── modules/Poop.Database/Poop.Database.dll
├── modules/Poop.Example/PoopExample.dll
└── shared/Poop.Shared/...
└── shared/Poop.Database.Shared/...
```

The `CopyAssets` target in `Poop.Core.csproj` mirrors `.assets/*` into `.build/` after each build, so default configs / locales ship alongside the DLLs.

## 🚀 Install

Drop the build output into your server's `game/csgo/addons/sharp/` tree:

| Source (`.build/...`) | Destination (`sharp/...`) |
|---|---|
| `configs/*.json` | `configs/` |
| `locales/*.json` | `locales/` |
| `modules/Poop.Core/*` | `modules/Poop.Core/` |
| `modules/Poop.Database/*` | `modules/Poop.Database/` |
| `modules/Poop.Example/*` *(optional)* | `modules/Poop.Example/` |
| `shared/Poop.Shared/*` | `shared/Poop.Shared/` |
| `shared/Poop.Database.Shared/*` | `shared/Poop.Database.Shared/` |

If the `configs/` files are missing on first load, both `Poop.Core` and `Poop.Database` write the bundled defaults — but shipping them up front keeps your edits sticky and saves a restart.

Game assets (the poop model, textures, sounds) live under `.assets/content/` and are referenced from `poop.json` as `models/yappershq/fun/poop.vmdl` + `soundevents/soundevents_general.vsndevts`. Compile those into your VPK / workshop addon separately.

## ⚙️ Configuration

### `sharp/configs/poop.json` — gameplay config

Tunes spawning, sizes, colors, sounds, commands, victim detection, chat prefix. Defaults are sane; see `Poop.Core/Config/PoopConfig.cs` for the full schema.

Highlights:

- `size.generationTiers[]` — weighted tier table; sub-tiers (Massive / Legendary / Ultra) under the `Rare` tier
- `color.availableColors[]` — palette shown in `!poopcolor` menu (set `isRainbow` or `isRandom` for the special entries)
- `sound.poopSounds[]` / `tauntSounds[]` — sound event names + optional per-event volume
- `commands.*.aliases[]` — rename commands without code changes
- `gameplay.maxPoopsPerRound` — set `0` to disable cap; `poopLifetimeSeconds` for auto-cleanup

### `sharp/configs/poop.database.json` — database config

```json
{
  "Database": {
    "Type": "litedb",
    "Host": "localhost",
    "Port": 3306,
    "Database": "poop",
    "User": "root",
    "Password": ""
  }
}
```

- `Type`: `litedb` (default — writes `sharp/data/poop.db`), `mysql`, or `postgres`
- For MySQL / PostgreSQL, fill Host / Port / Database / User / Password — the rest is ignored under LiteDB

## 🌍 Localization

Locale file: `sharp/locales/poop.json`. Format is **key-first then per-culture** (ModSharp `LocalizerManager` requirement — language-first nesting silently drops everything):

```json
{
  "poop.spawned_self": {
    "en-US": "You spawned a {0} poop ({{green}}{1:F3}{{default}}) at your position!",
    "zh-CN": "你在自己的位置生成了一坨 {0} 便便 ({{green}}{1:F3}{{default}})！"
  }
}
```

Notes:

- Color tokens are **double-braced** in JSON (`{{green}}` → `{green}` after `string.Format`) — single-brace throws `FormatException` at runtime
- Culture codes must match ModSharp's `Internationalization.SteamLanguageToI18N` table (`en-US`, `zh-CN`, `ru-RU`, …). `en` / `cn` are silently dropped as invalid
- Add a culture by adding the inner key to each string. Players whose Steam language has no entry fall back to `en-US`

## 🎮 Commands

| Command | Aliases (default) | What it does |
|---|---|---|
| `!poop` | `!shit` | Spawn on nearest dead player, else at your feet |
| `!poopcolor` | `!poop_color`, `!colorpoop` | Open color preference menu |
| `!toppoopers` | `!pooperstop` | Leaderboard — most poops placed |
| `!toppoop` | `!pooptop` | Leaderboard — most pooped-on |

All aliases / cooldowns are configurable in `poop.json` under `commands.*`.

## 🔌 Public API (`IPoopShared`)

Other plugins can drive Poop without referencing `Poop.Core`. Pull `Poop.Shared` from the build output, then resolve the interface in `OnAllModulesLoaded` (publishers register in `PostInit`):

```csharp
var poop = sharedSystem.GetSharpModuleManager()
                       .GetRequiredSharpModuleInterface<IPoopShared>(IPoopShared.Identity)
                       ?.Instance;

poop.ForcePlayerPoop(client, size: 2.5f);
var stats = await poop.GetPlayerStatsAsync(client.SteamId);

poop.OnPoopSpawned += e => Log($"{e.Player?.Name} dropped a {e.Size:F2} poop");
poop.OnPoopCommand += e => { if (!e.Player.IsAdmin) e.Cancel = true; };
```

See `Poop.Example/PoopExample.cs` for a complete consumer plugin (force-spawn, stats lookup, gating).

## ⚠️ ModSharp lifecycle reminder

`OnAllModulesLoaded` ordering across plugins is **not** guaranteed during `PostInit`. Rules used here:

- Publishers (`Poop.Database`, `Poop.Core`) register their interfaces in `PostInit`
- Consumers (`Poop.Example`, internal modules) resolve in `OnAllModulesLoaded` — ModSharp guarantees all `PostInit`s finish before any `OnAllModulesLoaded` fires
- `LocalizerManager` is looked up in `OnAllModulesLoaded` (it publishes itself in `PostInit`)

## 🙏 Credits

- **ModSharp** — Kxnrl & co. — the framework this is built on
- **Nuko / laper32** — manager patterns + early code review
- **yappershq** team — assets, ideas, putting up with this
