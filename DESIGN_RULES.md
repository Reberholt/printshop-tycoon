# Printshop Tycoon – designregler

## 1. Ingen Tex-Tryk-branding (fast regel)
- Spillet hedder **Printshop Tycoon** og har sin egen identitet.
- **Ingen** Tex-Tryk-logoer, skilte, T'er, tryk på tøj, maskiner eller emballage.
- Hue og Silke er stadig hovedfigurerne – men uden Tex-Tryk-logoer.
  - 3D-figurerne har en ren hue og et lille blæk-dråbe-mærke på smækken.
  - De officielle billeder er renset for logoerne automatisk (hue-T, smæk-T og "TEX-TRYK"-tekst).
  - 4 billeder, hvor Tex-Tryk-logoet er motivet (transfer, lup, tommestok, skærm), bruges ikke i spillet. Originalerne ligger stadig i `Art/Reference/`.
- Spillets eget mærke: **rund badge med en blækdråbe** (`Branding.Mark`).
- Farver: coral `#FF6B3D` (primær), teal `#2EC4B6` (sekundær), mørk `#23252B`.
- Kunder i ordrerne er fiktive (Nibe Bageri, Nibe Håndbold …). De må ikke blive til rigtige firmaer uden aftale.

## 2. Fiktive maskinmærker
| Maskine | Fiktivt mærke |
|---|---|
| Varmepresse | **PRESSO** |
| DTF-printer | **INKRA** |

Mærkerne står i `Branding.cs` og i Blender-scriptet. Skift dem ét sted.

## 3. Udskiftelige maskinmodeller
Maskinerne skal kunne erstattes af nøjagtige 3D-modeller af rigtige maskiner, hvis vi laver aftaler med producenter.

**Sådan skiftes en model:**
1. Læg en FBX i `Assets/PrintshopTycoon/Resources/PrintshopTycoon/Models/` med samme navn (fx `HeatPress.fbx`). Meter som enhed, origo i gulvet midt under maskinen, front mod −Z i Unity.
2. Navngiv de dele, spillet styrer:

| Maskine | Krævede dele | Bruges til |
|---|---|---|
| Varmepresse | `LidPivot` (tom, på hængslet), `HeatPad` (mesh), `StatusLamp` (mesh) | låget åbner/lukker om X-aksen, pladen gløder, lampen skifter farve |

3. Importindstillingerne sættes automatisk (`Editor/ModelImportSettings.cs`).
4. Mangler en del, falder spillet tilbage til den indbyggede model og skriver en advarsel i Console.

Fodaftryk (plads på gulvet) ligger i `GameBalance.asset` → `machines` og ændres ikke af modellen.

## 4. Blender-pipeline
- `Tools/Blender/heat_press.py` bygger varmepressen og eksporterer FBX:
  `blender -b -P Tools/Blender/heat_press.py -- export`
- `Tools/Blender/heat_press_render.py` renderer preview-videoen (Cycles):
  `blender -b -P Tools/Blender/heat_press.py -- render <mappe> 1080 1920 64`
- Nye maskiner laves på samme måde: ét script pr. maskine, samme navnekonvention.
