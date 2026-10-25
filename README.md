# Printshop Tycoon

> Mobilspil (iOS + Android) hvor Hue og Silke bygger et lille tekstiltrykkeri op til et trykimperium. Unity 6 · URP · C# · portræt 9:16 · offline.

## Status lige nu

**Prototype v0.1 – første spilbare vertical slice, skrevet uden adgang til Unity.**

- Al kode, opsætning og tests er skrevet.
- **Spil-logikken er testet** (31 automatiske tests, alle grønne).
- **Unity-delen compiler** mod Unity-referencer – men den er **endnu ikke kørt i Unity eller på en telefon**.
- Første gang projektet åbnes i Unity skal det tjekkes og evt. rettes til (se "Første gang i Unity").

## Acceptkriterier – ærlig status

| # | Kriterie | Kode skrevet | Testet |
|---|----------|:---:|---|
| 1 | Starter i portræt | ✅ | ❌ Ikke kørt i Unity |
| 2 | Hue styres med touch-joystick | ✅ | ❌ Ikke kørt i Unity |
| 3 | Begge kamera-modes virker | ✅ | ❌ Ikke kørt (PlayMode-test klar) |
| 4 | Ordre på 5 T-shirts kan gennemføres | ✅ | ✅ Logik testet · ❌ ikke i Unity |
| 5 | Varmepresse-minigame virker | ✅ | ✅ Logik testet · ❌ UI ikke kørt |
| 6 | Korrekt betaling (150 mønter) | ✅ | ✅ Testet (inkl. ingen dobbelt-betaling) |
| 7 | Ekstra varmepresse kan købes og placeres | ✅ | ✅ Logik testet · ❌ ikke i Unity |
| 8 | Medarbejder automatiserer en opgave | ✅ | ✅ Logik testet · ❌ ikke i Unity |
| 9 | Gem og hent virker | ✅ | ✅ Logik + JSON testet · ❌ ikke på enhed |
| 10 | Kører på telefon/simulator | ✅ build-script | ❌ Ingen Unity/Android SDK i miljøet |

## Første gang i Unity

1. **Installér Unity 6 LTS** (6000.0.x) via Unity Hub – med **Android Build Support** (og **iOS Build Support** på Mac).
2. Unity Hub → **Add → Add project from disk** → vælg mappen `apps/printshop-tycoon`.
3. Vent mens Unity henter pakker (URP, uGUI, Input System, Test Framework).
4. **Opsætning kører automatisk** første gang. Ellers: menu **Printshop Tycoon → Setup Project**. Den:
   - sætter portræt, bundle-id `dk.reberholt.printshoptycoon`, IL2CPP/ARM64, iOS 13+
   - laver et mobil-URP-asset og tildeler det
   - laver base-materialet, balance-filen og scenen `Scenes/Main.unity`
   - sætter Input Handling til **Both** (Unity kan bede om genstart – sig ja)
5. Tryk **Play**. Sæt Game-vinduet til en portræt-opløsning (fx 1080×1920) eller brug **Device Simulator**.
6. Kør tests: **Window → General → Test Runner** → EditMode og PlayMode.
7. Android-build: **Printshop Tycoon → Build Android APK** (→ `Builds/Android/PrintshopTycoon.apk`).

> Spillet starter også fra en tom scene – en bootstrapper opretter selv `GameManager`.

## Sådan spilles det

- **Joystick** (venstre nederst): styr Hue. I editoren virker WASD/piletaster også.
- **Stor orange knap** (højre nederst): brug den station Hue står ved. Teksten viser hvad der sker. Tastatur: E/mellemrum.
- **Gul pil** over en station = næste trin. Ordrekortet øverst viser trin og fremskridt.
- **Fabrik-knappen**: skift til fabriksoverblik. Træk = panorér, knib = zoom, tryk = se info.
- **Første ordre** (Nibe Bageri, 5 T-shirts, 150 mønter):
  ordretavle → hylde → DTF-printer (vælg logo) → varmepresse (stop viseren i den grønne zone) → tag T-shirts → pakkebord.
- Derefter: **Byg** en ekstra varmepresse (300) → **Ansæt** en medarbejder (250) → medarbejderen klarer småordrer selv.

## Økonomi (standardværdier)

