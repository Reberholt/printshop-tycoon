# Printshop Tycoon

> Mobilspil (iOS + Android), hvor Hue og Silke bygger et lille tekstiltrykkeri op til et trykimperium. Unity 6 · URP · C# · portræt 9:16 · offline.

## Status – v0.3

**Skrevet uden adgang til Unity.** Spillets regler er testet. Unity-delen compiler, men er **ikke kørt i Unity eller på en telefon endnu**.

| Del | Status |
|---|---|
| Spillets regler (økonomi, ordrer, opgraderinger, specialordre, gem/hent) | ✅ 65 automatiske tests, alle grønne |
| Unity-kode | ✅ Compiler (tjekket uden Unity) · ❌ ikke kørt |
| PlayMode-tests i Unity | ✅ Skrevet · ❌ ikke kørt |
| Skærmbilleder / video / WebGL | ✅ Værktøj klar (se nedenfor) · ❌ ikke lavet – kræver Unity |
| Test på rigtig telefon | ❌ Ikke gjort |

## Nyt i v0.3
- **Værkstedet:** props og zoner – lager (blå), produktion (orange), forsendelse (grøn) og byggefelt (turkis med streger).
- **Maskinerne:**
  - DTF-printeren printer med slæde, og filmen kommer ud med logoet på.
  - Varmepressen lukker, gløder og damper. Pakkebordet pakker kassen og lægger den på pallen.
- **Figurerne:** går, står og ånder, arbejder, bærer tøj og jubler. Tøjet ser forskelligt ud: T-shirt, sweatshirt og refleksvest.
- **Mobil-UI:**
  - kompakt ordrekort (tryk for detaljer)
  - statuslinjer over maskinerne, uden overlap
  - skærm når en ordre er færdig, med præcis opgørelse
- **Udbetaling:** tallet på skærmen er præcis det, der lægges i pungen. Det er betaling + bonus − DTF-film og blæk.
- **7 opgraderinger:** alle har målbar effekt og ses på maskinerne.
- **Specialordre:** "Nibe Festival" – valgfri. Tiden tæller ned. Når tiden er gået, får man stadig grundprisen.
- **Produkter:** sweatshirts er spilbare fra niveau 3. Refleksveste er klar i data ("kommer snart").
- **Fremgang:** fejring når du stiger i niveau, visning af næste oplåsning og oversigt over alle oplåsninger.
- **Gem-format v2:** gamle gemte spil fra v0.1 virker stadig.

## Kom i gang på Mac'en
1. Unity Hub → **Add project from disk** → vælg mappen `printshop-tycoon`.
2. Første gang kører **Printshop Tycoon → Setup Project** automatisk. Sig **Yes**, hvis den beder om genstart.
3. Game-vinduet → **Simulator** → vælg en iPhone → tryk **▶ Play**.
4. Tests: **Window → General → Test Runner** → EditMode og PlayMode.

## Video, skærmbilleder og WebGL
- **Video (20–40 sek.):**
  - Klik **Printshop Tycoon → Record Gameplay Preview**. Første klik installerer Unity Recorder; klik igen bagefter.
  - Spillet kører en demo af ca. 40 sek., hvor Hue går rundt og viser de nye funktioner.
  - Resultat: `gameplay-preview.mp4` i projektmappen.
- **Skærmbilleder:** start Play, sæt Game-vinduet til 1080×1920, og klik **Printshop Tycoon → Capture Screenshot Tour**. Billederne havner i `Screenshots/`.
- **WebGL:** **Printshop Tycoon → Build WebGL** (kræver WebGL-modulet i Unity Hub). Resultat: `Builds/WebGL`.
- Video, skærmbilleder og builds bruger et midlertidigt gemt spil, så dit rigtige spil ikke røres.

## Tests uden Unity
Kræver .NET 8 SDK.
```bash
dotnet test Tools/CoreTests                       # 65 logik-tests
dotnet build Tools/CompileCheck                   # compile-check af Unity-koden
dotnet build Tools/CompileCheck -p:InputPath=New  # samme, med Input System
```

## Struktur
```text
Assets/PrintshopTycoon/
├── Scripts/Core/      spilregler i ren C# (kan testes uden Unity)
├── Scripts/Runtime/   Unity: verden, stationer, figurer, UI, kamera, effekter
├── Resources/         Hue/Silke-billeder (UI)
├── Editor/            opsætning, builds, skærmbilleder, video
└── Tests/             EditMode (regler) + PlayMode (hele forløbet)
Art/Reference/         officielle Hue & Silke-referencer
Tools/                 tests og compile-check uden Unity
ROADMAP.md             plan og oplåsning pr. niveau
```
- Alle tal (priser, tider, oplåsning) ligger i `Resources/PrintshopTycoon/GameBalance.asset`.
- Hue og Silke er enkle 3D-figurer i jeres stil. Billederne i menuer og guide er de officielle referencer.

## Kendte begrænsninger
- Ikke kørt i Unity – forvent små rettelser første gang (layout, kameravinkel, tekststørrelser).
- Medarbejdere går ad en fast midtergang (ingen NavMesh) og kan gå gennem maskiner.
- Ingen musik og ingen rigtige 3D-modeller endnu.
- Engelsk tekst er ikke gennemlæst.
- Ydelse på telefon er ikke målt.

Se `ROADMAP.md` for næste skridt.

---
**Ejer:** Thomas Reberholt · **Version:** v0.3 · **Sprog:** Dansk (engelsk forberedt)
