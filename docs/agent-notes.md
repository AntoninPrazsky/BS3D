# Agent notes — sdílený deník

Sdílený deník pro AI agenty pracující na tomhle repu: provozní kronika toho, co se udělalo, co rozhodl majitel, co se ukázalo jako past a co zůstalo ležet, aby se dva agenti nepřeskočili. Nenahrazuje issues ani docs. **Před začátkem práce si přečti poslední zápisy; po dokončení práce přidej vlastní.**

Pravidla:
- **Cizí rozepsanou práci v working tree nikdo nedotýká.** Popiš ji tady a nech ji na rozhodnutí majitele.
- Dokončená práce jde **okamžitě na main** (`CLAUDE.md`): `git merge --no-ff` se zprávou `Merge branch '<větev>': <co dělá> (#NNN)` a větev se hned smaže.
- **Nadpis zápisu má pevný tvar, aby šel najít grepem (#594):** `## YYYY-MM-DD — #NNN <krátký název> — <stroj/agent>`, například `## 2026-09-26 — #598 hash šumu — desktop, Claude Code`. Víc issues: `#598 #599`; bez issue `#none`.
- **Zápis má nejvýš ~150 slov (#839, 2026-10-10)** a drží jen to, k čemu se deník používá:
  - co je hotové (issue, merge),
  - rozhodnutí majitele (`majitel: …`),
  - poučení (⚠) s příznakem,
  - co zůstává (`Otevřené: …`).

  Ověřovací příkazy, rozbory a výčty souborů patří do issue nebo commitu. **Poučení, které je návodem pro práci v určité oblasti, patří do jejího dokumentu v `docs/`**; sem stačí odkaz.
- **Převzetí issue se píše do issue (od 2026-10-03):** štítek `in-progress` a jeden komentář s tím, kdo (stroj a jméno sezení), co míní a kdy. Před začátkem pusť `gh issue list --label in-progress`. Podrobnosti jsou v `CLAUDE.md`, „How work is run“.
- Vizuální změny ověřuj screenshoty (`.claude/skills/screenshot`), ne jen buildem.
- **Deník se rotuje po měsících (#358).** Tady je vždycky jen **aktuální měsíc**; starší zápisy jdou do `docs/agent-notes-archive/YYYY-MM.md`. Rotaci dělá ten, kdo píše první zápis nového měsíce. **Hledá se přes obojí:** `grep -rn "#N" docs/agent-notes.md docs/agent-notes-archive/`.
- **Starší zápisy jsou od #839 zkrácené** na tvar výše. Plné znění všeho do 10. 10. 2026 je v gitu: `git show 0a3271c8:docs/agent-notes.md` pro říjen a `git show 0a3271c8:docs/agent-notes-archive/<měsíc>.md` pro archiv.

---

## 2026-10-30 — #none release v0.5.1 — desktop (RDT-PC), Claude Code (bs3d-10)
- Hotovo: v0.5.1 vydána (tag na 9738f584, run 41074849908, zip 402 MB, linux-arm64 384 MB). Zkušební běh na stejném commitu předem zelený. Stažený zip: SHA256SUMS OK, jedna složka (BS3D.exe + Content/Levels/Music/Sfx), v rohu menu `v0.5.1`, shadery `set 2f86e179` jako lokálně.
- Majitel: verze 0.5.1, obrázek nechal na mně (Caldera na Ultra), rychlost střely (#1007) zatím neřešit.
- ⚠ SSH z desktopu na Pi (port 22) dál končí timeoutem; `update-ceilings.sh` (starFloors z #1024) musí spustit majitel nebo Pi session.
- Otevřené: `update-ceilings.sh` na Pi, publikace webu (galerie #922, tři příspěvky blogu nejsou živě).

## 2026-10-30 — #1041 #988 #1006 #1012 #1040 benchmark před releasem, verdikty — desktop (RDT-PC), Claude Code (bs3d-10)
- Hotovo: #1041 benchmark běží jen s Online scores (Start bez nich: hláška + Settings, argument `benchmark` odmítnut `[bench] not started`, fronta `Benchmarks/` se při startu bez Online neposílá), zavřeno. #988: kanon z menu mimo celý běh (Pi +1,1 ms v trasách), roj klesá po polovinách, když 1024 neudrží (merge 8d6e542b, review c934dcc5).
- Majitel: Ano na kontrolu issues před releasem a tyto opravy. Verdikty #1040, #1006 a #1012: OK bez poznámky (30. 10.), zavřeno.
- ⚠ Skriptovaný benchmark (i na Pi) potřebuje v profilu `"online": true`.
- Otevřené: #1007 rychlost střely (rozhodnutí), poznámky k releasu, `update-ceilings.sh` po vydání (starFloors z #1024), kanon odložené hry v trasách benchmarku.

## 2026-10-28 — #1012 #1006 #1039 #1007 #988 #827 dávka poznámek majitele — desktop (RDT-PC), Claude Code (bs3d-83)
- Hotovo: #1012 nápověda na 3. minutí v řadě bez přesného míření, i opakovaně v levelu. #1006 zaostření později (4 s) a dál. #1039 menu v Neon City létalo 6 jednotek ve věžích (úzké okno), drží se v mýtině. #1007 `shotspeed=` na zkoušku. #988 finále „swarm“ (desktop 88 728 koulí při 60 FPS). #827 Top Players s pódiem.
- Majitel: #1023 a #875 OK, #1015 čas nikdy do skóre (zavřeno). #827 odparkováno.
- ⚠ Starý dial nápovědy umlčel nápovědu každému, kdo občas drží RMB, tedy i majiteli.
- ⚠ Stránka otevřená znovu přes menu nemá v `BeginSwarm` ještě `Manager`: na pozadí sahat přes `Game.Backdrop`.
- ⚠ Review (9d4923f4): náhled střely měl limit 0,5 s letu, takže pomalý `shotspeed=` ztrácel ghost. Teď je limit 100 jednotek.
- Hotovo i #1040 kanon v menu s průletem kolem něj (5c1d9512, stavěl agent ve worktree, záběry prošly kontrolou).
- Otevřené: #988 Pi segmenty, verdikty #1006/#1012 (hands), #827 a #1040 (eye). Větev `827-podium-mesh` zůstala lokálně (cherry-pick, strom shodný s main; `-d` ji odmítá).

## 2026-10-28 — #1038 sopka na Low: proudy a sloup kouře — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: pixel dál od proudu, než sahá jeho lem, proud přeskočí (všechny úrovně, žádný pixel se nemění). Ve hře Caldera 15,67 → 14,99 ms na `Low` a 31,94 → 29,81 na `Medium`, Paroxysm −0,73 a −2,16. `Low` kreslí polovinu sloupce kouře s dorovnanou hustotou a hrudky z hodnotového šumu (Caldera −0,16, změna vzhledu). `volcano=` má vrstvu `Steam`.
- ⚠ Herní kamera vidí kužel zblízka a sloup kouře je skoro celý nad snímkem. Pohled Testbedu `campos=0,-4,30` sloup přecenil desetkrát. Hledat úspory z herní pózy: `campos=0,-4,30.4 camtarget=0,4,0 fov=43`.
- Otevřené: `verdict-eye` (#1038, sloup na `Low`).

## 2026-10-28 — #1035 nálezy review — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: všech pět nálezů review `c1e13583` (komentář na #1035). Bloky moře a laguny se rozšiřují o boční výkyv vln (`SeaBackdrop.SwayReach`, test `SeaSwayReachTests` ho drží se `Sea.fx`). Měsíc bere blokovou mřížku, takže v cache už není druhá kopie 360/1200 a Měsíc se ořezává podle záběru (Colossus −0,13 ms na `Low` i `Medium`). `farring` jde kreslit samotný a `MeadowLayer` má zpět svůj summary.
- Šev vzdáleného prstence hor na `Medium` je na snímcích ze tří pohledů stejný jako před #1035.
- ⚠ Klíč cache obsahuje pořadí: kdo bere stejný pár v řádcích, drží druhou kopii.
- Otevřené: nic.

## 2026-10-28 — #1037 sopka na Medium — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: `Medium` kreslí `VolcanoMedium`, tedy autorský pixelový program svahu na geometrii z `Low` (hrubší mřížka a redukovaný vertex program). Ve hře Caldera 38,00 → 31,93 ms a Paroxysm 36,00 → 29,95, Colossus beze změny. `Low` a `High` jsou na snímcích stejné. `volcano=` má vrstvy `Planting` a `Country`.
- ⚠ #748 stál `Medium` 5,9 ms, třikrát víc než `Low`. Krajina nahradila oblohu, takže přibyly pixely terénu (8,8 ms škáluje s rozlišením, předtím 6,0), a vertex program běžel na plné mřížce. Ploché stínování krajiny ušetřilo jen 0,5 ms.
- ⚠ Vrstva `Plume` (sloup a pára fumarol) stojí 1,80 ms na `Low` a 3,38 na `Medium`. Doc dřív tvrdil, že nic.
- Otevřené: `verdict-eye` (#1037), rezerva sopky na `Low` (Caldera 16,00 ms) přes kouř.

## 2026-10-28 — #987 podsvícená klávesnice, verdikty 27. a 28. 10. — desktop (RDT-PC), Claude Code (bs3d-13)
- Hotovo: klávesnice svítí přes Windows `LampArray` (Dynamic Lighting), bez Logitech SDK, DLL a EULA (merge `355de3eb`, opravy z recenze `5696e5b8`).
  - Při hraní: barva nabité koule, Space naplno, fronta na F1–F4.
  - Karta tutoriálu: klávesy, o které žádá, blikají.
  - Výhra: duha, pak barva poháru. Prohra: červená.
  - Settings > CONTROLS > Keyboard lights. G513 hlásí 104 lamp a 33 ms.
- majitel: OK na všechno na obou stránkách verdiktů (#909–#916, #748, #1030, #1035, #922, #1034). U #917 vyhrálo popisné zadání 3 ze 4, Britney jen u sunny.
- ⚠ Windows pouští lampy jen oknu v popředí, takže běh bez fokusu nic nerozsvítí. `IsEnabled = false` klávesnici nevrací, vrací ji až uvolnění COM odkazu. `IsAvailable` je až v SDK 22000.
- Otevřené: `verdict-eye` u #987 (rozsvícení nikdo neviděl); #917, zda „sunny“ nahradit verzí B.

## 2026-10-28 — #922 galerie webu v pohybu — desktop (RDT-PC), Claude Code (bs3d-13)
- Hotovo: osm snímků galerie jsou okamžiky z benchmarkového dema z nastavených kamer, bez HUD, na Ultra v 2560×1440 (merge `15f1f7b1`). Nový `Tools/web-gallery.ps1` je bere podle `shot` v `gallery.json`. `web-screenshots.ps1` ve výchozím stavu bere jen Meander. Dva běhy daly všech 16 JPEG bajtově stejně.
- ⚠ Hra bere `fov=` užší, než by sedělo na svislé zorné pole: shluk visí od ostrova až k desce asi na y = 12. Kamery s cílem kolem y = 0 ho uřízly. Začni z jednoho širokého snímku zdálky.
- ⚠ `capturefrom` se zaokrouhluje nahoru na snímek (`ceil(F·fps)`), takže okno od 12,9 s při 15 fps začíná o půl snímku později. Okamžik se počítá z indexu, ne z popisku.
- ⚠ Na Vortexu demo nic pořádného neshodí.
- Otevřené: `verdict-eye` (karta na stránce 28. 10.), publikace webu je na majiteli.

## 2026-10-28 — #1035 mřížky terénu jen v záběru, hory na Low — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: `TerrainGridCache` drží mřížky i po blocích (16 čtverců) a `DrawSeenBlocks` kreslí jen bloky v záběru. Platí pro `TerrainPass` (10 scén) i sopku. Prstencová mřížka hor na High zůstala celá. Sonda `mountain=` ukázala, že terén hor stojí 5,48 ms, a `MountainReduced` má teď reliéf skály 1 oktávou místo 3. Spyglass na `Low` 17,32 → 15,44 ms, Belfry −1,95. Snímky 10 scén se liší jen tím, co se hýbe.
- ⚠ Balloon občas celý jeden běh ukáže o ~3 ms méně, na obou buildech. Minimum ze dvou běhů pak lže.
- Otevřené: #1035 `verdict-eye` (hladší skála hor na `Low`). Body 2 a 3 (sopka na `Medium`, rezerva sopky na `Low`) jdou dál.

## 2026-10-28 — #985 blog: loga studií, která nikdy neexistovala — desktop (RDT-PC), Claude Code (bs3d-13)
- Hotovo: článek `Web/src/blog/logos-of-studios-that-never-existed.md` (merge `94ff8d86`), devět obrázků a náhled. Publikace je na majiteli (`Web\publish.ps1`).
- Změřeno: Qwen3-VL-8B prošel všech 1891 souborů v `AI\sd\out`, 86 jeho ANO u renderů jsem prošel okem. 34 značek, všechny od kleinu (34 z 704), Z-Image 0 z 974. Všechny pochází ze zadání se slovem „game“ (34 ze 140), u fotografií 0 z 544. U „realistic 2026 graphics“ 7 z 8 (v rozích letopočty).
- ⚠ Qwen hlásí i to, oč se žádalo (loga, piktogramy), a nápisy na předmětech ve scéně. Každé ANO je potřeba zkontrolovat okem. Dva paralelní požadavky LM Studio nezrychlí, řadí je za sebe.
- ⚠ Headless Edge má minimální šířku okna. Šířka telefonu se ověřuje v iframu 360 px.
- Otevřené: nic. Čísla jsou na issue.

## 2026-10-28 — #400 #1036 #1012 #987 #751 #752 průchod 16, přezdívky, nápověda míření — desktop (RDT-PC), Claude Code (bs3d-13)
- Hotovo: tři čtenáři (Sonnet) prošli 15 merge bez vlastního review. Opraveno:
  - #1036: filtr přezdívek četl zakázané slovo přes mezeru mezi slovy („Jane Grey“ → „negr“). Opraveno ve hře `fb77325b` i v BS3D-API `a662387`. **Vydání a nasazení API zbývá.**
  - #1012: nápověda přesného míření se za tutoriálovými kapitolami neozbrojila hráči, který kartu nechal vypršet (`4c1f5884`).
  - Tři zastaralé komentáře (`edb20df6`).
- Ostatní:
  - #751 a #752 zavřeny: majitelovo OK z 25. 10. se na ně nepřepsalo.
  - #987: rešerše Logitech LED SDK 9.00 je na issue (licence, API, G513 neověřena).
- Otevřené: k #1006 je na issue podezření, že zaostřený pohled se kymácí v horní zarážce (pocit, `FollowAim` z `AimDirection`).

## 2026-10-28 — #1034 Online scores jako zaškrtávátko, seznam soukromí ve dvou sloupcích — desktop (RDT-PC), Claude Code (bs3d-13)
- Hotovo: řádek Online scores je zaškrtávátko (`CheckRow.ShowCallToAction`): vypnuté má prázdný červený box, bez serveru je zaškrtnuté s červeným „(no server)“. Seznam soukromí je dvojice popisek–text (`MenuPage.FactList`): popisky jsou písmem Anton ve velikosti řádků a zarovnané doprava, text vede do konce šířky stránky.
- About má kvůli tomu pátou stránku PRIVACY (druhou). Pod odstavcem o hře by seznam vytlačil Back z okna 1600x900.
- ⚠ Stránka ONLINE je nejvyšší. Mezera mezi položkami 24 nechala desku 7 px od kraje okna 1600x900, s mezerou 12 je to 20 px.
- Otevřené: verdikt oka (`verdict-eye`).

## 2026-10-28 — #1032 #1028 sopka a louka na Low — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo #1032: krok #748 na `Low` tvoří vertex shader terénu (`LandAt` a tři výšky na vrchol), ne pixely krajiny (plochá krajina ani zjednodušené detaily nepomohly). Mřížka se teď kreslí po blocích jen tam, kam kamera vidí. Caldera 16,34 → 16,00 ms (`Low`), na `Medium` −0,47. Snímky shodné.
- Hotovo #1028: v Testbedu číselník `meadow=`. Pole stojí 3,1 ms, vzdálený prstenec nic. majitel: „Trávu klidně můžeme obětovat. Také potok nemusí být na low tak detailní.“ `MeadowReduced` je teď bez reliéfu stébel a břehy potoka jsou bez textury. Amphora −0,71 ms, Diabolo −0,76, `High` beze změny.
- ⚠ `CloudSunlight` od #748 v programu rostlin louce ani savaně nic nestojí (+0,01).
- majitel: „Všechny scény vypadají na low dobře.“ #1029 zavřeno.
- Otevřené: Caldera na `Medium` 38,6 ms (v tabulce #1026 33,6), nerozloženo.

## 2026-10-27 — #1029 skála jeskyně na Low, oprava testu hudby — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: v Testbedu číselník `cavern=`. Na `Low` tvoří skalní plášť 10,69 ms ze 17,25. `RockReduced` má teď 3 oktávy reliéfu místo 5 a barevná pole o jedné oktávě. Spring na `Low` 16,80 → 15,31 ms (v0.4.0 15,74), Chest −1,51. Blízká skála přijde o nejjemnější zrno.
- Hotovo: po #915 byl main červený, protože `MusicDecodeHeadroomTests` počítal s přesahem nahrávky `mirage-musicbox`. Nový přípravek je `puppet-triphop` (478c6f8a).
- ⚠ Přegenerovaná nahrávka si nechá jméno, ale přesah ztratí. Test vázaný na jméno nahrávky se tím rozbije.
- Otevřené: #1029 `verdict-eye` (vzhled skály na `Low`). #1028 zbývá ~0,1 ms. Krajina sopky #748 na APU: Caldera na `Low` +0,61 (16,40 ms, znovu přes rozpočet), Paroxysm +1,01. Zapsáno v #748.

## 2026-10-27 — #909–#917 hudba šesti kapitol, hlasy, styl interpreta — desktop (RDT-PC), Claude Code (bs3d-8a)
- majitel: Přelud podle nápadu (a), dream pop na čistou kytaru. #912: Finale zůstává.
- Hotovo: 130 renderů ve třech kolech, 0 pádů. Přegenerováno 25 nahrávek (#909–#915) s novými bubnovými vrstvami. Trance zůstal, žádný kandidát nebyl lepší. Stránka verdiktů https://claude.ai/artifact/HPBD23MLMSRKjesaVd9usk (db `verdicts`, klíč je jméno skladby).
- ⚠ Taneční styly mají krátké mezery, dokud zadání nedrží akordy přes každý takt. Pomohlo zadání Clowns s „lush sustained supersaw chords … holding through every bar“ a gate/sidechain v negativu (`music-generation.md`).
- ⚠ Hlas: blok slabik místo `[Instrumental]` dá hlas spolehlivě, samotný popis ne vždy. `vocal-share.py` vokodér nevidí.
- Otevřené: verdikty z ucha. #917: slepý test na stránce, jména interpretů měření neodliší.

## 2026-10-27 — #1027 ořez vzdáleného lesa polární záře — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: v Testbedu nový číselník `aurora=` (vrstvy záře) pro `alt=`. Sonda na APU v nastavení `Low`: prstence vzdáleného lesa 2,54 + 2,50 ms, osázený les 2,27, obloha 2,05.
- Hotovo: prstence se kreslí jen v sektorech, kam kamera vidí (32 sektorů podle azimutu, kvádr na sektor, kreslení od `baseInstance`). Umbrella na `Low` 17,78 → 14,39 ms (v0.4.0 14,27), Fish 18,07 → 14,71, na `Medium` −3,05 / −3,37. Poušť beze změny. Snímky z pěti pohledů se liší jen vločkami.
- Otevřené: #1027 lze zavřít (Umbrella je pod 16,1). Zbývá #1029 (jeskyně) a ~0,1 ms louky (#1028).

## 2026-10-27 — #748 krajina za lávovým polem sopky — desktop (RDT-PC), Claude Code (bs3d-8a)
- majitel (27. 10.): druhé kolo AI referencí a podle nich krajina, ne až při velkém přepracování scény. Reference jsou v `C:\Users\panrd\AI\sd\out\748r2-zimage` a `748r2-klein`.
- Hotovo: lávové pole končí zubatou zvednutou frontou s ohořelým pásem. Za ním louky, políčka, les, řeka (skutečná voda), 22 domů a kostel s rozsvícenými okny, hřebeny a spící kužel. Tvary drží `VolcanoCountry.cs` pro shader, zrcadlo, sázení i testy. `Low` kreslí jen zem, `PotatoBackdrop` beze změny.
- Cena na desktopu: hra +0,13 až +0,23 ms, pohled na odvrácenou stranu +0,83 ms (High).
- ⚠ Les osvětlený plným sluncem a zamlžený k jasnému horizontu stál na tmavých svazích jako bledé kužely. Stromy proto mají vlastní barvu oparu a stín mraků (`PlantPass.DrawInWeather`).
- Otevřené: `verdict-eye`. Kulaté stromy u řeky zblízka čtou jako koule. Cena na APU nezměřena.

## 2026-10-27 — #1028 programy rostlin, louka na Low — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: `Acacia.fx` se kompiluje v pěti variantách. Kámen (#777), reliéf kůry (#980) a sláma (#971) jsou jen v programu bucketu, který je používá. Na APU na `Low` to dává louce −1,82 až −1,95 ms a savaně −1,41 až −1,54. Poušť, hory, sopka a záře 0. Snímky jsou na úrovni bloků shodné.
- Rozpad kroku louky: kámen +1,04, zářez potoka +0,97, kůra +0,66, okno 25. 10. večer +0,94 (nerozloženo), tok potoka −0,61. Amphora teď 16,22 ms (v0.4.0 15,18), tedy pořád o 0,1 ms nad rozpočtem.
- ⚠ Větev na uniformu ušetří výpočet, ne registry. Zapsáno v `BestPractices.md` §7.
- ⚠ Neměř, dokud se baterie nabíjí: rozptyl až 3,5 ms. `MeadowLow` (literál `NO_CUT`) zisk nepřinesl, zahozen.
- Otevřené: #1028 (zbývá ~0,1 ms), #1027, #1029.

## 2026-10-27 — #946 režim snímání ve hře — desktop (RDT-PC), Claude Code (bs3d-8a)
- majitel (25. 10.): snímky a smyčky pro web má točit hra sama (přístup A).
- Hotovo: `capture=<dir>` s pevným krokem herního času na snímek (`capturefps=`, `capturefrom=`, `captureseconds=`), bez HUD, s kamerou `campos=`/`camtarget=`/`fov=` nebo `campath=`, `capturedemo` (samostřílející dělo benchmarku). `Tools/web-capture.ps1` kóduje WebM, MP4 a plakát přes PyAV z venvu ComfyUI (ffmpeg na PATH není).
- Ověřeno: Meander s demem, dva běhy, 480 ze 480 snímků shodných. Hra bez `capture=` beze změny (`[alloc]` jako dřív).
- ⚠ Bepu na 24 vláknech není opakovatelný: dva stejné běhy se rozešly do 40 kroků. Snímání proto jede s fyzikou na jednom vlákně.
- Otevřené: jeskyně (Chest) se mezi běhy liší v desítkách pixelů, příčina nenalezena.

## 2026-10-27 — #957 blog o světelných motorkách — desktop (RDT-PC), Claude Code (Sonnet 5.5)
- Hotovo: `Web/src/blog/light-cycles-from-a-hash-and-a-strip.md`, osmý příspěvek: hash jízdy a pozice z hodin, zeď jako triangle strip, tělo jen ze švů, proč zdi bloomují. Tři snímky z Testbedu (`scene=grid sceneseed=0 shot=10`, `at=1:F12` schová překryv), náhled, váha v `docs/website.md`. Eleventy build projde, příspěvek je v seznamu, feedu i na úvodní straně.
- ⚠ Motorky se v Gridu nedrží Hilbertovy křivky, jen její mřížky (16 jednotek); osnova v issue s „stopou jako oknem indexů“ neplatila. Čísla v textu jsou z konstant: počty trojúhelníků a vrcholů, svítivost (Rec. 709) proti prahu 0,55; to, kdy zeď přestane bloomovat (36 až 63 jednotek), je aritmetika, ne měření ve snímku.
- Otevřené: cena motorek samotných nezměřena; publikuje majitel (`Web\publish.ps1`).

## 2026-10-27 — #1030 pořadí čipů klávesa – ikona – popisek — desktop (RDT-PC), Claude Code (Sonnet 5.5)
- Hotovo: #991 pověsil piktogram vlevo od sloupce kláves, řádek četl ikona – klávesa – text. `ChipColumnLayout` má třetí sloupec (`IconLeft`), `DrawChip` kreslí ikonu mezi klávesou a popiskem, šířka řádku se nemění. `ChipColumnLayoutTests` pinují pořadí a šířku (s rozbitou mezerou padají tři). Před/po na Pennantu 1920x1080, klávesnice i pad: pořadí sedí.
- Otevřené: `verdict-eye`, přijde na další stránku verdiktů; spuštění `BS3D.exe play level=Pennant powerups=swap:2,brake:1,cut:1`.

## 2026-10-27 — #none výchozí endpoint lokálního modelu je 8765 — desktop (RDT-PC), Claude Code (Sonnet 5.5)
- Hotovo: `Tools/SemanticSearch` a skripty skillů mají výchozí `http://localhost:8765/v1` (Base URL v Bionicu). Port 1234 a okolí Windows vyhrazuje (`netsh interface ipv4 show excludedportrange protocol=tcp`), server tam hlásí `EACCES`.
- ⚠ `lms status` a `lms ps` se dívají na vlastní port a hlásí `OFF`, i když server na 8765 běží; model se při prvním dotazu načte sám. Zkoušej `/api/v0/models`. Podrobnosti ve skillu `local-ai`.

## 2026-10-27 — #1026 tabulka APU po v0.5.0, alokace jeskyně — notebook (ThinkPad), Claude Code (github-4d)
- Hotovo: tabulka v `quality-measurements.md` přeměřena na APU, v0.5.0 vedle v0.4.0, `Low` i `Medium`, 15 levelů. Na `Low` jsou nově přes 16,1 ms polární záře (+3,76, #963), jeskyně (+2,29) a louka (+3,20, první kapitola). Navazují #1027 #1028 #1029. Na `Medium` záře +8,68 a louka +5,42.
- Hotovo: jeskyně alokovala 48 B na snímek. Lambda v `StartDetail` zachytávala parametr, closure tak vznikala před časným `return`. Teď 0 B (e5462816).
- majitel: zúžení na konkrétní merge jen v navazujících issues („Jen Medium, bez zúžení“).
- ⚠ Řazení po průchodech lže: APU po ~25 min zpomalí až o 50 % a tři kroky vyšly s opačným znaménkem. Proto `history-sweep.ps1 -ByLevel`.
- ⚠ Notebook zamrzl (Kernel-Power 41), když majitel odpojil nabíječku během měření s GPU naplno.
- Otevřené: #1027–#1029.

## 2026-10-27 — #1025 článek o poctivosti hráčů — Raspberry Pi (BS3DServer), Claude Code (rdt-9a)
- majitel: „Jinak až to bude hotové, myslím, že by téma poctivosti hráčů bylo dobré téma na článek na blog na webu.“ Pak: „Ano, napiš článek na blog.“
- Hotovo: příspěvek `two-and-a-half-hours-of-play` o skriptu z 25. 10. a o kontrolách z BS3D-API#16, #17 a #1024. Má dva grafy, které nakreslil skript z kopie databáze služby; žádná přezdívka ani adresa v něm není.
- Grafy mají verzi pro telefon (`<picture>`, `docs/website.md`), protože zmenšené by měly popisky kolem 7 px. Ověřeno buildem Eleventy na Pi při 320 až 1920 px.
- ⚠ Headless Chromium na Pi: snímek 1440×6200 od asi 4 090 px opakoval začátek stránky. Delší stránku snímej po částech, přes iframe posunutý nahoru.
- Otevřené: zveřejnění (`Web/publish.ps1`) je na majiteli. V hlavičce je datum 27. 10. 2026; při pozdějším zveřejnění ho přepiš.

## 2026-10-26 — #1024 hranice hvězd v tabulce stropů — Raspberry Pi (BS3DServer), Claude Code (rdt-9a)
- majitel: skript vydávající se za hru (25. 10.) obsadil první místa všech žebříčků. Rozhodnutí: „Ano, založ to issue“ a „Začni issues implementovat.“
- Hotovo: `StarRating.LeastScoreFor` a `ScoreSim --ceilings` píše ke každému řádku `starFloors` (2, 3 a 4 hvězdy). Nová brána STAR FLOORS hlídá, že hranice sedí přesně tam, kde se hodnocení láme. Strana služby je BS3D-API#16 (55327b6).
- Ověřeno: tabulka bez `starFloors` je shodná s tabulkou v0.5.0 (140 řádků). 96 skutečných výher na těchto deskách hranice splňuje, 133 výsledků skriptu by služba odmítla.
- ⚠ Přeladění `StarRating` nepotřebuje zvýšit `RulesVersion`. Služba bere hranice z kterékoli tabulky, která desku jmenuje (`docs/formats-and-tools/score-sim.md`).
- Otevřené: platí od příštího releasu hry, protože tabulku přikládá `release.yml`.

## 2026-10-26 — #1023 karta zaseknuté koule — desktop (RDT-PC), Claude Code
- majitel: kamaráda zmátly pixelovatě průhledné koule. Rozhodnutí: „Implementuj tu kartu tutoriálu, to bude všechno.“ Jiný vzhled dead weight (část 2 issue) se nedělá.
- Hotovo: karta „This ball is already shot down“ s ditherovanou koulí v barvě koule, která ji spustila. Učí se jednou a čeká za kartou, která už je nahoře. K tomu záznam „Shot down“ na záložce Odd balls v Helpu.
- Ověřeno naostro: úroveň se sondou (deska na čtyřech pilířích, nad ní skupina na bombě) a `detonate=22`. Skupina zůstane ležet, karta naskočí ~5 s po výbuchu v barvě skupiny. Recept je v `docs/game-feedback/dead-weight.md`.
- ⚠ Úroveň 110 nese power-upy: akční karta Swap stojí ve frontě až 22 s. Pro snímky `powerups=swap:0,brake:0,cut:0`.
- Otevřené: `verdict-newcomer`, jestli kartě rozumí nový hráč.

## 2026-10-25 — #1006 #1012 vydání v0.5.0 — desktop (RDT-PC), Claude Code (bs3d-21)
- majitel: „Dal jsem všude OK. Udělej release 0.5.0“. Verdikty v42 (#1022 #1018 #1019 #1017 #949 #942) zapsány a zavřeny.
- Hotovo: **v0.5.0 vydána** (tag na 80c498c0, Release run 40230514007). SHA256SUMS sedí, zip má jednu složku s BS3D.exe a Content/Levels/Music/Sfx, menu staženého buildu ukazuje v0.5.0. Poznámky doplněny o 25. 10. (9f032cc7): benchmark a co posílá, salva nepoužitých koulí, scény, úvody.
- Pi: služba odpovídá na `/v1/benchmarks` (schema 5), „KuloMrd“ už na žádné tabulce není.
- Hotovo po tagu: #1006 (ad56feb1) a #1012 (82d06768) sloučeny; af4235d3: zaostření čeká i na objektiv benchmarku a na salvu. Obojí `verdict-hands`.
- ⚠ SSH z desktopu na Pi pořád neprojde (port 22 timeout).
- Otevřené: `update-ceilings.sh` na Pi (nové tabulky Mirage) musí spustit majitel nebo Pi session.

## 2026-10-25 — večer: benchmark do releasu, poznámky majitele z testování — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo #988: živý panel (a1f8ea72). Výsledek jde po dokončení na server: `OnlineBenchmarks` v game (3b6285f2), BS3D-API `POST /v1/benchmarks` v0.1.28/0.1.29. Pi zatím na v0.1.27 (schema 4), čeká na majitele nebo Pi session.
- Hotovo #1018 (98f6206f, review e901c2cb): benchmark hraje Girandole, úvod města ke kanonu a pak kanon střílí sám. Míření jako ruka, cíl z `ShotPlacement`.
- Hotovo: #1019 louka nese kopuli 1, #1021 první úvod bez Skip, #1020 zakázaná slova v přezdívce (game 52ca3b9c a API v0.1.29).
- Hotovo pomocníky: #1017 krystaly, #949 dělo bez rámečku, #942 článek s před/po. #1022 lavička běží.
- ⚠ API CI: `10.0.x` → 409 při stahování SDK, připnuto `10.0.2xx`.
- ⚠ Klik do okna testovacího běhu ho zastaví: během hry benchmark myš neruší.
- Otevřené: „KuloMrd“ (vede 107 ze 150 tabulek) přejmenovat na Pi; 19 OK verdiktů zapsat.

## 2026-10-25 — #950 zvuk na webu, #948 web v pohybu — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #950 (7e212d43): tlačítko zvuku vpravo dole, do prvního stisku se nic nestahuje. Pak hraje smyčka z menu hry a výstřely živého děla. `verdict-eye`.
- Hotovo: #948 krok 1 (5e613c0e): úvod naskočí, kusy sekcí vyjíždějí při scrollu, čísla se načítají, dělo má viditelné „Pause“. Pod reduced motion nic. Když `motion.js` selže, hlavička po 3 s třídu `motion` sundá. `verdict-eye`; kroky 2–3 čekají na #946. Web nenahrán.
- ⚠ Oprava k #949: headless Edge animaci posouvá (rAF ~28/s přes DevTools protokol). Neposouvá ji jen virtuální čas `--virtual-time-budget`. Pauza se ověřuje počtem rAF, ne snímky plátna.
- ⚠ Majitel odpověděl 16:04–16:09 na 19 karet (vše OK), ale zápis a zavření issues klasifikátor hromadně odmítl. Čeká na jeho svolení.
- Otevřené: #922, #946 (rozhodnutí majitele), hudba #909–#917.

## 2026-10-25 — #881 salva nevyužitých kuliček, #949 dělo na webu — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #881 (0a1dddf3, oprava z review 8cc34ef5): po vyčištění pole dělo vystřílí nevyužité kuličky a bonus se načítá po kuličkách (`SpareVolley`, `GameplayScreen.Volley`). Kamera stojí vedle děla, výstřel jde přeskočit. `verdict-eye`.
- ⚠ Review našlo starší díru: odchod z pauzy během oslavy zahodil vyhraný level. Nově se zapíše (`RecordEnding`).
- Hotovo: #949 (3ab3e5dd): na úvodní stránce webu je živé dělo ve WebGL2 z meshů hry (`WindingCheck --export-cannon`). Nenahráno, web se nahrává jen přes `publish.ps1`. `verdict-eye`.
- ⚠ Headless Edge neposouvá animaci; zkouší se přes `?cannon-t=`.
- Otevřené: web #948, #950, #922; hudba #909–#917.

## 2026-10-25 — #751 jehličí, #870 Continue s úvodem, #988 opravy z review, alokace snímku — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #751 (67221435) jehličí na deskách blízkých smrků na High/Ultra (`Needles.fxh`), svítí se jako deska. Les i polární záře zlevnily: herní kamera 15,00 → 14,90 ms. `verdict-eye`.
- Hotovo: #870 (ede0dd6e) Continue z menu přehraje celý úvod scény i uprostřed kapitoly. Ne po Pauze, výběru, dalším levelu ani Retry. Argument `continue`. `verdict-hands`.
- Hotovo: #988 osm nálezů review (fe039397). Snímek zase nealokuje (f4780a66): bylo to 88 B/snímek, `BestPractices.md` §3.
- Sweepy bez pádu: 21 scén × 5 úrovní při startu, 21 levelů × 5 ve hře, 17 stránek × 4 okna.
- ⚠ `clip` v shaderu zakáže včasné zahození skrytých pixelů (stálo 1,0 ms); hřebínek se proto řeže v hloubkovém průchodu a stínuje se s testem EQUAL.
- ⚠ Na 4:3 zasahuje ucho poháru do tabulky výsledků (jen 4:3).
- Otevřené: #881 rozpracované (větev 881-spare-volley).

## 2026-10-25 — #988 fyzika a HDR sonda, pád Gridu (#956) — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #988 čtvrtá etapa (06901db0): `PhysicsBench` krokuje Girandole (1204 koulí) mimo obraz, krok má rozpočet 8,33 ms při každé obnovovací frekvenci; desktop medián 1,29 ms, 15,5 % každého snímku. Pátá (4d39f6c2): HDR cíl scény zkoumán na NaN a nekonečno.
- Hotovo: pád Gridu (488f05bc): v prvních 0,11 s hodin nemá žádný světelný motocykl stopu a `SetData` s počtem 0 hází výjimku; hra na Medium spadla při startu 2× ze 2. Žádné vydání ho nenese.
- ⚠ Benchmark na jiné úrovni než High našel pád, který všechna ověření na High minula. Pouštím sweep 21 scén × 5 úrovní.
- ⚠ `Texture2D.GetData` neumí `HdrBlendable` a `HalfVector4` ztrácí NaN (`docs/game-shell/benchmark.md`).
- Otevřené: #988 fyzika se střelbou, zátěžové finále, Pi segmenty.

## 2026-10-25 — #966 brouci a štíři v poušti, #988 doporučení a kontrolní snímek — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #966 (11366c98) 14 brouků a 6 štírů kolem ostrova (`DesertCritters`, `CritterMesh`, `Critter.fx`): vylezou z nory v obláčku písku, ujdou kus po výšce dun, zahrabou se; život je čistá funkce hodin (`DesertCritterTests`). Nový záběr úvodní túry „the scorpion“ (`SceneRenderer.CueDesertCritter` z `onStart`). `verdict-eye`.
- Hotovo: #988 druhá a třetí etapa (d42cf51a, 4abcdbb5): `BenchmarkVerdict` doporučí úroveň a nabídne ji, `FrameProbe` ve 3 s zkontroluje, že snímek scény není černý ani plochý.
- ⚠ Ocas štíra z koulí četl jako šňůra korálků; sytě písková barva krunýře splynula s pískem.
- ⚠ Časovač bez rezervy (`OVER_SLACK`) počítal snímky těsně nad rozpočtem jako přetečené.
- Otevřené: #988 fyzika zvlášť, Pi segmenty; fronta verdiktů je na 15, vzhledové změny stojí.

## 2026-10-25 — #988 benchmark, první etapa — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #988 (c59e4d06) Extras > Benchmark: prolétne úvodní túry Cavern, Neon City, Forest, Meadow na pevném seedu při hráčově úrovni, snímky volné (limit i čekání na kompozitor vypnuté, adaptivní sonda stojí), `FrameStats` (medián, p95, p99, 1 % low, nejhorší, podíl nad rozpočtem, první 1,5 s zvlášť), systémová zpráva, `[bench]` řádky, `Benchmark.json`, argument `benchmark`. Doc `docs/game-shell/benchmark.md`.
- ⚠ Vypnutý `FrameLimitHz` nestačí: `PaceFrame` čeká na kompozitor a drží 13,34 ms.
- ⚠ Stránka, která se změní, když stojí, potřebuje `InvalidateTree` + `Game.RebuildPage`.
- Otevřené: doporučená úroveň z čísel, kontrolní snímky, fyzika zvlášť, Pi segmenty.

## 2026-10-25 — #991 ikony power-upů, #989 kupy k obzoru — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #991 (d2080e5f) piktogramy u čipů Swap/Brake/Cut (`PowerupPictograms`, signed distance pečené ve velikosti písma, `PowerupPictogramTests`); `PowerupKind` má vlastní soubor kvůli testům. Reference `991-zimage`/`991-klein` kreslí stejné tvary. `verdict-eye`.
- Hotovo: #989 druhý krok (f38da57d) kupy až k obzoru na vlastní vzdálené rovině (vrstva nemá hloubku), vzdálené kupy 5 kroků, poloměr do půl buňky; hory (Broken) +1,08 ms.
- ⚠ Šipky Swapu přes koule splynuly v prstenec s dírou; ve 40 px musí být tvary oddělené.
- Otevřené: #991 karta výsledků, Help, výběr levelů (čeká na #990); #989 ostřejší okraje, Overcast/Storm.

## 2026-10-25 — #989 mraky jako objemy, oprava kamenů Marsu — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #989 první etapa (238d4a37): na High/Ultra je každá kupa u kamery objem (`CloudVolumes`, `CloudVolume.fx`) na mřížce driftující s vrstvou, stojí tam, kde má vrstva podle `CloudField.Cover` mrak; vrstva u kup ustoupí (`CloudVolumeCut`), Overcast/Storm zůstávají víkem. Pochod v rozlišení displeje do vlastní vrstvy, složené hned po obloze. +0,47 až +0,96 ms. Reference `989-zimage`/`989-klein`. `verdict-eye`.
- ⚠ Pochod do scénového cíle po zemi s hloubkou: Broken +3,2 ms. Bílé „žilky“ pod plochými dny byly nasvícené boky vzdálenějších kup: spodní třetina kupy musí šednout.
- ⚠ Měření běžící souběžně s generováním referencí (sd-server na GPU) nafoukla obě strany; měřit až po doběhnutí.
- Review po merge: kameny Marsu byly jen na High a hrubé (opraveno f7fcc33d, FracturedRockSet); objem ohně netestuje hloubku uvnitř krabice (poznámka na #886).
- Otevřené: kupy u obzoru (vzdálená rovina 2000), ostřejší okraje, Overcast/Storm.

## 2026-10-25 — #886 oheň jako objem — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #886 krok 3 (78d53123) na High/Ultra je oheň objem: krabice kolem ohně, pochod paprsku (12 vzorků jen uvnitř kužele jazyků), 3D turbulence zapečená do `Texture3D` 32³, emise nad absorpcí, pláty plazmy v nulách turbulence. Low/Medium/Potato mají billboardy. +0,30/+0,38 ms. `verdict-eye`, karta na stránce 25. 10.
- ⚠ První verze stála +2,0/+1,4 ms (celý válec, 20 kroků, ALU šum); kužel a zapečený šum ji srazily na pětinu. Skládání jako povrch (každý vzorek zakrývá) dělalo oranžový gel; emise s menší absorpcí nechá jádro prosvítat.
- Otevřené: pohyb neviděn, měkké okraje objemu, zrno na špičkách. Stránka verdiktů má 13,5 MB z 16.

## 2026-10-25 — #964 listí akácií podle referencí — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #964 (293721b6) změřen rozdíl proti referencím (tabulka na issue): list byl kapradina, koruna 3× světlejší a žlutá. Nový `TuftMask` (`LeafStrength` 4, dvakrát zpeřený list) pro akácie a trnité keře, tmavší zelenější `CanopyColor`/`CanopyDry`. +0,1 ms. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Hustší koruna (cover ×1,5) stála +1,6 ms pod korunou (overdraw kartiček u kamery); větší natočení kartiček +0,16 ms bez viditelného rozdílu. Obojí vynecháno.
- ⚠ Testbed se stejnými argumenty občas zabere jiný výřez (posun kamery); páry před/po kontrolovat.
- Otevřené: hvězdice větviček na konci větví, hustší rohož levněji, baobab má dál kapradiny.

## 2026-10-25 — #775 Mars jako z fotek roveru — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #775 (f53ad72a, 9fac92ed) kameny jsou geometrie (čedičový blok sopečky ve třech velikostech, v mýtině mimo stopu roveru, vrhají stín), štěrk v shaderu za `[branch]` na footprintu, tlumenější rez, světlejší měkké závěje, širší maska rovné země. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Tmavé ovály nebyly písek, ale kameny výškového pole: nízké hrboly bez obrysu. Teď mají barvu prachu (0,93 rzi).
- ⚠ Druhá oktáva závějí a štěrk bez branche stály ~0,3 ms bez viditelného rozdílu; po ořezu je cena stejná jako předtím (15,9 ms).
- Otevřené: kameny dál než mýtina (Mars nemá CPU kopii kráterů), úvod kapitoly neviděn.

## 2026-10-25 — #969 ropné plošiny na moři — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #969 (240d5a5d) dvě ropné plošiny za doběhem vln (`OilRigs`, `Rig.fx`): z kvádrů, žluté nohy s trubkovým vyztužením, paluba s moduly, červená věž, jeřáb, fléra, heliport; řez u hladiny `clip` a pruh pěny (hladina nezapisuje hloubku, #131). Reference v `969-zimage`/`969-klein`. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Příhradové panely s barvou oblohy mezi pruty vypadaly jako děrovaný plech zabarvený oblohou: vyztužení je z trubek, mezery věže tmavší ocel.
- Otevřené: lodě a maják, plamen fléry výraznější, Pi plošiny nekreslí.

## 2026-10-25 — #965 pyramidy v poušti — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #965 (c984b888) nad dunami stojí skupina z Gízy (`Pyramids`, `Pyramid.fx`): skutečné proporce i půdorys, rozestupy stažené do mřížky dun, Chufu 700 daleko za mrakem koulí; vrstvený vápenec, vypadlé bloky jako ve zdivu, obklad Rachefa, navátý písek; opar lehčí než u dun. Reference v `965-zimage`/`965-klein`. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Šum podle pevné osy (x + z) běží po jedné stěně a pruhuje ji: každá stěna vlastní vodorovnou osu. Diagonála mířící přímo od ostrova schová Rachefa za Chufu.
- Otevřené: úrovně pouště ve hře neviděny, Pi pyramidy nekreslí.

## 2026-10-25 — #956 světelné motorky v Gridu — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #956 (6f27982a) po podlaze Gridu jezdí tři motorky se zdmi světla (`LightCycles`, `LightCycle.fx`): pravoúhlé jízdy po mřížce 16 v prstenci 56–124, cesta z hashe, takže stejná při každé frekvenci; těla jen ze svítících hran. Reference v `956-zimage`/`956-klein`. `verdict-eye`, karta na stránce 25. 10.
- ⚠ `BlendState.Additive` je SourceAlpha/One: aditivní shader musí vracet alfu 1, jinak nepřidá nic.
- Otevřené: Pi motorky nekreslí; objektiv za prstencem by viděl zeď pod ostrovem.

## 2026-10-25 — #955 létající talíř ve Vesmíru — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #955 (c3d97589) kolem vesmírné stanice krouží chromový talíř (`SaucerFlight`, `SaucerMesh`, `Saucer.fx`): teplé žárovky, záře pod břichem, kopule s třemi šedými mimozemšťany a anténa s červeným světýlkem; 240 jednotek daleko, kolo za 70 s. Reference v `955-zimage`/`955-klein`. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Chrom, který zrcadlí jen tmavou sestavu světel scény, vypadá jako matný plast: chce studiový pás světla kolem horizontu. Tmavá posádka v tmavé kabině není vidět. Malá světla držet pod prahem glare (#401).
- Otevřené: pohyb mimozemšťanů (#718), Pi talíř nekreslí.

## 2026-10-25 — #752 #971 #980 verdikty 25. 10. a druhá kola — desktop (RDT-PC), Claude Code (bs3d-21)
- majitel (25. 10. ráno, stránka verdiktů): 21× OK, zavřeno (#744–#747 #749 #753 #754 #774 #776 #779 #903 #944 #951 #953 #959 #968 #978 #979 #981 #982 #984); Vrať: #752 (ostrov před tribunami v záběru Brána), #971 (plášť balíku bez pruhů), #980 (víc detailu kůry).
- Hotovo: #752 (7f254cfa) hlediště, stěny vchodu a obrubník zapisují hloubku; #971 (98a16042) plášť bez provázků; #980 (140c0841) dvě vrstvy brázd, bloky, zrno, lišejník (+0,25 ms s kmenem přes obraz). Druhá kola jsou na stránce, staré řádky v db smazané (odpovědi zůstaly na issues).
- ⚠ Python v bash heredocu s Windows cestou nebo textem obsahujícím zpětné lomítko a U spadne na unicode escape a commit odejde bez dokumentace: takové skripty psát přes Write.

## 2026-10-25 — #1013 #1014 #1015 #1016 upřesnění cizího hráče, paprsek za sklem — desktop (RDT-PC), Claude Code (bs3d-c9)
- Kamarád majitele odpověděl na otázky z #768: **nepoužil žádný nástroj** (RMB, Swap, Brake ani Cut), Cut si všiml až na konci, na 3 hvězdy měl asi 10 ze 140 levelů na první pokus a opakoval jen 2–3 levely. Zapsáno na #768, #1005, #1006, #1007, #692 (newcomer: ne), #990, #1012. Nová issues: #1014 (stejná barva se dotkne a nespojí; `owner-decision`), #1015 (čas ve skóre; `owner-decision`), #1016 (místo Brzdy něco, co shluk posune níž; `parked`).
- majitel: Swap má smysl kvůli násobiči, kamarád hrál offline. ⚠ Karta pro RMB ve v0.4.0 byla (Rainbow); akční karta po 22 s zmizí, takže ji lze přejít bez stisku.
- Hotovo: #1013 (fceedb3d) paprsek mířidla mizel na stropní desce: lámající sklo kreslí celé pixely z kopie pořízené před aditivními vrstvami. Zpod desky se sklo teď kreslí hned po koulích. `BS3DGame.CameraUnderCeiling`.

## 2026-10-25 — #976 opotřebovaný Velký stan — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #976 (26e40d5e) stan je starý a používaný: plachta má vybledlé panely, švy pruhů se šmouhami, skvrny od vody, plíseň u okraje a záplaty; sedadla, piliny, obrubník a sloupy jsou opotřebované. Jen High. Reference v `976-zimage`/`976-klein`. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Opotřebení plachty počítané na každý pixel stálo 1,3–1,8 ms (7680x3200 pixelů každý snímek pro vzor, který se nehýbe): je vypálené jednou do mapy v `BeforeShadowMaps`, střecha ji čte jedním `tex2Dlod` (+0,15 ms). Pod větví zásahu nejde implicitní gradient, proto explicitní úroveň mipmapy z footprintu.
- Otevřené: šněrování švů (jemnější než texel), praporky, hrazda a síť.

## 2026-10-25 — #954 vesmírná stanice — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #954 (dab19384) ostrov ve Vesmíru je paluba stanice: tvar `IslandShape.Station`, vlastní technika `InstancedModelTriplanarStation` (klínové pláty kolem límce, poklopy, výstražné oblouky, modrý pás v drážce okraje) a pět trysek se svítícími hrdly. Levnější než obrobený disk (8,08 proti 8,72 ms). Reference v `954-zimage`/`954-klein`. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Trysky nesmí sahat pod 6,5 pod vrch: film pádu tam drží kameru (`STONE_CLEARANCE`), proto jsou široké a nízké. Testbed bere knihovnu jen z vlastního buildu: po `Game.sln` je třeba sestavit i `Testbed.sln`, jinak snímek ukáže starý stav.
- Otevřené: měkký výtok světla pod tryskami (reference ho mají).

## 2026-10-25 — #779 ostrov Snění — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #779 (e46c607c) ostrov ve Snění je tající perla: tvar `IslandShape.Melt` (nafouklý val, z něj tlusté kapky, radiální pole `LatheMesh.radialField`) a vlastní technika `InstancedModelTriplanarDream`: pastelové pruhy z palety scény, duhový lesk, svítící prstenec, buben svítí sám. O 0,1 ms levnější než kámen. Pi kreslí tvar bez kapek a na fazetách kamene. `verdict-eye`, karta na stránce 25. 10.
- ⚠ Posouvat při kapce celé prstence dolů zubatě vrubovalo každý lalok stěny; dlouhá kapka, která se nezužuje, vypadá jako tyčka. Svislé plochy stíní polokoule všechny stejně: kapky vystoupí až proti ztmavenému výklenku. Spirála na 3D šumu stála +0,45 ms.

## 2026-10-25 — #953 bouře: laloky a déšť — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #953 (3d32bfa1, ec71e0b3) obláčky bouře mají okraj zvlněný do kulatých laloků se stínem v záhybech (květák místo vaty) a pod 42 % buněk visí clona deště (`StormRain` ve `StormClouds.fx`). Dohromady +0,7 až +0,9 ms. Karta na stránce 25. 10. je aktualizovaná.
- ⚠ 3D šum na rekonstruované kouli obláčku stál 3,5 ms (překryv obláčků): laloky jsou analytické. Prostý kosinus dělal z obláčků hvězdičky, `|cos|` poloviny úhlu dává kulaté koruny.
- Otevřené: scéna je pořád jasná, tmavá bouře potřebuje vlastní kupoli (rozhodnutí majitele).

## 2026-10-25 — #984 #752 úvody Jeskyně a Velkého stanu — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #984 (03b674e9) úvod Jeskyně začíná průletem z tunelu (`CavernChamber.Passage`), klouzání ke krystalům přilétá zpoza nich s ostrovem v pozadí; #752 (59f18adc) Velký stan má vlastní prolog: plachta, hlediště, brána (`CircusIntroShots`, test drží pózy ve stanu). Revize nočních merge: sáně stály 3,9 jednotky mimo tažnou osu (opačné znaménko rotace), opraveno; polární průlet nad trhlinami se vyhýbá táboru. Vše `verdict-eye`, karty na stránce 25. 10.
- ⚠ Stan nemá CPU geometrii: test drží kamery jen jeho čísly (stěna, kužel plachty, podlaha, sklon hlediště); viděno selhat se zlomenými konstantami.

## 2026-10-25 — #746 #745 #747 koleje, vlajky, polární tábor — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #746 (918247f5) koleje roveru na Měsíci (`RoverTracks.fxh/.cs`, decal v normále) a dvě bílé zmrzlé vlajky, ta vzdálená je vidět při hře; #745 (4d678711) stejné koleje na Marsu, test po bězích čtyř segmentů; #747 (a1387e0d) `PolarPlanting`: stanice, stožár, větrník, pásák se saněmi, sudy, závěje. Vše `verdict-eye`, karty na stránce 25. 10.
- ⚠ Jeden box kolem celé koleje poslal každý pixel pod ním přes všech 23 segmentů (Mars +0,43 ms); boxy po čtyřech to srazily na nulu. Trhliny polárního ledu pokrývají celou pláň za mýtinou: místo pro tábor se nenajde, led se kolem tábora čistí v shaderu i mirroru (`CampClear`).
- Otevřené: stopy bot kolem vlajky, rover na konci kolejí na Marsu.

## 2026-10-25 — #753 #968 #749 #776 tropická pláž a outback — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #753 (f427b4f6) keře na pláži jsou na High/Ultra mořské hrozny (`MeadowShrubMesh` s většími kartami, kulaté listy `LeafStrength` 3); #968 (4a1efd93) laguna u břehu průzračná a tyrkysová, krajka pěny, kaustiky, země laguny v `TropicalTerrain.fxh`; #749 (7f6e6a5b) `OutbackPlanting`: spinifex, saltbush, dřevo, mulga; #776 (23ebd9c7) popraskaná krusta v `Outback.fx` a gibber jako geometrie. Vše `verdict-eye`, karty na stránce 25. 10.
- ⚠ `PlantPass` kreslí každou instanci každý snímek dvakrát (scéna a mapa stínu): tisíce kusů chtějí LOD a levný `casterMesh`. Namalovaný kámen bez siluety vypadá zespodu jako díra. Hrubá síť moře s vlnami řezala břeh rovnými hranami (`SHORE_CALM_DEPTH`).
- Otevřené: mulga je zatím mladá akácie; ghost gumy a suchá koryta z referencí #749 chybí.

## 2026-10-25 — #903 #754 #980 #774 ryby, kořeny palem, kůra dubů, panorama města — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #903 (74188f8d) hejno plave asi délku těla za sekundu; #754 (9e4e5f02) palmy stojí v rohoži kořenů (`PalmFootMesh`, 40 trubic); #980 (7a8c1850) kůra dubů na High/Ultra s brázdami a reliéfem (+0,15 ms v záběru potoka); #774 (d234361d) dvě řady vzdálených budov kolem města uzavírají konce ulic (`City.FarSkyline`), kreslí je `CityRooftops` levnou technikou `InstancedCityFar`. Vše `verdict-eye`, karty na stránce 25. 10.
- ⚠ Cokoli přidaného do `CityPS` stojí všude (occupancy): panorama v něm stálo průlet ulicí v úvodu levelu 111 +0,45 ms, opar stejně. Vlastní program to srazil na nulu.
- Verdikty 24. 10. (#972 OK, #973 OK, #975 „Vrať“ s poznámkou „Nyní to stačí“) nikdo nepřepsal do issues; zapsány a zavřeny, poznámka k sopce je na #748.

## 2026-10-25 — #971 #944 #959 balíky, trávník, sníh v horách — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #971 (3cbec994) `StrawStrength` v Acacia.fx: spirála vrstev na konci balíku kolem volného jádra, provázky na plášti, balík 32 stran; #944 (8b77f12b) švy trsů (nulová množina šumu) pryč, trávník z měkkých skvrn, o 0,1–0,25 ms levnější, LatticeProbe 2 %; #959 (cd49d2f4) pod sněžnou čarou sníh na mírných dnech údolí (`LowlandSnowSlope` 0,15), i na Potato. Vše `verdict-eye`, karty na stránce 25. 10.
- ⚠ Polární UV se při 18hranu lineárně interpoluje přes sektor: poloměr na tětivě je nadsazený o 1/cos, spirála z něj vyjde mnohoúhelníková. A tón pásu podle indexu vrstvy skáče na švu u; podél pásu (vrstva + otočka) ne.
- Otevřené: #944 bohatší trávník zblízka jen pro High/Ultra (krok 3 issue) a reference, pokud majitel bude chtít.

## 2026-10-25 — #978 #982 #981 #951 mlhovina, logo, loga v About — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo: #978 (967eeb51) tyrkysová mlhovina je dvoubarevná (OIII jádro v červených vláknech) v měkké záři, podle referencí 978-zimage/klein; #982 (8bd38d44) disk odznaku se posouvá pod písmena podle paralaxy, yaw −0,27; #981 (ee1d6241, 9372ed17) Low/Medium kreslí logo z High bez aury (Low +0 ms, Medium +0,08), Potato kompozici loga přes instancovaný efekt; #951 (43747747) loga MonoGame a Bepu na Built on, mipmapy + anizotropní vzorkování. Vše `verdict-eye`, stránka https://claude.ai/artifact/3dzuLbU3uR3sd6MP5WwVh3 (db `verdicts`, ticho do 1. 11.).
- ⚠ Revize #981 našla: Potato disk kreslený po obrysech přemaloval jejich okraje (obrysy nepíšou hloubku). Disk patří hned za těla písmen, jako v High buildu.
- ⚠ CI na mainu ruší starší běhy novým pushem: zelený je až běh posledního merge.

## 2026-10-25 — #979 potůček teče z pramene do rybníka — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo (ec177f1f): změřeno `BrookWaterLevel` od ústí ven: rybník ležel 1,46 nad dnem údolí (posledních 30 jednotek teklo do kopce) a za hřebenem u d 206 potok stoupal o 16. Rybník je teď na dně údolí (`BROOK_START` 1,18), potok pramení pod hřebenem (`BROOK_CARVE_END` 190), malovaný potok zmizel. Vlnky jedou v souřadnicích toku, na strmém úseku rychlá vrstva s pěnou, čistá voda nad oblázky. Test `TheBrookFallsAllTheWayToThePond`, `mirrorcheck` PASS. `verdict-eye`.
- Cena (hra, High, 3840x1581): záběr potoka v úvodu nejhůř 13,10 ms (dřív 12,81), pod 13,33; kamínky nestojí nic, rozdíl je obsah záběru (strmý úsek).
- ⚠ 25. 10. se ve 3:00 posunul čas zpět: `quiet-capture` bere hodinu staré snímky za nové a ukončí běh hned. Do 3:00 nového času porovnávat jména souborů, ne čas (kopie skriptu ve scratchpadu).
- ⚠ Výsadba potoka bere z hlavního `rng` louky: posun potoka jednou přelosoval břeh rybníka, kameny u ostrova a trsy.

## 2026-10-25 — #744 Phobos je měsíc, ne díra — desktop (RDT-PC), Claude Code (bs3d-21)
- Hotovo (1e9a6838): oba měsíce jsou elipsoidy (dlouhá osa k Marsu), Stickney a krátery naklánějí normálu analyticky, Lommel-Seeliger + Marsshine, složení přes vzduch (`Transmission` 0,95, `Airlight` 0,62), odrazivost 0,40 místo 0,095. `verdict-eye`.
- ⚠ Tmavý měsíc na denní obloze bez vzduchu před ním je vždycky černá díra; vzduch (dome za ním se zčásti přičte) to řeší fyzikálně.

## 2026-10-24 — #1006 #1012 zaostření kamery a nápověda k přesnému míření — desktop (RDT-PC), Claude Code (bs3d-0b)
- majitel: kamera na začátku ukáže celek a po chvíli se zaostří na místo, kam se míří (smí se měnit FOV); po RMB nebo převzetí kamery znovu z celku. Pořád širší než RMB. K tomu nápověda, když střela mimo RMB nic neshodí; opakovat, když hráč RMB jen ťukne nebo ho dlouho nepoužije.
- Hotovo, ale **nesloučeno** (čeká na tag v0.5.0, dohoda s bs3d-80): větev `1006-overview-focus` (`OverviewFocus`: o 3 výš a o 4 blíž, FOV 0,88, ústí na 88 % obrazu) a `1012-precise-aim-hint` (`PreciseAimHint` + `Definition.Hint`, do save se nikdy nezapisuje).
- ⚠ Kanon z obrazu dostane hlavně zvednutí kamery, ne samotný zoom. Snímek `shot=` zastaví běh asi na 0,5 s, takže na měřeném úseku zkreslí časy v `logtilt`.
- Otevřené: sloučit obě větve po tagu, verdikt majitele (`verdict-hands`).

## 2026-10-24 — #1008 #1009 #973 příprava 0.5.0 — desktop (RDT-PC), Claude Code (bs3d-80)
- majitel (24. 10.): minutí nemá nic stát (#1009 zavřeno), při míření moc vysoko výstřel po hranici (#1008).
- Hotovo: #1008 (94fdc8de) střela leží na klampu, odmítnutí zmizelo; #973 (d740bf1b) Neon má vlastní úvod a švih se třemi rohy; opravy z revize #972 (bd793aee: náklon v menu, menu čeká na tour). Poznámky k 0.5.0 a snímek (Grotto) na mainu (f6335fd3). Verdikty #975 #972 #973: https://claude.ai/artifact/3pNjyHUKX2KR7Uqe4cQR7v.
- ⚠ Hra začíná s kanónem na ose (bearing 90): testy švihu s posunem 0,1 rad to schovaly, den našel švih jen pro 77 ze 108 póz na osách, Neon pro Arcade žádný. Teď alespoň čtvrtina kandidátů přijede příčnou ulicí.
- Otevřené: tag v0.5.0 až na pokyn majitele; při vydání `update-ceilings.sh` na Pi (Mirage).

## 2026-10-24 — #1011 kamera při pádu jen při opravdu velkém pádu — desktop (RDT-PC), Claude Code (bs3d-0b)
- majitel: kamera při pádu zůstane ve výchozím stavu zapnutá a v reálném čase, ale má se spustit jen při opravdu velkém uvolnění.
- Hotovo: `DropTrigger.MIN_SHARE_OF_LEVEL` jde z 9 % na 25 %. `LevelGen --drops`: 0,45 (greedy) a 0,40 (casual) kamer na level před čisticí střelou místo 1,11 a 0,98; čisticí střela si kameru nechává (#424). Test `OnlyAReallyBigReleaseTakesTheCamera` při 9 % viditelně selže.
- ⚠ Nejlepší důkaz byl `%LOCALAPPDATA%\BS3D\Logs\*.log` majitele: ze tří kamer uprostřed levelu dvě přeskočil do vteřiny (Umbrella 49 z 368, Pagoda 76 z 724). Ty novým prahem neprojdou, Fish (189 z 368) ano.
- Otevřené: `verdict-eye`.

## 2026-10-24 — #975 #972 úvody Erupce a Města — desktop (RDT-PC), Claude Code (bs3d-80)
- Hotovo #975 (bdb9d34b, 795ca590): sopka zdálky → erupce od úpatí → kráter. Výbuch je vyvolaný (`CueVolcanoBurst`), ne náhodný; záběr erupce ho při rozjezdu přeplánuje, když úvod zaostal (pauza, záškub). `shipped-awaiting-verdict`, `verdict-eye`.
- Hotovo #972 (2f5fb699): Město končí „švihem domů“ (`CitySwing`) ulicemi na herní pózu, bez cesty turné; náměstí výš (72–105). Neon zatím beze změny (#973).
- ⚠ Revize 1. verze #975 našla: výbuch na nástěnných hodinách, úvod na vlastních, takže pauza ho odpálila za stránkou pauzy. Cokoli v úvodu časované na událost scény musí počítat s tím, že hodiny úvodu stojí.
- ⚠ Pohled švihu podle tětivy k bodu vpředu se na rozích klopil 140 °/s; podle průměrovaného směru dráhy ne.
- Otevřené: #973 Neon na stejném `CitySwing` s vlastní trasou.

## 2026-10-24 — #1005–#1011 report cizího hráče (v0.4.0, všech 140 levelů) založen — desktop (RDT-PC), Claude Code (bs3d-0b)
- Hotovo: 7 issues `owner-decision`, nic nepostaveno: směr hry a obtížnost #1005, kanon zabírá půl obrazu #1006, střela bez fyziky #1007, odmítnutý výstřel nahoru #1008, minutí nic nestojí #1009, málo speciálních koulí #1010, výchozí stav kamery při pádu #1011. Komentáře: #768 (shrnutí, otázky zpět hráči), #936, #720, #290, #872.
- Změřeno: `LevelGen --clearfile=` nad všemi 140 soubory (jen čte): nejkratší vyčištění má medián 6 střel proti mediánu rozpočtu 48. Na snímku úvodního záběru (1920x1080) sahá kanon do 50 % výšky obrazu a je stejně velký jako shluk, nebo větší.
- ⚠ #1009 jde proti kartě z #936 („Wasting a ball is fine“), tak ať se o obou rozhoduje spolu. Nová výchozí hodnota v #1011 se dostane jen k novým hráčům, protože `Settings.json` ukládá i výchozí `dropCinematic`.
- Otevřené: všech 7 čeká na rozhodnutí majitele.

## 2026-10-24 — verdikty stránky 23. 10.: vše OK — desktop (RDT-PC), Claude Code (bs3d-80)
- majitel (24. 10.): všech 20 karet OK, vč. 2. kola nadpisů Helpu, lan stropu (#896), valů (#983), hrdla (#932), vlny (#940), úvodu louky (#945), #935/#937, #939, #868, #869. Zavřeno 15 issues s jeho odpovědí; otázka ke stínu na Měsíci zůstala bez odpovědi (případně nové issue).

## 2026-10-23 — #945 úvod louky: oblety dál a pomaleji — desktop (RDT-PC), Claude Code (bs3d-80)
- Hotovo (b52d8a2b): `ORBIT_AIR` 4,9, `FRAME_LIMIT` 0,78, `ORBIT_SECONDS` 5,6 → lavička 7,5 (6,3), balíky průměr 11,4 (10,2), 32°/s (39). Úvod louky 24,9 s, rozpočet testu zvednut na 25 s záměrně. Karta na stránce verdiktů; fronta verdiktů na stropu, další vzhledové změny až po odpovědích.
- ⚠ Hledání poloměru bere první vyhovující stanoviště: u lavičky ho určuje start (`ORBIT_AIR`), ne `FRAME_LIMIT` — poloměry měřit přes testy (24 rozmístění), ne odhadem.

## 2026-10-23 — #940 vlna: černá a hnědá — desktop (RDT-PC), Claude Code (bs3d-80)
- Hotovo (aedcd0fa), rozhodnuto místo majitele podle doporučení z měření: šedá figury černé podle plochy figury (`ColourCues.BlackFigureGrey`, průměr 0,0195, mez 0,08 až 0,15), hnědé zesílení 2,4. Na vlně (savana, obloha 14) čtverečky černé asi 2× jasnější. Karta na stránce verdiktů; fronta verdiktů 14.
- ⚠ Jedna konstantní šedá s rezervou pod limitem by širší figury ztmavila (vinyl): spodní mez je dosavadní 0,08, kterou test unesl ve všech sadách.

## 2026-10-23 — #932 koule v hrdle trychtýře zapadají do tmy — desktop (RDT-PC), Claude Code (bs3d-80)
- Hotovo (b3ad659e): v pevných scénách koule od jednotky pod ústím tmavne (jen barva) na 7 % o 10 jednotek níž (`ApplyKillPlaneFade`, `SceneRenderer.THROAT_*`); scény s otevřeným spodkem beze změny. Příčinou nebylo rozmazání pohybem. Karta na stránce verdiktů.
- ⚠ Skriptovaný výstřel na úrovni z `levelfile=` blok pokaždé neshodí (pod zátěží 9 FPS šel jinam); spolehlivý pád do odtoku dává Testbed `at=7:End` s pevnou kamerou nad hrdlem, před/po přes `git stash` a přestavbu.

## 2026-10-23 — #983 spoj ostrova se zemí: Les a Měsíc — desktop (RDT-PC), Claude Code (bs3d-80)
- Hotovo (b9b3f80f, 03cc9f92, opravy z revize 4e357245): val lesa se kreslí programem lesní půdy (`Forest.fx` `ForestBank` v `beforeGrid` `TerrainPass`), mech na bubnu (výškový pás), kapradiny na valu; Měsíc dostal návěj prachu (`Moon.fx` `MoonBank`) s prolnutím normály a kráterů k pláni. Reference v `C:\Users\panrd\AI\sd\out\983-zimage`/`983-klein`. Karta na stránce verdiktů.
- ⚠ Hladká normála valu na kráterové pláni kreslila okraj (bledý disk) — krátery jsou hlavně stínování; prolnutí podle hloubky zásypu ho smazalo.
- Doplněno (f6ce69ea): Mars a outback stejně (val jako pláň, prolnutí normály). ⚠ Outback techniku pláně nenastavuje a kreslí první technikou efektu — nová technika musí být v `Outback.fx` až za `technique Outback`, jinak pád na vertex deklaraci.
- Cena změřena (Testbed, 1600x900 ssaa 4, páry proti 2513465d přes `quiet-capture.ps1 -StdOut`): val jako zem stál +0,23/+0,82/+1,16 ms (les/Měsíc/Mars), lana a hrdlo +0,02. ⚠ Celou cenu dělalo dvojí stínování země pod valem, ne pořadí (to dalo 0,09): `BankCover.fxh` `UnderBank` odřízne terén uvnitř 2,4× rozptylu valu → +0,05/+0,51/+0,57 (67171adf).
- Otevřené: otázka na majitele, co na Měsíci „nesedí“ se stínem (komentář na #983); tropy a louka beze změny (val tam čte snesitelně).

## 2026-10-23 — #896 #996 #998 #999 #1000 #1001 #400 lana stropu, nadpisy Helpu, drift — desktop (RDT-PC), Claude Code (bs3d-80)
- Majitel na stránce (16:42): OK #1002 #1003 #1004 #867 #986 #997 → zavřeno. Vrať + „Malé nadpisy“ #996 #998–#1001 → čteno jako „nadpisy větší“, ne revert: `HelpPage.Headed`, nadpis na vlastním řádku v `MENU_FONT_BODY` (4d8cb8cf); 2. kolo na téže stránce, staré odpovědi v db smazány.
- #896 volba C: `CeilingRigging`, 4 lana k neviditelnému jeřábu a navijáky v rozích, buben `-y/CORE_RADIUS` (c31d5d61); opravy z revize (68dcd7ad): kreslí se podle strany skla, na které je kamera, a lano končí v klenbě Jeskyně a koruně Big Topu (`SceneRenderer.RoofAbove`).
- ⚠ Z herní kamery je rigging skoro mimo záběr; čte se z odletu kamery, menu a dálkových záběrů.
- #400 14. kolo (04410102); `Tools/quiet-capture.ps1` (a586b0a8): focení `shot=` bez krádeže fokusu.
- Otevřené: #935/#937, #939, #868, #869, #896 a 2. kolo Helpu čekají na stránce; #940 na volbu páky.

## 2026-10-23 — #996 #997 #998 #999 #1000 #1001 #1002 #867 #1003 #1004 #986 revize UI Help a High Scores — desktop (RDT-PC), Claude Code (bs3d-80)
- Hotovo (11 merge, CI větví zelená): Help má úvodní slova odstavců (`MenuPage.LeadIn`/`Strong`: FontStashSharp `/f[lead]`, `/f[strong]` přes `RichTextDefaults.FontResolver`), CONTROLS se vejde bez posouvání, SCORING bez aritmetiky („detached“ i na stránce výsledků), ODD BALLS bez popisu vzhledu, CAMPAIGN a THE GLASS přepsané; ořez posuvníků má pruh (`MENU_SCROLL_EDGE_GAP`), About/Settings/Help deska 94 %, období na High Scores jako vybraná záložka, ☆ místo „not finished“ v buňkách, obrys NEW BEST bez vyříznutých rohů.
- Stránka verdiktů: https://claude.ai/artifact/N6uqNLjGVFRJsv9SfHgEio, db `verdicts` (doc = číslo issue), mlčení 30. 10.
- ⚠ Myra ořezává obsah ScrollVieweru až na vnějším okraji marginu (`MenuScrollViewer.InternalRender` zužuje scissor). ⚠ `FontSystemEffect.Stroked` vynechává čtverec v každém vnějším rohu.
- Doplněno: #937 + #935 (skóre průhledné při čtení, černé číslo na světlé svatozáři, stín roste odmocninou; zkušební úroveň s černým blokem: `levelfile=` + `fire=` + `"tutorial": false` v scratch `Settings.json`); opravy z kontrolního agenta (pád měření na příkazu ze serveru, 1px okraje, pruh Jukeboxu). Stránka verdiktů má 12 karet.
- ⚠ `levelfile=` cestu piš s lomítky (`cygpath -m`): `\$1` v bashi zůstal jako text a hra tiše hrála skutečnou úroveň.
- Večer (majitel u klávesnice nebyl, „zpracovávej issues“): #939 (koule v hlavni otočená `BorePose.LOADED_TURN`, štěrbina ukazuje vzor; Cutter beze změny), #868 (soukromí jako seznam s úvodními slovy), #869 (DISPLAY → SCREEN + PICTURE, rozhodnuto: záložky, ne sloupce). Stránka verdiktů má 15 karet, fronta verdiktů je 15/15: žádná další změna vzhledu, dokud majitel neodpoví.
- #932 prozkoumáno (komentář na issue): koule přes okraj hrdla jsou vidět i bez rozmazání, spíš sklo trychtýře; #940 čeká na volbu páky (odstín hnědé jen pro wool vs. inkousty).
- Otevřené: CLEARED má tytéž zuby obrysu (3 jednotky, nevadí).

## 2026-10-22 — stav před vydáním: 4. kolo verdiktů, co zbývá — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo a na main (CI zelená): #936, #933, #952, #400 (13. kolo), #977, #961, #777, #941, #908, #962 (1. etapa), #967 (1. etapa).
- majitel (22. 10.): koupil kredity dvakrát; dokončit rozdělané, pak připravit vydání, nic nového nezačínat.
- Stránka verdiktů 4. kola (12 karet, účet panrdt): https://claude.ai/artifact/TZAF5xh2tZUVee4ZhfzmrE, db `verdicts`, odkaz na každém issue. Po odpovědích: Vrať opravit, odpovědi na issues, OK zavřít.
- Vydání: návrh v0.5.0, poznámky nadraftované (scratchpad bs3d-dc `release/v0.5.0.md`), screenshot Jeskyně; Mirage 133–140 má nové tabule → `update-ceilings.sh` na Pi po publikaci; BS3D-API se nemění (v0.1.26 stačí).
- ⚠ Tři Opus agenti narazili na týdenní limit (reset 25. 10. 22:00) 3,5 h po nákupu kreditů; resume přes SendMessage drží kontext. Nejvýš dva Opus agenti naráz.
- ⚠ #962 1. etapa stojí na 4K High +2,3 ms (12,6 → 14,9 ms, nad 13,33 ms stropu 75 Hz); úspory jsou až v 2. etapě — majitel rozhodne na stránce.
- Zbývá otevřené: #962 2. etapa, #967 (#776, #749), #751 jehličí, #922 web screenshoty (po vydání), rozhodnutí #896/#858/#838 a kde hráč potká Les.

## 2026-10-22 — #962 Les na High/Ultra, první etapa: hustší les, listnáče v listí, kapradí, stromy na kopcích — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: reference (Z-Image + klein, `out/962-*`); High/Ultra mají +400 stromů (80 % smrky), 160 semenáčků, souše, klády, kapradí, listnáče s listy (meadow karty) a vzdálený porost na kopcích (`ForestFarStand`). Low/Medium kreslí přesně totéž (test + měření). Výsadbu vlastní backdrop (`SceneRenderer.ForestPlanting`).
- Cena: High +2,3 ms, Ultra +2,6 ms (Testbed 3840×1600 ssaa 2, prázdné pole); největší položka jsou karty listů (pixely).
- ⚠ `alt=` varianta s `ssaa` ho nechá i dalším variantám; pinovat v každé. Full.json (1000 koulí) dělá Testbed CPU-bound (~5,5 ms) a scénu schová.
- ⚠ Jehličí jako karty na větvích vypadalo jako tmavé tečky (dva světelné modely); vyřazeno, patch v scratchpadu.
- Otevřené: jehličí, mlha, úspora ceny, tmavá podlaha; `verdict-eye`; kde hráč les potká (rozhodnutí majitele).

## 2026-10-22 — #967 Outback: obzor bez bledých „klobouků“ — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo (etapa 1, obzor): „klobouky“ změřeny – vzdálené útvary (151,137,138) proti pláni (128,59,42), červená pryč; dělal je opar, který do 480 j. došel do bledé mlhy, dozvuk k obloze *za* útvarem a hrubý vzdálený prstenec (lichoběžníky). Teď má každý útvar vlastní polární disk (instancovaný, ořez frustem) na stejném výškovém poli, pláň se na jeho elipse zastaví; bochníky s pilíři a ostrými žlaby, shluky kopulí (Kata Tjuta), stíny útvarů zapečené jednou na slunce na GPU, opar jako útlum vzdáleností k barvě obzoru, vlastní dome 21 (modrá nad teplým obzorem). Levnější na všech stupních (High 11,7 → 11,1 ms).
- ⚠ Testbed kreslí dome 21 (i 1 a 14) tyrkysově: barvu posuzuj ve hře. ⚠ Disky bez ořezu zpomalily Low o 0,4 ms.
- Otevřené: `verdict-eye`; #776 (zem) a #749 (vegetace) nezačaté, zbytek na #967.

## 2026-10-22 — #777 kameny na Měsíci na High a Ultra — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: na High/Ultra kreslí Měsíc i sopka místo dvacetistěnů s plochým stínováním `FracturedRockMesh` (rozlámaný blok z 9–12 rovin s otlučenými rohy, obroušené balvany) ze stejných instancí (`FracturedRockSet`, čtyři hustoty podle velikosti, stín z o stupeň hrubší sítě) a `Acacia.fx` je stínuje jako kámen (reliéf, skvrny, póry, vlasové praskliny, regolit u paty). Low/Medium beze změny.
- Cena (desktop, 3840x1600, ssaa 2): Měsíc +0,21 ms, sopka +0,08. Platí se trojúhelníky, ne pixely.
- ⚠ Normály zprůměrované přes trojúhelníky dělaly z bloků polštáře; berou se z plochy samé. Plocha natočená ke slunci je plochá při jakémkoli reliéfu, nese ji albedo.
- Otevřené: `verdict-eye`; nikdo to neviděl ve hře na High při běžném hraní.

## 2026-10-22 — #961 Les: tmavé nebe a světlušky, které se tváří jako světlušky — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: „něco bliká na zemi“ byly světlušky (naměřeno: tvrdý šestipixelový kotouč, vynořil se jako olivová koule, za třetinu sekundy bílý, zmizel); podlaha sama je klidná (pomalý drift stínů mraků, o 0,03 posunutá kamera mění ~1 % pixelů). Teď 12 světlušek, černé tělo svítí jen emisí, sin², záře 1–2 s, halo z bloomu (jas 10), pomalé bloudění. Les má vlastní dome 15 (`SceneDefault`) a počasí Overcast.
- ⚠ Dome samotná nebe nezatemní: pod Broken je střed horního nebe 90 (dome 13) a 89 (dome 15), jas dělá mraková vrstva; Overcast je 55. Dome 9 má hnědé kopce.
- Otevřené: `verdict-eye`; alternativa Broken + dome 15 je v captures (vrátit = `Weather = Broken`); pohyb světlušek posoudí majitel.

## 2026-10-22 — #941 červená síť končí na obrysu přes čtyři rohy — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: čáry už nepřesahují rohy a hrany (starý `FOOTPRINT_MARGIN` je táhl o 0,25–0,75 za nejkrajnější paprsek). Nový `LaserNetShape` (Prazsky.BS3D) drží tvar na jednom místě: rohy stojí tam, kam míří vysílače (`NetCorner`), mřížka je prohnutá jako čočkou (parabola přes rohy, `BOW` 0,08 poloosy uprostřed strany) a nejkrajnější paprsky jsou obrys, ostatní končí těsně na něm. Paprsek je stuha z 8 segmentů s tečnou v každé řadě, takže se pořád otáčí ke kameře; z kamery přesně v rovině sítě zůstává jasná čára. Rovina zůstává jedna. Shader (desktop i GamePi) má nový vrcholový formát; `GamePi/.../LaserGrid.xnb` přeložen `compile.ps1`. Testy `LaserNetShapeTests` (selžou na starém přesahu).
- majitel (2026-10-19): „parabolicky, nemusí být čtverec, ale pořád plochá v jedné rovině“.
- ⚠ Ve hře není kamera pin: pohledy shora a v rovině jsem dělal dočasným lokálním patchem (necommitnut).
- Otevřené: verdikt oka (`verdict-eye`) – míra prohnutí `BOW`.

## 2026-10-22 — #977 řádek nedohrané tabule: tónování a prázdná hvězda místo sloupce — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: slovo „not finished“ přestalo být sloupcem řádku (264 ze 780 jednotek desky, jménu zbývaly 3–4 znaky). Nedohraný pokus (0 hvězd) leží v zapuštěném pásu z #898 (`MENU_ROW_UNFINISHED_BRUSH`), jeho pořadí, jméno a skóre jsou o stupeň tmavší (vlastní řádek hráče zůstává purpurový) a ve sloupci poháru má prázdnou hvězdu ☆ – pohár je jen za clear, nedohraný pokus ho nemá ani na 1.–3. místě. Slova říká dál jen hráčova vlastní řádka pod tabulí. Jedno pravidlo v `BoardLayout.SignOf`; platí pro výsledek, plaketu v pickeru i `LevelBoardPage`. Při 1280x720 se „Nonexistent Pal“ vejde celé. Na 3840x1600 je deska teď široká jen tolik, kolik potřebuje nejdelší jméno (asi 1180 místo 1453 jednotek).
- ⚠ Myra `Widget` bez `Stretch` v obou osách nedostane žádnou velikost a pozadí nevykreslí (pás byl prvně neviditelný).
- Otevřené: verdikt oka (`verdicts\977\`), není zkoušeno na padu, v Potato ani na Pi.

## 2026-10-22 — #936 Amphora: cesta ven místo vítání — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: závěrečná karta žebříčku (klíč `throwaway`, dřív `graduated`, „That’s the basics“) učí „Fire a ball into empty space when stuck“ / „Wasting a ball is fine“. Podruhé se ukáže jednou za pokus o level, když svítí síť, zbývá výstřel a koule v hlavni nemá kam skórovat (`Tutorial.ThrowAwayIsDue`, `MatchReach`), nejdřív 8 s po první. `graduated` je `LegacyKey`: veterán kartu uvidí při dalším vstupu do Amphory a zapíše se mu `throwaway` (ověřeno během běhu). `Celebrates` zrušeno; Help má odstavec.
- Recenze: druhé ukázání se mohlo objevit i s vyčerpaným rozpočtem (v hlavni zůstává kulka); teď ne. Retry arming zůstává záměrně (nový pokus).
- ⚠ Druhé ukázání argumenty nenasnímáš (v živé hře koule bez cíle nenastane): snímek je s vynucenou mrtvou hlavní.
- Otevřené: `verdict-eye`; nikdo to nehrál jako nováček, časování druhého ukázání ve hře.

## 2026-10-22 — #933 síť se ukáže dřív, než varuje — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: dokud je vidět karta „Keep the cluster above the red line“ (Saturn), červená síť na podlaze bliká (0,75 s svítí z 1,2 s, `NetReveal`). Od téhle úrovně dál se na startu každé úrovně čtyři emitory otočí z klidové polohy (rovně nahoru) do míření (0,6 s) a síť se rozsvítí na ~1,4 s (celé odhalení 2,0 s, první výstřel ho dokončí hned). Není to alarm: `LaserGrid.SetPreview` je druhé světlo, které nevidí spoušť, karta `line`, zvuk ani HUD. Beze zvuku. `revealshot=` fotí hodiny odhalení a loguje otočení v číslech (sklon pouzdra 90 → 46,8° na 15 široké síti).
- Rozhodnutí: klidová poloha rovně nahoru; hodiny jsou herní čas (pauza je drží), ne zeď. První úroveň kapitoly odhaluje až po průletu.
- Otevřené: `verdict-eye` (vzhled otočení a klidové polohy, zda je síť při bliknutí vidět z hráčovy kamery); zvuk (hum) je na majiteli; `verdict-newcomer` (#768).

## 2026-10-20 — #970 potůček a rybník jsou jedna voda — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: v ústí vodu nestínuje to, která ze dvou sítí pixel kreslí, ale poloha: hloubka je hlubší z koryta a kráteru (koryto běží ~4,7 j. pod mělčinou), proud slábne poslední 3 j. potoka a nese se jazykem 7 j. do rybníka, stín rýhy (`GrooveSun`) mizí k břehu i na zemi. CPU kopie `MeadowCarve.WaterShading`; test spojitosti na příznaku padal skokem 1,0.
- ⚠ Dvě sítě na jednom břehu nemají šev, jen když každý vstup stínování je funkcí polohy, ne příznaku sítě.
- Otevřené: `verdict-eye` (vlnky v pohybu).

## 2026-10-20 — #958 jeskyně postavená znovu — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: celá síň je geometrie z CPU (`CavernChamber`): skořepina jako krychlová koule bez pólu a švu, dno jezera s ostrůvky (jemnější buňky kolem nich), záclony krápníků a stalagmity pod nimi, sloupy, 12 shluků průsvitných krystalů, 9000 světlušek jako pixelové sprity, dvě uzavřené šachty světla. Jezero zrcadlí síň. Staví se až při výběru scény. Levnější na všech stupních. #756 #757 #778 složeny sem.
- Majitel 20. 10.: „Cavern now looks good“ – vzhled schválen.
- Strom se světluškami byl sloup se sporami; natažené světlušky gaussovka na stěně z boku; „lom“ kruh, kde válec potkal víko.
- ⚠ NaN normála = černý osmiúhelník velikosti bloomu. ⚠ Hloubka síně se při ssaa 2 do scény píše zvlášť: skály a krystaly taky, jinak trychtýř prosvítá ostrůvkem.
- Otevřené: nic; Pi neměřeno.

## 2026-10-20 — #974 audit spár: každá procedurální síť je uzavřená, brána v CI — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: `WindingCheck --openness` pustí `MeshOpenness.ExposedLoops` přes 434 částí všech scén, tabulka `Allowed` s důvodem u každé výjimky, exit 1 na nepojmenovanou otevřenou síť; v `build.yml` brána + dvě samokontroly (podstrčená otevřená krabice). Zavřeno: kolena `SnagMesh` (jeden sweep), pata a špice dlaně, základy větviček louky (zapuštěné do větve), spára víka a těla lunárního ostrova, prstenec talíře, štěrbina vývrtu hlavně.
- ⚠ Před opravou 27 otevřených sítí; tabulka čeká na `#963`/`#960` (jehličí smrků) – kdo mění `ForestScatter*`, spustí `--openness`.
- Otevřené: spoje, které nejsou sítě (#970, #968, #959, #969, strop jeskyně v #958); `GroundSettling` mimo savanu neměřen.

## 2026-10-20 — #938 savana: dřevo je uzavřené, klády a skály leží na zemi — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: každý volný konec dřeva (větve, větvičky, kořeny, baobab, trnitý keř, doum) je disk, špička nebo kopule, do které další kus začíná; kloub je jeden `AddSweep`. `SavannaMeshClosureTests` přes `MeshOpenness.ExposedLoops`, staré stavby nalezeny (117–210 děr na strom). Kláda, skály a hliníky leží po svahu (`GroundSettling`; 36 z 60 klád mělo konec ve vzduchu).
- ⚠ Počet hran nerozliší prstenec zapuštěný v rodiči od díry; plocha pětibokého průřezu je na 0,81 poloměru, potomek začíná uvnitř.
- Otevřené: `verdict-eye`; `SnagMesh` má stejné spáry (#974).

## 2026-10-20 — #890 louka: volně stojící keře z kmínků a listů na High/Ultra — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: na High/Ultra je 55 volných keřů `MeadowShrubMesh` – 5–7 kmínků z jednoho místa v trávě, 13–17 trsů listových karet (spreje starých stromů), tmavý spodek, trsy trávy u paty, uvnitř bochníku, podle kterého se sázelo. Živé ploty a Low/Medium/Potato kreslí bochník beze změny (pixelově, census Potato shodný). Hra: +0,10/+0,12 ms ve hře, oblet balíků +0,27/+0,29.
- ⚠ Testbed `alt=finemeshes` tu měří artefakt (znaménko se mezi běhy otáčí); měř oddělené běhy obou buildů. Cenu nedělají trojúhelníky.
- Otevřené: `verdict-eye`, `verdicts\890\`.

## 2026-10-20 — #934 dlouhá přezdívka na tabuli — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: jméno na tabuli se ořízne „…“ na šířku svého sloupce (`BoardLayout`, sloupce mají pixelové šířky), řádek bez značky půjčuje sloupec značky. Na displeji širším než 16:9 deska roste o polovinu přebytku (3840x1600: 780 → 1453 jednotek). Reprodukováno proti loopback stand-inu, nikdy proti živé službě.
- ⚠ Na 16:9 a užším nechá „not finished“ jménu jen 3 až 4 znaky; značka jako tónování nebo druhý řádek je rozhodnutí o vzhledu.
- Otevřené: verdikt oka, `verdicts\934\`.

## 2026-10-20 — #943 štěrbina v kulatých balících a díry v rekvizitách louky — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: víka balíku stála na 0,98 poloměru prstence, kolem každého konce zela štěrbina. Konec je teď z vlastních vrcholů prstence, mírně promáčknutý a s vlastním polárním UV pro #971 (`BaleGeometry`). Kontrola uzavřenosti hran (`MeshOpenness`) našla i otevřené konce kolejnic plotu (zavřeny) a špičky větviček (zašpičatěny); lavice, keře, koruny a kmeny čisté.
- ⚠ Víko přes `AddCap` zvlášť nechá štěrbinu: `AddSweep(capStart/capEnd)` nebo kopie prstence. Doc: `scenes/meadow.md`.
- Otevřené: verdikt oka, `verdicts\943\`.

## 2026-10-19 — #887 ohně savany: víc než špičky — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: ohně stojí na návrších (zem pod nimi zvednutá na y -11,0, 2,5 pod deskou ostrova, `SeatBelowPlate`). Hrana ostrova skrývá z plamene nejvýš 15 % místo 33 až 49 % (viditelná výška nejméně 0,52 → 0,85, pixely plamene nad hranou 0,35 až 0,49 → 0,74 až 0,79). Posun kruhu ven nedává nic: zorný paprsek přes hranu klesá o 0,02 na jednotku (test `MovingTheRingOutBuysAlmostNothing`).
- ⚠ Světlo ostrova drží absolutní výšku: `LightHeightScale` 0,65 → 0,45, jinak by deska dostala o 55 % víc než v #885.
- Cena: kamera hráče +0,54 ms (10,65 → 11,19), shora +0,01; 0,4 ms z toho nevysvětleno. Potato beze změny.
- Otevřené: verdikt oka, `verdicts\887\`.

## 2026-10-19 — #924 cena louky po #892 — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: pixelově shodně levnější. Grid čte řez z vertex shaderu (flat interpolant) místo literálu `NO_CUT`: hra −0,5 ms (kratší program vyšel dražší), patch se ořezává frustem a na High/Ultra kreslí hloubku napřed u objektivu. High: hra 12,21 → 11,72, nejhorší vteřina průletu potokem 13,52 → 12,82 (rozpočet 13,33), Medium −0,1 až −0,3, Low −0,15 až 0.
- ⚠ Počet registrů cenu tohohle programu nepředpovídá (64 VGPR dražší než 96); měř při 3840×1600. Absolutní ms mezi sezeními driftují o 0,4.
- Otevřené: patch za pixel dražší než grid, příčina nenalezena.

## 2026-10-19 — #931 nestabilní OnlineStatsTests — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: chyba byla v testu, klient je v pořádku. `Stop()` jen žádá o konec, takže opakování zmrazené dávky mohlo dorazit na stand-in až po přečtení `sent` („expected 1, got 2“); a `File.ReadAllText` v `ReadOutbox` držel soubor proti `File.Replace`, zápis workeru selhal a test čekal 20 s. Opraveno `OneTry`, čekáním na worker a čtením s `FileShare.Delete`.
- ⚠ Při dvou jádrech padalo 11 z 42 běhů, po opravě 0 z 50.

## 2026-10-19 — #929 titulek v menu na High/Ultra jako PNG logo — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: na High/Ultra je nápis 3D verzí loga: barvy písmen z PNG, tlustší těsnější trubky, fialové obrysy, 3D na fialovém disku, studiový lesk podél tahů (`Wordmark.fx`). Low/Medium i Potato beze změny.
- ⚠ Ostrost brala hlavně aberace a zrno v rohu snímku: písmena jdou se zápornou alfou jako hotový obraz a resolve je klade bez křivky, zrna a aberace (`DisplayReferredLayer`).
- Cena +0,03 a +0,07 ms z 10,5 ms (Ultra, 4K); další páry zkreslil cizí Testbed na GPU.
- Otevřené: verdikt oka, snímky ve `verdicts\929\`.

## 2026-10-19 — #892 břehy potoka přirozeně rozložené — desktop (RDT-PC), Claude Code (bs3d-bf)
- Majitel (Vrať, 18. 10.): rybník dobrý, potok vypadá jako vykopaný bagrem. Hotovo: potok leží v širokém mělkém úvalu (`MeadowCarve.BrookSwale` = shader): štěrková mez, nízký hliněný stupínek, travnatý břeh 2–3,2 do vlastní výšky pole (do kopce širší), nízký lem. Úval 2,6–3,4× šířka vody; sklon břehu medián 0,47. Rybník beze změny, výsadba stojí kde stála (posazená, ne přesunutá).
- ⚠ Cena: konec průletu potokem +0,2 až +0,5 ms (13,48 > 13,33), pevná kamera +0,66: patch kreslí dvojnásob země u objektivu za svou přirážku (#924).
- Otevřené: verdikt oka, `verdicts\892b\`.

## 2026-10-17 — #892 potok a rybník vyříznuté do louky — desktop (RDT-PC), Claude Code (bs3d-48)
- Hotovo: potok je rýha s hliněnými stěnami, rybník nepravidelný kráter, voda je samostatná plocha uvnitř. `MeadowCarve` je CPU kopie shaderu (mirrorcheck 0,0021). Hrubá mřížka má díru a tu kreslí jemnější patch v souřadnicích potoka (`MeadowPatch`, CDLOD); Low a Medium řežou také, Potato ne.
- Cena (Game, Pennant, 3840×1581, High): hra +0,45 ms, průlet potokem na dnešním úvodu 10,9 → 12,2 ms, jeho poslední vteřina 12,8 (jednou 13,4). ⚠ Většinu ceny hry patch nezpůsobuje: i bez jeho země +0,3 ms (BrookLateral s atan2, společné tělo, vrcholy); Low/Medium na APU neměřeno.
- Review: trsy a rákosí drží od stěny šířkou vody + 0,9 (`OnBrookBank`); test drží konstanty shaderu na zrcadle (viděn selhat).
- Otevřené: verdikt oka (zoubky na hraně u hrubých úrovní patche, pohyb), snímky ve `verdicts\892\`.

## 2026-10-17 — #874 čtyři laserové vysílače na okraji nálevky — desktop (RDT-PC), Claude Code (bs3d-48)
- Čtyři malé kovové vysílače se skleněnou čočkou (hlaveň, žebrovaný límec, otočný třmen; materiál děla) stojí na zlatém pásu v rozích sítě; čočka má doutnající pohotovost a při síti svítí jejím jasem, od každé vede paprsek do rohu sítě. `LaserEmitterRing`, `LaserEmitters`, `LaserEmitterMesh`.
- ⚠ Dělo obíhá celý ostrov, na okraji pro vysílače není místo mimo jeho dosah: hlaveň jim jede nad hlavou, podvozek drží venku `Cannon.SetKeepouts` (jen chůze dovnitř do ~10° od vysílače). Test smetl skutečné pózy a našel, že kola sahají o desetinu dál, než se myslelo (`StandReach`).
- Otevřené: `verdict-eye` a ruka (podvozek obkluzem), karta a Help o vysílačích nejsou.

## 2026-10-17 — #878 kamera pádu pod plovoucím ostrovem jednou plynulou křivkou — desktop (RDT-PC), Claude Code (bs3d-48)
- Skok dělal přepínač podle výšky subjektu: nad horní plochou se čočka zvedala, pod ní ji strkal okraj; čočka s dosahem pod okrajem při přepnutí uskočila o 9 jednotek, pak se zřítila 90 j/s a zastavila. Teď poloměr sám drží dosah 34,5 od osy (`SoftMax`), ponor jede přes 12 jednotek poklesu a nejvýš 1,1 s (`Smoother`), postup je jedna monotónní kubika. Zrychlení 600–8800 → 120–470 j/s², pevné scény beze změny.
- `DropCinematicPathTests` (DropCinematic je v testech zkompilovaný). ⚠ Čočka je u menších hodů dál (až 43 j).
- Otevřené: oko v pohybu (`verdict-eye`).

## 2026-10-17 — #871 Swap: kuličky se protunelují a vlastní zvuk — desktop (RDT-PC), Claude Code (bs3d-48)
- Dvě kuličky v hlavni se prohodí posunem skrz sebe (`SwapMotion`, 0,35 s, komplementární dither nejsilnější při průchodu), rozmazání motion blurem a pruhem (`LaunchSmears.AddStreak`); zvuk `SwapSynth` (dva hlasy se křižují, 92,5 % v 300–4000 Hz). Logika `Magazine.SwapSlots` beze změny, výstřel uprostřed to ruší. Potato klouže a prolíná taky, bez motion blur. `swapshot=` fotí věk obrazu.
- ⚠ Oblouk do strany ≥ 0,1 prorazí ocel hlavně (vývrt 0,6, koule 0,5); proto 0,08. Bez zvukového zařízení nic neslyšel.
- Otevřené: oko a ucho majitele (`verdict-eye`).

## 2026-10-17 — #877 pád clusteru v běžném čase hry, bez zpomalení — desktop (RDT-PC), Claude Code (bs3d-48)
- `DropCinematic` už nemá `TimeScale`, roll zpomalení ani rampu; svět běží rychlostí 1, dokud kino drží kameru (`WorldTimeScale`). Cutterovo zpomalení (#767) zůstává; cut, který kino spustí, zpomalení nemá.
- Délka: dřív každý záběr skončil na stropu 6,5 s (svět stihl ~2,5 s), teď končí s pádem po 4,3 až 5,7 s. `Stalled` počítá snímek nejvýš 0,1 s (zasekl záběr po záchytu obrazovky). Řádek `[cinematic] ended …` hlásí důvod a nejpomalejší měřítko světa (1,00).
- Otevřené: oko majitele v pohybu (`verdict-eye`).

## 2026-10-17 — #880 finále Campaign Complete — desktop (RDT-PC), Claude Code (bs3d-48)
- Poslední clear kampaně dává před výsledkovou stránkou `CampaignFinalePage`: CAMPAIGN COMPLETE přes celou obrazovku (3,2 s), prolnutí do jedné náhodné zprávy z osmi (`CampaignFinaleCopy`: poděkování, „stay tuned“ a jedna detektivní nápověda na existující easter egg), pak výsledek. Stránka „stojí stranou“ (#861), skip jako flythrough, 1 s odklad proti náhodnému přeskočení (#347).
- Test hlídá pravidla textu, jednu nápovědu na zprávu a že každá míří na egg ze seznamu `Egg`. Replay posledního levelu finále nespustí.
- Otevřené: oko a ruka majitele (`verdict-eye`), skutečné dohrání kampaně.

## 2026-10-17 — #889 louka: větve vyrostlé z kmene — desktop (RDT-PC), Claude Code (bs3d-48)
- Hotovo (větev `889-meadow-fork`): dřevo lučních stromů je na všech tierech uzavřené z vlastních prstenců. Dřív kolenem každé větve prosvítala obloha. Low a Medium se proti mainu liší jen v těch dírách (168–255 px při 1600×900) a Potato vůbec. High a Ultra kreslí `MeadowTreeMesh.FineWood`: kmen se rozšíří do rozsochy tvarované podle větví a větve z ní vycházejí bokem. Silné i tenké větve rostou z rodiče zevnitř a kolena jsou uzavřené klouby. Cena fine dřeva v Game na Pennantu asi +0,05 ms (Testbed ukázal −0,03 až −0,06, to je řazení měření); Low a Medium platí záplaty, asi +500 trojúhelníků na strom, neměřeno. Hlídá to `MeadowForkTests`, každá kontrola viděna selhat.
- ⚠ Větev vedená podél povrchu kmene (jako u akácie) zaostala za rozšiřováním rozsochy a okraj kmene kolem ní stál jako lišta. Proto vychází s odklonem (`LIMB_SPLAY`). Normály se míchají podle vzdálenosti, a jen tam, kde obě plochy míří stejně.
- Otevřené: verdikt oka, snímky jsou v `verdicts\889\`.

## 2026-10-17 — #872 míření dolů bez poplachu — desktop (RDT-PC), Claude Code (bs3d-48)
- Spodní mez `MinElevation` už nezvedá `ElevationStrain` (žádný červený zaměřovač, zvuk ani rumble), nikdy neodmítne výstřel a guma se pouští kriticky tlumeně, bez zpětného švihu. Horní mez beze změny; `MinElevation` se nehýbe (majitel popsal stále měkký doraz). `CannonElevationTests`.
- Otevřené: pocit dojezdu (`verdict-hands`).

## 2026-10-17 — #923 žádné zvukové zařízení: jedna věta místo NullReference — desktop (RDT-PC), Claude Code (bs3d-48)
- `Audio/AudioDevice.Present` zkusí jednou vytvořit streamovací hlas; bez zařízení (monitor spí) řekne `[audio] no audio device …` a hudba, ambient, Jukebox i About přehrávač to přeskočí, nic se nedekóduje. Null byl v `SharpDX SourceVoice.CreateSourceVoice` (statické zařízení MonoGame je null).
- Efekty brány nepotřebují: bez zařízení jsou `SoundEffect` instance nečinné (běh se střelbou a výsledkem bez výjimky). Zařízení objevené později MonoGame nepoužije, pomůže restart.
- ⚠ Hlasitost ani cesta tvorby hlasů se v normálním běhu nemění, kromě jednoho zkušebního hlasu při startu; nesloucháno.

## 2026-10-17 — #851 #849 předvydávací kontrola v0.4.0 na notebooku — ThinkPad, Claude Code (fable-4f42)
- Release build mainu 188dda1b na čistém profilu (`userdata=`): front end, level 2 se střelbou, Potato (level 12), Cut v okně 1600×900 (level 42), všechny stránky menu, výsledek i prohra — bez výjimky, bez crash souboru, žádný ignorovaný argument; 1310 testů prošlo. Sonda jde ve fullscreen 1080p High → Medium → Low; Low drží v levelu 44–60 FPS, Potato 60.
- Tabulka tierů znovu změřena (Low 2×, Medium 1×): všechno o 4–20 % dráž, ale build tabulky e0c9a833 měřený tentýž den střídavě s mainem čte stejně → den, ne kód; zapsáno v `docs/game-shell/quality-measurements.md`. Colossus po #849: 13,83 / 21,23 ms.
- #851: při 125 % hlásí `[display] … scale 125 % (120 DPI)`, testovací poznámka (lokálně, neodeslána) nese resolution 1920x1080, scale 1.5, dpiAware true; komentář v issue.
- ⚠ `SystemParametersInfo(0x9F, n)` přepíná škálování relativně k doporučenému (0 = 125 % tady, −1 jako 0xFFFFFFFF = 100 %); krok +1 skočil na 150 %.
- ⚠ Po 30 min souvislého měření čte APU o 1–2 ms víc; minimum ze dvou pasáží drží.
- Otevřené: tag v0.4.0 (čeká na odpovědi majitele na stránce verdiktů).

## 2026-10-16 — #781 hory na High a Ultra — desktop (RDT-PC), Claude Code (bs3d-ac)
- majitel (2026-10-15): odparkováno do příštího vydání (hory, kapitola 4).
- Hotovo (větev `781-mountain-detail`): High a Ultra kreslí `MountainFine` na dvakrát jemnější mřížce. Pole má šestou oktávu vycentrovanou na střed hřebene a hroty na svazích. Skála se stínuje ve 3D: reliéf, ukloněné vrstvy s římsami, na které si sníh lehne sám, a laminy. Low, Medium a Potato zůstávají beze změny. Program je o 3,3 ms levnější než dřív (Game, Belfry, 4K, High 14,73 → 11,31 ms).
- ⚠ Detail omezovala hlavně projekce na XZ: na stěnách se všechno táhlo po spádnici. Z-Image img2img při síle 0,5 drží hladkost hry, frekvence se srovnávají s kleinem.
- ⚠ `clip()` vypíná hloubkový test před pixel shaderem. Díra pro ostrov je proto v geometrii. Low a Medium dál platí ~3 ms overdraw.
- Otevřené: verdikt oka, snímky jsou v `verdicts/781`.

## 2026-10-16 — #918 obrys poznámky o přeskočení — desktop (RDT-PC), Claude Code (bs3d-ac)
- Nalezeno předvydávacím smoke testem razítkovaného Release buildu `9a32bd3d` (14 prvních levelů kapitol, front end v 1080p a 720p, Potato, prohra a výhra): žádná výjimka, chybějící soubor ani pád.
- #918 (merge do mainu, zavřeno jako objektivní): `Skipping spends this chapter’s one skip` na stránce prohry byl jediný řádek bez obrysu (`Label` místo `ShadowedLabel`), nad mraky louky skoro zmizel. Teď nese obrys jako ostatní řádky (#465).
- ⚠ `fire=` střílí jen v aktivním okně (`Game.IsActive`), takže při spuštění bez fokusu někdy nevystřelí nic; `walk=`/`turn=` fokus nepotřebují.

## 2026-10-15 — #758 akácie: vidlice vyrostlá, ne nasazená — desktop (RDT-PC), Claude Code (bs3d-ac)
- majitel (2026-10-15): odparkováno do příštího vydání (savana, kapitola 2).
- Hotovo (větev `758-acacia-fork`): High a Ultra kreslí `AcaciaMesh.FineWood`. Kmen se ke vidlici rozšíří do tvaru kořenových kruhů větví, ze sedel vedou rýhy dolů a vršek uzavře nízká kopule. Každá větev roste zevnitř kmene podél jeho povrchu po Bézierově křivce do stejných konců, takže listí visí na obojím. Low, Medium a Potato mají dřevo beze změny (`SceneRenderer.FineMeshes`, Testbed `finemeshes=`). +0,06 ms na High i Ultra. Hlídá to `AcaciaForkTests` (viděn selhat na hrubém dřevě).
- ⚠ Kůra podél větví vyrobila šev přesně ve vidlici, proto zůstává ve světových souřadnicích. Normály kořenových prstenců větví se musí převzít od kmene, jinak je větev o desetinu světlejší.
- Otevřené: verdikt oka. Snímky před a po jdou na koordinátorovu verdiktovou stránku, spouštěcí příkazy jsou v komentáři #758.

## 2026-10-15 — #709 statistiky hry po levelech — desktop (RDT-PC), Claude Code (bs3d-ac)
- majitel (2026-10-15): ano, v příštím vydání, posílat jen hráčům se zapnutým online skóre; bez vlastního přepínače, souhlasem je přezdívka (#763).
- Hotovo (merge do mainu v BS3D i BS3D-API, API neotagované a nenasazené): počty v `Stats.json` (`PlayStatsRecorder`), `ScoreSim --stats`, dávky na `POST /v1/stats` bez tokenu, id a verze, na vlastním časovači a spojení, 2–8 min po každém požadavku se jménem hráče; vypnutí skóre frontu zahodí; `startsToFirstClear` zůstává na stroji. Server: `play_stats` bez adresy a času, limit po /64, nepravděpodobné řádky vynechá, `admin stats`, záložka Play. Páka `actions=`.
- ⚠ Revize našla, že dávka šla hned za skóre ve stejném drainu a stejném spojení: soukromí se dokazuje testem proti stand-inu (`OnlineStatsTests`), ne čtením.
- Otevřené: nasazení API na Pi před vydáním (majitel), pak první skutečná dávka zavře #709.

## 2026-10-14 — #829 létající útvary snu — Pi, Claude Code (584de7d2)
- majitel („možná pár geometrických létajících útvarů měnících tvar, ve stylu Windows 98 screensaveru"): hotovo (větev `829-potato-dream-solids`): osm nízkopolygonových těles (kvádr, osmistěn, drahokam, bipyramida) na drahách za poloměrem configu, otáčejí se kolem dvou os a dýchají proporcemi rychlostí morfu, každé svítí barvou palety, která se posouvá jako na desktopu; `PotatoBackdrop.Draw` dostává hodiny (`_wallClock`) a položka kresby umí `Animate`. Anvil 14,19/10,11 ms proti 14,21/10,06 — nic. Nárok hlášen v komentáři #829 předem.
- ⚠ Osamělý základ Anvilu vyšel 9,1 ms při první studené spuštění základního buildu; opakovaná dvojice 14,21/14,19. Při podezřelém čísle dvojici opakovat.
- Otevřené: okna domů a erupce sopky (ThinkPad), verdikt oka majitele na první kolo.

## 2026-10-14 — #829 verdikt oka, první kolo — Pi, Claude Code (584de7d2)
- majitel (verdikt na 14 scén): stromy, Měsíc, Space, sopka, město, neon, sen dobré; aurora bez země a stromů; jeskyně hnědá, krystaly nejsou krystaly; Grid bez mřížky; Měsíc bez hvězd a Země; Space bez velké planety; šapitó moc září; „možná“ erupce, okna domů, létající útvary snu.
- Hotovo (větev `829-potato-verdict-round-1`): planeta Space (koule, pásy, bouře, svícená rigem), hvězdy a Země nad Měsícem, mřížka Gridu (tenké kvádry, každá druhá čára stopa), jeskyně chladnější a krystaly jako šestiboké hranoly se špičkou, šapitó na zlomku barev a tmavší ke koruně, sen v barvách desktopu (cyklus palety kolem jedné). Cantor 15,22/10,78 proti 14,63/10,40, ostatní v šumu. Auroru se zemí a lesem jsem rozepsal souběžně s ThinkPadem (47c0d7df), jeho verze je na mainu, moje šla pryč.
- ⚠ Dvě session na jednom issue: hlásit nárok v komentáři před prací (ThinkPad to udělal, já ne). ⚠ `ForestScatter` chce aspoň jednu variantu každého druhu (0 = ArgumentOutOfRange při startu scény). ⚠ Emise nad 1 v lineáru křivka odbarví: krystal září pod jednou a plášť má skoro černý. Mřížka Gridu je z děla vidět jen jako pruh na obzoru (podlaha je za okrajem ostrova); z menu je to mřížka.
- Otevřené: erupce sopky, okna domů, létající útvary snu; střechy město (ThinkPad); verdikt oka na toto kolo.
## 2026-10-14 — #829 les pod polární září na Potato — ThinkPad, Claude Code (github-5f)
- majitel (stránka): aurora Vrať, „Chybí stromy.“ Hotovo (větev `829-potato-aurora-stand`, merge do mainu): `PotatoBackdrop.Aurora` — sníh `TerrainMirror.Forest` na 96 buňkách a desktopová výsadba `ForestScatter` na témž seedu, každý smrk kužel na kmeni v proporcích své varianty; tabulka šesti variant je teď jedna (`ForestScatterRenderer.CONIFER_VARIANTS`, renderer z ní staví meshe). 157k vrcholů ve 3 drawech (850 kmenů × 48, 850 kuželů × 72). Snímek `Research/pi-port/829/sky/anchor-stand.jpg`, před/po vedle. Test `SpruceProxies` (viděn selhat); 1104 testů OK.
- ⚠ Koruna z lerpu jehličí ke sněhu 0,13 četla šedozeleně, 0,07 je blíž skoro černým smrkům desktopu. Osiřelý `<param>` nad konstantami sloupu (dopolední commit z Pi) hlásil CS1572, přesunut k metodě.
- Otevřené: cena na Pi (jen Windows dvojče); okna měst a základ erupce (#829); stránka má nový snímek aurory.

## 2026-10-14 — #829 reference všech scén a stránka Potato proti desktopu — ThinkPad, Claude Code (github-5f)
- Pi session (584de7d2) odpověděla na #829: chce reference všech scén z kamery svých záběrů, stránku s cizím okem pro majitele, opravu startovního NeonCity si vzala sama (8b5929f4), střechy města nabízí mně.
- Hotovo (merge do mainu): `Research/pi-port/829/desktop/<level>.jpg` (High) a `829/twin/<level>.jpg` (Windows dvojče Potato, main e6a091f2 se třemi nebi a kmeny), čtrnáct úrovní z tabulky #829, okno 1600×900, `seed=829 sceneseed=829`, snímek 13 s (Pennant 16/19 s, po průletu). Stránka pro majitele s dvojicemi, výřezy, čtením od děla a odpověďmi OK/Vrať/Je mi to jedno ukládanými na stránce: https://claude.ai/artifact/S4zhX3Kuy8VaT8YkZcNfYN (zkouška zápisu prošla).
- ⚠ Úroveň vždy znovu ukáže své vlastní karty (Pennant: aim), save nepomůže; `Settings.json` s `"tutorial": false`. Průlet kapitoly posouvá snímek podle doby načítání: Pennant na High potřeboval 19 s. High PNG 1600×900 má 3,5 MB, JPEG 92 ~0,45 MB.
- Otevřené: odpovědi majitele (přepsat na #829); střechy města (#829) jako další práce; figury koulí se na Potato u některých stylů liší od desktopu (#828).

## 2026-10-14 — #829 desktopové reference tří nebí, #815 sprintf-js z webu — ThinkPad, Claude Code (github-5f)
- majitel: udělat desktopové reference pro space, auroru a dream a vyřešit střední zranitelnost webu.
- #829: `Research/pi-port/829/sky/{analemma,anchor,anvil}-desktop.jpg` — High, 1920×1080, tytéž úrovně jako záběry z Pi vedle nich, se seedovaným savem (všechny lekce tutoriálu v `lessons`, jinak karta překryje nebe). Co má desktop a Potato ne: space planetu s prstenci a červenou mlhovinu, aurora zasněžený jehličnatý les a sněžení, dream fialovo-oranžovou paletu s azurovými prstenci (Potato čte modrozeleně). Odkazy v komentáři #829.
- #815 (merge do mainu): `overrides.argparse ^2.0.1` v `Web/package.json` vyhodí `sprintf-js` (GHSA-hp3w-g68c-fv3c, bez opravené verze) ze stromu; js-yaml 3 bere argparse jen pro CLI, `_site` bajt po bajtu stejný, `npm ci` čistý. Dependabot alert 2 uzavřen. `braces` (high, chokidar) opravenou verzi nemá, jen build.
- ⚠ PNG záběr z High má ~5 MB (zrno), JPEG 92 ~0,6 MB; reference ukládat jako JPEG.
- Otevřené: zda Pi session chce les a sníh pod aurorou (scatter bez zrcadla terénu).

## 2026-10-14 — #863 zmrzlá koule na Potato je led, ne růžová — Pi, Claude Code (584de7d2)
- majitel (test na Pi): na Help se Frozen kreslí růžově; je to led, má být bíloazurová. Hotovo (větev `863-potato-frozen-ice`): `PotatoKindLookFor` Frozen — plášť, záře i tón skoro celé `POTATO_ICE` (0,9/0,85/0,85 místo 0,65/0,6/0,65), led světlejší (0,72, 0,88, 1,0); barva koule je jen nádech, jako jádro desktopu skrz blok. Snímek `Research/pi-port/863/frozen/help-frozen-before-after-4x.png`.

## 2026-10-14 — #829 start nestaví NeonCity backdrop nadarmo — Pi, Claude Code (584de7d2)
- Nález ThinkPad session (github-5f) z logu Windows dvojčete: `_scene` startuje jako NeonCity a `BuildScene` volal `EnsurePotatoBackdrop` před startovním `SetScene`, takže každý Potato start postavil a zahodil 3 950 věží. Hotovo (větev `829-potato-startup-backdrop`): volání v `BuildScene` pryč, první backdrop staví až startovní `SetScene`; v logu je teď jediný řádek `[potato] … backdrop`, ten vybrané scény.

## 2026-10-14 — #829 kmeny jako sloupy pod korunou — Pi, Claude Code (584de7d2)
- majitel (test na Pi): kmeny na louce i savaně procházely korunami jako ostny; kmen má být jen dole a válcový. Hotovo (větev `829-potato-trunks`): kmen je `PotatoShellMesh` zvenčí (osm stěn, zavřený), od paty do kousku nad střed sítě, kde ho koruna schová; `PotatoShellMesh` umí líc ven (`inside: false`).
- Snímky `Research/pi-port/829/savanna/giraffe-trunks.png`, `meadow/pennant-trunks.png`.
## 2026-10-14 — #829 hvězdy, záclony a sen v PotatoSky — Pi, Claude Code (584de7d2)
- majitel: hvězdy a záclony pro space, aurora a dream v PotatoSky. Hotovo (větev `829-potato-sky-figures`): `PotatoSkyFigure` — koule 48×96 kolem kamery, jejíž vertex shader počítá Mléčnou dráhu s mlhovinou, záclony polární záře nebo mramor snu s koulemi světla (`PotatoSkySpace/Aurora/Dream`), a hvězdy jako dávka 900–1400 malých čtverečků (`PotatoSkyStars`), uniformy z configu scény (`PotatoSceneRig.SkyLookOf`). Analemma 13,64/9,97 ms proti 13,40/9,33, Anchor 14,17/9,85 proti 13,52/9,50, Anvil 14,14/10,08 proti 13,60/9,66.
- ⚠ První řez počítal totéž po pixelech: +12,5 ms nativně (13,4 → 25,9). Co je hladké, patří do vrcholu; bod je čtvereček. Tři kola kompilace shaderu v CI (`fetch.sh`).
## 2026-10-14 — #829 #863 Potato na Windows proti Pi, nabídka pomoci — ThinkPad, Claude Code (github-5f)
- majitel: Pi session dělá port scén a druhů kvůli vydání pro další hráče; zeptat se jí, zda nepotřebuje pomoc z Windows. Nabídka je v komentáři #829 (desktopové reference, DX11 dvojče, lokální kompilace GL, oko, rozdělení práce); zatím bez odpovědi.
- Windows Potato (#808) na mainu 247c0698: 16 běhů (jedna úroveň na scénu, Caldera, `help=4`, fullscreen 1080p), bez výjimky, každá scéna má řádek `[potato] … backdrop` se stejným sčítáním jako na Pi a snímky odpovídají záběrům z Pi; DX11 kompilace `PotatoSky.fx` z větve `829-potato-sky-figures` prošla CI.
- ⚠ `_scene` startuje jako NeonCity a stavba ostrova volá `EnsurePotatoBackdrop` před startovním výběrem scény: každý Potato start postaví a zahodí NeonCity backdrop (3950 věží), komentář u stavby tvrdí opak. Nechán Pi session (její soubor, otevřená větev).
- ThinkPad (integrovaná Radeon) drží v Potato 37–53 FPS na 1080p, Auto sníží na 720 — jen Windows.
- Otevřené: odpověď Pi session; desktopové reference na vyžádání.

## 2026-10-14 — #829 krok 7: šapitó na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-bigtop`): kvadriky desktopu jako pláště zevnitř (`PotatoShellMesh` zobecněný na komolý kužel s filtrem segmentů): plachta jako dva kužely nad jedním (červené a smetanové panely, koruna), závěsová stěna, svah sedadel, obrubník, piliny s dírou, královské sloupy jako kvádry. BigTop 14,57/10,26 ms proti 13,90/9,69.
- Stav #829: jedenáct ze čtrnácti vydaných scén má pozadí, space/dream/aurora stojí ve svém prázdnu s vlastním rigem; issue jde na verdikt oka (shipped-awaiting-verdict). Zbývá: hvězdy/záclony/sen v PotatoSky, střechy města.

## 2026-10-14 — #829 krok 6: vlastní rig a prázdno scén bez nebe na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-scene-rig`): `PotatoSceneRig` odpovídá za sedm scén, které na desktopu nahrazují nebe (space, dream, jeskyně, Měsíc, aurora, Grid, cirkus): jejich rig a slunce z configů (přes `SkyLightRig.SceneRig`/`SceneSun`, jen když host nemá scene renderer) a barvu prázdna, na kterou se snímek maže místo dómu. Měsíc má černé nebe a nízké slunce, Grid tyrkysové kvádry na tmavém, sen fialový, cirkus teplý. Bez ceny (o jeden draw méně).
- Otevřené: cirkus (stan), hvězdy/aurora/sen jako PotatoSky, střechy města; verdikt oka na 10 scén.

## 2026-10-14 — #829 krok 5: jeskyně a Grid na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-cavern-grid`): jeskyně jako plášť zevnitř (`PotatoShellMesh`: válec a strop v barvě skály), řeka jako rovina s trochou své záře, osm trsů krystalů jako nakloněné kvádry svítící svou barvou na místech `CrystalCenter`; Grid jako podlaha s dírou a podílem světla stop a kvádry podle configu. Grotto 17,97/11,90 ms proti 16,90/11,59 (plášť je celý snímek výplně), Cantor 15,45/10,73 proti 14,98/10,45.
- ⚠ Obě scény i Měsíc stojí pod fialovým dómem a jeho rigem, kde desktop nebe nahrazuje prázdnem či jeskyní a svítí vlastním rigem (`TryGetLightRig`, Potato host nemá renderer, kterého by se zeptal): to je další krok.
- Otevřené: rig a dóm scén nahrazujících nebe, cirkus, aurora/dream/space, střechy města; verdikt oka.

## 2026-10-14 — #829 krok 4: město a neon na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-city`): věže a římsy `City` z configu hosta (seedy už hozené), každá jedna krabice v barvě fasády bez oken, jen do 250 jednotek od ostrova, ořez frustem po snímku, nad plochou asfaltu. Girandole 18,67/12,97 ms proti 18,21/12,64, Cabinet 18,80/12,74 proti 17,68/12,03 (nativně/720).
- ⚠ Základ a novou verzi měřit v jednom řetězu ze základního buildu mainu (`~/.cache/bs3d-wt/baseline-main`): osamělý základ Cabinetu 11,5 ms (bez session) poslal první číslo neonu na +8 ms a stál tři pokusy (lampy pryč, dosah 100, nic), než se základ přeměřil na 17,7.
- Otevřené: jeskyně, grid, cirkus, aurora, dream, space; střechy města; verdikt oka.

## 2026-10-13 — #829 krok 3: hory, Měsíc a sopka na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-mountain-moon-volcano`): hory jako dvě mřížky nad jedním polem (sníh/skála podle pravidla `Mountain.fx` po trojúhelnících), Měsíc s balvany `MoonPlanting` (zvětralé jako elipsoidy), sopka s řekami `VolcanoLava` jako svítícími pásy (`PotatoStripMesh`) a jezerem v kráteru, na seedu lamp. Belfry 18,84/12,46 ms proti 17,88/12,09, Colossus 17,38/12,13 proti 16,35/11,27, Vent 16,00/10,97 proti 15,21/10,65 (nativně/720).
- ⚠ Tři opravy prvních kroků: barvy configu jsou lineární, materiál rendereru sRGB (terén byl tmavý a pod dómem fialový); `Custom` instance je pro Potato efekt okluze (w = 0 u výsadby = „zahrabáno“, stromy na čtvrtinu, balvany černé); pozadí je matné (`SpecularAmbientStrength` 0), protože Fresnel zrcadlí celý dóm pod ostrým úhlem — bílý pruh na obzoru.
- Otevřené: jeskyně, město/neon, grid, cirkus, aurora, dream, space; verdikt oka.

## 2026-10-13 — #829 krok 2: poušť a louka na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-desert-meadow`): `PotatoBackdrop.For` umí Desert (duny z `TerrainMirror.Desert`, písek) a Meadow (`TerrainMirror.Meadow` + `MeadowScatter`: duby jako bloky, plot), stejnou cestou jako savana. Basket nativně 15,60 proti 14,08 ms (720p 10,54 / 9,75), Pennant 14,90 / 13,10 (10,48 / 9,35) — v prvním čísle i figury #828.
- Otevřené: jeskyně (krystaly, vlastní efekt), město (vlastní renderer na desktopu), ostatní scény; bílý okraj mřížky na obzoru; verdikt oka.

## 2026-10-13 — #829 krok 1: savana na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `829-potato-savanna`): `PotatoBackdrop` = terén z `TerrainMirror.Savanna` jako mřížka 64×64 s dírou arény + `SavannaScatter` kbelíky, vše `InstancedModelRenderer` na Potato efektu, nasvícené rigem s ostrovem, kreslené před ním; technika `PotatoVertexLit` (světlo ve vertexu). Giraffe: nativně 15,55 ms proti 14,88 bez pozadí (bez figur), 720p 10,92 / 10,27; s figurami 17,49 / 12,03.
- ⚠ Rostliny desktopu jsou tisíce vertexů (kmen 5–9 k, termitiště 50 k, celkem 1,2 M/snímek): síť nad 1 200 vertexů je elipsoid své obalové koule (kmen úzký vysoký, koruna plochá). ⚠ Jednoinstanční široký kbelík (holá země) jako elipsoid obalil kameru: černý snímek; nechává se mřížce. ⚠ Barvy kbelíků jsou lineární, materiál renderereru sRGB: kódovat, jinak černé stromy. ⚠ Pozadí přes `PotatoLit` per pixel stálo 3–8 ms (třetina snímku): svícení ve vertexu to srazilo na 0,7.
- Otevřené: další scény (poušť, louka, město, jeskyně) stejnou cestou; obzor je bílý okraj mřížky (Fresnel) — mlha chybí; verdikt oka majitele.

## 2026-10-13 — #828 stupeň 2: figury #737 na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `828-potato-colour-cue`): `PotatoBallCueBands/Spots/Checks/Emblems` v `PotatoModel.fx` — funkce figur z `BallColourCue.fxh` doslova (test `PotatoColourCueSourceTests` drží shodu), čtené ze stejných čtyř uniforem figury; inkousty skládá CPU do plášťů a září v obou inkoustech, pixel je lerpuje pokrytím. `DrawsColourCue` je na Potato true, HUD disky figury mají. Ověřeno na Girandole, Heart, Tightrope (Research/pi-port/828/stage2).
- Cena proti `colourcue=none`: Girandole nativně 17,66 / 15,15 ms, 720p 12,48 / 11,23; Cabinet 17,64 / 15,61, 12,12 / 10,99. Jeden program s druhem v uniformě byl 969 instrukcí a stál skoro totéž (17,97): cena je v cestě (sin, cos, atan2, derivace), ne v registrech. V mezích rozhodnutí majitele (#830: 720p drží 60, nativně 56 FPS).
- Otevřené: verdikt oka majitele; stupeň 3 (textury stylů) nepokračuje bez jeho slova — na 720p zbývá ~3,5 ms.

## 2026-10-13 — #828 stupeň 1: lesk a emise per styl na Potato — Pi, Claude Code (584de7d2)
- Hotovo (větev `828-potato-style-gloss`): deset materiálů dostalo v `PotatoKindLookFor` svůj lesk z desktopových konstant (kov `MetalF0Floor` 0,16 → odrazivost ×6, drsnost 0,18; porcelán/mramor/drahokam/bublina z `*Gloss` 210/190/260/220 → drsnost ~0,10; vlna matná ×0,3; led sklovitý; plazma svítí vlastní silou 0,45, protože desktopová emise je 0). Barva stylem nikdy nemění. Bez změny shaderu; A/B Girandole nativně 15,09 proti 15,33 ms. Záběry před/po pěti stylů v Research/pi-port/828/stage1.
- Otevřené: stupeň 2 — figury #737 (`BallColourCue.fxh`, 363 řádků) jako program `PotatoBallCue`: inkousty z `ColourCues.Paint` jako uniformy, pole figury v objektovém prostoru (nový výstup VS), měřit proti rozpočtu 720p.

## 2026-10-13 — #863 stupeň 3: figury druhů ve vlastním programu — Pi, Claude Code (584de7d2)
- Hotovo (větev `863-potato-kind-figures`): `PotatoBallKind`/`PotatoBallKindDither` v `PotatoModel.fx` maluje na `BallRadiance` před `ToDisplay` figuru podle uniformy `KindFigure`: kyselina spodní půlka zářící kapaliny s pěnovou čárou, nákaza roztřepená krusta na třetině koule obrácené k hráči (`KindAxis` z kamery, skloněné dolů), gravitace tmavé oko tam, kde koule hledí do oka, s prstencem ve vlastní barvě a ztmaveným okrajem. Obyčejná koule běží dál svým programem (shaderdb beze změny). GL xnb z `gamepi-shaders.yml` přes `fetch.sh`.
- ⚠ Řetěz `dotnet test | grep && git commit && git push` commitne i s chybou kompilace: grep vrací 0, když našel řádky s `error`. Oprava šla jako druhý commit (větev už byla pushnutá).
- Otevřené: verdikt oka majitele na `help=4` (všech osm druhů), případně jemnější doladění prahů; #828 a #829.

## 2026-10-13 — #863 stupeň 2: tvary druhů jako geometrie — Pi, Claude Code (584de7d2)
- Hotovo (větev `863-potato-kind-meshes`): `PotatoKindMesh`/`PotatoKindGeometry` v Prazsky.Core: `StoneVS` (řez sinů + pět lomových ploch) a `FrozenVS` (zaoblená kostka p 4,5) přenesené číslo po čísle na topologii koule per LOD, kreslené místo ní; hroty miny (`MineRadius`: 8 komolých kuželů, zdvih 0,13) jako vlastní síť kreslená po plášti na tomtéž instančním bufferu. Bez kroužku (rim) pro nahrazenou kouli. Test `PotatoKindMeshTests` (dovnitř, hroty ven, vinutí po směru hodin zvenku).
- ⚠ Hrot je na herní kouli Pi (24 × 12) užší než jeden vertex: zvedat vertexy koule jako `BombVS` by nezvedlo nic; proto vlastní síť.
- Otevřené: stupeň 3 (spodní půlka kyseliny, krusta nákazy, oko gravitace: větve v `PotatoBall` za uniformou, GL kolečko přes CI), verdikt oka na `help=4`, Vent a Anvil.

## 2026-10-13 — #863 stupeň 1: vzhled druhů z uniforem — Pi, Claude Code (584de7d2)
- Hotovo (větev `863-potato-special-kinds`): `InstancedModelRenderer.PotatoKindLookFor` dává každému druhu na Potato plášť, barvu záře (klidový a tepový podíl), lesk, alfu a tón, kterým se stáhne i ambient typu. Bomba tmavá s červeným tepem, blesk břidlicový s bledě modrým blikáním, kyselina tmavá se zelenou září, sklo průsvitné přes `PotatoLit` (alfa 0,38), zmrzlá bledá a hladká, nakažená olivová matná, gravitace tmavá lesklá, těžká šedá matná. Žádná změna shaderu; A/B nativně Vent 15,07/14,99, Caldera 16,30/16,31, Bell 13,76/13,80 ms (nový/základ). Test `PotatoKindLookTests`.
- ⚠ Koule na Potato je z velké části svou EMISÍ: zbledlý plášť a stažený ambient zmrzlou kouli nezměnily ani o pixel (192,38,47 proti 192,21,30); teprve barva záře. A bomba se třemi desetinami klidové záře byla červená koule; klid je 0,07.
- Otevřené: stupeň 2 (hroty bomby, ledová kostka, předřezaný kámen jako geometrie), stupeň 3 (kyselina, nákaza, oko gravitace). Verdikt oka majitele na `help=4`.

## 2026-10-13 — #863 speciální druhy na Potato, Pi port odparkován — Pi, Claude Code (584de7d2)
- Kontrola mainu 13ee3c52 pro Pi: GamePi build čistý, ARM64 CI zelené; #737 na Potato figuru nekreslí (záměr, #828), #856 kreslí živé koule i na Potato, #859 řádek držený „(Potato)“. Přestavěný Colossus 15,1 ms nativně (byl 17,0), Caldera 16,25.
- majitel: speciální druhy (bomba, cut) vypadají na Pi špatně → #863 se záběry z Pi vedle desktopových referencí (stránka k #622 z 26. 9.; bomba, blesk a sklo tam jsou ještě před #624–#626). Pi se má přiblížit **levnějšími metodami**: geometrie, konstanty, emise tepu; pixelové větve jen v kreslení druhu.
- majitel: „Pokud chceme zveřejnit hru pro Pi…, mělo by se brát i to, co je zaparkované“ → Pi port vyňat z mrazu šíře (CLAUDE.md, repo-conventions), #828/#829/#830 se berou.
- majitel k #830: 1080p na Pi může běžet 30–40 FPS; 60 FPS má držet 720p (Auto na něj sníží kvůli desktopovějšímu vzhledu); kdo chce vyšší rozlišení, přepne ho v Settings a vypne automatickou kvalitu. Rozpočet pro #828/#829/#863 je tedy ten 720p (~5 ms).
- ⚠ Na Potato je bomba jen tmavší koule než černá Type8 a blesk skoro navy: na Vent a Caldeře splývají s paletou levelu.
- Otevřené: #863, #828–#830, #858 (Pi-Apps). `parked` z #828–#830 a #785 sundáno. ⚠ Klasifikátor auto režimu na Pi odmítá `git push` do mainu („Merge Without Review“) i po ústním svolení majitele: větev se pushne a merge udělá majitel, nebo přidá pravidlo do settings.

## 2026-10-01 — #681 obloha pouště a savany do modra (claim) — desktop, Claude Code (bs3d-78)
- Claim #681 (majitel: „Přebarvit do modra“): kopule 6 (poušť) a 14 (savana) vygenerovat z výškových bodů jako kopuli 8 (#661), spodní pětina zůstane teplá.

## 2026-10-01 — #681 obloha pouště a savany do modra — desktop, Claude Code (bs3d-78)
- #681 na mainu, `shipped-awaiting-verdict`: kopule 6 a 14 jako kopule 8; poušť 168° → 205°, savana 152° → 203°, spodní pětina beze změny.
- ⚠ Herní kamera vidí kopuli jen do t ≈ 0,6 (bledý opar do 0,59 dal skoro bílou oblohu); sytá modrá jde přes tonemap do fialové a zezelená trávu.
- ⚠ Skript na palety hledal konec bloku podle první uvozovky (byla v komentáři) a sežral paletu.

## 2026-10-01 — #542 #691 online skóre: veřejná adresa, cizí 404 — desktop, Claude Code (bs3d-a3)
- Služba venku na `https://scores.winphonew.eu/` (Cloudflare, tunel na Pi), `OnlineScores.DefaultServer` na mainu (merge 80cd0238). Otevřené: před ostrým spuštěním smazat testovací DB a rozhodnout o dev tabulce stropů na Pi.
- #691: klient zahazoval výsledek při jakékoli 4xx (majitel přišel o 8 výsledků při přechodu DNS); teď odmítnutí jen 4xx s JSON `reason`, jinak „no answer“ a výsledek zůstane v outboxu.
- ⚠ Stará verze smazala `Online.json` při cizí 404 na „Remove scores“. ⚠ Přechod DNS trvá až hodinu (TTL 3600), `ipconfig /flushdns` nepomůže.

## 2026-10-01 — #687 online řádky v Nastavení (claim) — desktop, Claude Code (bs3d-a3)
- Claim #687: Off a Not set červeně, přezdívka jako pole, stav bez serveru na řádku Online; #686 ne.

## 2026-10-01 — #687 online řádky v Nastavení — desktop, Claude Code (bs3d-a3)
- #687 na mainu (merge 3dcac6a9), `shipped-awaiting-verdict`: červeně (`MENU_TEXT_ALERT`) Off, Not set a „No server“; přezdívka jako pole s podtržítkem; odchod z řádku platné jméno uloží, Esc zahodí.
- ⚠ `|` v Antonu vypadá jako L, proto podtržítko. Páka `settings=` umí `type:<text>`, `enter`, `esc`.

## 2026-10-01 — #682 přejmenovat level One (claim) — desktop, Claude Code (bs3d-a3)
- #682 hotovo (merge 22710ccf), zavřeno: „One“ → „Cannonball“, soubor `One.json` zůstal, nový klíč tabulky `One.json#1e913f0a50d358cb`. Otevřené: dev tabulka stropů `BS3D-dev-22710cc-ceilings.json` na Pi čeká na instalaci.
- ⚠ LevelGen zapisuje CRLF, `git status` ukáže vše změněné; rozhoduje `git diff`, vrátit `git checkout -- Game/Levels`.

## 2026-10-01 — #683 signál u „Sending your score“ — desktop, Claude Code (bs3d-a3)
- #683 na mainu (merge e9476d9d), `shipped-awaiting-verdict`: ikona `OnlineSignal` před stavem pod tabulkami výsledku.
- ⚠ Odeslání startuje až se stránkou výsledku a klient po 5 s vzdá, zpoždění zástupného serveru musí být pod 5 s.
- ⚠ Nalezený pád (merge `music-theme-null`): level bez pole `music` shodil `GameMusic.SetTheme` (alias bohemia volal `StartsWith` na null).

## 2026-10-01 — #689 sklo láme ohňostroj — desktop, Claude Code (bs3d-a3)
- #689 na mainu (merge 353e50ef), `shipped-awaiting-verdict`: ohňostroj se kreslí v `DrawTranslucentsBehindGlass` před kopií pro sklo.
- ⚠ Přední menu s `celebrate` se na ověření nehodí (deska z boku). ⚠ `shot=` každou půl vteřinu srazí hru na 2 FPS; lepší čtyři záběry po vteřině.

## 2026-10-01 — #688 kulaté duny — desktop, Claude Code (bs3d-a3)
- #688 na mainu (merge 722c74eb), `shipped-awaiting-verdict`: kulaté hřebeny dun (`CREST_ROUND`, `SET_BLEND`), `TerrainMirror` totéž.
- ⚠ Plnější profil posunul střed pole: `DUNE_MEAN` navzorkovat přes skutečný `TerrainMirror`, jinak se ostrov posune v písku. Další páka: `DUNE_WINDWARD`.

## 2026-10-01 — #686 nastavení po stránkách — desktop, Claude Code (bs3d-a3)
- #686 na mainu (merge a6b5ae4e), `shipped-awaiting-verdict`: záložky ONLINE · DISPLAY · AUDIO · CONTROLS · GAME, otevře se vždy na ONLINE, LB/RB, kolečko, klik; pad nezkoušen.
- ⚠ Myra počítá `Margin` dovnitř explicitní `Height` (useknutý poslední řádek) a měří jen viditelné prvky, plochu měřit ze všech stránek.
- ⚠ Snímky ve 4K v okně se napoprvé neuložily (`shot=11`, 17 s), s `shot=9` prošly; příčina nehledána.

## 2026-10-01 — #692–#694 poznámky z hraní (Cut/Brake, HUD při průletu, čipy) — desktop, Claude Code
- Založeno, nic nestaveno: #692 (Cut a Brake nepochopené, nejdřív měřit), #693 (HUD přes úvodní průlet kapitoly), #694 (klávesy čipů nejsou v jednom sloupci); komentář na #213.
- ⚠ „Break“ v poznámce čteno jako Brake (Q); majitel ať opraví, pokud myslel jiné.

## 2026-10-01 — #684 nejlepší skóre na dlaždicích výběru levelů — desktop, Claude Code (bs3d-a3)
- #684 na mainu (merge b2fda176), `shipped-awaiting-verdict`: na dlaždici vlastní nejlepší skóre (z `Progress.json`), jen u dohraných; `TILE_HEIGHT` 210 → 260. Rozhodnuto za nepřítomného majitele; online #1 ne, kontrakt v1 nemá endpoint se seznamem (jako #685).

## 2026-10-01 — #693 HUD při úvodu kapitoly, #694 klávesy čipů v jednom sloupci — desktop, Claude Code (bs3d-a3)
- #693 na mainu (merge 4b6fc277): `ChapterIntro.HudOpacity`, HUD během průletu 0, pak `1 − Blend`. #694 (merge f613a4c6): `ChipColumnLayout`. Obě `shipped-awaiting-verdict`.
- ⚠ Majitel u počítače odmítl běh, který bral fokus (keybd_event + SetForegroundWindow): scénáře řídit bez kláves (`swap=`/`brake=`/`powerups=`, `shot=` přes `quiet.ps1`).

## 2026-10-01 — #692 Cut a Brake v hraní — desktop, Claude Code (bs3d-a3)
- Měření `LevelGen --cuts` (merge 6972a6aa): na startovním clusteru 98 ze 100 levelů s řezem neuvolní řez 3+ koulí, na 93 jen zasaženou. Brake čip ztlumený, dokud `CanBrake` neplatí (merge 6529b995), `shipped-awaiting-verdict`.
- Otevřené (majitel): co má řez dělat (silnější řez posune stropy ScoreSim i whitelist online tabulek, nová tabulka na Pi ve stejném vydání) a karty tutoriálu pro Brake a Cut.

## 2026-10-01 — #690 krok 1: prototyp měkkosti — desktop, Claude Code (bs3d-a3)
- #690 krok 1 na mainu (merge ac9d2e43), `shipped-awaiting-verdict`: `LatticeSoftness`, `softness=<Hz>[:<tlumení>]` v Testbedu, `LevelGen --softness=`, mapy `Sag_*.json`.
- ⚠ Dnešních 15 Hz dlouhé tenké rozpětí už prověsí a rozhoupe; 6 Hz hluboké V, 3 Hz bungee; sonda zasáhla čáru už při 15 Hz v 1–2 z 5 běhů.
- ⚠ PowerShell nerozlišuje velikost písmen: smyčková `$s` přepsala `$S`.

## 2026-10-01 — #695 kapitola (block) v tabulce stropů — Pi, Claude Code (BS3D-API)
- #695 na mainu: řádek tabulky stropů (`ScoreSim --ceilings`) nese `block`, formát `bs3d-ceilings` verze 1 beze změny; služba čte od BS3D-API v0.1.11 (BS3D-API#5). Ověřeno na Pi (130 řádků, 13 kapitol).
- Otevřené: tabulka s kapitolami na Pi s příštím releasem nebo z ručního `release.yml`; instaluje majitel do `/var/lib/bs3d-api/ceilings`.

## 2026-10-01 — #685 obrazovka High Scores — desktop, Claude Code (bs3d-a3)
- #685 na mainu (merge 1515c789), `shipped-awaiting-verdict`: nový endpoint `GET /v1/boards?period=&player=` (BS3D-API#7, 253c131); otevřené otázky rozhodnuty za nepřítomného majitele (přehled po kapitolách, položka v menu i bez online). Otevřené: API neotagované a nenasazené, `update.sh` na Pi dělá majitel.
- ⚠ Myra kreslí vypnutý `Button` světlým pozadím a šedým textem (ručně `DisabledBackground`/`DisabledTextColor`); `ApplyNavHighlight` přepisuje pozadí při každém průchodu, období odlišit jasem textu.
- ⚠ Hashe levelů se od v0.2.1 změnily (`ScoreSim --ceilings`).

## 2026-10-01 — kritické čtení nočních mergí (#684–#694, #690, #685, #688) — desktop, Claude Code (bs3d-a3)
- Dva agenti přečetli dnešní merge, opravy v mainu: `685-review-fixes` (3c204d7c), `688-true-peak` (be27f1e0; první #688 měla plošinu 12 j. se dvěma zlomy, `DUNE_MEAN` 0,403), `690-review-fixes` (acc3a7c1; Bepu recykluje čísla constraintů).
- ⚠ Merge bez nezávislého čtení nechal projít skutečné chyby ve třech z devíti mergí: u větší změny pustit recenzenta hned po sloučení.

## 2026-10-01 — #230 červí díra po třech minech ze stejného úhlu — desktop, Claude Code (bs3d-a3)
- Claim #230, jen kosmeticky: tři miny ze stejného místa a úhlu otevřou ve vzduchu díru, nasaje je a zavře se. Bez bodů (stropy `ScoreSim --ceilings`), bez zásahu do clusteru; nový `Game/Effects/Wormhole.cs`.

## 2026-10-02 — #495 hudba: řídká a plná varianta podle nebezpečí — desktop, Claude Code (bs3d-a3)
- Claim #495, jiná cesta než issue: místo druhé generace ACE-Step oddělit bicí Demucsem (venv `C:\Users\panrd\AI\stems`), řídká varianta = tatáž nahrávka bez bicích; hra míchá `plná − (1 − nebezpečí) × bicí`. Nejdřív jedna kapitola, pak majitelův sluch.

## 2026-10-02 — #230 červí díra na mainu — desktop, Claude Code (bs3d-a3)
- #230 na mainu (merge 2087d40d, opravy 1c07aef3), `shipped-awaiting-verdict`: tři rány ze stejné pózy ven za ostrov otevřou díru, pohltí venkovní rány, pukne; bez bodů; `KeepInView`; páka `aim=<t>:<elev>:<trav>`. Issue zůstává otevřené (tvar v troskách); zvuky neslyšeny.
- ⚠ Barvy přes 2 v HDR zbělají a zašedí hrdlo (`SWIRL_DIM` 0,85). ⚠ Hash buněk v polárních souřadnicích skáče na řezu atan2: hashovat id modulo počet.
- ⚠ Snímek ve 4K zastaví hru na ~1,5 s. ⚠ Dva běhy `render-references.ps1` naráz: druhý podepsal klein snímky jako Z-Image (ověřit `tasklist | grep sd-server`).

## 2026-10-02 — #495 bubny jako vrstva podle nebezpečí; #674 per-building hash; DocDrift — desktop, Claude Code (bs3d-a3)
- #495 na mainu pro Louku (merge 1f21677e, opravy 34924a98), `shipped-awaiting-verdict`: bicí z Demucsu (`MusicBake --drums`), mix po čtvrtsekundových kouscích podle výšky koule nad čárou; 8 z 10 nahrávek, neslyšeno (v noci bez zvukového výstupu). #674 `BuildingRoll` (merge f1ce0f6d); DocDrift: jediný drift `MENU_MIN_DESIGN_WIDTH` 2600 (merge 0e98daed); #230 tvar v troskách nestavěn.
- ⚠ `DynamicSoundEffectInstance.SubmitBuffer` buffer kopíruje, vlastní ring je zbytečný; `GameMusic` od #486 neuvolňoval odehranou variantu.
- ⚠ Demucs na smyčku: separovat třikrát za sebou a vzít prostřední kopii. ⚠ Bash heredoc zhltne `\\`: skripty s cestami psát přes Write.

## 2026-10-02 — #257 bedna, krok 1 — desktop, Claude Code (bs3d-a3)
- Claim #257 krok 1 (majitel 2026-09-29: „bedna první, pak buckshot v levném čtení“): statický kvádr, od kterého se rána odrazí analyticky jako od zrcadla (jedna funkce pro fyziku i ducha), pole `crates` v levelu. Žádný vydaný level ji nedostane (krok 2).

## 2026-10-02 — #257 bedna krok 1 na mainu; #696 duch na šikmých ranách — desktop, Claude Code (bs3d-a3)
- #257 bedna krok 1 na mainu (merge 814bdef3): `Crates`, `CrateMesh`, pole `crates`, páka `logshots`; brány o ní nevědí (krok 2).
- #696 založeno: na šikmých ranách duch a dopad nesouhlasí i bez bedny; tři změny handleru nic neřeší celé, žádná nenechána; rozhodnutí o škrtnutí je na majiteli.
- ⚠ Testovací runy: `levelfile=` zabírá slot prvního levelu, přidat `level=3`; dva `shot=` platí jen poslední; 3 instance Game paralelně na desktopu jsou v pořádku.

## 2026-10-02 — #257 buckshot na mainu; oprava bedny po recenzi; sweep `down` — desktop, Claude Code (bs3d-a3)
- #257 buckshot na mainu (merge b92fa221): `BallKind.Buckshot = 12`, brána `LevelGates` „buckshot that can pour“; bedna po recenzi (merge d0b177fc: pomalá rána se neodrazí). Žádný level ho nemá.
- ⚠ `DrawTintlessRegion` s cizí `BallShading` kreslí černě (uniformy musí nastavit speciální kresba). ⚠ `Tools/lattice-sweep.ps1` vantage `down` se díval kolmo dolů (degenerovaný look-at), všechny „down“ snímky byly ničím.
- ⚠ Větev smazána `git branch -D` místo `-d`.

## 2026-10-02 — #674 okna města: celočíselný hash okna — desktop, Claude Code (bs3d-a3)
- #674 okna města na mainu (merge 97b55139), #674 zůstává otevřené: `WindowSeed` celočíselně (různé rytmy 9/400 → 94 %), lampa teplá/studená dřív bez budovy. #257 buckshot po recenzi (merge af03a514): brána počítá s bombou a kyselinou.
- ⚠ Samostatný mix na každý los stál +0,11 ms den / +0,16 neon (3840×1600 ssaa 2); losy s málo bity se řežou z bitů semene (+0,02 / 0).

## 2026-10-02 — #674 moře: mřížka vln (swell aliasuje na mřížce vrcholů, málo vln, chop ze čtyř sinů) — desktop, Claude Code (bs3d-a3)
- #674 na mainu, zůstává otevřené pro majitelův verdikt: moře (merge 05554069, opravy adae0c8a; 20 vln, +0,95 ms), květy louky `HashMix/HashCell` v `Noise.fxh` (93a7bc9d), reliéf ostrova (7a78e7b9, f2a55cc7), pěna kyseliny (798a6c2d), řeka v jeskyni (4c7a39e8). Mraky a hvězdy Mléčné dráhy zkontrolovány, vada nejsou. Cena broků #257 +0,46 ms (merge 3bf1a511).
- ⚠ `GradientNoise3` stál na High 3840×1600 +0,83 ms (přes rozpočet 75 Hz), promítnutý 2D +0,24; Pennant už běží na hraně 12,9–13,25 ms a rozpočet ukáže jen hra v majitelově rozlišení. ⚠ Numerický sklon jemných oktáv vzorkovat aspoň 10× jemněji než nejmenší buňka; `[loop]` s předčasným koncem je dražší.
- ⚠ CI byl od 23:41 do rána červený (14 mergí): `CrateTests` chtěl přesně slíbenou kouli, na runneru trefil sousedy (oprava merge 1d186416). Po merge kontrolovat `gh run list`.

## 2026-10-02 — #697 koruny stromů louky — desktop, Claude Code (bs3d-a3)
- Založeno #697 z majitelovy poznámky (obdélníky v korunách, „nalepené papírky“). Beru část 1: `BroadLeafMask` za koncem stonku dává 0 a `clip(0)` pixel nechá, takže špičková čtvrtina karty je plný pruh (stejně `LeafMask` akácií). Část 2 (listy z větví) potom.

## 2026-10-02 — majitelovy verdikty a další dávka — desktop, Claude Code (bs3d-a3)
- majitel: OK a zavřeno #674, #686, #687, #685, #694, #693, #669, #675, #95, #280. OK, ale otevřené kvůli dalšímu kroku: #257 (krok 2), #230 („tvar v troskách“), #690 (krok 2), #692 (co dělá řez, karty).
- Beru: #495 (bicí pro zbylých 12 rodin), #488 (záběry do ulic výš), #684 (žebříček vpravo), #688 (duny ostré, zakulatit jen špičky), #697 část 2 (koruna z větví).

## 2026-10-02 — #684 žebříček ve výběru levelu, #688 ostré duny — desktop, Claude Code (bs3d-a3)
- #684 na mainu (merge 5a959e41, opravy ef2cad68): deska žebříčku vpravo od okna levelů, páka `pickfocus=<level>@<s>`; recenze našla pět chyb (cache přežila zápis, přestavba menu jen při změně výšky…). #688 (merge f7221e64, po recenzi eeb584dc): ostré hřebeny, zakulacené jen špičky. #697 část 2 (merge 4a802a55): koruny z větviček, Pennant 14,31 → 14,11 ms. #495: 50 vrstev bicích na `495-drums-rollout`.
- ⚠ Hladká funkce nestačí, hladké musí být i její vstupy (poloměr z ostrých výšek dal záhyb 38°).
- ⚠ Rainbow, Zigzag, One a Shuttle mají v dev buildu jiný hash než zápisy na serveru (soubory změněné od v0.2.1): jejich žebříček je prázdný.
- ⚠ Pády desktopu se vrátily i s podtaktovanou GPU (Kernel-Power 41; Demucs na 12 jádrech + sd-server): těžké úlohy po jedné. Po pádu byl `Desert.xnb` nulový: smazat a přestavět, ověřit hlavičky `XNB`/`MZ` a `git fsck`.

## 2026-10-02 — beru #698–#702 (majitelovy poznámky z dneška) — desktop, Claude Code (bs3d-1b)
- Na mainu: #701 (merge 8908737f), #699 (41441044), #700 (ac48ca93), #702 (a4dc9b26; dvě smyčky místo pulzující, −39 dBFS), #697 po recenzi (5ce6972f), #495 uzavřeno (e7b29e39; 107 ze 118 nahrávek má vrstvu bicích, +150 MB). #698 (plot na louce) zůstává.
- majitel 2. 10.: #677 jen duny (zavřeno); #257 a #690 nová kapitola s menší prioritou; #696 doporučeno (b) až po releasu; priorita dne první release s online skóre (v0.3.0).
- ⚠ Přepnutí z větve s commitnutými vrstvami bicích na main je z pracovního stromu smaže (jsou v commitu).

## 2026-10-02 — v0.3.0, první release s online skóre — desktop, Claude Code (bs3d-1b)
- v0.3.0 vydáno (tag na 7a25c3e0, na majitelovo „ano“): zip 397 MB, ověřeno ze staženého zipu. Obrázek README: louka, Cannonball, 3840×1600 (volba majitele).
- Pi: snapshot DB, majitel smazal testovací skóre, `update-ceilings.sh` (130 tabulí, health 148). Otevřené: tři dev tabulky odstraní majitel.
- ⚠ Od smazání jsou tabule skutečných hráčů: žádné testovací zápisy z dev buildů ani scratch profilů.

## 2026-10-02 — beru #690 krok 2: nová kapitola v cirkusovém šapitó — desktop, Claude Code (bs3d-1b)
- Claim #690 krok 2. majitel: scéna je cirkusový šapitó, hudba ve stylu Colossu, bedny a broky z #257 patří do téže kapitoly.

## 2026-10-02 — #690 krok 2: kapitola Šapitó je v main — desktop, Claude Code (bs3d-1b)
- #690 krok 2 na mainu: pole `"softness"` v levelu (`0418b4a2`), 21. `SceneKind` šapitó (`f3d266a1`), hudba `bigtop` (`dae3b557`, 10 skladeb), 12. blok před Gridem, 10 levelů (`57030b4e`); uvolnění shluku čeká na kameru po průletu (`1a5a900b`).
- Otevřené: bedny z #257 (krok 2), majitelův verdikt na scénu, hudbu a levely.
- ⚠ Při příštím releasu spustit na Pi `update-ceilings.sh`, jinak online tabulky nových levelů odmítnou zápisy.

## 2026-10-02 — Šapitó po majitelových verdiktech, bedny z #257 — desktop, Claude Code (bs3d-1b)
- majitel: scéna „vypadá skvěle“; reflektory musí být vidět a pomalu se točit („podstatné je světlo“); hudba Juggler, Ringmaster, Spotlight, Swing, Finale, Carousel OK; Calliope, Trapeze, Tightrope až ve 2. kole; Clowns ve 3. kole (původní „infantilní“, prostorová verze bez „high“ momentu); chce víc sterea a prostoru.
- Lampy (`4d98eee6`), revize kapitoly (`111eb749`), #257 krok 2 (`8a84fde5`, `Crates.AddSpecs`). Detektory hudby ve scratchpadu `690music` (`drumonly.py`, `bands.py`, `peak.py`).

## 2026-10-02 — Šapitó po třetí revizi, cena reflektorů; beru #716 — desktop, Claude Code (bs3d-1b)
- Oprava: „cena lamp se nezměnila“ platila jen při 2560×1440; ve 3840×1600 (ssaa 2) stály 5,4 ms, po řezech +0,3 až 0,7 ms. ⚠ Průchod je omezený výpočtem, ne obsazeností, takže se vyplatí každý řez. Třetí revize `7b501eb3`.
- Claim #716 (herní strana, neúplné pokusy s 0 hvězdami); testovat jen proti lokálnímu BS3D-API, nikdy proti živému.

## 2026-10-03 — beru #712 #728 #729 #727 #719 #721 #722 #725 #723 (UI z majitelových poznámek) — desktop, Claude Code (bs3d-54)
- Claim devíti drobností z poznámek z 2. 10., každá na vlastní větvi v pořadí #712, #728 + #729, #727, #719, #721 + #722, #725, #723; neberu #703, #715, #711, #710, #720, #704, #706, #726, #730, #714, #696 (#716, #707 má bs3d-1b). Worktree `../BS3D-ui`.

## 2026-10-03 — #712 #728 #729 #727 #719 #721 #722 #725 #723 jsou na mainu — desktop, Claude Code (bs3d-54)
- Na mainu, všechno `shipped-awaiting-verdict`: #712 `954a0fdb`, #728 + #729 `dcc13de7`, #727 `4bfcd430`, #719 `4a10ea77`, #721 + #722 `fef5d043`, #725 `d0a54cbb`, #723 `cd5d4801`.
- ⚠ #712: pruh na plechu není okraj, `SolidBrush`/`FillRectangle` v Myře natahují texel `WhiteRegion` z atlasu; nová plocha v menu = `FlatBrush`. #719: podíl 5 → 9 % (`LevelGen --drops`). #721: pohled nahoru naměřen 52° na objektivu 60°, `GLANCE_HEIGHT` 28.
- ⚠ #725 a #723 neověřeny proti službě. ⚠ `LevelGen` zapisuje CRLF (`git checkout -- Game/Levels`); `CrateTests` spadl na CI (nestálý test).

## 2026-10-03 — červené CI: nestálý crate test opraven, zapomenutá větev `crate-test-deflake` sloučena; `Tools/play-latest.ps1` — desktop, Claude Code (bs3d-54)
- Nestálý `CrateTests` opraven (merge 5ba98ac3): bank shot zasáhne v kroku čtyři koule a `FirstTouch` bral první callback, ale Bepu vlákna hlásí kontakty v pořadí doběhnutí; assert teď bez pořadí; CI 10 ze 10 zelená. Zapomenutá větev `crate-test-deflake` sloučena a smazána.
- `Tools/play-latest.ps1` (merge e32411a2): build nejnovějšího mainu v `..\BS3D-play`.
- ⚠ Push commitu bez změny souboru na novou větev workflow nespustí (`paths-ignore` vidí prázdnou sadu změn jako „vše ignorováno“).

## 2026-10-03 — beru #708 #724 #703 #730 — desktop, Claude Code (bs3d-54)
- Na mainu, `shipped-awaiting-verdict`: #708 `b241fe7d` (sklo děla zapuštěné), #724 `af66820a` (korálky u ucha, `HandleHalfAngle`), #703 `3fe6c09a` (čipy menší), #730 `eba93575` (vizualizér, 18 LED, `PeakHold`).
- majitel povolil pro #725 a #723 testovací skóre na živý server pod hráčem „Test“: ověřeno a hráč smazán (`DELETE v1/players/<id>`); `Working` na skutečné síti nezachyceno.
- majitel: barva vizualizéru je záměrná výjimka z „jas, nikdy odstín“. ⚠ Horní pásma původních skladeb jsou tichá (vážení v `ProceduralJukebox`), otevřená otázka. ⚠ Skript na komentáře spadl na backslashi, `gh issue comment` vložil neopravená tvrzení: opraveno `--edit-last`.

## 2026-10-03 — poznámky majitele ke scénám a UI → issues #738–#762, uzavřeno #722 a #728 — desktop, Claude Code (bs3d-85)
- majitel: issues založit/uzavřít, implementace až později; nic jsem nebral. Zavřeno na verdikt #722 a #728 (nové #738); komentáře #703 (klávesy menší asi o 20 %) a #730. Nová issues #738–#762 (scény, Help pager, #741 gravitace per scéna, #742 Quarry, #743 jeskyně jako reference…).
- Z kódu: gravitace je všude zemská, Mars nemá žádný level, tmavý disk na Marsu je záměrně Phobos, Storm blesk se kreslí (#750 začíná otázkou „proč ho hráč nevidí“), jeskynní kapitola nemá speciální koule.
- ⚠ Neověřeno během běhu: příčiny #755, #740, #762, #761.

## 2026-10-03 — beru #734 (plate „Sending your score...“ s hotovou stránkou) — desktop, Claude Code (bs3d-54)
- Claim #734: plate „Sending your score...“ od prvního snímku výsledku, ne po `_revealSettled`; repro bez služby `userdata=` s `"online": true` a `"server": "http://127.0.0.1:9/"`.

## 2026-10-03 — beru #731 (průhledné středy písmen ve startovním logu) — desktop, Claude Code (bs3d-54)
- Claim #731: z `logo-9110-x4.png` vyříznout devět středů písmen průhledně, fialový obrys zůstane (rozhodnutí majitele v `Images/logo/README.md`); `cutout-alpha.py` ze skillu `design-references`.

## 2026-10-03 — #734 a #731 jsou na mainu — desktop, Claude Code (bs3d-54)
- #734 (`3567aed3`) a #731 (`d26476a9`) na mainu, `shipped-awaiting-verdict`: plate od prvního snímku; devět středů průhledných (průchod `counters 0.65` v `cutout-alpha.py`).
- Build s ON a bez serveru dřív nabízel „Online leaderboards are off“, teď „no score server“; možná příčina majitelova „plate už nevidím“, nepotvrzeno (řádek `[online]` v `Logs\last-run.log`).
- ⚠ #731 našlo 13 skvrn, ne 9 (čtyři stíny číslic v „3D“ jsou stejně tlusté): rozsah písma se zadává argumentem.

## 2026-10-03 — beru #711 (Exposure jako jas v %, 100 % = výchozí) — desktop, Claude Code (bs3d-54)
- Claim #711: řádek Exposure jako Brightness v % (70…130, výchozí 100), jedno `DEFAULT_EXPOSURE` v Core; `exposure=<f>` zůstává syrový násobič.

## 2026-10-03 — beru #710 (milost čáry delší než 1 s) — desktop, Claude Code (bs3d-54)
- Claim #710: `ClusterHang.BELOW_LINE_GRACE` 1,0 → 1,5 s (start, ladí se hrou, zůstane otevřené pro majitele); brána `--sag` sólo před a po v oddělených worktrees („ranking, not a verdict“).

## 2026-10-03 — #711 a #710 jsou na mainu — desktop, Claude Code (bs3d-54)
- #711 (`5a23d471`): Brightness v %, `BrightnessLadder`, `Settings.json` drží syrový násobič, `settings=brightness`. #710 (`9d2ddc31`): `BELOW_LINE_GRACE` 1,5 s. Obě `shipped-awaiting-verdict`; #710 zůstává otevřené pro majitele (pocit z hraní).
- Brána `--sag` přes 141 úrovní: 73 sagged při 1,0 s, 75 při 1,5 s; 44 z 45 končí klauzulí hloubky, takže delší čas pomáhá jen na mělkém držení čáry; zda dát okno hlubokému průniku, rozhodne majitel.
- ⚠ `LevelGen --sag` přepíše 141 levelů s CRLF (`git checkout -- Game/Levels`); `git branch -D` použit místo `-d`.

## 2026-10-03 — beru #714 (zip vydání: obvyklá cesta dovnitř) — desktop, Claude Code (bs3d-54)
- Claim #714: uklidit složku, vyzkoušet single-file self-contained bez trimování, jinak podsložka s launcherem. Otevřené: skutečně stažený zip s mark-of-the-web agent ověřit nemůže.

## 2026-10-03 — #714 je na mainu — desktop, Claude Code (bs3d-54)
- #714 na mainu (`b517dc46`): zip vydání je `BS3D.exe` + `Content`, `Levels`, `Music`, `Sfx` (5 položek místo 299 souborů), single-file bez trimování, `SatelliteResourceLanguages=en`, `Game.csproj` vyřazuje balíčky runtime 2.1.30 (táhne je `MonoGame.Framework.WindowsDX`); zip 397,9 MB proti 417,0.
- ⚠ Nezkoušeno: stažený zip z Release a SmartScreen. První tag po mergi je první skutečné publikování z tohoto rozložení: sledovat běh `release.yml`.

## 2026-10-03 — beru #713 (varování HLSL) — desktop, Claude Code (bs3d-54)
- Claim #713: sedmnáct odlišných varování HLSL, každé buď oprava s důkazem beze změny obrazu (shodný `.xnb`, jinak párové snímky), nebo ponechat s důvodem v komentáři.

## 2026-10-03 — #713 je na mainu (částečně, zdokumentováno) — desktop, Claude Code (bs3d-54)
- #713 na mainu (`134c08f3`), částečně: z 19 odlišných varování zbylo 11; opraveno beze změny obrazu Aurora, Glowworms, Earth, Tonemap `SampleScene`, `FireworkVS`, `HilbertPoint`, lávový `pow`. 6× X4000 ponecháno s důvodem (Space: překompilování posunulo mlhovinu až o 19/255; `ForestShadow`, `MoonDisc`, Circus). CI krok proti návratu nepřidán.
- ⚠ Dva běhy Testbedu téhož sestavení se liší skoro všude, `nopost` to nespraví: kontrolní běh + pevné oblasti + `D3DDisassemble`.

## 2026-10-03 — verdikty majitele: #730 #708 #724 #703 #725 #723 #734 #731 #711 uzavřeno, nové #764 — desktop, Claude Code (bs3d-54)
- majitel: #730 „Vypadá to skvěle!“ (+ nápad: paleta Spectrum jako easter egg na narozeniny hry), #708, #724, #703, #725, #723, #734, #731, #711 „Super!“; všech devět zavřeno.
- Nové #764 (easter egg); otevřená otázka den narozenin (první commit 30. 12. 2019, v0.1.0 18. 9. 2026; doporučení 30. 12.). Čeká na verdikt #710, #714, #713.
- Stránku před/po majitel pod aktuálním účtem nevidí (účty se střídají).

## 2026-10-03 — beru #763 (první spuštění se zeptá na přezdívku pro žebříčky) — desktop, Claude Code (bs3d-aa)
- Claim #763 na majitelův pokyn. majitel: souhlasem je jen pole pro jméno, „online“ = jen připojení k internetu, druhé Esc = Skip.

## 2026-10-03 — #763 je na mainu (první spuštění se zeptá na přezdívku pro žebříčky) — desktop, Claude Code (bs3d-aa)
- #763 na mainu (`2a3c0ed2`, oprava `b2b5726e`), `shipped-awaiting-verdict`: okno „NICKNAME“ po logu, `GameSettings.Online` je `bool?`, `InternetCheck`; majitel po prvním pohledu: tlačítko OK/Skip podle pole. Páky `nickprompt[=…]`, `nointernet`.
- ⚠ `Manager.Pop()` jen zařadí operaci: dvě odpovědi mezi snímky sundaly i hlavní menu (zámek `_answered`); každá stránka zavíraná `GoBack()` z více vstupů má stejnou díru.
- ⚠ `shot=` počítá od prvního snímku (~6 s po startu), `-WaitSeconds` pod ~16 s zabije hru uprostřed zápisu PNG. Nezkoušeno: skutečné klávesy, pad, odpojení sítě.

## 2026-10-03 — #none CLAUDE.md zkrácen a čtyři procesní pravidla (#768 cizí hráči) — desktop, Claude Code (bs3d-ec)
- majitel souhlasil se všemi čtyřmi body: `CLAUDE.md` z 48 111 na 27 930 B (merge 52f8b9f2; historie do `docs/build-and-release.md`, `libraries.md`, `repo-conventions.md`), sekce „How work is run“ (hotovo = hráč pochopí, strop 15 verdiktů, claim štítkem `in-progress` + komentář, CI po každém merge), #768 cizí hráči.
- Změřeno: od 19. 9. 308 issues vzniklo, 287 zavřeno, 21 s `shipped-awaiting-verdict` (strop už překročen).
- Otevřené (majitel): ekonomika nábojů power-upů (#765 §5), den narozenin (#764), cena Swapu a Brzdy, závora #768.

## 2026-10-03 — #696 je na mainu: rána se lepí do buňky ducha v 97,9 % místo 63,6 % — desktop, Claude Code (bs3d-aa)
- #696 na mainu (`ffbe9f42`, opravy `684fc1bf`), `shipped-awaiting-verdict`: příčina `TryFindFirstHitOnSegment` ztrácel koule; nový přístroj `ShotRig`/`ShotAgreementTests` (152 z 239 → 233 z 238).
- ⚠ Odmítnutá rána zůstala zaklíněná a odmítala se každý krok (184 řádků/s): `ContactEvents.MarkBounced`. ⚠ Pracovní strom má 235 souborů s CRLF; skript musí zachovat konce řádků.
- Otevřené (majitel): duch bliká (`PREVIEW_BLINK_*`), i když důvod pryč; cíl 0.3.1: hráči v0.3.0 s `"online": false` nový dotaz na přezdívku nedostanou, Pi `update-ceilings.sh` při vydání (10 levelů Big Top).

## 2026-10-04 — #739 je na mainu: „tohle jsi ty“ v žebříčcích je magenta; #696 a #542 zavřeny — desktop, Claude Code (6aa39589)
- #739 na mainu: majitel vybral magentu (240, 110, 230) pro „tohle jsi ty“ (`BS3DGame.BOARD_YOU`); majitel #696 otestoval („je to ok“) a nechal zavřít #542. Před v0.3.5: tabulka stropů 140 proti 130, 10 nových (Big Top).
- Rozhodnuto za majitele: glóbus `OnlineSignal` ve stavu Done následuje magentu (vratné jednou řádkou).
- ⚠ Spouštěč s `param([string]$Args)` předal hře nula argumentů a hra běžela na majitelově profilu (`$Args` je automatická proměnná); spouštěč teď běh bez `userdata=` odmítne.

## 2026-10-04 — #740 je na mainu: zvýraznění v seznamu už neodjede mimo obraz — desktop, Claude Code (6aa39589)
- #740 na mainu: `ScrollNavEntryIntoView` porovnával souřadnice obrazovky (`ToGlobal`) s `ScrollViewer.ActualBounds` ve widgetových; nově `NavViewport`, `KeepNavFocusInView`, `MenuPage.NavArrival`. Nezávislá recenze (dekompilovaná Myra 1.6.3) bez vady.
- Neověřeno: skutečná klávesnice, pad, tažení posuvníku, skutečné kolečko.

## 2026-10-04 — #738 je na mainu: nápověda má Back, Previous a Next v jednom řádku — desktop, Claude Code (6aa39589)
- #738 na mainu, `shipped-awaiting-verdict` (vzhled): v nápovědě Back vlevo, Previous a Next vpravo v jednom řádku, `BODY_SURROUNDINGS` 820 → 680; kurzor přichází na Next (`HelpPage.NavArrival`). Neověřeno: klávesnice a pad v běhu.

## 2026-10-04 — v0.3.5 vydán — desktop, Claude Code (6aa39589)
- v0.3.5 vydán na majitelův pokyn (anotovaný tag na `0fd280fb`, zip 417,2 MB); snímek louka + Pinwheel (volba majitele), poznámky `Images/releases/v0.3.5.md`. Pi: `update-ceilings.sh` přes SSH, health 158 tabulí. Ověřeno jako hráč (#714, první ostré publikování nového rozložení).
- Otevřené: dialog SmartScreen jen majitel.

## 2026-10-04 — verdikty majitele (13 zavřeno), #769, #770, #704 (Track pryč), #764, #702 — desktop, Claude Code (6edf655a)
- Fronta verdiktů 18 → 4. Zavřeno s OK: #712, #729, #727, #701, #721, #700, #719, #697, #698, #688, #488, #713 (šest X4000 zůstává bez CI hlídky), #764; #770 „vypadá to dobře“. Nové #769–#771. Merge: #769 `e9386660`, #770 `1ac24457`, #704 `5734620c` (Track pryč), #764 `a74f5478` (duhová paleta na 30. 12.), #702 `d004e4cb` (brum 89 % pod 300 Hz, přebarveno, +12,5 dB v 300 Hz–4 kHz), #699 `3d7db89e`.
- majitel: #699 nápověda se neukáže nad padající animací rány, která level dohrála; #704 Jukebox hraje všechnu hudbu bez ohledu na odemčení.
- ⚠ FontStashSharp láme i za `.`; `Scaled(0)` je 1 px; inscenovaná prohra potřebuje `lasers` i `nofocuspause`.
- Otevřené: #692 (koule uříznutého patra = zničené, ochrana horních pater po měření), #765; dávka #772–#784 jen založena.

## 2026-10-05 — noc bez majitele: #783, #784, #773, #772, #761, #762, #755, #715, #704 Jukebox, #782, #692 změřeno, #750, #743, #400 — desktop, Claude Code (6edf655a)
- majitel: „zpracovávej až do rána issues bez mého inputu“. Na mainu, `shipped-awaiting-verdict` (fronta 15 = strop): #783 `28c106f2`, #784 `9cd2e9b2`, #773 `be39126a`, #772 `7b642831` (MusicBake odmítá ticho), #761 `b7b0f1dd`, #762 `c021be81`, #755 `4370cc9d`, #715 `d258f4ce`, #704 `b1cbe8a3`, #782 `ae9f6283`, #750 `448f19ae`+`fbb495de`, #743 `05fa782b` (po review `870b9a9c`), #759+#760 `179abc3c` (`87d505a2`), #692 změřeno `a8c2f522`; #400 sedmý průchod čistý.
- ⚠ #750: kanál utopila vlastní záře, schoval ho ostrov, polovina úderů mimo záběr; krátkou událost přes `shot=` testovat nejde. ⚠ #743: záplava prázdných buněk na „uzavřeno“ špatně. ⚠ V noci audio výstupy vypnuté: Jukebox neslyšen.
- Review (merge `24441319`) našla chyby v #715, #704, #755, #750 (výhled úvodu přepisoval jediný slot). Otevřené: #743 „speciální koule“ a přenos vzoru do dalších kapitol jsou majitelovy.

## 2026-10-05 — verdikty majitele ráno: 11 zavřeno, rybky, Jukebox bez ambience, řez po patrech (#692) — desktop, Claude Code (6edf655a)
- Zavřeno OK: #750 (napsal „758: OK“, čteno jako #750), #784, #773, #761, #715, #782, #772, #783, #759, #760, #704. #759+#760 (`3b801672`): ryby plavaly pozadu (`sin(t + x)`). #704 (`a6082ed1`): ambience v Jukeboxu ztlumena. #692 (`b1cfea1c`): řez bere celé souvislé patro, horní 4 patra chráněná. #780 (`f00323d0`): záběr kolem vybavené střechy; review #692 opravila zákaz z náhledu o snímek starého.
- Otevřené (majitel): #743 speciální koule jsou jen v Eruption, Big Top, Mirage (jeskyně žádné); tutorial karta řezu (#705); zda žebříčky od The Tower začít znovu (`RulesVersion`).

## 2026-10-05 — #785 #786 port na Raspberry Pi 5: kostra GamePi je na mainu, issues #785–#793 — Pi (BS3DServer), Claude Code (296408e3)
- majitel (#785): hra poběží na Raspberry Pi 5 (Linux ARM64), nový stupeň Potato (v OpenGL buildu jediný), v menu „ARM64“, Potato smí pod nativním rozlišením (cíl 1920×1080 na 40–50 FPS), port zůstává v tomto repu, Potato má vlastní sadu GL shaderů (43 SM 5.0 efektů se nepřevádí).
- #786 (merge `1a5cb00d`): `GamePi/GamePi.csproj` překládá zdroje z `Game/`, nahrazuje tři soubory z `Game/Platform`; issues `pi-port` #785–#793.
- ⚠ `bad-echo-mgcb` 3.8.2.1 na ARM64 nejde (`libFreeImage.so` jen x64), GamePi pinuje `dotnet-mgcb` 3.8.5. ⚠ GL shadery nejde zkompilovat na Pi (mgfxc chce D3D): CI na Windows (#789).

## 2026-10-05 — #787 #788 #789 Potato na Pi: CI na ARM64, zamčený stupeň, vlastní GL shadery, 64 FPS na Girandole — Pi (BS3DServer), Claude Code (296408e3)
- #787 (`8b9dadbd`): job `gamepi` na `ubuntu-24.04-arm`, CA1416 chyba. #788 (`ff424e20`): `QualityLevel.Potato`, `Platform/QualityLock`. #789 (`f581b102`): Potato render path (`GamePi/Shaders` kompilované jen na Windows a commitované), Girandole 30,8 → 15,5 ms. Čeká na verdikt #788, #789.
- majitel: zvětšování/zmenšování místo ditheru chce i na Windows (→ #794). Nová #794–#797.
- ⚠ MojoShader a mgfxc se neshodnou u dvou dynamicky indexovaných uniform polí (tiše čte cizí sloty): jedno pole na shader. ⚠ `EnableMGCBItems=false` pro GamePi (Wine); `BuildStamp` hledal `Content\Shaders` s backslashem.

## 2026-10-05 — Windows ověření Pi práce (#788, #789) a `compile.ps1` na PowerShellu 5.1 — notebook, Claude Code (github-cf)
- Windows ověření (main `f581b102`): `Game.sln` čisté, 597/597 testů; Pi práce #786–#789 Windows cestu nezpomalila (APU Girandole +0,03, Pennant −0,04 ms). Oprava `c3069c46`: `GamePi/Shaders/compile.ps1` padal na PowerShellu 5.1 (`Encoding.Latin1`). Osm GL efektů se kompiluje za 3,5 s bajt po bajtu stejně jako v CI.
- Otevřené: #794 neberu, dokud neodpoví Pi session a majitel; #790 a novější merge měřením nepokryté.

## 2026-10-05 — #794 duch při míření je velikost, dither zůstává pro všechno ostatní — notebook, Claude Code (github-cf)
- #794 hotovo (CI zelená), `shipped-awaiting-verdict`: majitel: zmenšování jen při míření, odpojené koule a barevné přechody zůstávají dither (případně levněji). Duch = `ModelInstance.Dissolve` pod −1 (`GhostDissolve`), Potato `PotatoBallDither`; 617/617 testů, Pi A/B v šumu.
- GamePi jde postavit a spustit i na Windows (`dotnet build GamePi\GamePi.csproj`). Neověřeno: mrtvá váha na Windows, vzhled ducha u majitele.

## 2026-10-05 — dělba pi-port issues, #796 řádky Aberration a Film grain na Potato — notebook, Claude Code (github-cf)
- Dělba s Pi session: já #793 a #796, ona #792, #795, #797. #796 na mainu: na Potato řádky Aberration a Film grain `Off (tier)` (`BS3DGame.HasLensLooks`). #793 (rehearsal 37314115805): `release.yml` tři joby, Linux tar.gz 386 MB. Nevyzkoušeno: poslední krok `gh release create`.
- ⚠ Bez `~/.local/share` vrací `LocalApplicationData` na Linuxu prázdný řetězec (log ani save se nezapíše; opraveno v #798). `CLAUDE.md` chce doplnit větu o Linux balíku.

## 2026-10-05 — pad v ruce majitele: #378 #520 dvě nové vibrace a RT skip, #188 motory ve spouštích přes Windows.Gaming.Input — desktop, Claude Code (ce223402)
- majitel poprvé hrál s Xbox One S: strop moc výrazný, chyběla vibrace na dorazu a na „Cluster reached the line“, RT nepřeskakoval animaci kamery. Merge `b345b481` (#378, #520), `ce8159bd` (#188: `PadMotors` přes `Windows.Gaming.Input`), `20050fc6` (páka `pad`), `5b941aea`; #188, #378, #520 `shipped-awaiting-verdict` + `needs-gamepad`.
- ⚠ MonoGame 3.8.5 WindowsDX nemá u spouští mrtvou zónu (`TRIGGER_REST` 0). ⚠ `TargetPlatformVersion` samotné nestačí, TFM `net10.0-windows10.0.19041.0`. ⚠ Čip Cut ukazoval PlayStation „R1“: cmap neříká, co glyf kreslí.
- Neověřeno rukou: zjemněný strop, puls prohry, knock dorazu, RT skip, motory ve spouštích.

## 2026-10-05 — #790 #791 #792 #797 #798 na Pi: zvuk přes OpenAL, myš, ikona, Bubble jedna stěna, složka dat, zkouška tarballu #793 — Pi (BS3DServer), Claude Code (296408e3)
- Pi: #790 zvuk (`c6e0531e`, `92b4dc58`), #791 myš (zavřeno), #792 ikona (`834c38c7`, zavřeno), #789 obloha (`5a2e116a`), #798 složka dat (`678b4a5c`), #797 Bubble jedna stěna (`5114210b`, Pennant 22,2 → 18,7 ms); tarball #793 z čisté složky OK. Čeká na verdikt: zvuk ze správné strany (#790).
- ⚠ OpenAL Soft umístí hlas jen po `Play` (`AudioBackend.PlacesAfterPlay`); recyklovaný zdroj nese `AL_PITCH` posledního hlasu (`Pitch = 0`); `GetFolderPath(LocalApplicationData)` je na Linuxu prázdný bez `~/.local/share` (`DoNotVerify`).
- ⚠ Vlastní chyba: `git merge … | tail && git push --delete` smazal větev nemergnutou (obnovena `0b52eba8`): při merge nepoužívat rouru. VNC a prostředí kolísají čísla o 3–5 ms.
- Doplněk: #800 (`be373d97`, `padrt=`), RT skip `9f72ca74` (majitel hlásil, že nepřeskakuje; ověřit), #802 `8d1a569f`, čip Cut `711407c4` (⚠ box glyfu RB v PromptFontu sahá pod inkoust), #803 `cc5b5148`, návrh AA #804 (`de9098f0`, majitel chce předgenerované řešení); založeno #799, #800. ⚠ Moje spuštění hry brala majiteli fokus. Fronta verdiktů 15 = strop.

## 2026-10-05 — #795 světla scény na Potato, #801 rozlišení pro všechny verze, měření AA pro #804 — Pi (BS3DServer), Claude Code (296408e3)
- Verdikty majitele: duch (#794), strana zvuku (#790), lávové koule (#795) v pořádku, zavřeny. #795 (`80655071`): `PotatoModel` přebírá `SceneLight*`, sopka z `VolcanoLava`. #801 (`53962e2c`): řádek Resolution pro všechny verze, na Pi Auto. majitel: 50 FPS stačí; #804 dělá notebook (lem koulí).
- ⚠ Světla po pixelech stála 13 ms, po vrcholech ~1,5 ms. ⚠ Menší back buffer v bezokrajovém fullscreenu MonoGame 3.8.5 nejde nikde (3D do vlastního targetu). ⚠ V3D zapisoval hloubku offscreen targetu: `glInvalidateFramebuffer` (`TargetDiscard`). ⚠ VNC na Pi stojí 3–5 ms.
- Otevřené: online z Pi nezapnuto (lokální build nemá server, classifier odmítl úpravu `Settings.json`); verdikt k lemu a 720p (#801).

## 2026-10-05 — #804 AA pro Pi: lem koulí, ploutve hran, konstanty na CPU; Windows ověření #801 — notebook, Claude Code (github-cf)
- #804: lem koulí (`rims=`, výchozí 1), konstanty na CPU (pixel koule 391 → 250 instrukcí), ploutve (`fins=`, výchozí 0), `upscale=soft`. majitel: hráč nesmí vidět hranaté pixely ani rozmazané; měkký obraz nevadí. Windows ověření #801 v pořádku, oprava `e937b44d` (GamePi na Windows s `render=720`).
- ⚠ Barva lemu na okrajové normále je vrchol Fresnelu; ploutve na všech sítích 142 000 hran; práh 1,5° sebral ploutve oblým stěnám (0,25°). Bash heredoc padá na apostrofech.
- Pi: ploutve +1,05 až +1,43 ms (vypnuté), `upscale=soft` +1,5–1,8 ms. Otevřené: konkávní hrany nestavět, živé hrany na CPU; neověřeno švy odtoku, kola, jiný poměr stran než 16:9.

## 2026-10-05 — poznámky majitele z hraní založeny jako #805–#811 a sedm komentářů (jen založit, nic nestaveno) — desktop, Claude Code (bs3d-a3)
- Z osmi poznámek („založ issues“): #805 (pad ◀ ▶ na About/Jukebox, `PageSideways`), #806 (typografie), #807 (About na stránky; otázka Jukebox), #808 (Potato na Windows), #809 (mraky moc rychlé, spočítáno, neměřeno), #810 (vibrace při uvolnění skupiny od 20 koulí vždy plná), #811 (zvuk zarážky míření) + 7 komentářů (#748 hory a obydlí, #765/#767 náboje „za level“, #788…). Nic se nestavělo.
- ⚠ #808: Potato se volí při startu a není jisté, zda se GL shadery přeloží pro DirectX.
- Majitel šel spát a prosil, ať je desktop k dispozici Pi session.

## 2026-10-05 (noc) — claim #808; #804: lem je na mainu, druhý řez ploutví čeká na měření Pi — notebook, Claude Code (github-cf)
- CLAIM #808 (Potato na Windows) na žádost Pi session, větev `808-potato-on-windows`. #804: `pi-aa-rims` na mainu (`23acaf10`); `upscale=soft` +1,5 až +1,8 ms zůstává za argumentem; druhý řez ploutví `804-fins-live-edges` (`c8bdbc43`) NENÍ na mainu, čeká na měření Pi.
- ⚠ Límec ústí hlavně (#425) je vysoustružený naruby, `WindingCheck` to nepozná (issue #812). ⚠ Stínovací normály stěny B se četly z opačného konce hrany; tmavá čárkovaná linka na bočnici lafety byla ploutev uvnitř společné stěny.

## 2026-10-05 — noc na Pi: všech 14 scén nativně v 62 FPS, 10min zátěž, měření #804 (ploutve, měkké roztažení) — Pi (BS3DServer), Claude Code (296408e3)
- Po #804 (`23acaf10`) drží Pi nativní 1920×1080 na všech 14 scénách (61,8–62,0 FPS), 10 min na Cabinetu 62 FPS, 55–60 °C; zapsáno do `docs/game-shell.md` (`9fc8cea8`) a na #801. Druhý řez ploutví +0,53–0,67 ms (binning VERTEX_BIN 243 instrukcí na vrchol), `upscale=soft` +1,5–1,8 ms.
- Otevřené: verdikty #801, #804 a starší pi-port (#788, #789, #793, #796); řádek `server` pro online. Fronta verdiktů 17, nic vzhledového nezačato.

## 2026-10-05 (noc) — #808: Windows build umí `potato` (renderer Raspberry Pi na desktopu) — notebook, Claude Code (github-cf)
- #808 hotovo na větvi `808-potato-on-windows`, `shipped-awaiting-verdict`: `BS3D.exe potato` spustí hru přes cestu Potato; varianta (b) řádek „Potato (restart)“ je rozhodnutí majitele. Devět GL zdrojů se pro DirectX nepřeložilo (`Invalid profile 'vs_3_0'`), tři úpravy v `PotatoProfile.fxh`; `Prazsky.Shaders` je překládá podruhé do `Content/Potato/Shaders`.
- ⚠ DirectX 11 páruje výstupy VS se vstupy PS podle pořadí, ne sémantiky (konfety padaly černé); drží `PotatoShaderSourceTests`. ⚠ Snímky v sekundě zápisu PNG kazí `[fps]`.
- ⚠ Dvě věty `CLAUDE.md` neplatí (třetí `[build]` řádek, „no OPENGL build“); opraveno později (#808 b). Neověřeno: Display v Settings pod `potato`, jiná GPU než Vega.

## 2026-10-06 (noc) — #804: ploutve ve třetím řezu, výchozí zapnuté; #812 založeno — notebook, Claude Code (github-cf)
- #804 na mainu: ploutve (`fins=`) výchozí zapnuté (`POTATO_FIN_PIXELS` = 1), třetí řez stínuje ve vertex shaderu, na Pi +0,42 (Pennant) / +0,48 (Cabinet) ms. #812 založeno (límec ústí hlavně naruby); oprava na `812-collar-inside-out` nemergovaná, horní část kroužku po ní zesvětlá a tu barvu majitel ladil v #478.
- ⚠ Při merge #808 smazána vzdálená větev před pushem mainu (`;` místo `&&`): smazání větve jen jako poslední článek `&&`.
- Fronta verdiktů 18; lem i ploutve jeden verdikt na #804. Neověřeno: Girandole ve třetím řezu.

## 2026-10-06 — #804: švy odtoku (zlatý pás × kámen, límec × šachta) vyhlazené analyticky — notebook, Claude Code (github-cf)
- #804: švy odtoku vyhlazené analyticky (vrchol pásu nese výšku nad plochou a druh švu, technika `PotatoBand`, `SEAM_HEIGHT` 0,004); na Potato ve scénách se šachtou jde sklo před pásem.
- ⚠ Dvě slepé uličky: napůl pokryté pixely límce zapisovaly hloubku nad sklem (tmavé tečky); pás bez zápisu hloubky nechal bližší stěnu překreslit ret.
- Neověřeno: cena na Pi, třetí řez na Girandole; zbývá tmavé pruhy hlavně, kola a válečky.

## 2026-10-06 — #804: konkávní švy (tmavé pruhy hlavně) a kola mají ploutve; pás odtoku bez ploutví na okrajích — notebook, Claude Code (github-cf)
- #804: konkávní švy mají ploutev v rovině přivrácené stěny s hloubkovým posunem (`FinRasterizer`), kola ploutve po instancích, pás odtoku bez ploutví na okrajích (`EdgeFins.Wanted(rims: false)`).
- ⚠ `RasterizerState.DepthBias` je v [0, 1] hloubkového bufferu, MonoGame ho pro GL násobí 2^24: posun −2 dal každou ploutev před všechno.
- Neověřeno: cena na Pi (Pi session skončila s majitelovým limitem); zbývá válečky a okno hlavně.

## 2026-10-06 — #811 vrznutí, když míření narazí na zarážku; odpovědi majitele k #807, #765 a #811 — desktop, Claude Code (bs3d-a3)
- #811 hotovo (merge `40be61c6`), `shipped-awaiting-verdict`: `Game/Audio/AimStopSynth.cs` (stick-slip vrznutí + pufnutí vzduchu, 95 % energie v 300–4000 Hz). Neslyšeno, hák se ve hře nespustil (pomohlo by `push=` v `ScriptedPlay`).
- majitel: #807 „oba“ (přehrávač originální skladby na první stránku About, Jukebox v Extras beze změny); #765 „jednou za level, dlouhý efekt si můžeme dovolit“; #811 „udělej, co navrhuješ“ (dolně filtrovaný dech, ne syčení).
- ⚠ `python3 - <<EOF` v Git Bash visí (zástupný python z Windows Store). Fronta verdiktů 19 (strop 15), na výslovné zadání. #808 mezitím udělala peer session (`f71cae14`).

## 2026-10-06 — Pi: kurzor myši, online zapnutý, měření ploutví #804 — Pi (BS3DServer), Claude Code (296408e3)
- Kurzor myši na Pi (merge `4f53370d`): majitel hlásil „náhodnou změť pixelů“; ⚠ kurzorová vrstva Pi 5 je 64×64, `PointerCursor` bitmapu doplní na násobek 64; majitel potvrdil. ⚠ labwc kurzor do screencopy nevkládá.
- Online na Pi: s majitelovým svolením `"server": "https://scores.winphonew.eu/"` v jeho `Settings.json`; odeslání skóre zatím nikdo neviděl.
- #804 ploutve: třetí řez +0,28 až +0,48 ms, main s koly/švy/pásem +0,66 až +0,78 ms; #808 na Pi beze změny.

## 2026-10-06 — #none Pravidla verdiktů: co potřebuje majitele, jedna stránka, mlčení o vzhledu = souhlas — desktop, Claude Code
- majitel: „ta pravidla se mi líbí a zavedeme je“ (úzké hrdlo jsou verdikty): objektivní zavře agent s důkazem, `owner-decision`, verdikt = `shipped-awaiting-verdict` + `verdict-eye`/`verdict-hands`/`verdict-newcomer` (nováček mimo strop 15), jedna stránka OK / Vrať / Je mi to jedno, `verdict-eye` po 7 dnech `accepted-by-silence`. Dnešních 15 nejdřív 2026-10-13; dělba s bs3d-f3 (dávková stránka).
- Změřeno: zip vydání stažen 0–4× na verzi.
- ⚠ Python na stroji odmítl certifikát `scores.winphonew.eu` jako prošlý (openssl, curl i hra fungují); nezkoumáno.

## 2026-10-07 — #813 Send a Note: dokumentace a stav API; inventura rozdělané práce — desktop, Claude Code (bs3d-c5)
- #813 na mainu od 6. 10. (`59cf5aed`), dopsán ocas: oddíl v `docs/game-shell.md`, `Notes/` v `docs/formats-and-tools.md`; BS3D-API v0.1.22 s poznámkami nasazeno. Neověřeno: poznámka z release buildu do živé služby. Inventura: kromě mainu žádná větev ani stash, CI zelené.
- ⚠ Dávka verdiktů ze 6. 10. neměla štítky ani odkaz na issues (klasifikace bs3d-f3 čekala na svolení majitele). ⚠ Deník za září se zrotoval až 7. 10. (1,44 MB → 175 kB).

## 2026-10-07 — dávka verdiktů ze 6. 10. provedena, #762 sklo 0,2, testovací poznámka #813 — desktop, Claude Code (bs3d-c5)
- majitel („Ano, proveď to“): dávka provedena: `verdict-hands` #188, #378, #800, #520, #802; `verdict-eye` #780, #811, #257; `owner-decision` #803, #213, #692, #808; zavřeno s důkazem #769, #793; BS3D-API#10 zavřené. Fronta mimo nováčka 9.
- #762 (merge `cfbec3de`): vůle kresleného skla 0,1 → 0,2 (průnik do stěny změřen 0,055–0,069). #813 testovací poznámka do živé služby prošla (`[note] Sent 0c20ef9b (201)`). Otevřené: smazat ji může jen majitel (`admin notes`, `admin delete-note <id>`).
- ⚠ Admin stránka přes SSH tunel vrací 400, když lokální port není stejný jako vzdálený. ⚠ `shot=` ve 4K během průletu pádu posouvá hodiny hry.

## 2026-10-07 — verdikty majitele zapsané; #814 #813 #817 #800c #819 #821 na mainu; #816–#821 založené — desktop, Claude Code (bs3d-c5)
- Odpovědi majitele opsané do 13 issues: přijaté a zavřené #780, #188, #378, #520, #802; „je mi to jedno“: #803 pořadí zůstává, #213 rozvrh z #705 (Swap od 3. kapitoly, Brake od 4., Cut od 5.), #692 b červené odmítnutí zůstává; vráceno do práce #813 c, #811 a+c, #257 a, #800 c, #692 d, #808 a.
- Na mainu: #814, #813 (tlačítko Write to the Author), #817 (vibrace jen s rukou na padu, `GamepadRumble.HandOnPad`), #800 c, #819 (`CursorRateScale`), #821 (`WildcardCycle.Returning`), #808 b; založeno #816–#821 (majitel: wildcard = „chameleon“).
- Otevřené: na majitele čekají #813 a #821 (oko), #800 a #819 (ruce); fronta mimo nováčka 4.
- ⚠ Pad majitele je u desktopu připojený (každý start posílá podpis). ⚠ `shot=` ve 4K stíhá jeden snímek za 0,4 s. ⚠ Heredoc s více `cat > … <<'EOF'` spadl: těla issues přes Write a `--body-file`.

## 2026-10-07 — první poznámky z hraní přes #813: #823 a #824 založené — desktop, Claude Code (bs3d-0a)
- Z admin stránky poznámek (#813) pět poznámek (dvě testovací, č. 3 „I like! :)“ u Hilbertu bez issue a komentáře); založeny #823 (Gyroid: vyčerpaná kulička Cut zůstává uvnitř shluku, `LandCutter` ji vyřadí jen kill plane) a #824 (hudba Gridu „občas příšerně“, rodina `pulse`; `DescribeForNote` nemá stopu). Nic neimplementováno.
- ⚠ `aim=0:E:T` při elevaci ≥ 25° řez Cutem tiše nevystřelí (chrání horní patra, #692). ⚠ Hra běží ve 3840×1600 i s `width=1920 height=1080`, snímek ~15 MB.
- #692 (d) od `bs3d-c5` takových řezů přidá; komentář v #692.

## 2026-10-07 — #692 řez nejvýš do poloviny s náhledem, #823 odklizení řezací koule, kontrola po mergi našla chybu — desktop, Claude Code (bs3d-c5)
- #692 (merge `8cdd8e0c`, oprava `780d2dfe`): řez vezme nejvýš dolní polovinu shluku (`CutReach.IsProtected`), odříznuté koule se před výstřelem zesvětlí (majitelovo Vrať u d). #823 (merge `e1718de8`, zavřeno): vystřelená řezací koule se odklidí hned v kroku zásahu. #817: `HandOnPad` z tutoriálu při stavbě levelu.
- ⚠ Kontrola po mergi našla v #692 chybu: háček vlny bral `max` z vlny a záře, nula u nerozsvícených koulí přebila zápornou výstražnou vlnu stropu a červená zhasla (~45 min na mainu); oprava `ClusterRipple.WithCutPreview`.
- Otevřené: stránka verdiktů v5 čeká na #813, #821, #692 (oko) a #800, #819 (ruce).

## 2026-10-07 — #822 boční profil: koule v trychtýři neblikají — Raspberry Pi 5 (BS3DServer), Claude Code
- #822: řez pod čarou smrti kreslil volnou kouli podle znaménka svislé rychlosti čteného každý snímek, v trychtýři se otáčí při každém nárazu (8 přepnutí u 29 koulí). Oprava: `BallMarker.FromAbove` se rozhodne jednou, `PhysicsBall.RoseAboveDeathLine` se zamyká v `StepPhysics`; po ní 0 přepnutí.
- ⚠ Skriptované výstřely se během úvodního průletu (9,5 s na Pennantu) nevystřelí, `fire=` začínat až po něm; střely ven z ostrova polyká červí díra (#230) po třech netrefených. Střela, která nikdy nebyla nad čarou, se pod ní už nekreslí.
- Ověřeno jen na GamePi (Potato); Windows zkompiluje CI.

## 2026-10-07 — #816 zaškrtávátko pro dvoustavové řádky — Raspberry Pi 5 (BS3DServer), Claude Code
- #816 na mainu, čeká na verdikt (oko): dvoustavové řádky Nastavení mají místo On/Off políčko s fajfkou (`MenuPage.CheckBox`, kreslený `CheckGlyph`, protože Anton nemá ☐ ✓); slova zůstala u Resolution, Quality, Brightness, Sky, FPS limit, hlasitostí, Rumble a Online scores; výhrada se ukazuje ztlumeně jako „(tier)“ / „(locked)“.
- ⚠ `settings` s novým userdata ukazuje řádek FPS opačně než skutečný stav (`_info.Visible` true, políčko nezaškrtnuté), platilo už před #816, nehledáno.

## 2026-10-07 — #808 Potato na řádku Quality a řádek Scenery, opravy po revizi — desktop, Claude Code (bs3d-c5)
- #808 na mainu (merge `b772ac17`, opravy `92cb9d41`), čeká na verdikt (oko): Quality cyklí i Potato (`QualityLevels.NextOnRow`, zapne se až dalším startem přes `QualityLock.ChooseAtPotato`); nový řádek Scenery (`GameSettings.Scenery`, vypnutá nechá oblohu, ostrov, sklo, shluk, dělo; Quality pak čte `Custom`); páka `settings=auto`.
- ⚠ Revize po mergi našla pět chyb (Auto quality nad čekajícím Potato, Pi krok rozlišení na Potato z nastavení, prohlídky/erupce/zvuky bez scenérie, zastaralé věty). ⚠ Dvanáct řádků Display se na 1600×900 nevešlo, Back uříznutý: `ROW_PADDING_Y` 18 → 12.

## 2026-10-07 — #818 Konami na padu, #820 paprsky u speciálního náboje, druhá kontrola #808 a #818 — desktop, Claude Code (bs3d-c5)
- #818 (merge `5ceb2651`, čeká na ruce): Konami kód i na padu, levá páčka jako ťuknutí (`StickTap`), páka `padpress=<start>:<krok>:<tlačítka>` (potřebuje fokus okna). #820 (merge `11c8f17b`, oko): paprsky u ústí pro chameleona a řezací kouli, ~0,015 ms ve 3840×1600; na Potato nejsou.
- ⚠ Druhá kontrola (opravy `66cdfe69`): `PadTouched` četl spoušť nad nulou, ale MonoGame 3.8.5 dává spouště bez mrtvé zóny, takže dělo nikdy neusnulo (teď tlačítka jmenovitě, spoušť přes 30/255); Scenery vypnutá nechala záblesky erupce; Resolution na Potato se vracelo na „Auto“ (`ResolutionHasAuto`).
- ⚠ Pennant High 3840×1600 měří 14,0 ms (nad rozpočtem 75 Hz 13,33), není to regrese: stejným příkazem dal 14,2 ms už commit `7a78e7b9` z 2. 10.; údaj 12,9–13,25 byl z jiného příkazu.

## 2026-10-07 — #735 karty druhů s živou koulí, #811 „e-e“ u dorazu, třetí kontrola — desktop, Claude Code (bs3d-c5)
- #735 (merge `f853d1d3`, opravy `8e218d9d`, oko): jedenáct karet druhů se skutečnou koulí ve 3D scéně (`kind-…` v save). #811 (merge `b8dd1f0a`, ucho): odmítnutý výstřel místo vrzání dva tóny „e-e“. Help (merge `0026d9ab`) opraven (tvrdil, že power-upy ve hře nejsou).
- ⚠ Kontrola našla tři chyby v #735: koule mimo záznam pohybu se rozmazávala (`MotionBlur.fx` dává prázdným pixelům rychlost kamery, teď `_motionPinnedBack`), blok rozdělil `DrawTutorial` od dokumentace (CS1572), broky pátý v pořadí. ⚠ Test dvou úderů nesmí číst konstanty syntézy, mutant prošel. ⚠ GitHub jednou odmítl push na main bez souběhu, podruhé prošel.
- Otevřené: při přesném míření okraj obrazu kouli rozostří (#214).

## 2026-10-07 — #257 broky jako olovo a Juggler, #705 power-upy po kapitolách a prohození, #805 řady v menu, čtvrtá kontrola — desktop, Claude Code (bs3d-c5)
- #257 (merge `6967b8cb`, oko): broky jako 38 olověných kuliček (šeď 0,74, emise 0,22; 0,46 byla ve stanu černá), Juggler s jádrem z celého prostředního patra; změnil se jen `Juggler.json`. #705 (merge `748aa787`, `407f69bc`, opravy `d2558460`, oko): Swap od 3. kapitoly, Brake od 4., Cut od 5., karty nástrojů nezávislé na kapitole; Cut uzavírá #692. #805 (merge `5e6b671c`, ruce): šipky doleva/doprava posouvají kurzor po řadě.
- ⚠ LevelGen píše CRLF: skutečné změny ukáže `git diff --ignore-cr-at-eol`, `git add --renormalize Game/Levels` stav srovná. ⚠ Karta nástroje, kterou hráč nechal vypršet, se vracela na každém levelu. ⚠ Python v heredocu v Bash toolu ztratil `\` (`\b` = backspace v cestě): skripty s cestami přes Write.
- Otevřené: tabulka stropů se s Juggler mění, při příštím vydání `update-ceilings` na Pi.

## 2026-10-07 — #none v0.3.6 vydán — notebook (THINKPAD), Claude Code (github-a9)
- v0.3.6 vydán na majitelův pokyn (anotovaný tag `07f87913`, běh Release 37664492570), první ostré publikování se dvěma balíčky (#793): zip 422,3 MB, `linux-arm64.tar.gz` 404,1 MB. Stropy: změnil se jen Juggler (#257), `/v1/health` boards 158 → 159; `update-ceilings.sh` spustil majitel přes Connect (SSH z notebooku zamítl klasifikátor).
- ⚠ Notebook má displej 1920×1080, přesto `windowed width=3840 height=1600` dá PNG 3840×1600: snímek vydání jde udělat i mimo desktop.
- Otevřené: tarball na Pi, SmartScreen a odeslání skóre z vydaného buildu neověřeno.

## 2026-10-08 — #832 řádek Auto quality na Pi říká „(resolution)“; založena #828–#833, port pozastaven do #737 — Pi (BS3DServer), Claude Code (c5ec9e80)
- majitel: port na Pi pokračuje až po #737 (geometrický vzor koulí pro všechny styly, aby se Potato koule nestavěly dvakrát). Založeno #828–#833 (#830 owner-decision: nativní 1080p, nebo 720p s ~5 ms navíc; #831 duch zaměřování roste do celé koule, přesah majitel odvolal).
- #832 (merge do mainu): `Auto quality` na zamčeném Pi čte `On (resolution)`, dokud je Resolution Auto (varianta (a), volba majitele); je to tatáž sonda `TuneQualityToFrameRate`, jejíž jedinou pákou je krok rozlišení (#801). Tip: `settings=auto` je nejkratší cesta na DISPLAY.
- Otevřené: majitelův běh v0.3.6 v okně 1600×900 naměřil 37 FPS a šel na 720p, fullscreen drží 62; příčina nezměřena (A/B okno proti fullscreen, poznámka v #832).

## 2026-10-08 — #834 krok rozlišení na Pi až po 10 s pod prahem — Pi (BS3DServer), Claude Code (c5ec9e80)
- majitel: sonda má snižovat rozlišení jen při dlouhodobě špatném výkonu (při manipulaci s oknem FPS dočasně klesne). #834 (merge do mainu): `RESOLUTION_SUSTAIN_SECONDS` = 10 s oken za sebou pod prahem; desktopový stupeň má dál jednookenní verdikt (#298). A/B na Pi: v0.3.6 krok po 5 s, větev až po 9–10 s trvalé zátěže.
- ⚠ FPS na Pi nesrazí `xdotool windowmove` ani `yes`, zátěží je jen druhá instance hry. ⚠ `wait` bez PID čeká i na hru na pozadí (okno otevřené 36 min): vše pod `timeout`, `wait $PID` jen na zátěž; `pkill -f` zabije vlastní shell, použít `pgrep -x BS3D`.

## 2026-10-08 — #833 Settings: popisky doprava, hodnoty doleva, šev na středu — Pi (BS3DServer), Claude Code (c5ec9e80)
- majitel: popisky doprava, hodnoty doleva, šev na středu. #833 (merge do mainu): `SettingsPage` má oba sloupce stejně široké, popisky vpravo, hodnoty vlevo, nadpis CAMPAIGN nad švem; viděno na Pi ve 1600×900, 1280×720 i 1920×1080.
- ⚠ Snímek záložek AUDIO a CONTROLS: `settings` + xdotool klik v 10. s, `shot=14`, `timeout 22`; s `shot=11` a `timeout 14` snímek nevznikl.

## 2026-10-09 — verdikty 9. 10., #800 (h), #835, #836, #837 — desktop (RDT-PC), Claude Code (bs3d-69)
- majitel odpověděl v chatu na 11 verdiktů (stránka „Verdikty 6. 10.“ patří druhému účtu): OK a zavřeno #820, #811, #809, #808, #735, #705, #257, #378, #818, #805; #800 (g) přijato jako tvar, puls záložek ještě slabší → (h) (merge d01e4ca4, verdict-hands).
- #835 (merge c260f22f): stálá vibrace u dorazu (`GamepadRumble.Hold`, `ElevationStrain`). #836 (merge 9a258ad1): vibrace pro Brake a bombu. Obě verdict-hands. #837 (merge 14053827, zavřeno): z-fighting rohů beden, trámy končí na `half - BRACKET_BEAMS * beam`.
- ⚠ #835 skriptem nevyvolatelné: `aim=`/`sweep=` strain nezvedají, `padpress=` zná jen levou páčku a tlačítka.

## 2026-10-10 — projektová revize: štítky `priority-now` a `parked`, brána šíře do #768, #838 itch.io, podklady k #768 — desktop (RDT-PC), Claude Code (bs3d-5a)
- majitel po revizi: zmrazit šíři do prvního kola cizích hráčů (#768) a zavést štítky `priority-now` a `parked` (pravidlo v `CLAUDE.md`, důvody v `docs/repo-conventions.md`). `priority-now`: #768, #737, #692, #736, #742, #743, #799; `parked`: 34 issues (Pi #785/#828–#830, #690, #741, #827, #251, #230, #292, #717, #771…). Zip stažen 13× za šest vydání, na deskách tři přezdívky.
- #838 (owner-decision): majitel si itch.io nejdřív prostuduje sám, agenti nic nestaví. #768: podklady v komentáři issue, oslovení hráčů je na majiteli.
- Otevřené: dokumentační dieta (vlastní issue; majitel chce kratší dokumenty a deník rozdělený tak, aby se nečetlo všechno).

## 2026-10-10 — #826 Quit se ptá: stránka „Quit BS3D?“ nad pauzou i hlavním menu — desktop (RDT-PC), Claude Code (bs3d-52)
- #826 (merge do mainu, verdict-eye): obě položky Quit otevřou `QuitConfirmPage` (Cancel první, Escape/B = pop; Restart, zavírací tlačítko a Alt+F4 beze změny); dodělán kód necommitnutý včerejší session na `826-quit-confirm`; páka `quitconfirm[=<s>[:<kroky>]]`.
- ⚠ Nad hlavním menu je deska průsvitná a Cancel/Quit se od ní skoro neliší (50 proti 42); posoudit musí majitelovo oko.
- Otevřené: #825 (merge 6e6ccd98 z 9. 10.) zůstal s `in-progress` bez záznamu; štítek sundán, komentář: škálovaný displej (125 %) a navazující body (velikost okna, `WM_DPICHANGED`, kurzor, ikona) nikdo neověřil.

## 2026-10-11 — #799 #826 klávesy a tlačítka padu přímo v menu — desktop (RDT-PC), Claude Code (bs3d-90)
- majitel: #826 OK (zavřeno 10. 10.).
- #799 (merge do mainu, verdict-eye): `PromptGlyph` kreslí glyf podle ruky, která naposledy ovládala menu (`PromptsForPad`; pauza přebírá ruku hry, level ruku menu). B/Esc na Back, Resume a Cancel; LB/RB nebo ←/→ u záložek Settings, u Previous/Next v Help a v tabulkách levelu a u šipek kapitol; A/Enter na položce s kurzorem; pravá páčka nebo kolečko u „Scroll“; sloupec padu v Help › Controls. Tlačítko Menu na padu pauzuje i pouští. Páka `menucursor=`, `pad` připíná i menu.
- ⚠ PromptFont: LB/RB jsou U+2B98/2B99 (U+2198/2199 z HUD je poloviční a u záložek se ztrácí); jedna šipková klávesa jako klávesa neexistuje.
- Otevřené: přepnutí ruky na skutečném vstupu (chce fokus okna), stránka tabulek levelu (otevře se jen online), pad v rukou majitele.
- ⚠ Review po merge (#799) našla dvě díry, opraveno: první level po spuštění přepsal ruku padu na myš (ukazatel před prvním vystředěním se bral jako pohyb, teď až po `_mouseAim.Initialized`); Escape a B/Start v jednom snímku vrátily o dvě obrazovky (navigace snímek vynechá, když Escape odešel). Obojí potřebuje fokus okna, během hry neověřeno.

## 2026-10-11 — #839 dokumentační dieta hotová — desktop (RDT-PC), Claude Code (bs3d-5a, bs3d-68)
- Sedm velkých dokumentů je index + `docs/<oblast>/*.md` (merge testbed fdee8dbd, rendering 8cef1117, game-session f190d933, formats-and-tools 31cc931d, scenes ede53a0d, game-feedback 3c00b33c, game-shell 612fc476). `docs/` mají 475k → 156k slov, deník 277k → 46k (ad7af939). Index mapuje každý starý nadpis na soubor, takže citace v kódu dál platí. Plné znění je v gitu (`git show ad7af939:docs/<oblast>.md`, u game-shell/-feedback `f1183681`).
- `CLAUDE.md`: číst index a jen dotčené pod-dokumenty; dokument říká, co platí teď, historie patří do issue a commitu. SemanticSearch a DocDrift čtou `docs/` rekurzivně.
- ⚠ 11–13 souběžných agentů vyčerpalo limit sezení dvakrát; nejvýš 3 najednou a pevný strop slov předem.
- Otevřené: scenes, game-feedback a game-shell mají ~36 % původní délky (ostatní 22–28 %); další utažení je možné, ale úkol čte jen 2–6 tisíc slov.


## 2026-10-11 — #842 #843 limit míření a krmení vysokých levelů — desktop (RDT-PC), Claude Code (bs3d-b1)
- majitel: limit míření nahoru platí na vysokém levelu stejný od začátku a zmizí, jakmile mapa klesne dost nízko; modré krmení jen na dostřel, nízko smí strop jen červeně. Založeno z jeho hraní #840–#848.
- #842 (merge 2ab13c59): pásmo se drží z načtení, kroky limit nepřepočítávají; na 80° se zvedne, jakmile celý cluster visí v záběru (Plume na 3. kroku).
- #843 (merge a4ed6ac1, opravy z review 03d199be): krmení jen do dosahu (dvoubarevný Totem: pás 6 úrovní 0 kroků místo 7, 12 úrovní 6 místo 14); Brake na vysokém levelu odmítnut, když by zdvih vynesl spodek z dosahu (`BrakeCanLift`).
- ⚠ Rozptýlené skriptované střely spodek vysokého levelu nikdy neuvolní; krmení vyvolá jen kopie levelu s jednobarevným spodním pásem (`levelfile=`).
- Otevřené: obojí verdict-hands; #844 staví na `BrakeCanLift`.

## 2026-10-12 — #847 #848 #844 výběr levelů a čipy power-upů — desktop (RDT-PC), Claude Code (bs3d-c2)
- majitel: u šipek kapitol jen ikony padu (LB/RB pod podložkou), klávesové ←/→ vedle šipek ne; Brake jen při stojícím stropu; čip má rozlišit „vyčerpáno“ a „teď ne“.
- #847 (merge 94e5821c): šipky a puntíky jako kreslené tvary (`MediaGlyph.Back`, `PipGlyph`), vystředěné na podložkách. #848 (merge b40e245c, review f1787633): pad chodí po mřížce (`PadWalksGrid`, `NavWalk`), kapitoly LB/RB. #844 (merge c8e5d646): `CanBrake` čeká na stojící strop, čipy ready / not now / used.
- ⚠ Noc 12. 10.: tři resety při souběhu ACE-Step a běhů hry; těžké příkazy obou sezení přes mutex `Global\BS3D-heavy-job` (`heavy.ps1` noční session).
- Otevřené: chůze padem neověřena (skriptovaný pad potřebuje fokus), desky levelu jen online; verdikty #847/#844 eye, #848 hands.

## 2026-10-12 — #742 hloubka Lomu — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #742 (merge následuje): Lom (Měsíc, levely 64–69) přestavěn v `Block06_Quarry.cs` — Archway, Cornice, Hoist, Eclipse, Geode, Locket, každý s jedním nápadem, který zbraň vidí (oblouk, převis, kámen na bílém klíči, dvě hloubky, uzavřený krystal, krystal na šňůrce). Soubory si nechaly jména, hvězdy hráčů zůstávají. Šest levelů nese 1 583 koulí oproti 1 590, 137 skupin místo 185. Mosaic, Prism a Hopper beze změny.
- Bez majitele (spí), konzervativně: Colossus je ručně kreslený, proto zůstává; jeho přestavba čeká na rozhodnutí v #849 (owner-decision).
- ⚠ Opravný průchod obarvil okrajové koule u krku Locketu na bílou; dlaždice kamene se teď počítají od `LOCKET_STONE_TILE_FROM`, takže bílý je jen krk. Geode se pomalu kroutí kolem svislé osy (dělala to i těžší v1).
- Otevřené: verdict-eye; na Pi spustit při příštím vydání `update-ceilings.sh` (desky šesti levelů se resetují); nikdo je nehrál rukou.

## 2026-10-12 — #840 stožár na střeše v intru města — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #840 (merge následuje): střešní záběr intra City a Neon City drží celý stožár i s majákem (`CityIntroShots.FrameMast`): vršek majáku 6 % pod horní hranou, pata 15 % nad dolní, aby nezmizela pod nápovědou k přeskočení. Kompozice z #780 zůstává, kde se vejde, jinak kamera stoupá po půl jednotky. `CityRooftops` dává `RoofShowcases` bez zařízení; v neonu hlásí vyšší ze dvou stožárů. Test `TheRooftopShotKeepsTheWholeMastAndItsBeaconInFrame` (tři rozlišení) se starým záběrem padal ve 394 ze 448 oblouků.
- Bez majitele (spí), konzervativně: dvojitý stožár v neonu je vada už na mainu, zapsaná jako #850, ne opravená tady (přestavěla by všechny neonové střechy).
- Otevřené: #850; jak čte strmější pohled na nejvyšší stožáry, viděno jen ve snímcích.

## 2026-10-12 — #831 duch dopadu na celou kouli — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #831 (merge následuje): duch v náhledu dopadu roste na přesně celou kouli a nikdy nad ni, s gumovým odskokem. Čistá funkce `GhostBeat.Scale(WallClock)` (`Game/Screens/GhostBeat.cs`) nahradila sinus 0,5 ± 0,22 při 2,2 Hz: rychlý pružný nárůst, výdrž na 1 díky jednomu `MathF.Min(1, …)`, propad na `LOW` = 0,30 a pomalejší dosednutí. 14 testů `GhostBeatTests`, shadery a Potato okraj beze změny.
- majitel: celá koule smí vypadat jako už přítomná koule (přijal to v issue).
- ⚠ Review: 1,6 Hz bylo totéž co `MUZZLE_MARK_HZ`, duch a límec u ústí tepaly ve fázi; teď 1,8 Hz a pravidlo „různé frekvence“ stojí v obou docs komentářích i v docs.
- Otevřené: verdict-eye; živý pohyb (gumovost) a Pi neviděny, snímky jen velikosti.

## 2026-10-12 — #736 Siluety jako tělesa — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #736 (větev `736-silhouettes-depth`, merge následuje): všech deset levelů Polární záře (41–50) přestavěno v `Block13_Silhouettes.cs` na tělesa, každé visí jinak: Fish reliéf ze stínohry, Umbrella rotační s 12 klíny, Bell s červeným srdcem za skleněným dnem, Cat loutka před plátnem, Teapot s uzavřeným čajem, Key na desce, Rocket vodorovně s okénky, Coronet na pěti hrotech, Guitar ve stínohře se skleněnými strunami, Anchor na pažnici. Soubory si nechaly jména.
- Majitel (11. 10.): celá kapitola najednou, první speciál kampaně je sklo. Sklo poprvé na Umbrelle (42), Fish (41) nese kartu Řezu; karty druhů i Nápověda řazeny podle prvního setkání.
- 3 116 koulí proti 3 254, 150 skupin proti 211, 356 střel proti 524.
- ⚠ Revize: sklo vedle uzavřené odměny ji otevře jednou ranou (okénka Rakety se dotýkala jádra). `RevealFaults` teď sklo u odměny odmítá; jádro Rakety visí v zadní půlce kabiny (6 koulí místo 10).
- ⚠ Kotva na tenkých šňůrách se při startovním zhoupnutí převrátila přes čáru bez výstřelu (sag 5 z 5); tenké vazby u těžkých dílů nedělat.
- Otevřené: verdict-eye; na Pi při příštím vydání `update-ceilings.sh`; nikdo je nehrál rukou.

## 2026-10-12 — #850 neonové střechy: jedna mřížka buněk na střechu — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #850 jde na main (merge následuje), `shipped-awaiting-verdict` + `verdict-eye`: neonové extras sdílejí s denní výbavou jednu mřížku buněk střechy (`CityRooftops.Arrange`), žádné dva kusy v jedné buňce, stěžeň nanejvýš jeden; `CityRooftopsTests` to drží nad skutečným rozložením (viděn selhat: 29 549 z 226 109 kusů). Denní výbava beze změny.
- `NeonExtra` zůstává 1,45: stěžňů −9,8 %, 5G sloupů −11 %, talířů beze změny; z herní kamery (pět pohledů Testbedu + hra, `sceneseed=3`) se rozdíl nepozná. 1,70 by stěžně vrátilo za +15 % talířů (`docs/scenes/city.md`).
- ⚠ Hustotu střech měř po druzích, které herní kamera vidí jako siluetu (stěžně, talíře, sloupy), ne obsazenými buňkami: ty rostly s klimatizacemi.
- Otevřené: verdikt majitele (1,45 nebo 1,70); intro neonu při daném `sceneseed` teď rámuje jinou střechu.

## 2026-10-12 — #825 kontrola DPI na škálovaném displeji — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #825 (merge následuje): sloučená oprava DPI ověřena živě při 125 %: `[display]` hlásí 3840x1600 a 120 DPI, aware; řádek Rozlišení „3840 x 1600 (native)“ proti 3072x1280 v unaware běhu; back buffer = fyzický klient. Kontrola našla vadu: po každé změně měřítka nebo přesunu mezi monitory dal WinForms do malého slotu titulku velký rámec ikony. Nově `DisplayRefresh.WatchScale` (na Pi prázdné) ikonu znovu nasadí a `WindowIcon` bere rámce pro DPI okna (`GetSystemMetricsForDpi`). Z review: řádek Rozlišení v Nastavení teď sleduje změnu velikosti okna.
- Bez majitele (spí), konzervativně: kurzor, výchozích 1600x900 a pixelová velikost okna při změně měřítka nechány.
- ⚠ Callback přes `BeginInvoke` z `Form.DpiChanged` spoléhá na pořadí WinForms 10 (událost, ikona, rozměry).
- Otevřené: #851 (řádek do release notes, kontrola na skutečném notebooku se 125 %); co DWM skutečně vykreslí na panel, nikdo neviděl.

## 2026-10-12 — #737 druhé vodítko barvy, prototypy — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #737 jde na main (merge následuje), `owner-decision`; jen přepínatelné prototypy, výchozí vypnuto: `colourcue=<none|motif|shape>` v Testbedu i Game. Stříbrná, hnědá, bílá, žlutá (černá bez figury); (a) malovaný motiv, (b) vyřezaný tvar, na vinylu, vlně a mramoru. Vypnuto = 45 vstupních bodů efektu instrukci po instrukci jako main. Stránka: https://claude.ai/artifact/FbJry9FNp1JdGeREWxfkZ6
- ⚠ Koule je z velké části vlastní emise: emise v čisté barvě smyla tmavý inkoust (vlna pod kopulí 13, vinyl béžová/žlutá i pod kopulí 1). Motiv svítí svým inkoustem.
- ⚠ Vyřezaný obrys je jen tak dobrý jako síť: na LOD2 žebro padlo mezi vrcholy. Vyřezané barvy jdou na LOD0, cena +0,2–0,3 ms.
- ⚠ Motiv táhne světlé barvy k tmavým (bílá/hnědá až 4,8, bílá/stříbrná 7,9–11,6 dE).
- ⚠ `alt=colourcue=` cenu tvaru změřil nesmyslně; měř po procesech.
- Otevřené: majitel volí (a), (b), obojí; pak 13 barev, HUD fronta, Pi (#828: (a)).

## 2026-10-12 — #810 tlumené dunění při uvolnění skupiny — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #810 (větev `810-release-rumble-curve`, merge následuje), `shipped-awaiting-verdict` + `verdict-hands`: přímka uvolnění (plno při 20 koulích) nahrazena v `LandingRumble` křivkou ln(1+n/10) až do 250 koulí (≈ p99 z ~20k uvolnění LevelGen `--drops`, medián 19–27). Strop: těžký motor 0,5, lehký 0,65, délka 0,26–0,9 s. `PadChannel` drží dvě vrstvy: běžné kopance (beze změny jako po #378) a uvolnění s vlastním doznívání; uvolnění motor nikdy nezvedne nad max(strop, běžné kopance). Ťuknutí dopadu je přesně to z #378.
- Bez majitele (spí), konzervativně: pět bodů z těla issue, řádek Rumble v Nastavení beze změny.
- ⚠ Review: kopanec chrastění zkracoval 0,9 s ocas uvolnění na svou délku a výstřel na ocasu přeskočil strop (0,88); opraveno vrstvami, testy viděny selhat.
- ⚠ Argument `pad` nepřipne ruku na pad, jen výzvy (#852), skriptovaný běh dunění nedosáhne.
- Otevřené: verdict-hands na padu; #852.

## 2026-10-12 — #806 typografie: pravidla a zlom odstavce — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #806 (merge následuje), `shipped-awaiting-verdict` + `verdict-eye`. Pravidla v `docs/game-shell/menu-look.md`, „Typesetting“: míra 67 znaků (`MENU_PROSE_MEASURE`), proklad 1,46 em, odstavec 1 em, pomlčka s mezerami, ’ “ ”, …, nedělitelné mezery. `SpaceWrap` neplní hladově: nejméně řádků, žádné osamělé slovo na konci, rovný okraj. `MenuPage.Paragraph/Aside/Prose`, `CopyRules` hlídá tutoriál. Help, Settings, přezdívka, karty tutoriálu, About (jen znaky), stavové řádky žebříčků a výsledku.
- Bez majitele (spí), konzervativně: About bez prokladu do #807; nadpis nad blokem = sekční velikost.
- ⚠ FontStashSharp bere U+00A0 jako mezeru: nedělitelná mezera drží jen přes `SpaceWrap`.
- ⚠ Nástroj Write převádí `\u00A0` na skutečný znak; escape vkládat skriptem.
- Otevřené: čísla v tabulkách (žebříčky, rozpis výsledku), výběr úrovně, stránka CLEARED; Myra stránky hlídá jen review.

## 2026-10-12 — #845 zlom násobiče a nápověda Swapu — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #845 jde na main (merge následuje), `shipped-awaiting-verdict` + `verdict-eye`. Ztracený násobič praskne a rozletí se se střepy, na jeho místě „×5 lost“, pod tím klesající běh not (`StreakBreakSynth`, 99,8 % energie v 300–4000 Hz); ×5 větší, delší a hlasitější než ×3. Nikdy po rozhodnutí úrovně ani na poslední ráně (`StreakCues.BreakIsTold`).
- majitel 2026-10-11: ano i bliknutí čipu Swap, když hráč má Swap, běží série a náboj v hlavni nemá kde skórovat. Bliká „Swap · keep ×3“ (`MatchReach`, jednou za stav pole).
- ⚠ První verze zlomu byla na 1600×900 šmouha 20 px: zlom musí růst, ne se smršťovat. ⚠ Flood celého pole před každou odpovědí stál 1,4 ms; teď průzkum 0,06 ms. ⚠ Ráně do koule na stěně pole zvenku najde umístění volnou buňku vedle ní, i uzavřenou: „nikde“ ji musí počítat.
- Bez majitele, konzervativně: bliká jen, když náboj za ním skórovat může; karty tutoriálu beze změny; `streak=` drží jen do první neskórující rány.
- Otevřené: verdikt oka a ucha; tichý konec na poslední ráně neověřen ve hře.

## 2026-10-12 — #807 About na čtyři stránky — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #807 jde na main (merge následuje), `verdict-eye`: About má čtyři stránky: přehrávač originální skladby, hra s větou o soukromí, knihovny, nástroje s repozitářem. Šipky ◀ ▶ s LB/RB pod nimi; „1 of 4“. Stránka kreslí jen bloky z `AboutCopy.Pages`, `AboutCopyTests` čte totéž: kredity, pravidla sazby (viděny selhat).
- Bez majitele (spí), konzervativně: přes řadu Play/Next šipky chodí po řadě a za jejími konci otáčí stránku (klávesnice nemá ramena, našla review), LB/RB otáčí odkudkoli (`MenuPage.ShoulderTurn`); About se vždy otevře na první stránce.
- Strana 2: původní odstavec, věta o soukromí (sdílená se Settings) je delší než on. K verdiktu.
- Nová páka `menucursor=<s>:<kroky>:<stisky>` (left, right, lb, rb) ověřila otáčení bez klávesnice.
- ⚠ Ten večer stroj neměl zvukové zařízení (`[music] … could not be realized`), vizualizér na snímcích nesvítí.
- Otevřené: verdikt oka; přehrávač s hudbou neslyšen.

## 2026-10-12 — #852 ruka na padu pro vibrace se nastavuje při stavbě levelu — ThinkPad, Claude Code (github-17)
- #852 (merge do mainu, zavřeno jako objektivní): `Game.Rumble.HandOnPad = _tutorial.OnGamepad` a `_tutorial.NoteDevice(PromptsForPad)` přesunuty z konstruktoru `GameplayScreen` (běží jednou za program, před `AudioDirector`, za null-checkem, tedy mrtvý řádek) do `BuildLevel`: každý level začíná rukou, kterou naposled vidělo menu (#799), a vibrace stejnou rukou (#817); `pad` připne ruku na pad celý běh.
- Měřeno bez padu dočasným řádkem logu na každý nenulový snímek vibrací: `level=1 nosplash pad fire=20,22,24` dalo 15 snímků kolem tří ran, bez `pad` 0.
- ⚠ Tichý `if (Game.Rumble != null)` schoval mrtvý řádek od 7. 10.; v `BuildLevel` schválně bez null-checku, level se staví jen z Update.
- ⚠ `timeout 30` tři rány nestihne: Debug start a zpomalená cinematika první rány posunou třetí ránu za 25. s herních hodin; stačí 42 s.
- Neověřeno: na skutečném padu v ruce (jen log); karta druhého levelu rukou menu neviděna.

## 2026-10-12 — #846 velké body za ránu — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #846 jde na main (merge následuje), `shipped-awaiting-verdict` + `verdict-eye`. Body za ránu vyrostou nad velikost skóre (1,5× za holý match, až 3× za 2000+), drží se ke čtení 0,55–1,2 s (roste s druhou mocninou váhy), pak se zmenší a letí do rohu jako dřív. Pět velikostí fontu, nikdy natažené (`AwardZoom`). Souběžné body se předávají, ne slučují (starší odchází po ≥ 0,4 s). Bonus za dokončení: bílé „unused“ vzejde z balls-left a dopadne před stránkou výsledku (`LevelPhases.ClearedBeatSeconds`).
- Bez majitele (spí), konzervativně: předání místo sloučení; popisek „unused“ podle stránky výsledku a Helpu.
- ⚠ Lineární doba čtení nechala obyčejný match stát moc dlouho (test to chytil); ⚠ nápřah `InBack` vyhodil bonus z levého okraje, poloha teď drží v rámu.
- Otevřené: verdikt oka (překryv dvou čísel 0,1–0,3 s); skriptovaná rána na 3840×1600 od ~15:20 nevystřelí (příčina neznámá), pár zlomu série ve 4K chybí; první vykreslení velkých velikostí neměřeno.

## 2026-10-12 — #720 kamera se naklání s mířením — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #720 jde na main (merge následuje), `shipped-awaiting-verdict` + `verdict-hands`. Bez RMB přehledová kamera naklání cíl pohledu s elevací hlavně (zesílení 0,3, plynule 0,125 s v reálném čase); nula je mířidlo, se kterým level začíná, takže úvodní záběr se nemění. RMB náklon vytratí. Nahoru se staví, než ústí klesne pod 12 % rámu (pravidlo majitele „trochu zbraně v záběru“); dolů, než vršek orámovaného shluku zajde pod 3 % horního okraje (na startu levelu jen 0,5°). `logtilt` tiskne náklon.
- Bez majitele, konzervativně: hloubka ponoru dolů malá; horizont scény není slibem fitu.
- ⚠ Nula náklonu skočila, když vysokému levelu povolil limit míření (Totem: 30,7 → 33,8°, pohled klesl bez vstupu); nula teď sedí pod `OpeningElevationLimit()`.
- Otevřené: pocit v ruce (myš, pad), skutečné okno 3840×1600.

## 2026-10-12 — #737 varianty motivů barev — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #737 jde na main (merge následuje), `owner-decision`. Čtyři sady malovaných motivů dávají všem 13 barvám (i černé) vlastní figuru: 1 figurky, 2 emblémy, 3 pruhy, 4 puntíky. Za `colourcue=motif:1..4`, výchozí vypnuto, takže hra se nemění. Vyřezaný tvar je pryč. Inkousty drží průměrnou barvu koule na podílu, který vidí kamera; bledé inkousty (hnědá, námořní modř) bez šedi. Srovnávací stránka: https://claude.ai/artifact/QtAvLS7AxbhN4WBC72bu5U
- Bez majitele (spí), konzervativně: nic výchozího, výběr či mix sad je jeho.
- ⚠ Pruhy kolem svislé osy ukazují kameře o 15–18 % víc plochy než kouli; vyvažovat na podílu koule ztmavilo červenou o 4,7 dE. Pokrytí se musí přeměřit při každé změně figury.
- Otevřené: volba sady; na vinylu sady nahradí bílé klíny plážového míče; HUD disky, kanón, ostatních 7 stylů a Pi figuru nekreslí; čas na GPU neměřen.

## 2026-10-12 — #400 osmý krátký průchod revize — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- Osmý průchod #400 (448f19ae..b1a5622b) jde na main (merge následuje), zavřeno jako objektivní. Chyby v kódu žádné. Opraveno deset rozbitých citací dokumentů po dietě #839 (sekce „Two kinds of control“ z #816 je zpět v `docs/game-shell/settings.md`, odkazy na staré nadpisy vedou do pod-dokumentů, komentář `Cavern.fx`) a jedno špatné číslo: karta tutoriálu má na 3840×1600 148 px, ne 178.
- DocDrift už nebere číslo issue jako znak historie (to pravidlo schovalo 178); kandidátů je 29 místo 26.
- Bez majitele (spí), konzervativně: jen dokumenty, komentáře a triážní nástroj.
- ⚠ Pravidlo „věta s #NNN je historie“ v DocDriftu skrývalo živá tvrzení; od #839 nesou čísla issues i pravidla.
- Otevřené: 148 px spočteno z kódu, ne změřeno v záběru; citace bez uvozovek skript neověří. Další průchod začíná na b1a5622b.

## 2026-10-12 — #726 vlastní zvuk modrého kroku stropu — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- #726 na větvi `726-feed-step-sound` (merge následuje), `shipped-awaiting-verdict` + `verdict-eye` (ucho). Modrý krok (feed i zvednutí Brzdou) má vlastní nahrávku `Sfx/ceiling-feed.ogg`: dva údery marimby stoupající o oktávu (C4 → C5), 76,7 % energie v 300–4000 Hz, `CEILING_FEED_LEVEL` 0,25 (na monitoru o 12 dB slyšitelnější než dřív, pod releasem i ránou); za ní bake `CeilingFeedSynth`. Řádek `[ceiling]` končí `heard <cue> at <hlasitost> +<n> st`; Totem: 17 kroků feedu a 2 Brzdy `ceiling-feed.ogg`, 8 červených `ceiling-step.ogg` (cue podle hlasu, který zazněl; bez souborů `… bake`). Stoupání proudu počítá herní čas (`CeilingFeedPour`), pauza ho drží. Poslech před/po: https://claude.ai/artifact/9Q9VCdRZF7YNy6VaUJ85ea
- Bez majitele, konzervativně: Brzda hraje tentýž stoupající zvuk, žádný protějšek (klesající figura tu znamená „ne“); proud feedů stoupá o půltón za krok až o kvintu; pod nahrávkou přimíchané tělo 132 Hz (3,4 %).
- ⚠ Podíl pásma celého zvuku měř bez okna: Hann přes celý zvuk dal 90,9 % místo 59,6 %. Časování ve hře nikdy na Stopwatch: pauza pak proud restartuje.
- Otevřené: ucho majitele (monitor, notebook); hlasitost proti hudbě jen měřena.

## 2026-10-12 — #854 Unlock all jen v dev buildu — ThinkPad, Claude Code (github-44)
- #854 (merge do mainu, zavřeno jako objektivní): řádek Unlock all na záložce GAME se kreslí jen při `BuildVersion.Release == null` a `ToggleUnlockAll` v release odmítne, takže vydání nemá, čím otevřít kampaň. Brána je slovo buildu (tag), ne konfigurace Debug/Release: majitel hraje Release buildy mainu a ty řádek mají.
- Ověřeno snímky `settings=tab:game shot=8`: plain build (dev-2f39123) řádek má, build s `-p:BS3DReleaseVersion=v0.0.0-test` končí u Reset progress, Back stojí na místě (rám stránky je měřen podle nejvyšší záložky).
- ⚠ Build se stavem release stav do vlastní složky (`-o`) a hned smaž: jinak leží vedle plain buildu exe, které by s online zapsalo na veřejné tabule.
- Otevřené: nic; dev build nadále bez změny.

## 2026-10-12 — #855 mezera na začátku řádku — ThinkPad, Claude Code (github-44)
- #855 (merge do mainu, zavřeno jako objektivní): příčina je vlastní zalamování FontStashSharp — láme i za tečkou, a když přeteče právě mezera za větou, zlomí za tečkou a mezera začne další řádek. Text Help sázený syrově (stav před #806) dal v okně 1600×900 řádek „ The aim preview shows the bend.“; přes `SpaceWrap` 0 řádků při osmi výškách (měřeno skutečným fontem mimo hru).
- Help už od #806 jde přes `SetProse`; zbylých sedm Labelů s `Wrap = true` a syrovým `Text` (stavové řádky tabulek v pickeru a výsledku, odemykací poznámka, oznámení kvality, stav a nápověda poznámky, Quit) převedeno. `ProseLabelsTests` čte `Game/Screens` a drží každý `Wrap = true` u `SetProse` (viděn selhat na sedmi); `SpaceWrap` nikdy nezačne ani neskončí řádek mezerou (TIE na kraji slova) a samotná mezera jako text zůstane (drží výšku řádku).
- ⚠ Regex „`name.Text =` kromě `SpaceWrap.Wrap(`“ musí mít lookahead za `=` včetně mezer, jinak backtracking projde.
- Otevřené: nic.

## 2026-10-12 — #857 Help jako záložky, Controls první — ThinkPad, Claude Code (github-44)
- #857 (merge do mainu, `shipped-awaiting-verdict` + `verdict-eye`): Help má řádek šesti záložek CONTROLS · BASICS · SCORING · ODD BALLS · CAMPAIGN · THE GLASS podle vzoru Settings; otevírá se vždy na Controls (`help=<n>` přečíslováno, 1 = Controls). Řádek záložek je sdílená `MenuPage.TabRow` (Settings převedeno, snímky beze změny). Každé téma ve vlastním scrolleru v jednom rámu, přepnutí jen viditelností; Previous/Next pryč, pod textem Back a výzva Scroll.
- Jména krátká: se sekčním písmem při 1080p má řádek 1036 px proti rámu 930, „HOW TO PLAY“ by dal 1210; rám sleduje řádek.
- Ověřeno snímky všech šesti záložek v 1920×1080 a v okně 1600×900 (Back celý), klikací cestou `menucursor=4:0:rb,rb` (→ SCORING) a `left` (obtočení na THE GLASS), ruka padu (LB/RB u krajů, B na Back).
- ⚠ Krátké tělo stojí v rámu svisle na středu (MenuColumn), jako před změnou (#729).
- Otevřené: verdikt oka (jména záložek, šířka řádku nad textem); 3840×1600 neviděno; #856 (modely koulí v Help) staví na tomhle.

## 2026-10-12 — #856 živé koule v Help — ThinkPad, Claude Code (github-44)
- #856 (merge do mainu, `shipped-awaiting-verdict` + `verdict-eye`): záložka ODD BALLS má u každého druhu čtverec a v něm otáčející se kouli kreslenou herní cestou (trik karty tutoriálu #735: koule 3 jednotky před objektivem na paprsku čtverce) do ostré vrstvy popředí, kterou hostitel skládá přes UI na konci snímku (#242) — deska ji tedy netlumí. Nová `MenuPage.PresentsForeground/ForegroundClip/DrawForeground`, `BS3DGame.DrawPageForeground` z rámce pozadí po koulích a skle; mimo klip scrolleru se vrstva maže (krytí 0), takže koule za okrajem rámu se řízne jako text. Potato kreslí rovnou přes UI, jen celé čtverce.
- ⚠ Myra `ActualBounds` je v prostoru widgetu; na desktop přes `ToGlobal` (první pokus dal všech deset koulí v levém horním rohu).
- Cena: 40,2 ms proti 38,4 ms (High, 1080p, ssaa 2×, Měsíc, ThinkPad) ≈ 1,8 ms za vrstvu a skládání.
- ⚠ Dodatek (13. 10., merge následuje): Help z pauzy kreslí scénu GameplayScreen, kde hák chyběl — čtverce byly prázdné (snímek `level=1 help=4`). Hák je i tam; koule jdou přes `BallRenderSet.DrawAside` (druhý malý rámec, který zachová záznam pohybu prvního, jinak by `BeginFrame` vynuloval velocity pass session), sběr přes method group bez closure.
- Otevřené: verdikt oka (barvy po sloupci, velikost čtverce, sklo jako průsvitný kotouč); Pi neměřeno; duhová koule bez obrázku.

## 2026-10-12 — #400 devátý průchod (19eb1484..ad53e22f) — ThinkPad, Claude Code (github-44)
- Rozsah: 14 commitů bez merge (#726 review, #854, #855, #807, #857, #856), 55 souborů. Kód čistý: přidané řádky v Draw/Update/Refresh bez alokací za snímek (closure `onlineAlert` a lambdy záložek jen při Refresh/stavbě; `AboutPage.PlayerPage` statický inicializátor), DocDrift 29 kandidátů, žádný v změněných souborech, WindingCheck 243 dílů/0, VectorBoundary 0 VEC001 nad Core, BS3D, Physics a Game (podstrčený `System.Numerics` → `Xna` přechod viděn chycen), build 0 varování. Citace: 183 zkontrolováno vlastním skriptem (`"Název" in docs/x.md` proti nadpisům a tabulkám „Where the old sections went“), 1 nevyřešená opravena (`PlayerProgress` → `Progress.json` v `player-files.md`).
- ⚠ `dotnet build Game.sln -t:Rebuild` s analyzátorem si nejdřív smaže `VectorBoundary.dll` a čtyři nástroje pak padnou na CS0006; projekty stavět jednotlivě `--no-incremental`.
- Otevřené: další průchod od merge tohoto.

## 2026-10-12 — #807 přehrávač původní hudby na poslední stránku — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- Hotovo na větvi `807-about-player-last`, merge na main následuje: About má dál čtyři stránky, ale otvírá se na THE GAME, pak BUILT ON, MADE WITH a THE ORIGINAL SCORE jako poslední (velikonoční vajíčko). Změnilo se jen pořadí v `AboutCopy.Pages`, text ne.
- `about=play` bez čísla otevře stránku přehrávače (`AboutPage.PlayerPage`); `about=<n>` beze změny. Test `The_player_is_the_last_page`, příklad `menucursor` v docs přepsán na poslední stránku.
- majitel: „Vrať“ u #807 (přehrávač nemá být první).
- Konzervativní čtení: po otočení na stránku přehrávače zůstává kurzor na ◀ (dosavadní pravidlo), Play je o řádek níž.
- Otevřené: `shipped-awaiting-verdict` + `verdict-eye`; Play a Next nikdo neslyšel (běhy bez zvuku), na skutečném padu se neotáčelo.

## 2026-10-12 — #806 tabulková čísla a sazba stránek — desktop (RDT-PC), Claude Code (bs3d-71, noc)
- Hotovo na větvi `806-typesetting-figures-and-pages`, merge na main následuje: čísla ve sloupcích (pořadí a skóre na tabulích, High Scores jako skutečné sloupce, rozpis výsledku) mají tabulkové číslice. FontStashSharp 1.5.6 nemá tnum, takže `TabularFigures` dá každé číslici buňku široké jako nejširší číslice a vycentruje ji ohraničenými mezerami `/s[n]`; `FontFigures` to váže na skutečný font.
- Sazba: texty výběru úrovní v `PickerCopy`, výsledku v `LevelResult`, obojí hlídají testy přes `CopyRules`; jedna mezera kolem „ · “, ‚Moon‘, skutečná „…“, prózové proložení u poznámky o odemčení a stavového řádku tabulí.
- ⚠ Nastavený text je rich-text s příkazy: vypnout `SupportsCommands` nebo měřit přes `MeasureString` rozbije sloupce (stojí v `menu-look.md`).
- Otevřené: `shipped-awaiting-verdict` + `verdict-eye` (jedničky v tabulkových číslicích, husté tečky, High Scores ve sloupcích); deska tabulí na stránce výsledku a živé online tabule nezachyceny.

## 2026-10-13 — #859 10bitový back buffer — ThinkPad, Claude Code (github-44)
- #859 (merge do mainu, `shipped-awaiting-verdict` + `verdict-eye`): back buffer `R10G10B10A2`, kde monitor okna hlásí DXGI ≥ 10 bpc (`DisplayRefresh.TryGetBitsPerColor`, shoda přes `\\.\DISPLAYn`); řádek „10-bit colour“ na DISPLAY (místo FPS counter, který šel na GAME — DISPLAY má 12 řádků jako celý rozpočet), držený s důvodem „(8-bit panel)“, „(Potato)“, „(driver)“; `tenBitColour` v nastavení, výchozí zapnuto; argument `tenbit`/`tenbit=off`.
- ⚠ MonoGame `ApplyChanges` zařízení sám vytvoří, když není: první zařízení vždy 8bitové a reset na 10 až v Initialize, aby odmítnutí driveru bylo chytitelná výjimka (fallback na 8 bitů + řádek logu). Na ThinkPadu (AMD, 8bpc) jde 10bitový swap chain ve všech režimech; E_INVALIDARG z GTX 1060 nereprodukováno.
- ⚠ `GetBackBufferData` nic nepřevádí: 10bitový buffer čtený do `Color` je slova čtená jako bajty (modrá 100/149/237 → 145/89/121). Screenshot i obrázek poznámky jdou přes `BackBufferPixels` (test pinuje dekód i tu chybu); ověřeno `tenbit level=1 shot=9`.
- Argument `ramp` (merge následuje): šedá rampa 0–1 přes spodní třetinu snímku rovnou do back bufferu z 16bitové textury (256 kroků na 8 bitech, 1024 na 10); jediný způsob, jak okem na 10bitovém panelu poznat, zda DWM 10 bitů zobrazí — PNG je 8bitové.
- Otevřené: majitelův 10bitový monitor (zda driver vezme, zda DWM prezentuje 10 bitů — `ramp` se zapnutým a vypnutým řádkem), NVIDIA readback neověřen.

## 2026-10-13 — #400 kontrola citací jako nástroj — ThinkPad, Claude Code (github-44)
- `Tools/DocDrift -- --citations [--selftest]` (merge do mainu): každá `"Název" in docs/x.md` v kódu i docs musí jmenovat nadpis dokumentu, jeho pod-dokumentů nebo řádek tabulky starých sekcí; průchody 8 a 9 to dělaly skriptem ze scratchpadu. Self-test podstrčí citaci neexistující sekce (§10). Stav: 187 citací, 0 nevyřešených. Zapsáno v `checking-tools.md`, `CLAUDE.md` a `build-and-release.md`.
- ⚠ Příklad vzoru v komentáři nebo usage (`"Title" in docs/x.md`) je sám citací; psát s lomenými závorkami.
- Otevřené: citace bez uvozovek nástroj nevidí.

## 2026-10-13 — #849 Colossus přestavěn jako vygenerovaný závěr Quarry — desktop (RDT-PC), Claude Code (bs3d-71)
- majitel: přestavět Colosse s vlastním nápadem, generovaný LevelGen jako nejtěžší level kapitoly.
- #849 (merge do mainu, verdict-eye): obr se zdviženýma rukama, který drží skleněný strop, se srdcem (výplata Type6) zapečetěným v hrudi. 337 koulí (dřív 364), 33 skupin, 42 ran; brány včetně reveal prošly, sag 0 z 5, ScoreSim i 1010 testů OK. Přípravky testů přesunuty (ShotAgreement přeměřen: 97,5 %), ChapterTests drží každý blok na jedné scéně, kopuli, rodině a materiálu.
- ⚠ Pět resetů desktopu přerušilo agenta pětkrát; dokončeno ručně po krátkých krocích, každý hned pushnutý. Snímky z 12. 10. 16:49 platí, `Colossus.json` se od 16:36 nezměnil (porovnáno bez CR).
- Otevřené: oko majitele; `update-ceilings.sh` na Pi při příštím vydání (deska Colosse začne od nuly).

## 2026-10-13 — #737 sada motivů na každý styl, zapnuto ve výchozím stavu — desktop (RDT-PC) + ThinkPad, Claude Code (bs3d-f3, github-42)
- majitel (12. 10. večer): použít všechny sady, jednu na každý styl koulí; přiřazení nechal na agentovi.
- #737 (merge do mainu, verdict-eye): vlna, porcelán, plazma = figury; vinyl, kov = emblémy; mramor, led, drahokam = linky; bublina = puntíky; **láva bez figury** (ani škála spár, ani popelová kůra nebyly z hrací vzdálenosti vidět). Cue zapnuto v Game, Testbedu i editoru, `colourcue=none` vypíná; disky fronty v HUD nesou figuru jen tam, kde ji nesou koule. 1063 testů OK.
- Desktop při jakémkoli vykreslování padal, takže kód psal desktop jen na CPU a snímky, posouzení a stránku (https://claude.ai/artifact/6jwiQxHqTjAiNZh3GyGTGF) udělal notebook: devět stylů OK, `colourcue=none` = dodaný kód (průměr přes dech koulí).
- ⚠ Černá v kovu a plazmě: šedý inkoust ~24× černé; škály mají vlastní uniform.
- Otevřené: oko majitele (výrazné pruhy žluté a stříbrné, vinylová béžová a stříbrná z dálky splývají, tenké linky černé a námořní modré na mramoru mizí, plazma čte jen na světlých barvách); cena na GPU neměřena.

## 2026-10-13 — #860 Bomba velká jako ostatní koule — ThinkPad, Claude Code (github-b9)
- #860 (merge do mainu, `shipped-awaiting-verdict` + `verdict-eye`): plášť miny zpět na plný poloměr, osm výstupků stojí 0,13 nad ním (`MineStudRise`, stejný reliéf, jaký #625 schválil). Měřeno na `Specials.json` (pevná kamera, rozdíl proti témuž snímku s uvolněným clusterem): plošně ekvivalentní poloměr bomby 0,87 obyčejné koule vedle ní, teď 1,00.
- majitel: „Výstupky mohou být klidně přes 1.“
- Zkoušeno 0,06 / 0,10 / 0,13: při 0,06 výstupky zepředu skoro zmizí. Výstupek ukazuje do 10° k sousedům o patro výš a níž a zanoří se do nich o svou výšku, ve štěrbině; na neprůhledných stylech i přes bublinu, led a drahokam to čte jako výstupek opřený o souseda.
- Otevřené: oko majitele; kámen a led dál řežou dovnitř (mimo rozsah, rozhodnutí majitele).

## 2026-10-13 — #824 poznámka jmenuje hrající nahrávku — ThinkPad, Claude Code (github-b9)
- #824 krok 1 (merge do mainu): kontext poznámky nese `music` (kmen nahrávky, který si úroveň umí připnout) a `musicDrums`, dokud hraje téma úrovně (`GameMusic.SoundingRecording`/`SoundingLayered`). Ověřeno: `level=77` a poznámka do místní schránky (`userdata=`, bez serveru) má `"music": "pulse"`, `"musicDrums": true`, shodně s řádkem `[music]`.
- Otevřené: kroky 2–3 (najít a přegenerovat špatné nahrávky) čekají na stroj s velkou GPU; issue zůstává otevřené.

## 2026-10-13 — #400 desátý průchod (77be2c66..28396e83) — ThinkPad, Claude Code (github-b9)
- Rozsah: 17 commitů bez merge (#849, #859 `ramp`, #856 dodatek, #737, #860, #824), 67 souborů. Hygiena snímku čistá (žádná alokace, hledání podle jména ani stavový objekt za snímek; techniky cue v shaderu volí technika, ne větev). Build čtyř řešení 0 varování, WindingCheck 243/0, VectorBoundary 0 VEC001 (podstrčený přechod viděn chycen), citace 187/0, DocDrift 29 stojících kandidátů.
- Opraveny tři komentáře: `AsideCollector` (delegát z metody instance alokuje při každém převodu, volající si ho drží), cue tabulka a `ColourCueField` jmenují `ColourCueStyleTests`, figury HUD po změně okna uprostřed levelu.
- Otevřené: zda kompilátor nezplošťuje vnořené `if` v `BallColourCue.fxh` (cue je teď zapnuté všem; cena na GPU neměřena, viz #737). Další průchod od merge tohoto.

## 2026-10-13 — #824 #706 změřené rodiny pulse a mural — ThinkPad, Claude Code (github-b9)
- Rodiny `pulse` a `mural` změřeny proti `ember` metodou #841 (na CPU); tabulky a rozhodnutí v komentářích #824 a #706. Pulse: přegenerovat osm (acid, bigroom, electrofunk, italo, techno, trance, progressive, synthpop), nechat vector a holý pulse. Mural: desertblues a jive; „opilost“ není ve výšce tónu ani v tempu (kontrola: umělé kolísání ±25 centů měřítko chytí).
- Skript je v repu: `Tools/Generation/measure-music.py` (merge do mainu), popsaný v `music-generation.md`.
- Otevřené: generování čeká na stroj s velkou GPU; které mural nahrávky zní „opile“, řekne ucho majitele (poznámka z pauzy teď jmenuje skladbu).

## 2026-10-13 — #815 web stránka hry v `Web/` — ThinkPad, Claude Code (github-b9)
- Zdroj stránky na mainu (merge následuje): statický web v Eleventy (`Web/`), podle doporučení v issue: jedna stránka, angličtina, bez analytiky a cizích požadavků, místo pro video, blog jako složka Markdownu. Obrázky ze hry na High přes `Tools/web-screenshots.ps1` (přetáčí se s každým vydáním), nasazení `Web/publish.ps1` (SFTP/FTPS, přihlášení z proměnných prostředí), postup v `docs/website.md`.
- ⚠ Headless Edge má minimální šířku okna 492 px: telefon se ověřuje emulací přes CDP (puppeteer-core), ne `--window-size=390`; ta ořízne stránku vykreslenou širší.
- Otevřené: nasazení je na majiteli (subdoména na Webglobe, záznam a přesměrování v Cloudflare); `publish.ps1` proti hostingu nikdy neběžel; stránka popisuje příští vydání (vzory barev), takže patří ven s ním.

## 2026-10-13 — #862 HUD po výhře zmizí — ThinkPad, Claude Code (github-b9)
- #862 (merge do mainu, `shipped-awaiting-verdict` + `verdict-eye`): od vstupu do `ClearedBeat` se herní údaje (čipy, fronta, boční řez, zbývající koule, série, karta) za 0,5 s rozplynou přes overlay vrstvu; skóre, ocenění (i bonus za čistku) a výzva k přeskočení dropu zůstanou ostré do stránky výsledku. `PlayHud.Draw` bere `HudParts` místo `previewsOnly`; prohra beze změny (#639).
- Ověřeno: úroveň „kříž z pěti“ přes `levelfile=` vyčištěná jednou ranou; main nechal HUD celou oslavu, tady je fronta zachycena v půli rozplynutí a o snímek dál pryč. Staged `lineloss=` dál drží řez a frontu.
- ⚠ První úroveň kapitoly přes `levelfile=` začíná průletem kapitoly (asi 12 s); střílet až po něm.
- Otevřené: oko majitele (délka rozplynutí); Potato vrstvu nemá, tam údaje zmizí naráz.

## 2026-10-13 — #861 průlet scény ze Extras odstoupí celý — ThinkPad, Claude Code (github-b9)
- #861 (merge do mainu, `shipped-awaiting-verdict` + `verdict-hands`): během průletu scény stránka Scene „ustoupí“ (`MenuPage.StandsAside`): hostitel nekreslí nic z ní (ani výzvu u kurzoru, která zůstávala) a nedává jí chůzi, Enter, kolečko ani Zpět; Space, Enter, Escape, klik, A, B nebo RT průlet přeskočí (`TrySkipTour`, dosud nevolané), nad ním hint z průletu kapitoly. Klik je pro návrat stránky odjištěn.
- Ověřeno `padpress=`: main nechal glyf A přes průlet a down,a změnilo scénu pod letem (kamera skončila vzhůru nohama pod ostrovem); teď čistý let, down ignorován, A přeskočí, „RT to skip“ po 4 s, B přeskočí bez odchodu ze stránky.
- Otevřené: klávesnice a myš neřízeny (stejné hrany jako zbytek menu); ruce majitele.

## 2026-10-13 — #400 jedenáctý průchod (6942a67e..e92a6b28) — ThinkPad, Claude Code (github-b9)
- Revize vlastních #862 a #861 (merge do mainu): vrstva overlaye se váže a skládá vždy ve stejném snímku, `_clearedElapsed` se nuluje jen při vstupu do beatu, vstup během „ustoupení“ se nezdvojí. Opraveno: klik se odjistí až v Draw na snímku návratu stránky (průlet končí v update obrazovek, po armování v `BS3DGame.Update`, takže stisk přesně v tom snímku dostal Myra); armování už během průletu nečte myš každý snímek; karta tutoriálu se v textech nevypisuje mezi mizícím (konec levelu ji ukončí dřív, #700).
- Další průchod od merge tohoto.

## 2026-10-14 — #none zdroj desktopu vyměněn, resety skončily — desktop (RDT-PC), Claude Code (bs3d-39)
- majitel: „na desktopu už nedochází nikdy k pádům v žádné zátěži (CPU ani GPU). Problém byl ve zdroji. Ten jsem vyměnil.“ Poslední Kernel-Power 41 byl 14. 10. v 9:45, start na novém zdroji ve 12:45. V desktopu je zase RX 6900 XT (GTX 1060 byla jen 12.–14. 10.).
- Neplatí už žádné omezení z resetů: `fpscap` jako ochrana, zákaz benchmarků, ptát se před GPU sezením, `heavy.ps1` (jedna těžká úloha naráz), žádné snímky na desktopu, ACE-Step/sd-server jen samotné. Lokální AI na desktopu (ACE-Step, sd-server, Gemma) zase jde.
- Skilly `benchmark`, `verify` a `design-references` a komentáře `AtomicFile`/`PlayerProgress` opraveny: příčinou byl zdroj, ne takty karty.

## 2026-10-14 — #841 hudba Eruption přegenerována — desktop (RDT-PC), Claude Code (bs3d-39)
- #841 (merge do mainu, `shipped-awaiting-verdict` + `verdict-eye`): osm nahrávek rodiny `magma` nahrazeno podle vzoru `ember` (metal a tribal zůstaly), 36 renderů ACE-Step ve třech kolech, výběr měřením (`measure-music.py` + šev, díra, stereo); bicí vrstvy oddělené znovu z nových nahrávek. Mastery v `C:/Users/panrd/AI/output/masters-841`, kandidáti v `masters-night-magma`. Stránka k poslechu: https://claude.ai/artifact/SZH18zrnQWmU8oaBARcNgj (odpovědi v db, kolekce `verdicts`).
- ⚠ Pásma nestačí: `doom` c3 v pásmu, ale šev +15 dB; `electro` c4 má 24 s z 43 s tiše. Breakbeat a electro si výšky drží přes jakékoli zadání (devět renderů, nejlépe 2,1 % a 2,5 % nad 6 kHz). Zapsáno v `music-generation.md`, „Choosing a candidate“.
- Otevřené: ucho majitele; `electro` stále 4,0 % nad 6 kHz a `breakrock` středy jako dřív.

## 2026-10-15 — #866 #864 #865 About bez AI, balíky slámy, Help na celou šířku — desktop (RDT-PC), Claude Code (bs3d-ac)
- #866 (f8616d63): MADE WITH říká, že původní hra je autorův vlastní kód bez AI a od roku 2026 ji rozšiřuje Claude přes Claude Code; přeformulovat jde jen v `AboutCopy.MADE_WITH`.
- #864 (22bbebff): balík v měřítku plotu (`BALE_SCALE = FENCE_SCALE`, 3,2 napříč), sázený po plotech mimo každé jejich pole, z vlastního generátoru. ⚠ Skupiny se dřív nikdy nevytvořily (rozestup 2,2 proti odstupu 1,6) a balík mohl stát na plotu (0,15 u jednoho z 81 seedů); teď nejméně 3,2 od plotu.
- #865 (tato větev): odstavce Help od BASICS na `TEXT_WIDTH` (83 znaků), majitel 15. 10. „celá šířka“; těla tabů stojí nahoře. BASICS se při 1600×900 vejde bez rolování.
- Otevřené: oko majitele na všech třech (`verdict-eye`), snímky pro stránku verdiktů v scratchpadu sezení bs3d-ac.

## 2026-10-15 — #732 hudba Silhouettes přegenerována — desktop (RDT-PC), Claude Code (bs3d-ac)
- #732 (tato větev, `shipped-awaiting-verdict` + `verdict-eye`): všech deset nahrávek rodiny `puppet` nahrazeno podle pokynu majitele (11. 10.: tajemné a kouzelné, s melodií; z `ember` jen melodii a středy). 57 renderů ACE-Step v nejvýš třech kolech, výběr měřením; pět komediálních jmen přejmenováno (caper→fantasy, kalimba→aurora, rag→oud, tango→waltz, toybox→triphop), rodina zůstává `puppet`. Inkrementální build staré soubory z výstupu sám smaže (ověřeno). Bicí vrstvy má sedm z deseti. Mastery `C:/Users/panrd/AI/output/masters-732-chosen`, kandidáti `masters-732`; poslech a tabulka v `verdicts/732` scratchpadu bs3d-ac.
- ⚠ Zadání „hushed breakdown“ dává díry (13 z 30 renderů prvního kola); nové vyřazovací chyby „výpadek“ (2 s pod −20 dB) a šev s nesouhlasným spektrem. Orchestr a valčík si dynamiku drží přes jakékoli zadání. Zapsáno v `music-generation.md`.
- Otevřené: ucho majitele; fantasy volné tempo, triphop a jazz pod `ember` v počtu změn tónu.

## 2026-10-15 — #766 vlastní vzhled náboje Cut — desktop (RDT-PC), Claude Code (bs3d-ac)
- Hotovo (merge na main, `verdict-eye`): řezací náboj už není zap. Ocelová koule s kotoučovou pilou: vlastní technika `InstancedModelCutter` (na lici koule žhnoucí věnec zubů kolem náboje se třemi šrouby) a `InstancedModelCutterBlade` na `SawBladeMesh` (24 zubů, dva vylomené, špičky 0,585 pod vývrtem 0,6). Rámec staví CPU (`BallRenderSet.CutterFrame`): pila natočená k oku, sklopená k vodorovné, otáčí se na nástěnných hodinách, v hlavni pomalu. Jedna barva `BasicEffectParamsProvider.CutterAccent` (bílá žhavá, k patám zubů chladne do jantaru) pro disk v pásu, paprsek, objímku, paprsky, stopu, kroužek v profilu i číslo bodů. Potato: ocel, prstenec obrázkem studny, čepel svítí.
- ⚠ Vodorovná čepel byla v hlavni neviditelná (hlaveň zakryje rovník); proto pila na lici. Review: renderer čepele nebyl ve světelném rigu (zrcadlil bílé nebe), 18 rad/s za letu dělalo stroboskop podle obnovovací frekvence (teď 3 rad/s, pod půl zubu za snímek i při 30 Hz).
- Reference `C:\Users\panrd\AI\sd\out\766-zimage`, `766-klein`; důkazy `<scratch>\verdicts\766`.
- Otevřené: verdikt oka; #767 (inscenace řezu).

## 2026-10-15 — #767 použití Cut jako okamžik — desktop (RDT-PC), Claude Code (bs3d-ac)
- Hotovo (větev `767-cut-staging`, `verdict-eye`): stisk nabije náboj (pila se roztočí a zpět, záře, objímka, paprsky, čip, zvuk nabíjení, stoupající držení ve spouštích), let zúží objektiv, zásah je bílý žhavý řez přes patro s jiskrami (druhý styl `Blast.fx`, i Potato), zvuk řezu, úder do ruky a hit-stop; pád ve zpomalení podle velikosti, řezné plochy žhnou. Reference oběma modely.
- Review: krok stropu čeká přes zpomalení, kinematografie zpomalení řezu ukončí, náboj si po Swapu nese záři i úhel, odměna řezu se narodí 0,7 s po zásahu (stála přes řez). `CutStaging` v testech, 12 zasazených chyb každá shodí test.
- ⚠ Úder přičtený k ráně výstřelu přešel strop, proto `PadChannel.KickUpTo`. Snímek 1920×1080 stojí ~0,5 s, proto `cutshot=`.
- Otevřené: oko a ucho majitele, ruka na padu, PresentMon.

## 2026-10-16 — #824 hudba The Grid přegenerována — desktop (RDT-PC), Claude Code (bs3d-ac)
- Hotovo (merge na main, `shipped-awaiting-verdict` + `verdict-eye`): osm nahrávek rodiny `pulse` podle vzoru `ember` (melodie ve středech, pohon z aranže); `vector` a holý `pulse` zůstaly. Big room → piano house (`pulse-piano-house`). 83 renderů v nejvýš pěti kolech, výběr měřením. Pět uvnitř rozsahu `ember` ve všem, techno a electrofunk těsně pod v počtu změn tónu, synthpop tenký v 200 Hz–2 kHz (6,8 %). Vše ≤ 2,0 % nad 6 kHz. Progressive nemá bicí, starou vrstvu bylo nutné smazat.
- ⚠ Výšky tanečních stylů jsou v hi-hatech a liší se víc mezi seedy než mezi zadáními; jasné house piano je také výška. Zapsáno v `music-generation.md`.
- Mastery `C:/Users/panrd/AI/output/masters-824-chosen`, kandidáti `masters-824`; poslech v `verdicts/824` scratchpadu bs3d-ac.
- Otevřené: ucho majitele; synthpop (sedmnáct renderů nedrželo středy i výšky zároveň).

## 2026-10-16 — #706 hudba Gallery: desertblues a jive přegenerovány — desktop (RDT-PC), Claude Code (bs3d-ac)
- Hotovo (`verdict-eye`): `mural-desertblues` a `mural-jive` nahrazeny podle vzoru `ember` (melodie kytary, žádná píšťala). Jména zůstala. 12 renderů ve dvou kolech, výběr měřením; obě nahrávky jsou v rozsahu `ember` ve všech sloupcích. `mural-mbira` se nepřegenerovávala, jen se upekla o 1,5 dB tišeji. Bicí vrstvy všech tří jsou oddělené znovu. Mastery jsou v `C:/Users/panrd/AI/output/masters-706-chosen`, kandidáti v `masters-706`, poslech ve scratchpadu bs3d-ac v `verdicts/706`.
- ⚠ Ořez přes `bake.trim_db` v sidecaru je nový `MusicBake`; patří jen masteru se špičkami bez plochého vrcholu (`music-bake.md`). Township jive drží výšky přes jakékoli zadání (7 z 8). Kapela, která se v renderu na 2 s zastaví, se dá střihnout znovu z části před zastavením (`music-generation.md`).
- ⚠ Sdílený GPU zámek s pollem 10 s vyhladoví proti dávce, která si ho bere hned zpátky; dávka #706 polluje po 0,5 s a po renderu 15 s čeká.
- Otevřené: ucho majitele; `jive` má kolísání výšky 3,9 c (ember max 3,8).

## 2026-10-16 — #882–#896 poznámky ze hraní ke scénám a Help — desktop (RDT-PC), Claude Code (bs3d-5f)
- Majitel poslal 15 poznámek (savana, Meadows, stín náhledové kuličky, Help, skleněný strop): patnáct issues #882–#896, jedno na poznámku. Jen čtení kódu a issues, na desktopu běžela druhá relace (žádný build, žádný snímek). #864 zavřen jeho verdiktem („balíky jsou správně veliké“). Komentáře s odkazy na #758, #753, #749, #856, #831, #481, #641, #609, #874, #733.
- majitel (2026-10-16): scénická grafika se parkovala kvůli gameplayi a nestabilnímu desktopu; druhý důvod padl (měření bez limitu FPS, lokální AI, nový zdroj). Nová issues jsou bez parked; na otázku odpověděl „všech 18“: odparkovány #744–#749, #751–#754, #756, #757, #774–#779 (komentář na každém i na #768), CLAUDE.md a docs/repo-conventions.md mají výjimku „scene graphics“ (c1e0c815). Parkované zůstává #230 #251 #292 #690 #717 #718 #741 #771 #827 #853.
- ⚠ Každé tělo prošel druhý agent proti kódu a 14 z 15 opravil (špatný řádek, zastaralé „v běhu“ u #758, který už je na mainu). Podíl listů proti větvi (52 %, #882) je z Python repliky, ne z C#.
- ⚠ Testbed kreslí obruče až po ohních, takže chybu z #888 nepředvede. Fronta verdiktů je 19 (nad 15), práce na vzhledu čeká.
- Otevřené: snímky před implementací; fronta verdiktů 19 (nad 15) drží start změn vzhledu, nabídnutá je stránka verdiktů.

## 2026-10-16 — #898–#917 druhá dávka poznámek: High Scores, Jukebox, město, moře, hudba po kapitolách — desktop (RDT-PC), Claude Code (bs3d-5f)
- Majitel poslal poznámky ke scénám a verdikty z poslechu celé hudby v Jukeboxu (nejmenované skladby jsou OK). Dvacet issues, jen čtení kódu: #898 High Scores, #899–#901 Jukebox, #902 stožáry, #903 ryby, #904–#915 hudba po kapitolách, #916 hlasové textury, #917 styl umělce v promptech. Komentář na #853 (denní doba), #873, #704, #486, #760, #685, #436.
- majitel: potvrdil, že obrázky v #21 jsou jeho ruční rendery z Wings 3D (zapsáno do #896). Verdikty #706, #732, #824, #841: zbytek OK, jmenované skladby na navazujících #905, #907, #910, #913; čtyři issues zavřeny, fronta verdiktů je 15.
- ⚠ Příčina ryb po ocasu (#903) je ve Fish.fx: úhel dělí čas relace poloměrem, který se mění s časem, takže rychlost roste a u části ryb jde do záporu. Jukebox hraje nahrávky bez vrstvy bicích (RecordingPlayer.cs:13-14): „bubny přebíjejí“ slyšel na plné hlasitosti.
- Otevřené: žádný snímek ani poslech (druhá relace na desktopu); smyčka v Jukeboxu (#899) obrací jeho rozhodnutí z #704.

## 2026-10-16 — #897 Potato: nálezy předvydávací revize — desktop (RDT-PC), Claude Code (bs3d-ac)
- Hotovo (větev `897-potato-review-fixes`): Potato backdrop se staví jen při změně scény; dřív se stavěl znovu při každém startu levelu, restartu i zastavení ukazatele v pickeru. Na desktopu to byl záškub 4–15 ms ve městě, 30–125 ms na louce a v savaně a 130–170 ms na Měsíci. Čas stavby je teď na řádku `[potato]`.
- Potato už nestaví hostitelské město, střechy ani ulice: start je asi o 165 ms kratší (BuildScene 1356 → 1191 ms).
- Kruh skleněné koule bral barvu posledního barevného kbelíku, takže měla tmavý obrys. Teď nastavuje vlastní uniformy a obrys je bledý.
- `PotatoSkyFigure` si parametry a techniky uloží při stavbě. Řádek Scenery na Potato ukazuje „On (Potato)“. Opravené docs a komentáře.
- ⚠ Tools/SemanticSearch neběžel, protože se démon LM Studia nespustil. Duplicita se hledala podle klíčových slov.

## 2026-10-16 — #733 smrky z větví na High a Ultra — desktop (RDT-PC), Claude Code (bs3d-ac)
- majitel (2026-10-15): odparkováno do příštího vydání (polární záře, kapitola 5).
- Hotovo (větev `733-conifer-crown`): High a Ultra kreslí korunu smrku z jednotlivých větví (`SpruceCrownGeometry`) podle téhož plánu přeslenů jako soustružená koruna. Pod větvemi je tmavé jádro, sníh leží v chomáčích jako geometrie a pod `FINE_NEAR_RADIUS` 110 je jemnější detail. Low, Medium a Potato mají starou korunu beze změny (`finemeshes=0` proti mainu: liší se jen padající vločky a zásobník děla). Platí i pro denní les. Ve hře na Fish +0,23 ms (High 9,49 → 9,72), v Testbedu +0,32 ms u hrací kamery.
- ⚠ Sníh přes prachový člen zbělil všechny ploché větve. Cena je v trojúhelnících, ne v pixelech (ssaa 1 i 2 stejně), proto vzdálené stromy dostávají lehčí korunu.
- Otevřené: verdikt oka; snímky před a po jsou v `verdicts\733\`.

## 2026-10-16 — #903 #894 #888 #901 ryby, stín náhledu, obruče, Jukebox — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 80922935, eb8505d8, CI zelené): #903 úhel ryb ze základního poloměru (sonda: 52–63 ryb ocasem napřed v čase 600 s, po opravě 0), Testbed má `clock=`; #894 náhledová kulička se sbírá až po stínovém průchodu (426 → 425 vrhačů); #888 obruče trychtýře se kreslí s ostrovem před ohni; #901 výběr kapitoly v Jukeboxu plní nahrávky na místě, seznam drží posun.
- Otevřené: #903 rychlost hejna (3,2 j/s) na oko majitele; #901 vyznačit hrající kapitolu; #888 krok 3 (shluk a dělo zvenku, nepotvrzeno).
- ⚠ Agenti na Sonnetu, snímky 1920×1080 a menší, GPU zámek relace bs3d-48. Reviewer jedné opravy vrátil prázdný výsledek; diff jsem pak prošel sám.

## 2026-10-17 — #899 #886 #882 smyčka Jukeboxu, okraj plamene, listy akácií — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 3668bfbe, CI zelené): #899 Jukebox opakuje hrající nahrávku (obrací rozhodnutí z #704, jak majitel chtěl); #886 plamen slábne před okrajem čtyřúhelníku (svislý ořez ve 35 z 80 snímků → 0, +0,068 ms); #882 listy akácie podle tečny větvičky (C#: 40–65 % karet proti větvičce → 0, výkon beze změny).
- Otevřené: #886 objemová plazma (krok 3); šev smyčky Jukeboxu nikdo neslyšel.
- ⚠ Fronta verdiktů 17: beru jen chyby, nástroje a měření (#876, sloupce `measure-music.py` pro #913/#910/#914).

## 2026-10-17 — #876 #913 #919 debug řádek, měřicí sloupce hudby, přebuzení — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge c7a0ca78, CI zelené): #876 pauza dev buildu nabíjí každý vystřelitelný druh (dnes jen Wildcard a Cutter: mapové druhy by při přistání ztratily druh), běh s ním se neposílá na tabulky; #913 `measure-music.py` měří krátké díry, opakování a tóny mimo tóninu. Měření všech jmenovaných skladeb je jako komentář na #904–#915 (Drone bez melodie, Sunny 40 stejných taktů, Ringmaster 7,2 % nad 6 kHz).
- ⚠ Založen #919: 38 ze 140 nahrávek se ve hře tvrdě přebudí. Koleno bake míří na plnou úroveň, Ogg přestřelí až o +0,8 dB a `OggTrack.Decode` ořízne. Oprava běží (rezerva při dekódování, hlasitost vyrovnaná, bez přepeku).
- Otevřené: #919; regenerace hudby až pod limitem fronty (17).

## 2026-10-17 — #919 #895 přebuzení hudby, Help ODD BALLS — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 119b1926, faa10f29, CI zelené): #919 hudba se dekóduje s rezervou 1,5 dB a hlasy jsou zesílené stejně (`MusicLevels`), takže 78 ze 140 nahrávek už nepadá na hranici 16 bitů; MusicBake odmítne nahrávku, jejíž špička po kodeku rezervu přeroste. #895 Help ukazuje duhovou kuličku a Cutter, test drží tabulku u všech druhů.
- ⚠ Tvrdý ořez dělal už NVorbis (`ClipSamples` je ve výchozím stavu zapnuté), ne clamp v `OggTrack`: zisk až po čtení by škáloval už zploštělé vzorky. Sloupec „samples clipped“ v MusicBake hledal 32767 a ukazoval vždy 0.
- Otevřené: #920 (stín blednoucí kuličky, nízká priorita); hudba a vzhled čekají, dokud fronta (17) nespadne pod 15.

## 2026-10-17 — #920 #896 stín blednoucí kuličky, reference stropu — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 46fb5454): #920 stínový průchod používá rozpouštění kuliček, stín blednoucí kuličky slábne s ní (0,38 při 0,5, dřív 1,00); ostatní vrhače beze změny.
- #896: 24 referencí konstrukce stropu (A věže z #21, B prstenec, C lana s navijáky, D světelné vysílače), obě modely, img2img nad Testbedem; stránka pro volbu majitele https://claude.ai/artifact/JnRZaQgamBLiKBEjYr3nAk, štítek `owner-decision`. Doporučeno C (nic pevného v prostoru kamery), nebo A s mizením věží u kamery.
- ⚠ Věž na okraji ostrova (poloměr 26) stojí 2–5 jednotek před objektivem (28–31): každá pevná konstrukce na okraji kříží pohled.

## 2026-10-17 — #none kritická review dnešních merge, reference scén — desktop (RDT-PC), Claude Code (bs3d-5f)
- Review po merge (tři revieweři podle oblastí, každý nález ověřil skeptik): potvrzeny 3, opraveny v d01887f9. Hodiny vizualizéru Jukeboxu a About se po zaseknutí už neodchylují, jsou připnuté k tomu, co hlas přehrál (#899). Běh s debug řádkem se neposílá ani do online statistik (#876). Opraven komentář (#895). Render a zvuk (#888 #920 #903 #886 #919 #882) bez nálezu.
- Reference pro #886 (plazma), #883, #890 (keře), #884 (vyšší koruny), #892 (potok a rybník): 40 obrázků, výběr je na issues.
- ⚠ Reviewer i agent občas vrátí prázdný výsledek: oblast pak není prověřená, spusť ji znovu (tady render a zvuk).

## 2026-10-17 — #751 #753 #754 #756 #757 #778 #777 #749 #774 #775 #776 #779 #744 #745 #746 #747 #748 reference scén — desktop (RDT-PC), Claude Code (bs3d-5f)
- Reference pro 17 odparkovaných scénických issues, obě modely (Z-Image-Turbo, klein), 136 obrázků v `C:\Users\panrd\AI\sd\out\<issue>-zimage|klein`, výběr 2–3 obrázků s důvodem je na každém issue. Nic se nestaví: fronta verdiktů je 17, stavba počká na limit 15.
- ⚠ Klein na Marsu a Outbacku přesytí oranžovou; Z-Image drží barvy roverových snímků. Klein lépe kreslí oblohu a opar podle vzdálenosti.
- ⚠ Workflow se stejným skriptem znovu spustil pevný seznam issues předchozí dávky; zastaveno, GPU zámek zůstal volný. Každá dávka potřebuje vlastní skript.

## 2026-10-17 — #875 #881 ztracený násobič a bonus za kuličky slovy, červený main — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo: #875 nad křížem stojí „STREAK LOST ×3“ ve stejném snímku jako zvuk (246417d6). #881 krok 1: „29 UNUSED BALLS / +1 624“ uprostřed, čte se 1,5 s, úder pro výsledek se neprodloužil; mimochodem opraven detail na stránce výsledků, který násobil 50 místo ceny kuličky v levelu (c3769a08). Oba mají verdikt `verdict-newcomer`, ten se do fronty nepočítá.
- ⚠ main byl jeden merge červený: `PlayStatsSessionTests` (z oprav po review) četly Stats.json dřív, než ho pomalý ARM runner zapsal; `Dispose` čeká jen 500 ms. Opraveno v ed9de955 (`Flush` s 30 s v testech).
- Otevřené: #881 kroky 2–3 (animace a kamera, čekají na frontu).

## 2026-10-17 — #709 #851 release v0.4.0 připraven, API nasazeno — desktop (RDT-PC), Claude Code (bs3d-48)
- Rozsah releasu z 15. 10. je na mainu celý: #866 #864 #865, hudba #732 #824 #706, Cut #766 #767, #709, scény #758 #733 #781, k tomu #897 (Potato po review) a #918 (ze smoke runu). Majitel 17. 10.: verze **v0.4.0**, snímek Fish, tag až po jeho odpovědích na stránce verdiktů (https://claude.ai/artifact/TZrCF1JU1FruZqeUb9gBra, 17 položek, odpovědi v db `verdicts`).
- **BS3D-API v0.1.26 nasazeno na Pi** (snapshot, `update.sh v0.1.26` — bez argumentu jen vypíše usage; schéma 3→4, 159 tabulí, řádky beze změny; `POST /v1/stats` s `{}` dává 400 i přes Cloudflare). #709 zůstává otevřené do první skutečné dávky.
- Smoke run release buildu (čistý profil, 14 kapitol, menu, Potato, prohra i výhra): bez výjimky; Column není regrese (4,3 ms, propad byl souběh na GPU). Notes: `Images/releases/v0.4.0.md` (Sonnet pisatel + dva ověřovači našli 15 chyb konceptu). Založeno #921 (clip() overdraw terénů na Low/Medium, ~3 ms na 4K).
- ⚠ Pět holých „OK“ na issues pod sdíleným účtem nejde agentem zavřít (klasifikátor); čekají na potvrzení na stránce. Tokeny: grafické issue 0,65–1,4 M, proto od 16. 10. Sonnet a snímky ≤1920×1080.
- Otevřené: tag v0.4.0 (screenshot Fish, `update-ceilings.sh` na Pi po publish), odpovědi majitele.

## 2026-10-17 — #400 dvanáctý průchod — desktop (RDT-PC), Claude Code (bs3d-48)
- Prošly se merge od e92a6b28: DocDrift (35 kandidátů, všechno jiné veličiny téhož jména nebo odvozená čísla; BONUS_HOLD, DECODE_HEADROOM a počty nahrávek sedí), citace, odkazy `#kotva` mezi dokumenty (0 rozbitých), identifikátory a cesty z nových vět, údaje v CLAUDE.md (osm nástrojů, 21 scén, devět Potato efektů) proti kódu.
- Opraveno jedno: komentář `SceneRenderer.FineMeshes` citoval nadpis „The acacia's fork“, který #758 přejmenoval; citace teď 0 nevyřešených.
- Zbytek dokumentů říká, co platí teď.

## 2026-10-17 — #767 #766 Cut po verdiktu majitele — desktop (RDT-PC), Claude Code (bs3d-48)
- Majitel (Vrať na obě): zvuk nabití zůstává, desktopový vzhled #766 zůstává. Hotovo: pila v rámci hlavně (`CutterFrame`, `CutterLaunchTurn`), takže se v hlavni nenaklání ani při výstřelu; řez dvakrát tlustší a delší (1,8 s světa), 192 jisker ~1 s; čára vodorovně přes zničené patro kolmo na střelu (`ShotDirection` = rychlost na začátku kroku zásahu); Potato má pilu se zuby (`PotatoBallCutter`).
- Po review: stav blastů v `BlastSlots` (bez zařízení), testy krokují skutečné stáří; výstřel přehraje zbytek 6× rychleji (`HURRY_RATE`) a jen když opravdu vyletí; odměna čeká 0,9 s času světa; extra jiskry se kreslí jen když hoří řez.
- ⚠ Obnovený `.bak` má staré mtime: MSBuild ho nepřeloží, po podstrčené chybě `touch`.
- Otevřené: oko majitele, Pi naživo, PresentMon.

## 2026-10-17 — v0.4.0 vydán — desktop (RDT-PC), Claude Code (bs3d-48)
- **v0.4.0 vydán** bez majitele u klávesnice (jeho pokyn): tag `9ed06dc4` (ed7ce2e4 + snímek), běh Release 39024374391, zip 419,6 MB, linux-arm64 401,3 MB, `SHA256SUMS` ověřen ze staženého zipu, jedna složka s `BS3D.exe` + Content/Levels/Music/Sfx. Notes `Images/releases/v0.4.0.md`, snímek Fish (po úvodním průletu: `shot=24`, při `shot=9,12` je v záběru „Click or Space to skip“). Pi: `update-ceilings.sh` → 159 → 176 tabulí (přestavěné Silhouettes a Quarry), API v0.1.26 už od rána.
- Před tagem: majitel odpověděl na stránce verdiktů (13 OK, #859 jedno, Vrať #766/#767 → opraveno ve workflow a zmergováno `ed7ce2e4`), krátký smoke na přesném commitu (Sonnet) čistý.
- ⚠ Monitor usnul → Windows bez zvukového výstupu → každý běh loguje „theme could not be realized … NullReference“, i build ze 16. 10.; stav stroje, ne kód (#923). ⚠ `build.yml` ignoruje `Images/releases/**`, snímkový commit nemá vlastní běh CI.
- Otevřené: verdikty druhého kola (#766, #767, #781) na stránce; #709 do první skutečné dávky; #922 snímky na web.

## 2026-10-17 — #891 #885 #898 #884 #902 #900 vzhled po vydání v0.4.0, stránka verdiktů — desktop (RDT-PC), Claude Code (bs3d-5f)
- Fronta verdiktů klesla na 3 (majitel odpověděl bs3d-48), změny vzhledu se připravovaly na větvích a zmergovaly se až po tagu v0.4.0: lavička v metru plotu (#891), oheň svítí na desku z těla plamene (#885), High Scores pro všechny levely se zapuštěnými nedohranými řádky (#898), vyšší koruny akácií (#884), majáčky v osmi skupinách (#902), řádek Jukeboxu pod vizualizérem (#900). Merge 5de356e6, 0b99c470.
- Stránka „Verdikty 17. 10.“ https://claude.ai/artifact/V2vmb1wY9qRE8Do95vXmCZ, výzva zapsaná na všech šesti issues; fronta je 7.
- ⚠ Herní kamera lavičku na louce nevidí (zakrývá ji okraj ostrova), je vidět jen v úvodu. První tónování řádků High Scores bylo nad světlými scénami neviditelné, review ho chytila.
- Rozpracováno: #893 (nízké oblety v úvodu louky), #883 (savanský keř), #904 (hudba louky).

## 2026-10-17 — #893 #883 #904 úvod louky, savanský keř, hudba louky; jedna stránka verdiktů — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (eef4f283): #893 dva pomalé nízké půlkruhy v úvodu louky (balíky, lavička), úvod 22,9 s; #883 „melouny“ byly keře, na High/Ultra je nahradil trnitý keř z větviček a lístků (+0,03/+0,06 ms); #904 Sunny přegenerována (krátké díry 38 → 6,6/min, opakování 40 taktů → 1), Ska ne (8 kandidátů neprošlo hranicí děr, c3 je k poslechu).
- Jedna stránka verdiktů pro obě relace: https://claude.ai/artifact/V2vmb1wY9qRE8Do95vXmCZ (18 karet včetně #767 #766 #781 #877 #872 #880 #871 #878 #889 od bs3d-48). Rozdělení s bs3d-48: já #887 #890 #896, on #892 #905–#917 #872 #877.
- ⚠ Merge #883 se srazil s #889 v komentářích a dokumentaci, které vyjmenovávají `FineMeshes`; obě položky jsou zachované a 1721 testů je zelených. Řetěz `&&` po konfliktu zastavil push správně, ale `git branch -d` pak smazal lokální větve sloučené jen s upstreamem.
- Otevřené: fronta je zase 17, změny vzhledu a zvuku stojí, dokud majitel neodpoví.

## 2026-10-17 — #879 Fata morgána lehčí, bomby od Seam — desktop (RDT-PC), Claude Code (bs3d-48)
- `Block11_Mirage.cs`: kamenná pětice 328–433 koulí místo 601–673, kamenů 318 místo 637, skupin 14–32 místo 17–41 (Anvil/Keystone/Seam/Obsidian/Cairn o dvě patra míň, Keystone užší s nižšími pilíři, zdi Seam a Cairn končí 3 a 4 patra pod sklem, `SEAM_ROOF`/`CAIRN_ROOF`). Harlequin a Diadem zeslabeny, výstřely a krok stropu upraveny.
- Bomby od třetí kamenné úrovně (Seam 4, Obsidian 5, Cairn 4): konec zdi / okraj žíly; Anvil a Keystone zůstávají jen kámen. Karta bomby je z Vent (#105).
- ⚠ Ceilings pro ranking se mění u osmi levelů, nové žebříčky až po dalším vydání. Zbývá verdikt oka a rukou.

## 2026-10-17 — #767 #877 #872 opravy po křížové recenzi — desktop (RDT-PC), Claude Code (bs3d-48)
- #767: cena řezu, který vyprázdní pole bez kamerového pádu, se rodí hned (`AwardZoom.CutWaitLeft`, `PlayHud.BirthWaitingAwards`); dřív ji čekání 1,1–1,6 s hodilo za 2,5 s závěr a stránka výsledku ji přeřízla.
- #877: hodiny `Stalled` počítají snímek nejvýš 4/120 s (co svět stihne); pod 30 fps předtím předbíhaly svět.
- #872: výstřel na spodní mezi letí v sevřené elevaci (`Cannon.FireDirection`/`FireMuzzle`, i duch), natažení gumy je jen kresba.
- Log `[cinematic] ended ... (skipped)` se čte před vstupem; komentáře o zpomalení pádu opraveny. Každý test viděn padat.

## 2026-10-17 — #901 hrající kapitola, šest drobností z křížové recenze — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge c3503441, dda638a3, testy 1744 zelené): #901 kapitola, ze které hraje nahrávka, nese trojúhelník play u levého okraje, ať je zobrazená kterákoli (jas dál značí zobrazenou); #901 zavřeno s důkazem. #898 identity levelů pro High Scores se čtou na vlákně z poolu (`LevelIdentityCache`). #919 `MusicBake --shipped` je třetí brána v build.yml **i v release.yml** (recenze našla, že tag by ji obešel), číslo 78 → 77 z 140. #875 nápis o ztrátě série a bonus berou odstup z měřítka zaměřovače (`Crosshair.PixelsPerUnit`). #893 test hlídá pásmo záběru 0,7–1,1.
- Poučení: monitor ve spánku = žádné zvukové zařízení (#923), snímek hrající nahrávky jen s dočasným harnessem.
- Audit čtení disku ve snímku (komentář na #400): nic nad ~25 ms, nic k opravě teď.
- Karta #874 na stránce verdiktů (20 položek). Otevřené: #887, #890 až pod strop fronty; #896 čeká na volbu majitele.

## 2026-10-17 — #400 rozpory dokumentace s kódem, tři vlny — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 18bcb416, 1e6e781a, 65cd14bf, 7b7942c5; CI zelené): tři vlny po třech čtenářích (Sonnet) a jednom opraváři prošly celé `docs/`. Opraveno **55 tvrzení, která kód vyvrací** (hodnoty konstant, přejmenovaná API, spouštěcí argumenty, počty scén, soupisy knihoven), k nim komentáře v kódu, které je opakovaly. Ani jedno nebyla chyba v kódu. Seznam po vlnách je na #400.
- Měsíc: čočka je na -7,4 (ne -7,9), úhly přepočteny vzorci z komentářů (obzor 2,5°, hřeben ~4,4°).
- Poučení: čtenář musí každé tvrzení doložit řádkem kódu a opravář ho ověřit znovu; dvě tvrzení se ukázala jen napůl pravdivá. `git worktree remove` selže, dokud je v adresáři cwd session.
- Otevřené: fronta verdiktů 20, #887 a #890 až pod strop; #896 čeká na majitele.

## 2026-10-17 — alokace ve snímku: `params` pole při každém instancovaném kreslení — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge dea2ac62, 04dc28f9): vlákno snímku alokovalo **4,2 KB za snímek** v nečinném menu a 2,3–4,2 KB při hraní; posluchač `GCAllocationTick` ukázal skoro jen `VertexBufferBinding[]` — `SetVertexBuffers(params …)` se dvěma vazbami zapsanými na místě, 14 míst, ~75× za snímek. `InstanceBinding.Set` (Prazsky.Core) používá jedno pole (MonoGame vazby kopíruje, ověřeno dekompilací 3.8.5); po opravě 24 B v menu, 27–100 B ve hře mezi výstřely. `InstanceBindingSourceTests` jiné volání nepustí (viděn padat).
- `logfps` teď píše i řádek `[alloc]` (B za snímek, gen0), BestPractices §3 doplněno.
- Harness s posluchačem typů: `scratchpad/alloc/harness.patch` této session (nepatří do repa).

## 2026-10-17 — brána hudby viděna selhat v CI, obrazovky bez alokací, #873 — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 1b4e500e, CI zelené včetně obou kroků hudby): `MusicBake --gate-scale <0..1>` zpřísní obě meze; build.yml spustí `--shipped --gate-scale 0.25` a spadne, pokud skutečné nahrávky nejsou odmítnuty pro špičku i pro ticho (BestPractices §10). Opraven i formát záporného limitu (`-+10.54`).
- Změřeno řádkem `[alloc]` (Debug): výběr levelu, Help, About s přehrávačem, Jukebox, Settings, High Scores i výsledek stojí na 88 B/snímek jako menu, žádná stránka nepřidává. Vyhledávání `Parameters["…"]` v `Draw` (bouře, sopka, plameny) jsou mikrosekundy, podle §9 bez změny.
- #873 zavřeno jako nahrazené #915 (majitel 16. 10.: celá kapitola znovu).

## 2026-10-17 — kanárek v bráně levelů, zkouška release.yml — desktop (RDT-PC), Claude Code (bs3d-5f)
- Hotovo (merge 4abbb7ec, CI zelené): brána regenerace levelů v build.yml i release.yml nejdřív vyprázdní `Levels.json`; generátor, který nic nezapíše (nebo zapíše jinam), už neprojde tím, že nechá commitnuté soubory být.
- Zkouška release.yml přes „Run workflow“ (běh 39064166894, `dev-4abbb7e`, nic nepublikováno): všechny tři joby a tři brány (levely s kanárkem, GL efekty GamePi, nová hudební) zelené — změny v release.yml z dneška jsou ověřené před příštím tagem.

## 2026-10-17 — po v0.4.0: devět merge, strop fronty, křížová recenze — desktop (RDT-PC), Claude Code (bs3d-48)
- Hotovo (všechno Sonnet, implementace #889 a #892 Opus): #923 zvuk bez zařízení (objektivní, zavřeno), #872 dolní limit bez alarmu (ruce), #877 cinematic v reálném čase, #878 kamera u okraje ostrova, #880 finále kampaně, #871 Swap s pohybem a zvukem, #874 emitory laserů, #879 Mirage lehčí + bomby, #889 rozsocha dubů, #892 potok a rybník v geometrii. #921 změřeno a zavřeno (clip() na Low/Medium nic nestojí), #709 zavřeno (první dávka statistik na Pi: Pennant), #924 založeno (louka po #892: +0,45 ms ve hře, záběr potoka nad 75 Hz; většina ceny v programu pole).
- Jedna sdílená stránka verdiktů (bs3d-5f, 21 karet, db `verdicts`); fronta 17 → hudba #905/#906 zastavena před renderem. Křížová recenze s bs3d-5f: u mých 10 merge 2 chyby (odměna za řez vs. 2,5 s beat při vypnuté cinematic; stall hodiny vs. svět pod 30 fps) a jedna změna chování (#872 střílel pod podlahu), opraveno `ab2e6d24`; u jejích 21 žádná chyba, 6 drobností. Smoke run `f83d983f` čistý.
- ⚠ Před claimem číst stav issue: #895, #898, #891, #902 už měla bs3d-5f. ⚠ Monitor spí = žádný zvukový výstup, nikdo dnes nic neslyšel.
- Otevřené: #924 (měření), odpovědi majitele na stránce; hudba #905–#917 čeká na pokles fronty.

## 2026-10-19 — #927 krátké release notes — desktop (RDT-PC), Claude Code (bs3d-bf)
- Poznámky v0.4.0 zkráceny na dvanáct řádků o hře a jeden závěrečný: stránka vydání 116 → 61 řádků, nadpis „Download and play“ z řádku 80 na 24. Dlouhá verze je v komentáři #927 a na tagu (`blob/v0.4.0`); `Images/releases/v0.4.0.md` je teď krátká.
- Pravidlo je v `docs/build-and-release.md`. Pořadí v `release.yml` zůstává: GitHub řadí assety pod celé tělo, rozhoduje tedy délka, ne pořadí.
- Hotové vydání se upravuje přes `gh release view --json body` + `gh release edit --notes-file`, sekce Download beze změny.

## 2026-10-19 — #904 Ska: kandidát c3 jako bloom-ska — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: majitel 18. 10. řekl, že c3 je lepší než původní Ska; nasazen jako `bloom-ska.ogg` (45 s, bez trimu, post-codec špička -0,25 dBFS), bicí vrstva oddělena znovu, sidecar nese brief, seed, měření a výrok majitele, který pro tuto jednu nahrávku přebíjí hranici krátkých děr (29,3 za minutu proti 27,0; stará 99,2, 21 oříznutých běhů → 0). `MusicBake --shipped`, LevelGen a 1764 testů v pořádku; `[music] bloom: bloom-ska.ogg (drums layered)` ve hře.
- Nic neposlouchal agent; poslech je majitelův.

## 2026-10-19 — #925 „Fire!“ pro hráče, který přestal střílet — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: v prvních pěti levelech první kapitoly se po 10 s bez výstřelu (dvojnásobek 5 s na výstřel z majitelových `Stats.json`), když nemluví karta, alarm ani kamera, objeví tučný jantarový čip „Fire!“ s tlačítkem pro aktuální ruku (LMB, RT) pod zaměřovačem; tepe jednou za vteřinu, výstřel ho smaže, zpět až po 30 s. Pravidla v `FireHint` (čistá logika, testy viděny selhat), popis v `docs/game-feedback/fire-hint.md`.
- ⚠ `turn=`/`walk=` teď hlásí držení kartám traverse/walk; bez toho skript kartu orbitu nikdy nedokončil.
- Otevřené: verdikt newcomer (pauzu u nováčka nikdo neměřil), snímky ve `verdicts\925\`.

## 2026-10-19 — #874 zářiče uvnitř trychtýře, projekční paprsky — desktop (RDT-PC), Claude Code (bs3d-bf)
- Po majitelově „Vrať“: čtyři zářiče sedí na skle trychtýře 0,6 pod okrajem (patka podél normály skla) a hlavice se na čepu natočí přesně na svůj roh sítě; z čočky vede měkký, rozšiřující se kužel, který na rohu přistane. `Cannon.SetKeepouts` je pryč, dělo obíhá volně. Čep třmenu je výš (0,95), jinak by se ocas hlavice zabořil do patky.
- ⚠ Na 16–17 širokých sítích je roh skoro nad čočkou: hlavice míří svisle a kužel měří 0,8.
- ⚠ Kulička přes okraj v ose zářiče jím projde (bez kolize, záměrně).
- Otevřené: `verdict-eye`, snímky ve `verdicts\874b\`.

## 2026-10-19 — #930 karty tutoriálu: jedna akce na krok — desktop (RDT-PC), Claude Code (bs3d-bf)
- Inventura všech karet je na issue. Dvě věci chtěla jediná karta, `combine` (pravé tlačítko + A/D): je z ní dvoukroková karta („Hold to look down the barrel“, po splnění „Now walk the gun round“); zaznamená se po druhém kroku pod starým klíčem, uložené hry se neučí znovu. Ostatní karty jsou jeden rozkaz bez „proč“ (to drží Help), `TutorialCopyTests` je hlídá a na starém textu selhal.
- ⚠ Lekce s držením nehrají skripty `rmb=`/`walk=`: druhý krok je vidět jen v reelu `tutorial=demo`; skutečnou detekci nikdo ručně nehrál.
- Otevřené: verdikt newcomer + oko majitele na formulaci (snímky ve `verdicts\930\`).

## 2026-10-19 — #926 NEW BEST jako oslava — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: po nejlepším výsledku slovo NEW BEST (písmo 200, nad titulem) dopadne po poslední hvězdě s odskokem (pružina, přestřelí na 1,36×); výplň je pastelová duha plynoucí po slově, 24 jisker při dopadu a 8 blýskání dál. Barva jde po vrcholech přes `IFontStashRenderer2` a `BasicEffect`, bez shaderu, takže ji kreslí i Potato a GamePi. Zvuk `NewBestSynth` (trylek zvonků a akord, 99,9 % energie v 300–4000 Hz).
- ⚠ `BasicEffect` vybere techniku až při prvním `Apply` (dřív bílé kostky); obrys `Stroked` přes `DrawText` sedí o `effectAmount` vpravo dole.
- Slovo kreslí `MenuPage.DrawOverUi` nad Myrou, strom drží prázdné místo.
- Otevřené: `verdict-eye`; pohyb a zvuk agent neposoudil, snímky ve `verdicts\926\`.

## 2026-10-19 — #928 Blog na webu: sekce a prvních pět článků — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: `/blog/` (od nejnovějšího, datum, témata, shrnutí), stránka článku ve stávajícím vzhledu, Atom feed `/feed.xml` s plným textem, odkazy z hlavičky, patičky a pod galerií; nový článek je jeden Markdown v `Web/src/blog/` (`docs/website.md`). Pět článků z deníku, docs a issues: mřížka a řez (38 míčů z jednoho, spočteno na Umbrelle), Pi a Potato, hudba a měření děr, 10bitový buffer, dráha kamery kolem ostrova (#878).
- ⚠ Headless Edge má okno nejméně 492 px; 400 px dá až emulace přes DevTools protokol. Markdown běží bez Liquidu.
- Otevřené: oko majitele na vzhled a znění; všechny články mají datum 19. 10., nastaví se při publikaci (`publish.ps1` je majitelův).

## 2026-10-19 — #905 #906 Sunset a Glacier přegenerovány — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: `mural-sunset` (c4 přeřezaný ze 40–120 s surového renderu: první střih měl krok −9,3 dB přes zlom při 1 s; trim −2,5 dB) a `summit-glacier` (c15 z 18 kandidátů, jediný uvnitř `ember` s tichými bicími). Nové `drum-share.py`: starý Glacier měl bicí na úrovni mixu (−2,3 dB v 300 Hz–4 kHz, rodina −6 až −21), Sunset ne (−8,9, rodina −8 až −18). `key-share.py`: tóny na stupnici, Sunset 90,7 → 97,4 %.
- ⚠ Bez zvukového zařízení nikdo neposlouchal; ve hře `[music] … (drums layered)`.
- Otevřené: `verdict-eye` obou, `verdicts\905\`.

## 2026-10-19 — poznámky majitele z hraní (scény, web, UI) → issues — desktop (RDT-PC), Claude Code (bs3d-34)
- Jen založeno, nic nestavěno: BS3D #946–#976, ClaudeUsage #5–#7 (jiný projekt), 19 komentářů (#922 #690 #775 #777 #746 #779 #938 #752 #748 a odkazy na #756 #757 #778 #892 #943 #677 #718 #749 #776 #751). `priority-now`: #958 Cavern, #963 Aurora, #960 špičky smrků, #970 potůček a rybník, #777, #938.
- Pokyn majitele: viditelná chyba ve scéně je bug první kategorie a spoje musí plynule navazovat, i za cenu pomalejšího vývoje.
- Zjištěno: loga v `Game/Logos` mají 1bitovou alfu a nejsou v gitu (#951); kapitola Big Top (91–100) už je prověšená (#690); Testbed aberaci i zrno má (#922); Měsíc kreslí na High/Ultra stejný dvacetistěn jako na Low (#777).
- ⚠ Resume workflow po limitu nevzal kontrolory z cache (prompt nesl `JSON.stringify` výsledku, změnilo se pořadí klíčů): zastavit hned, chybějící krok dodělat úzkým agentem.
- Otevřené: dotazy majiteli (strom v Cavern, náměstí v City, které modré pruhy ve Space).

## 2026-10-20 — #960 levitující špičky smrků — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo (větev `960-spruce-stem`): na High/Ultra neměla osa smrku pod vrcholovým výhonem 1,25–6 jednotek žádné dřevo (vnitřek začíná 1/8 koruny pod špičkou, na polární záři ho vlnění dokonce převrátilo), takže výhon a horní přesleny visely ve vzduchu. Nově jeden kmínek z kůry (stínovaný ×0,55) od vrcholu kmene až do výhonu; větve koření v něm. Low/Medium a Potato beze změny (sítě bajtově shodné). Fish +0,02–0,03 ms.
- ⚠ Testbed `nocap` dával na desktopu nesmysly (rozdíl −0,13 až +7 ms); `fpscap=1000` sedí se hrou.
- Otevřené: verdikt oka.

## 2026-10-20 — #907 hudba Silhouettes přegenerována — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: Fantasy, Jazz, Nordic, Oud a Theatre (`puppet`) nahrazeny v místě, názvy i levely beze změny. 50 renderů ve třech kolech, výběr měřením proti `ember` na vybalených .ogg: Fantasy c1 (flétna a housle, kapela místo orchestru), Jazz c10 přeřezaný 0–100 s (klavírní trio; první střih měl 9,4 % not mimo tóninu), Nordic c3, Oud c7 přeřezaný 10–110 s, Theatre c7. Trim: Theatre −3,5 dB (292 clipped runs), Fantasy a Oud −1,0, Nordic −0,5. Bicí vrstvy má teď všech pět (Fantasy a Jazz dřív žádnou).
- ⚠ Nikdo neposlouchal (stroj bez zvuku). Nesáhl jsem na waltz (222 clipped runs) a triphop (275).
- Otevřené: `verdict-eye`, `verdicts\907\`.

## 2026-10-20 — #963 polární záře přestavěna od základu — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: měsícem osvětlené sněhové pole pod velkou zvlněnou září podle referencí obou modelů. Nebe: tři oblouky (ostrá zelená spodní hrana, paprsky, červený vršek, svítí po úsecích), Měsíc, záře nad obzorem. Sníh jako geometrie (závěje, val u ostrova, závětrné ocasy) a ke každé patě kmene, klády a kamene vlastní „límec“ sněhu (`AuroraCollars`). Měsíc vrhá stíny lesa, přízemní mlha (`SceneFog.fxh`, i pro les), 25 000 vzdálených smrků v mezerách mezi háji až k posledním hřebenům, nové intro. Potato beze změny (i led ostrova). Fish High 9,73 → 9,11 ms, Ultra 9,69 → 9,46, Medium 3,96 → 4,40 (stínová mapa).
- ⚠ Závěj zapečená do mapy (1 jednotka/texel, mřížka 0,75) se rozmazala a paty vypadaly jako válce v rovině; iterovaný průsečík přes záhyb kreslil hranu jako pilu; tmavé nebe pod ostrou hranou četl recenzent jako holý hřeben.
- Otevřené: verdikt oka (pohyb), sníh navrchu klád je jen pruh, APU neměřeno, `verdicts\963\`.

## 2026-10-20 — #947 věta o obraze na Pi — desktop (RDT-PC), Claude Code (bs3d-bf)
- Hotovo: věta „no scenery behind the island“ měla dvě ruční kopie (web, šablona poznámek k vydání). Teď je jedna: `piPicture` v `Web/src/_data/site.json`, web ji tiskne, `release.yml` ji čte do `@@PI_PICTURE@@`. `PiPictureCopyTests` zakazuje ruční kopii a ověřuje, že každá kapitola má na Potatu pozadí. Živá stránka v0.4.0 opravena (`gh release edit`).
- Otevřené: web se opraví až ruční publikací (`Web/publish.ps1`, majitel).

## 2026-10-20 — stav po poznámkách z 19. 10., přestavby, pauza — desktop (RDT-PC), Claude Code (bs3d-bf)
- Od 19. 10. na mainu: #904 #874 #930 #925 #926 #929 #928 #892 #905 #906 #927 #931 #887 #924 #943 #934 #890 #960 #907 #938 #974 #958 (zavřeno na OK majitele) #963 #970 #947. Majitel na stránce „Verdikty 19. 10.“ (https://claude.ai/artifact/BTJogBsnikYAxpjtbND6gh, účet panrdt@live.com, db `verdicts`) dal 15× OK → zavřeno; bez odpovědi zůstaly #963 (aurora) a #970 (potok do rybníka).
- ⚠ Přestavby (#958, #963) i po recenzi: hlavní nálezy byly „spoje nesplývají“ (závěje u pat nebyly vidět, texel 1 jednotka) a chybějící hloubka ostrůvků; recenze před merge se u velkých změn vyplácí. ⚠ Starší stránky patří druhému účtu a nejde je z nového otevřít.
- Pozastaveno na přání majitele (limit potřebuje jinde), rozpracováno nezcommitované ve worktrees `BS3D-wt-400/-933/-936/-952`: #400 (nálezy 13. průchodu v komentáři), #933, #936, #952; claimy uvolněny. #941 vrácen (strop fronty).
- Otevřené: verdikty #963 #970; nové poznámky #946–#978 (bs3d-34 jen zakládá); hudba #908–#917; #777.

## 2026-10-22 — #952 kolečko myši v menu — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: všechny rolující seznamy (Help, Jukebox, Scény, mřížka výběru) stojí v `MenuScrollViewer` místo Myry: pomalý zářez posune pětinu výšky seznamu, rychlé kolečko zrychluje (každý zářez v řadě do 0,1 s přidá půl kroku, nejvýš 4x = 0,8 výšky, jako Page Down na Helpu). Aritmetika je čistá (`WheelScroll`, `WheelScrollTests`), první otočení vypíše jeden řádek `[menu] wheel:`.
- Změřeno ve hře (sonda v `InternalArrange`, bez vstupu): Scény 1080p/4K měly krok 19 px (2,5 % / 1,7 %, 38 / 55 zářezů), teď 154 / 225 px (20 %, 5 zářezů). Reálná cesta `OnMouseWheel` ověřena v běhu: 154, pak 385 (boost 1,5x), pak zpět 77 (obrat resetuje).
- ⚠ Myra 1.6.3: `ScrollMultiplier` krok neladí (10 × obsah / výška viewportu v px, hodnota delty se ignoruje).
- Otevřené: `verdict-hands`, pocit v ruce majitele (kolečko nikdo nezkoušel).

## 2026-10-22 — #400 drift, třinácté kolo — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo (větev `400-drift-pass-13`): 23 nálezů čtenářů (jeskyně, záře, smrky; Cut, kinematika, HUD, tutoriál, tiery) opraveno v komentářích a docích, chování beze změny; k tomu opravená rozepsaná práce třetího čtenáře (potůček, FineMeshes, hudba, web). `WindingCheck --openness`: řádky smrků „pending #960“ lhaly (#960 je mergnuté), zbývají otevřené kořenové prstence větví (4 body) a pata výhonu (5 bodů): strop 10 a 6 a pravdivý důvod.
- ⚠ Číslo v docu nestačí: kinematika „450 / 94“ byla ve skutečnosti 417 / 88 (560 letů v `DropCinematicPathTests`); `MusicBake --shipped` dnes čte 71 z 140. Počet scén `OpenBelow` je sedm.
- Otevřené: řádky `Spring (cavern)` v APU tabulce měří starou jeskyni, nepřeměřeno.

## 2026-10-22 — #908 hudba Reveal: Bossa a Vibes přegenerovány — desktop (RDT-PC), Claude Code (bs3d-dc)
- Hotovo: `nocturne-bossa` a `nocturne-vibes` nahrazeny v místě (názvy i levely beze změny), bicí vrstvy znovu oddělené. 42 renderů v pěti kolech, výběr měřením proti `ember`: Bossa c10 (tenorsaxofon místo flétny nad kytarou; střih 40–110 s z druhé poloviny renderu, smyčka 34,4 s), Vibes c13 (tlumený vibrafon nad kytarou, 120 bpm; střih 10–100 s, 68 s). Oba uvnitř `ember` ve všech sloupcích, bez trimu (staré: Bossa 2,67 změn tónu/s, Vibes 104 clipped runs).
- ⚠ Žádný z 20 (Bossa) ani 22 (Vibes) prvních střihů uvnitř nebyl; rozhodl střih z druhé poloviny renderu. `measure-music.py --key` drží ve stejném klíči i referenci (ember „mimo tóninu“ 47–78 %), takže se nic nezvýrazní.
- ⚠ Nikdo neposlouchal. Nové Vibes mají 11,0 % v 300 Hz–4 kHz (staré 19,6) a nic nad 2 kHz: „slabé“ se může vrátit.
- Otevřené: `verdict-eye`, `verdicts\908\`.

## 2026-10-22 — #979–#995 poznámky majitele založeny (jen zakládání) — desktop (RDT-PC), Claude Code
- Hotovo: 17 issues z jedné dávky: potůček #979, kůra Meadow #980, loga #981 #982, spoj ostrova se zemí #983, úvod Cavernu #984, blog #985, NEW BEST #986, klávesnice G513 #987, benchmark #988, mraky #989, power-upy #990 (plán a rozhodnutí 1–7, `owner-decision`) a jeho bloky #991–#995 (#992–#995 `parked`, dokud majitel neodpoví). Komentáře: #951 (nová loga: RGBA, 256 hodnot alfa; kód je ještě nečte), #970 (ostrá čára v poznámce znovu), #926 #213 #392 #608 #929 #953 #942 #892.
- majitel: power-upy se mají získávat za 4 hvězdy a hromadit; přepisuje to jeho pravidlo (a) z #213 (jeden náboj na level).
- ⚠ Nic nebylo sestaveno ani nasnímáno: `BS3D.exe` na stroji je z 17. 10., před #926, takže #986 je jen z kódu. Popis Logitech SDK v #987 je z paměti a neověřený.
- Otevřené: `Game/Logos/` je dál nezasledované (`??`); `git add` je na majiteli nebo na tom, kdo vezme #951.

## 2026-10-23 — #941 ohyb sítě −0,08, verdikty kola čtyři, poznámky k Help (#996–#1004) — desktop (RDT-PC), Claude Code
- majitel (v chatu, stránka kola čtyři pod jeho účtem nečitelná): OK na #936 #963 #977 #962 #961 #970 #777 #967 #908 #933 #952 (zavřeno s komentářem); #941: „−0,08 vypadá nejlépe“. `LaserNetShape.BOW` je −0,08 (síť stažená dovnitř, rohy beze změny), `LaserNetShapeTests` mají dosah `max(1, 1+BOW)`, doc upraven. 2194 testů, Game.sln i GamePi.sln se sestaví.
- Hotovo: z recenze Help (Gemini) založeno #996–#1003, komentáře k #867 a #977; zbytek #977 (buňka „You: …, not finished“) je #1004.
- ⚠ Dřív se záporný ohyb zavrhl, protože nechá boky kratší než půdorys; teď je to volba majitele. Rozdíl je spočtený z `CornerFor`, ne ze snímku. Nic nenasnímáno.
- ⚠ Recenzní náhrada u Zapu je chybná: Zap bere všechny koule barvy výstřelu z pole, ne „řetěz“.
- Otevřené: #941 `verdict-eye` jen pro potvrzení na snímku, pokud ho majitel chce vidět.