- Start: **500 mønter**, 20 blanke T-shirts
- Første ordre: **150** (perfekt pres +20 %, skævt pres −20 %)
- Varmepresse: **300** · Medarbejder: **250** · 10 blanke T-shirts: **40**
- Småordre (medarbejder): 2 T-shirts → **40**
- Alle tal ligger i `Resources/PrintshopTycoon/GameBalance.asset` (oprettes af opsætningen) – ingen tal er hardcodet i gameplay-koden.

## Arkitektur

```text
Assets/PrintshopTycoon/
├── Scripts/Core/      ← spilregler i ren C# (ingen Unity) – kan testes uden editor
│   ├── GameSession     samler hele spillets tilstand og regler
│   ├── Wallet          mønter, aldrig negativ, hver belønning kun én gang
│   ├── OrderSystem     ordre-trin som state machine
│   ├── HeatPressMinigame  timing-logik
│   ├── Automation      medarbejder-FSM (Idle → Moving → Working → Packing)
│   ├── PlacementValidator + WorkshopLayout
│   ├── SaveData/SaveStore  JSON, version, backup-fil, sikker skrivning
│   └── Loc             dansk + engelsk tekst
├── Scripts/Runtime/   ← Unity-laget (visning, input, kamera, UI)
│   ├── Systems/        GameManager, kamera, interaktion, byg, gem
│   ├── Stations/       hylde, DTF-printer, varmepresse, pakkebord, ordretavle
│   ├── Characters/     Hue, medarbejdere (placeholder-figurer)
│   ├── Input/          joystick + touch-flade (pan/pinch/tap)
│   ├── UI/             HUD, menuer, minigame – bygget i kode
│   ├── Audio/          lyde genereres i koden (ingen licens-problemer)
│   └── World/          værkstedet bygget af simple former
├── Editor/            ← opsætning + build-scripts
└── Tests/             ← EditMode (logik) + PlayMode (hele forløbet i Unity)
Tools/
├── CoreTests/         ← kør logik-tests uden Unity
└── CompileCheck/      ← compile Unity-koden uden Unity (fanger tastefejl, ikke alt)
```

- **Logik og visning er adskilt**: al økonomi sker i `Core`. Unity-laget kan aldrig betale to gange – heller ikke ved kameraskift eller efter genindlæsning.
- **Ingen prefabs/scener at vedligeholde endnu**: alt bygges i kode, så projektet kan laves og versionsstyres uden editor. Kan senere erstattes af rigtige prefabs og modeller.

## Tests uden Unity

Kræver .NET 8 SDK.

```bash
cd apps/printshop-tycoon
dotnet test Tools/CoreTests                       # 31 logik-tests
dotnet build Tools/CompileCheck                   # compile-check (legacy input)
dotnet build Tools/CompileCheck -p:InputPath=New  # compile-check (Input System)
```

## Hue og Silke

- **Figurerne er midlertidige placeholders** (farvede kapsler med kasket/hestehale).
- Deres rigtige udseende skal laves ud fra **jeres referencebilleder**. Intet design er opfundet eller låst.
- Udskiftning: `Characters/CharacterFactory.cs` – gameplay bruger kun rod-objektet og `Visual`-barnet.

## Kendte begrænsninger

- **Ikke kørt i Unity endnu** – forvent små rettelser første gang (layout, kameravinkel, tekststørrelser).
- Medarbejdere går ad en simpel midtergang, ikke NavMesh – de kan gå gennem maskiner.
- Ingen musik, ingen rigtige 3D-modeller, ingen animationer ud over simple "hop".
- Ingen offline-indtjening endnu (gem-filen har tidsstempel, så det kan bygges på).
- Engelsk er klar i tekst-tabellen, men ikke gennemlæst.

## Næste skridt

1. Åbn i Unity 6, kør opsætning, ret evt. fejl, kør PlayMode-testen.
2. Test på en rigtig Android-telefon (mellemklasse) – mål FPS.
3. Justér kamera, knapstørrelser og tekst på små skærme.
4. Referencebilleder af Hue og Silke → rigtige modeller.
5. Næste spil-lag: hastigheds-opgraderinger, flere ordretyper, offline-indtjening.

---

**Ejer:** Thomas Reberholt · **Status:** Prototype · **Sprog:** Dansk (engelsk forberedt)
