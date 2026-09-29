# Issue #316 — Small delights: rolling numbers in AnimatedLabel

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/316
**Branch:** issue/316-rolling-numbers
**Status:** In Progress

Issuen har två halvor. Den här grenen tar **rullande siffror**. Typade action sheets med ikoner
(`ShowActionsAsync`) är native-arbete per plattform och görs för sig.

## Plan

`Mode="RollingNumber"` på `AnimatedLabel`: när texten ändras rullar de tecken som skiljer sig
lodrätt, som en vägmätare. Resten står still. Med Reduce Motion blir det en toning, samma som
kontrollens vanliga textbyte.

1. **`RollingNumber.cs`**, utan MAUI-typer. Den räknar ut vilka tecken som hör ihop, åt vilket håll
   de rullar och var de står, och ritar en bildruta med SkiaSharp. Eftersom den inte har några
   MAUI-typer kan den länkas in i ett `net10.0`-testprojekt, på samma sätt som `Svg.Tests` och
   `Core.Tests` gör.
   - **Parning:** gemensamt prefix utan siffror står kvar (`Score 9` → `Score 10`), och resten
     högerjusteras så att entalet står över entalet när talet växer med en siffra.
   - **Riktning:** talet växer → det gamla lämnar uppåt och det nya kommer underifrån. Minskar det
     går det åt andra hållet. Talet läses med appens kultur och annars invariant. Går inget av dem
     (`12:59`) jämförs siffrorna i ordning.
   - **Läge:** varje kolumn glider från sin x-position i den gamla texten till den i den nya, så
     att en siffra som tillkommer eller försvinner inte får resten att hoppa.
2. **`AnimatedLabel`:** `Mode` (`Marquee`, som är standard, och `RollingNumber`) och
   `RollDurationMs`. I `RollingNumber`-läget rullar texten inte i sidled (tal är korta). Rullningen
   drivs av den befintliga delade tickern, och när den är klar ritas samma förrenderade bild som i
   dag.
3. **Tester** i ett nytt `tests/Plugin.Maui.Spine.Controls.Tests` för parning och riktning. De
   körs i CI.
4. **Sample:** ett tredje exempel på Marquee-sidan, med ett tal som räknas upp och ned.
5. **Wiki:** `docs/wiki/animated-label.md`.

## Open Questions

- Hur det ser ut och känns (varaktighet, easing, klippningen) kan bara bedömas på en enhet. Det
  här gjordes i en Linux-container utan Mac eller emulator.

## Changes

- `RollingNumber.cs`: parning (`Pair`), riktning (`Increases`), easing, och ritningen av en bildruta.
  Äger sin `SKFont` och sin `SKPaint`.
- `AnimatedLabel`: `Mode` (`AnimatedLabelMode.Marquee` / `RollingNumber`) och `RollDurationMs` (350).
  Rullningen går på den delade tickern. En ändring mitt i en rullning börjar om från det senaste
  värdet. I rullningsläget blir det ingen marquee och inga kantfades. `CreateFont()` är utbruten ur
  `RebuildBuffer` så att rullningen och bilden ritar med samma font.
- `ReduceMotion.cs` i paketet, samma form som i Shimmer och Scanner. Med Reduce Motion på tas den
  vanliga toningen (eller ett direkt byte).
- `tests/Plugin.Maui.Spine.Controls.Tests`: 25 tester för parning, riktning och easing. Tillagda i
  `Spine.slnx`, `Spine.Packages.slnf` och CI.
- Samplet: exemplet "Numbers that roll" på Marquee-sidan, med ett poängtal (−1, +1, +95), en
  nedräkning från 0:15 och ett reglage för varaktigheten.
- Dokumentation: `docs/wiki/animated-label.md`, paketets README och csproj-beskrivning, och
  `spine-controls`-skillen.

**Verifierat (Linux-container):**
- `dotnet test` för Controls.Tests: 25 gröna.
- `AnimatedLabel.cs` med de nya filerna kompilerar utan varningar mot MAUI:s `net10.0`-referenser
  (Microsoft.Maui.Controls 10.0.50, SkiaSharp.Views.Maui.Controls 3.119.2).
- `RollingNumber` renderad till en bildremsa med SkiaSharp (OpenSans Semibold, 3×) för `99 → 100`,
  `12:59 → 13:00`, `0:10 → 0:09`, `Score 9 → Score 10` och `1 999 → 2 000`. Oförändrade tecken står
  still, nya siffror kommer från rätt håll, och sista rutan är den färdiga texten.

**Inte verifierat:**
- Plattformsbyggena för iOS, Android, Mac Catalyst och Windows. Android-workloaden gick att
  installera, men Android-SDK:n från dl.google.com nås inte från containern. CI bygger alla fyra.
- Samplets XAML är inte kompilerad.
- Hur det ser ut och känns på en enhet: 60 fps, varaktighet, easing, och övergången från sista
  ritade rutan till den förrenderade bilden.

## Decisions

- **`Mode` i stället för en `TextChangeAnimation`-egenskap.** Issuens skiss har `Mode="RollingNumber"`,
  och marquee och rullning går inte ihop: ett tal som rullar ska inte samtidigt glida i sidled.
- **Ingen marquee i rullningsläget.** Tal är korta. Text som ändå blir för bred klipps, och det
  står i wikin.
- **Glid in och ut, inte genom mellanliggande siffror.** `9 → 3` byter en siffra mot en annan i
  stället för att snurra förbi 0, 1 och 2, på samma sätt som SwiftUI:s `numericText`. Stora hopp
  (`+95`) blir annars långa snurr, och vid tätt uppdaterade värden (sträcktider) hinner ingenting
  läsas.
- **Ett gemensamt prefix räknas bara om det saknar siffror.** Annars behöll `10 → 1` ettan och
  tappade nollan, när det är tiotalet som ska försvinna.
- **Riktningen tas från talet, inte per siffra.** `9 → 10` ska rulla uppåt även i entalskolumnen,
  där 9 → 0 annars hade gått nedåt.
- **Inget nytt paket och ingen `Spine`-referens.** AnimatedLabel ska fortsätta fungera utan Spine,
  så Reduce Motion läses i paketet självt.
- **Testerna i ett nytt `Plugin.Maui.Spine.Controls.Tests`** och inte i `Core.Tests`, som testar
  `Plugin.Maui.Spine`. Fler kontrollpaket kan lägga sin MAUI-fria logik där.
