# Deadhaul

Open-world post-apocalyptische survival-RPG in **Unity 6 (HDRP)** met een 3D-voxelwereld en **raytracing**.

- Wat er al in zit en wat er komt: [`ROADMAP.md`](ROADMAP.md)
- Oorspronkelijk ontwerpdossier (v0.3): [`archive/deadhaul/ontwerpdossier.html`](archive/deadhaul/ontwerpdossier.html)

## Openen en spelen

Nodig: **Unity 6** (6000.0 of nieuwer) via Unity Hub, Windows. Voor raytracing: een NVIDIA RTX- of AMD RX 6000+-kaart
(zonder zo'n kaart werkt het spel ook, dan met schermruimte-effecten).

1. Haal de repo op met GitHub Desktop (**File → Clone repository** → `yordivekshins-afk/Yordi`) en kies de branch
   `claude/focused-brown-o1nptd`.
2. Unity Hub → **Add → Add project from disk** → kies de map `Yordi`. Open het project
   (kies je geïnstalleerde Unity 6-versie als Hub erom vraagt).
3. Vraagt Unity om het nieuwe Input System aan te zetten of opnieuw op te starten: klik **Yes**.
4. Er verschijnt een vraag "Het project is nog niet ingesteld" → **Ja, instellen**
   (of later via het menu **Deadhaul → Project instellen**).
5. Het **HDRP Wizard**-venster gaat open: klik op het tabblad **HDRP** op **Fix All**, en op het tabblad
   **HDRP + DXR** ook op **Fix All**. Start Unity opnieuw als dat gevraagd wordt (DirectX 12).
6. Open de scène `Assets/Deadhaul/Scenes/Deadhaul` en druk op **Play**. Je komt in het hoofdmenu: kies **Doorgaan** of **Nieuw spel**.

### Besturing

| Toets | Actie |
|---|---|
| WASD, Shift, C, Spatie | lopen, sprinten, sluipen, springen/zwemmen |
| Muis | rondkijken |
| Linkermuis | schieten / slaan / slopen (vasthouden) |
| Rechtermuis | richten (met wapen) / blok bouwen (met bouwmateriaal) |
| R, B | herladen, vuurmodus |
| E | doorzoeken (kisten, lijken), plukken, drinken, praten (handelen, zwervers rekruteren, bevelen, onderhandelen), pijlen oprapen |
| 1–4 in een gesprek | keuze maken |
| Q | eten/drinken/verbinden, kleding aantrekken |
| 1–6 of scrollen | snelbalk |
| Tab | rugzak, uitrusting, wapenbank (rechtsklik op een wapen) en crafting |
| F | zaklamp |
| V | first-/third-person |
| F5 | opslaan |
| M | wereldkaart |
| Esc | pauzemenu en instellingen (raytracing, zichtafstand, gezichtsveld, muis, volume) |

## Structuur

| Map | Inhoud |
|---|---|
| `Assets/Deadhaul/Scripts/Core` | Pure C# zonder Unity: wereldgenerator, voxel-mesher, opslag, botsingen, items, survival |
| `Assets/Deadhaul/Scripts/Runtime` | Unity: chunk-streaming, HDRP-omgeving, speler, personage, HUD, opslaan |
| `Assets/Deadhaul/Scripts/Editor` | Menu **Deadhaul**: project instellen, save wissen |
| `Tests/CoreTests` | Tests voor de kern: `dotnet run --project Tests/CoreTests` (`-- map kaart.png` rendert een wereldkaart) |
| `Tests/UnityTypeCheck` | Compileert alle Unity-scripts zonder Unity: `dotnet build Tests/UnityTypeCheck` |

Grote bestanden (FBX, PNG, WAV, …) gaan via Git LFS (zie `.gitattributes`).
