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

## M2 – Wapens, uitrusting en gevechten (à la BattleBit, maar dan filmischer)

- Echte vuurwapens in voxel-detail: pistolen, SMG's, geweren, shotguns, sniper, bogen, messen/steekwapens
- **Attachments**: dempers, scopes/red dots, handgrepen/grips, lasers, zaklampen, slings, grotere magazijnen
- Schieten met ballistiek, terugslag, mondingsvuur, tracers, inslagdecals, bloed, geluid dat vijanden aantrekt
- **Loadout**: plate carriers/rigs met vakken, rugzakken in maten, holsters, helmen
- **Kleding** per laag: sneakers, schoenen, laarzen, legerkistjes; jeans, cargobroeken, militaire uniformen,
  **hazmatpakken** en gasmaskers (nodig in straling), winterkleding; elk kledingstuk heeft opslag, warmte en bescherming
- Inventaris als in extraction shooters: rig/rugzak/zakken, gewicht, rotatie van voorwerpen

## M3 – Gevaar: raiders, mutanten en straling

- Raiders en bendes met dekking-AI, groepen, moraal, onderhandelen (uit het dossier)
- **Gemuteerde mensen en dieren**, gewelddadig, met eigen gedrag per soort
- **Stralingszones** rond inslagkraters en reactoren: geigerteller, dosis, pillen, hazmat verplicht
- Sloopbare gebouwen: explosies en instortingen van voxelconstructies

## M4 – Levende steden

- Nederzettingen met **NPC's die realtime hun werk doen**: boeren, sjouwers, bewakers, handelaars, koks, mechaniekers
- **Landbouw** met meerdere gewassen (aardappel, graan, maïs, kool, wortel, tomaat, …), zaaien, water, oogsten, seizoenen
- NPC's eten, slapen, verplaatsen voorraden, reageren op aanvallen; rekruteren voor je eigen nederzetting
- Handel en economie per stad (dossier S-07)

## M5 – Voertuigen en reizen

- Autowrakken met **motoren en onderdelen die je moet repareren** (accu, bougies, banden, brandstofpomp)
- Brandstof stoken en stroom opwekken (dossier S-06)
- **Boten**: rivieren, meren, de grote zee; **eilanden** en het Stille Eiland; **vissen**

## M6 – Wereld verdiepen

- Enorme biomen: nucleaire woestijn, as-bossen, verdronken kustvlakte, bevroren hoogland, giftig moeras
- **Grotten** en ondergrondse complexen, metro's onder capitals
- Nog veel meer loot: vrijwel elk meubel en elke kast is doorzoekbaar

## M7 – Presentatie op AAA-niveau

- Hoofdmenu met filmische achtergrond, laadschermen, instellingen (grafisch, audio, besturing)
- Ontworpen HUD en inventaris in UI Toolkit (vervangt de tijdelijke IMGUI-HUD)
- Geluid: ambience, voetstappen per ondergrond, wapens, muziek
- Referentie-onderzoek (foto's, games) voor straten, gebouwen en wapens voor een realistische, filmische look
