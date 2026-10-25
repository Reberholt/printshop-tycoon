# Printshop Tycoon på iPhone via TestFlight

> Målet: spillet installeres på din iPhone fra TestFlight, uden at du åbner Unity hver gang. Ét kommandolinje-script på Mac'en klarer build, arkiv og upload.

## Hvad projektet allerede gør (kode, klar i repoet)
- **Portræt, kun iPhone, iOS 15+**, IL2CPP/ARM64, status bar skjult, bundle-id `dk.reberholt.printshoptycoon` (`Editor/ProjectSetup.cs`).
- **Xcode-projektet rettes automatisk** efter hvert build (`Editor/IosPostProcessor.cs`): kun portræt, "ingen kryptering" besvaret (ellers spørger App Store Connect hver gang), bitcode fra, automatisk signering med dit team-id.
- **Touch:** virtuelt joystick + stor knap, multitouch, safe area (notch og hjemmebjælke), systembevægelser i bunden udskudt så joysticket ikke åbner Kontrolcenter.
- **Skalering:** UI'et skalerer efter skærmbredden (1080×1920 reference), så det passer fra iPhone SE til Pro Max.
- **Ydelse:** URP mobil-asset (ingen HDR, MSAA 2×, én skyggekaskade), 60 fps mål, vSync fra, statiske props samles i få draw calls, delte materialer, pool til effekter, ingen fysik-simulering.
- **Script:** `Tools/iOS/build_testflight.sh` – Unity (batch) → Xcode archive → export → upload til App Store Connect.

## Det kræver din Mac og din Apple-konto (kan ikke gøres herfra)

### Én gang
1. **Apple Developer Program** (99 USD/år) – nødvendigt for TestFlight. *Kræver din godkendelse og betaling – jeg køber ikke noget.*
2. **Xcode** fra App Store. Åbn det én gang, acceptér licensen, log ind under **Settings → Accounts** med dit Apple ID.
3. **Unity Hub → Installs → Unity 6 → Add modules → iOS Build Support** (hvis det mangler).
4. **App Store Connect** → **Apps → +** → Ny app:
   - Platform iOS, navn **Printshop Tycoon**, primært sprog dansk
   - Bundle ID: **dk.reberholt.printshoptycoon** (opret den først under *Certificates, Identifiers & Profiles → Identifiers*, hvis den ikke findes)
   - SKU: `printshop-tycoon`
5. Find dit **Team ID** (10 tegn) på developer.apple.com → Membership.

### Hver gang du vil have en ny version på telefonen
```bash
cd ~/printshop-tycoon
git pull
PT_APPLE_TEAM_ID=DIT_TEAM_ID Tools/iOS/build_testflight.sh
```
- Første gang spørger Xcode måske om adgang til nøglering – sig ja.
- Efter 5–15 minutter står buildet i **App Store Connect → TestFlight**.
- Tilføj dig selv under **Internal Testing** (ingen Apple-gennemgang nødvendig). Installér **TestFlight-appen** på iPhonen, og tryk *Install*.
- Byg uden upload: `… build_testflight.sh --no-upload` → `.ipa` i `Builds/ipa/`.

### Upload uden at logge ind hver gang (valgfrit)
App Store Connect → Users and Access → **Integrations → App Store Connect API** → ny nøgle (rolle *App Manager*). Gem `.p8`-filen og sæt:
```bash
export PT_ASC_KEY_ID=...  PT_ASC_ISSUER_ID=...  PT_ASC_KEY_PATH=~/keys/AuthKey_XXXX.p8
```

## Hurtigere alternativ uden TestFlight (til dig selv)
Med Xcode åbent: **Printshop Tycoon → Build iOS Xcode Project** i Unity, åbn `Builds/iOS/Unity-iPhone.xcodeproj`, sæt dit team under *Signing & Capabilities*, sæt iPhonen i kablet og tryk ▶. Det kræver kun et gratis Apple ID (appen udløber efter 7 dage).

## Status – ærligt
| | Status |
|---|---|
| iOS-indstillinger, post-processing, build-script | ✅ Skrevet · ❌ ikke kørt (ingen Mac/Unity her) |
| Spillets regler | ✅ 65 tests grønne |
| Unity-koden | ✅ Compiler (tjek uden Unity) · ❌ ikke kørt i Unity |
| Touch, skalering, ydelse på rigtig iPhone | ❌ Ikke testet – første build vil vise det |
| Kører på iPhone via TestFlight | ❌ Kræver punkterne ovenfor |

Forvent at første build i Unity/Xcode kan give små fejl, som skal rettes (typisk pakkeversioner eller signering). Send loggen (`Builds/unity-ios.log`) eller et skærmbillede, så retter jeg.
