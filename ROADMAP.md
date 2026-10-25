# Printshop Tycoon – roadmap

> Opdateret ved v0.3. Niveauer for oplåsning er **foreløbige** og ligger som data i `GameBalance.asset`, så de kan justeres uden kode.

## ✅ v0.1 – første spilbare udgave
- Værksted, Hue med joystick, to kamera-visninger.
- Første ordre (5 T-shirts), varmepresse-minigame.
- Køb og placering af maskine, én medarbejder, gem og hent.

## ✅ v0.3 – sjovere og flottere (denne version)
- **Fase 1 – grafik og UI:**
  - props og zoner i værkstedet
  - detaljerede, animerede maskiner
  - figurer med animation
  - nyt mobil-UI med kompakt ordrekort
  - skærm når en ordre er færdig, med præcis opgørelse
- **Fase 2 – produktion man kan se:**
  - DTF-print med slæde og film med logo
  - pressen lukker, gløder og damper
  - pakke-animation, konfetti og statuslinjer over maskinerne
- **Fase 3 – opgraderinger:** 7 opgraderinger med målbar effekt
  - presse-tid
  - timing
  - printer-tid
  - lagerplads
  - indkøbsstørrelse
  - medarbejder-tempo
  - flere småordrer
- **Fase 4 – specialordrer:** én fuldt spilbar tidsbegrænset ordre (Nibe Festival, 8 T-shirts, 4 min). Den er valgfri og har ingen hård straf.
- **Fase 5 – produkter:** produktkatalog. Sweatshirts er spilbare. Refleksveste er defineret, men "kommer snart".
- **Fase 6 – fremgang:**
  - niveau-op-fejring
  - visning af næste oplåsning
  - oversigt over alle oplåsninger
- **Fase 7 – tests:** 65 logik-tests plus nye PlayMode-tests. Gem-format v2 med migrering fra v1.

## Foreløbig oplåsning (data)
| Niveau | Låser op |
|---|---|
| 1 | T-shirts, første varmepresse, medarbejdere |
| 2 | Bedre timing, større lager, specialordre "Nibe Festival" |
| 3 | Sweatshirts, hurtigere presse, hurtigere printer |
| 4 | Større indkøb, hurtigere ansatte, flere småordrer |
| 5 | Refleksveste (kommer snart) |

> Prompten foreslog sweatshirts ved niveau 7–10. I prototypen er tallene trukket sammen, så man kan nå det hele på én spilletime. Ret i `GameBalance.asset`.

## ✅ Designregel (efter v0.3)
- Al Tex-Tryk-branding fjernet. Eget mærke (blækdråbe-badge), fiktive maskinmærker PRESSO/INKRA.
- Varmepressen er nu en rigtig 3D-model fra Blender, udskiftelig med producent-modeller (se `DESIGN_RULES.md`).

## ⏭️ Næste (v0.4)
1. **Køre og rette i Unity:** første rigtige kørsel, PlayMode-tests, skærmbilleder og WebGL-build.
2. **Test på telefon:** mellemklasse-Android, mål FPS, justér tekststørrelser.
3. **Refleksveste spilbare** (DTF-tryk på refleksveste).
4. **Flere specialordrer:** fodboldklub, firma-arbejdstøj, hastende sportstøj (kun data – systemet findes).
5. **Offline-indtjening** (gem-filen har allerede tidsstempel).
6. **Rigtige 3D-modeller af Hue og Silke** ud fra `Art/Reference/` (uden logoer) – og DTF-printer/pakkebord som Blender-modeller.

## Senere
- Hoodies, arbejdstøj, sportstøj, broderi, tasker, patches (produktkataloget er klar til det).
- Flere produktionslinjer, automatisk pakning, større værksted.
- Musik og flere lyde.
- Engelsk tekst gennemlæst.

## Bevidst fravalgt
- Ingen manipulerende betaling (ingen energi-systemer, ingen loot boxes, ingen "betal for at vente").
