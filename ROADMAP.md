# Deadhaul – roadmap

Open-world post-apocalyptische survival-RPG in **Unity 6 + HDRP**, 3D-voxelwereld (voxels van 0,5 m),
raytracing op RTX / RX 6000+. De wereld is veranderd door een **nucleaire oorlog**: verstrahlde biomen,
gemuteerde mensen en dieren, en overlevers die opnieuw beginnen.

Werkafspraak uit het dossier blijft: **elke mijlpaal is speelbaar**, en er wordt niet verder gebouwd op iets dat nog niet goed voelt.

---

## ✅ M1 – Fundament (deze versie)

- Oneindige procedurele voxelwereld, deterministisch per seed; alleen jouw wijzigingen worden opgeslagen
- Terrein met heuvels, meren, bossen, dorre vlaktes; **snelwegen** met bruggen en autowrakken
- **Steden** op een raster (gehucht → capital): flats, huizen, winkels, apotheken, politiebureaus,
  benzinestations, parken, parkeerplaatsen, ruïnes; schade aan gebouwen, trappenhuizen, straatlantaarns
- Streaming van chunks op achtergrondthreads; hoek-AO in de mesh
- HDRP: fysieke lucht met sterren, volumetrische wolken en mist, HDRP-water, dag-nachtcyclus (24 min)
- **Raytracing** (reflecties, GI, AO, schaduwen) met terugval naar schermruimte-effecten
- Speler: lopen, sprinten, sluipen, springen, zwemmen, third- en first-person
- Slopen en bouwen, looten van kisten/rekken/tonnen per gebouwtype, crafting, zaklamp met batterijen
- Survival: gezondheid, honger, dorst, uithouding, warmte, bloeden, ziekte (vies water), kampvuren
- Opslaan en laden, pauzemenu, doodscherm

## ✅ M2 – Wapens, uitrusting en gevechten (à la BattleBit, maar dan filmischer)

- ✅ Vuurwapens in voxel-detail (1,5 cm-voxels): G19, MP5, AKM, M4A1, pompgeweer, jachtgeweer .308; melee (pijp, bijl, breekijzer)
- ✅ **Attachments** op montagepunten: 9mm- en geweerdemper, compensator, red dot, holo, 4×/8×-scope, verticale en hoekgreep,
  laser, wapenlamp, draagriem, vergrote magazijnen — met effect op terugslag, spreiding, geluid, richtsnelheid en vergroting
- ✅ Ballistiek met kogelval en penetratie (glas breekt, hout en autoplaat laten zware kalibers door), treffers per lichaamsdeel
- ✅ Terugslag, spreiding, richten, scope-overlay, herladen, vuurmodus, mondingsvuur met lichtflits, lichtsporen, hulzen, vonken, stof, bloed
- ✅ Geluid (gesynthetiseerd): schoten per kaliber, gedempt, herladen, inslagen, glas; schoten trekken vijanden aan
- ✅ **Kleding en uitrusting** per slot: sneakers, schoenen, wandelschoenen, legerkistjes; jeans, joggingbroek, cargobroek, legerbroek;
  t-shirt, hoodie, werkjas, legerjas, winterjas, **hazmatpak**; chest rig, kogelwerend vest, plate carrier; schoudertas,
  rugzak, legerrugzak; muts, pet, bouwhelm, gevechtshelm; bandana, **gasmasker** — met vakken, draaggewicht, warmte, pantser en stralingsbescherming, zichtbaar op je personage
- ✅ Wapenbank in de rugzak om attachments te monteren
- ✅ **Stille wapens**: recurveboog (spannen door vast te houden, loslaten om te schieten), kruisboog; pijlen vallen in een boog, blijven steken in hout en grond en zijn terug te rapen (E), ook uit lijken
- ✅ **Gevechtsmes en machete**, met **sluipaanvallen**: van achteren of ongezien doet een steek tot 8× schade
- Nog te doen: holsters, voorwerpen draaien in de inventaris, opgenomen geluiden

## ✅ M3 – Gevaar: raiders, mutanten en straling

- ✅ Raiders (aaseter, bendelid, scherpschutter) in groepen, met willekeurige uitrusting en wapens die ze bij hun dood laten vallen
- ✅ **Mutanten**: ghouls (snel, 's nachts actief, gloeiende ogen), brutes (2,7 m, 380 HP), mutantwolven in roedels; herten om te jagen
- ✅ AI met zicht (kijkhoek, afstand, dag/nacht, zaklamp verraadt je, sluipen helpt) en gehoor, onderzoeken, aanvallen op afstand of melee, vluchten
- ✅ **Atoomkraters** met stralingsniveau, as, kratermeren en gloeiende kristallen; geigerteller, jodium, hazmat en gasmasker beschermen
- ✅ Legerposten met zandzakken, tenten, wachttoren en munitiekisten
- ✅ Lijken doorzoeken, rauw vlees bakken bij het vuur, vacht voor kleding
- ✅ **Dekking zoeken**: schutters rennen bij herladen of na een treffer achter muren, auto's of heuvels en komen daarna weer tevoorschijn; aaseters dragen soms een boog en mikken hoger op afstand
- Nog te doen: onderhandelen, instortende gebouwen

## ✅ M4 – Levende nederzettingen

- ✅ Ommuurde nederzettingen van overlevers langs de snelweg (palissade, poort, wachttorens, huisjes met bedden,
  voorraadschuur, marktkraam, waterput, kookvuur met banken), elk met een eigen naam
- ✅ **Bewoners die realtime hun werk doen**: boeren oogsten en herplanten, de sjouwer brengt voorraad van de schuur naar de markt,
  bewakers lopen rondes (dag- en nachtdienst), de kok kookt, de handelaar staat achter zijn kraam
- ✅ Dagritme: samen ontbijten, lunchen en avondeten bij het vuur, 's avonds praten, 's nachts slapen in hun eigen bed
- ✅ **Landbouw** met zeven gewassen (aardappel, graan, maïs, kool, wortel, tomaat, pompoen) die echt groeien; zelf zaaien en oogsten
- ✅ Handel met doppen, praten met bewoners; val je iemand aan of steel je van de akker, dan keert het dorp zich tegen je
- ✅ Nieuwe recepten: brood, groentesoep, gepofte aardappel
- Nog te doen: rekruteren en je eigen nederzetting bouwen, seizoenen, economie tussen steden, mechaniekers

## ✅ M5 – Voertuigen, boten en vissen

- ✅ Redbare **auto's en pick-ups** op snelwegen, in straten en op parkeerplaatsen: repareer accu, bougies, banden en brandstofpomp,
  tank met jerrycans, en rij weg — met draaiende wielen, koplampen, motorgeluid, toeter, botsschade en aanrijdingen
- ✅ Onderdelen vind je in garages, benzinestations en door autowrakken te slopen
- ✅ **Roei- en motorboten** langs de oevers; varen over meren en kratermeren
- ✅ **Vissen** met hengel en aas (wormen uit de aarde): baars, karper, snoek — en gloeivis in stralingswater; bakken bij het vuur
- Nog te doen: brandstof stoken en stroom opwekken (dossier S-06), zeewaardige schepen, eilanden en het Stille Eiland, rivieren

## ✅ M6 – Wereld verdiepen

- ✅ Biomen: vlakte, bos, dorre vlakte, **nucleaire woestijn** met duinen en gebleekte botten, **moeras** met modder en riet,
  **besneeuwd hoogland** (kouder naarmate je hoger komt); as-bossen rond de kraters
- ✅ **Grotten** onder de heuvels met gloeizwammen en vergeten kisten
- ✅ Meer loot: kasten en koelkasten in huizen en flats, vuilnisbakken op straat
- ✅ **Sloopbare gebouwen**: handgranaten en explosieve vaten (ook door erop te schieten) slaan muren weg — hout sneller dan beton —
  met puin, vuurbal, rook, kettingreacties, schade en cameraschudden
- Nog te doen: metro's en bunkers, instortende constructies, verdronken kustvlakte

## M7 – Presentatie op AAA-niveau ✅ (eerste deel)

- ✅ Hoofdmenu met filmische achtergrond: de camera draait bij avondlicht rond de speler in de startstad (Doorgaan / Nieuw spel / Instellingen / Afsluiten)
- ✅ Laadscherm met voortgangsbalk en tips
- ✅ Instellingen (raytracing, zichtafstand, gezichtsveld, muisgevoeligheid, volume), bewaard in PlayerPrefs
- ✅ Nieuwe HUD-stijl met afgeronde, doorschijnende panelen, kompasbalk met plaatsen in de buurt, en een wereldkaart (M) met reliëf, wegen, gebouwen, akkers, straling, steden, nederzettingen en kraters

Nog te doen:
- Ontworpen HUD en inventaris in UI Toolkit (vervangt de IMGUI-HUD)
- Geluid: ambience, voetstappen per ondergrond, opgenomen wapengeluid, muziek
- Referentie-onderzoek (foto's, games) voor straten, gebouwen en wapens voor een realistische, filmische look
