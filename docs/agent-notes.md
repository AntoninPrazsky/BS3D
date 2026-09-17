# Agent notes — sdílený deník

Sdílený deník pro AI agenty pracující na tomhle repu (ZCode, Claude Code). **Před začátkem práce si přečti poslední zápisy; po dokončení práce přidej vlastní záznam** (datum, kdo, co, stav). Nenahrazuje issues ani docs — je to provozní kronika „kdo co právě dělá / udělal / nechal ležet", aby se dva agenti nepřeskočili.

Pravidla:
- **Cizí rozepsanou práci v working tree nikdo nedotýká** — popiš ji tady a nech na rozhodnutí majiteli.
- Dokončená práce jde **okamžitě na main** (standing rule v `CLAUDE.md`); squash-merge s `(#NNN)` v subjektu.
- Vizuální změny ověřuj screenshoty (`.claude/skills/screenshot`), ne jen buildem.
- **Deník se rotuje po měsících (#358).** Tady je vždycky jen **aktuální měsíc**; starší jdou beze změny do `docs/agent-notes-archive/YYYY-MM.md`. Rotaci dělá ten, kdo píše první zápis nového měsíce. **Hledání jde přes obojí** — `grep -r "..." docs/agent-notes.md docs/agent-notes-archive/`.

---

## 2026-09-01 — Claude Code

**#300: dvě městské kapitoly prohozeny do denního pořadí — Spectrum (svítání) devátá, Arcade (neon) zavírá kampaň.** Větev `300-neon-after-dawn`. Nebyl to mechanický swap, ale rozhodnutí, které issue výslovně žádalo: narativ světla se MUSEL přepsat, ne obejít.

**Nové čtení oblouku, a je silnější než staré:** světlo se po prázdnu vrací geologicky (sopka #295) → přijaté (svítání nad městem) → vyrobené (neon po setmění). Závěr kampaně je teď JEDEN DEN V JEDNOM MĚSTĚ, od rána do noci — „normální město před neonovým" je doslova plynutí dne, přesně majitelovo dvakrát vznesené čtení. Kampaň končí tím, že si město rozsvítí vlastní světla; poslední slovo jede na posledním záznamu setu a stěhuje se z Turbine na **Globe**.

**Obtížnostní půlka issue, přesouzená po #301/#302** (v době založení byla nesouditelná — polovina finále nehratelná): Arcade měří 1,33–1,65 výstřelu na skupinu proti Spectru 1,37–6,67, takže těsnější blok teď sedí poslední — kampaň vrcholí tam, kde končí. Obě půlky původního argumentu tedy ukazují stejným směrem.

**Mechanika:** prohození dvou položek v tabulce BLOCKS + pořadí polí ve WriteLevelSet; odemykací rampa poziční (20 záznamů si přepočítalo brány samo, poslední zůstává 186), PlayerProgress klíčovaný jménem souboru — save přežije. Level soubory bajt za bajtem netknuté, změněny jen Levels.json a komentáře. Hudební komentář se prohozením ZLEPŠIL: „Arcade bere pulse — poslední blok na skladbě, kterou první otevřel" je teď doslova rám kolem celku, který ten komentář vždy chtěl být; bohemia Spectra poctivě přepsána (koda jako závěrečná figura dne, ne finále).

**Přepsáno v témže commitu:** oba array komentáře v Main, hlavičky regionů Arcade (×2) a Spectrum (×2), Eruption (×2), WriteLevelSet doc (nesl zastaralé „the Nebula's finale closes the campaign" — historie posledního slova teď vyjmenovaná celá: Colossus → Garland → Globe → Turbine → Globe), hudební odstavec, `docs/formats-and-tools.md` (třístupňový návrat světla, tabulka, historie vkládání) a `docs/game-session.md`.

**Ověřeno:** LevelGen exit 0 (blok 9/10 Spectrum, 10/10 Arcade, Globe na 95. s bránou 186), ScoreSim exit 0. Sondu netřeba — fyzika levelů se pořadím setu nemění.

**Co zůstává:** merge na slovo majitele. A příště kdokoli u „posledního slova": mění se čtyřmi rozhodnutími, všechna jsou teď vyjmenovaná ve WriteLevelSet docu.

**Nic dalšího si neberu — jdu na #265.**

---

## 2026-09-01 — Claude Code (druhý zápis dne)

**#296 dovřeno z konce, který zbýval: tiery hory přeměřeny na slabém stroji po `8b332af`, a díra v žebříku je na hoře ZAPLNĚNÁ.** Kolegova práce (ocenění snímku, dvě look-identické optimalizace změřené na nule, oběť reliéfu závějí na High) byla na mainu; co chybělo, bylo číslo ze stroje, pro který tiery existují — tabulka #298 byla z doby před obětí a před novým partnerem MountainReduced.

**Změřeno** (APU Ryzen 7 5700U, `BS3D.exe level=Ten quality=<tier> nocap`, okno 1600×900, 70 s, mediány přes benchmark.ps1, podmínky čtené z `[fps]`):

| tier | teď | #298 před |
|---|---|---|
| High | 38,3 ms | 39,06 |
| Medium | 18,0 ms | 18,71 |
| **Low** | **13,0 ms** | 18,73 |

- **Low je poprvé POD rozpočtem stroje** (13,0 proti 16,1 ms limitu 62 Hz) — v #298 se do něj nevešel žádný horský tier. Stroj, který neudržel Medium, má konečně kam jít.
- **Nový partner Low** (třpyt + čtvrtá oktáva skály, na desktopu oceněný 0,18/0,58 ms) **na APU kouše ~5 ms** — „attribution does not travel" z benchmark skillu potvrzené počtvrté: co je na širokém čipu šum, je na occupancy-chudém čipu pětina snímku.
- High −0,76 ms proti #298 — směr i velikost konzistentní s desktopovou obětí závějí (1,08 ms na 12,3 Mpix; tady 4,6 Mpix + drift APU).

**#172 re-scopnut na horu** (komentář v issue, jak #296 výslovně žádalo): čtyři z pěti jmenovaných scén byly zavřené jako nereprodukující (podpis polovičního vsyncu z #270), a z checklist technik jsou dvě na hoře už změřené na nule (předčasný konec fbm — potřetí „runtime branch si nechá registry"; sdílení ddx/ddy — kompilátor je sdílel sám).

**Žádná změna kódu** — měření a bookkeeping; commit je jen tenhle zápis.

**Co zůstává:** #296 je tím zodpovězené celé (ocenění, oběť, tier čísla z obou tříd strojů, #172 re-scope) — zavření na slovo majitele.

**Nic dalšího si neberu.**

---

## 2026-09-01 — Claude Code (třetí zápis dne)

**#256 rozpadnuto na jedenáct implementačních issue a první z nich je na mainu.** Majitel si vyžádal rozpad („jako jeden task by to bylo příliš práce"), pak výběr jednoho typu a jeho stavbu. Tvar rozpadu je doslova #272 → #304 → #305–#312 u stylů: **#323** nese datový model a kontrakt, **#324–#333** po jednom speciálu (Rock, Transparent, Bomb, Zap, Acid, Frozen, Wildcard, Infectious, Gravity, Heavy). Pořadí je doporučení s důvody, tvrdé závislosti jen #327/#328 → #326, #331 → #324, vše → #323. #256 zůstává trackovacím issue a nese komentář s celým rozpisem.

**#323 a #324 shipnuty JEDNÍM commitem, ne dvěma** (`a645aaa`, merge `9f22696`) — a to je vědomá odchylka od toho, co #323 samo o sobě říká („implementuje žádný speciál"). Důvod: enum o jediném členu `Normal` a tři švy, na kterých nic nestojí, se nedají ověřit. #304 si to mohlo dovolit, protože oba jeho shadingy už existovaly.

**Datový model: speciál je druhá osa, ne čtrnáctá barva.** `BallKind` vedle `BallType`, na `StaticBall`, zrcadlený na `PhysicsBall` (kvůli `ClusterCollector`, který vidí jen fyzikální pole), ve formátu jako **nepovinný klíč `"k"` psaný jen když není `Normal`** — precedent #258, takže žádný existující soubor se nepřepsal. `Type14 = Rock` by rozbil šest věcí a ani jednu hlasitě: `BallTypes.Count` dimenzuje buckety, každý člen dluží odstín, census by ho počítal, `RandomBallType` by ho **nabíjel**, `Transmute` by na něj přebarvoval, generátor počítá ve třinácti inkoustech.

**Pravidlo shody čte druh na jednom místě** — `GetConnectedSameTypeCells` přeskočí kuličku, na kterou `BallKinds.Matchable` řekne ne, takže kámen zastaví flood fill přesně jako prázdná buňka. To je **celé** odstraňovací pravidlo Rocku; kámen odchází jen jako vedlejší škoda a `GetCellsDisconnectedFromCeiling` se nikdy neptal, jaký druh kulička je.

**⚠ Obě koncové podmínky musely změnit počet, a selhávají OPAČNĚ.** `CheckLevelCleared` i out-of-shots prohra četly `GetBallsCount`. Level s jedním kamenem se pod ním **nikdy nevyčistí** (hráč dostřílí, koule tam pořád visí, level neskončí) a **vždycky prohraje** (týž počet, druhá podmínka). Obě teď volají `GetMatchableBallsCount` — jedna metoda, ne dva predikáty. Co zbyde stát, když spadne na nulu, se pustí (`ReleaseAllBalls`; konstrukcí nezbylo nic matchnutelného, takže „všechno" JSOU kameny) a neskóruje nic.

**Vzhled: `BallShading.Stone`, jedenáctý člen a jediný úplně bez barvy typu.** Kreslený **vlastní oblastí bucketů** — technika, emise i hloubka tepu jsou per-renderer uniformy, takže „tahle koule je kámen a nedýchá" nemůže cestovat s instancí; je to týž argument, na kterém stojí still rovina z #252, podruhé. Oblast, ne rovina: kámen nemá barvu, takže `LodCount` bucketů místo `TYPE_COUNT × LodCount`, čtyři draw cally místo dvaapadesáti. Kreslí se **první**, protože neprůhledné jde před průhledné. Kámen se **vyváže ze stylu, který level jmenuje** — je to kámen na bublinovém i na lávovém levelu, jinak to není signál.

**Čtyři figury změřené, ne odhadnuté, a všechny čtyři byly napoprvé špatně:**

1. **Součin vln nejde pásmově omezovat po oktávách.** Fleck pole je `sin·sin·sin` (součet dělá rýhy, což je žilkování mramoru a přesně to, čím tohle být nesmí) a útlum na každém činiteli se **násobí**: pole, které mělo zeslábnout na 45 %, zesláblo na 9 % a zrno bylo na herní vzdálenosti pryč. Jeden činitel, měřený proti **součtu** tří frekvencí, protože tam leží nejjemnější obsah součinu. S tím dolů i počet, 30 → 14: zrno dost jemné na to, aby bylo fyzikálně správné, je zrno, které koule na herní vzdálenost neukáže.
2. **Tři čisté vlny vynásobené jsou golfový míček** — pravidelná mřížka stejných důlků v řadách. Fázi rozhodí warp z pole hrbolů (mramorova konstrukce pro žilky: warpuj vstup, ne výstup), zadarmo, pole je stejně už v ruce.
3. **⚠ Fyzikální poctivost byla špatně TŘIKRÁT po sobě.** Kámen nesvítí a nemá prosvit slupkou, tak dostal nulovou emisi — a četl jako osmička. Hráč se na cluster dívá **zdola**, z ostrova, tedy na jeho neosvětlenou stranu, a **každá** ostatní koule ji svítí dvěma cestami, které tenhle materiál nemá: vlastní emisí a `TranslucencyStrength`. Zvednutí těla nepomohlo, nejsilnější ambient v paletě taky ne (a ten byl potřeba tak jako tak — draw napoprvé posílal `null` efektové parametry a spadl na **modrý** `DefaultLighting`). Emise 0,45 zastupuje prosvit; `PulseDepth` zůstává nula. **Kámen je jediná koule ve hře, která nedýchá, a to je to, co ho pojmenuje přes celé pole** — pohyb je první, co oko čte, a jeho nepřítomnost je čitelná na každou vzdálenost a pod každým dómem.
4. **Matný neznamená slepý vůči obloze.** `StoneEnvironment` 0,20 udělal z kamene jedinou kouli, která ignoruje dóm. Při téhle smoothness je odraz rozmazaný až na průměr oblohy, takže ten člen dodává **rozptýlené světlo z oblohy** — většinu toho, čím je skutečný kámen venku osvětlený. Na 0,55.

**⚠ A pátý nález, který je RETRAKCÍ mého vlastního (commit `4e311aa`, merge `12296e0`).** K bodu 4 jsem zapsal do shaderu i do `docs/rendering.md` příčinu: „editorové ball renderery vezou mnohem silnější specular ambient než Testbedu, změřeno sedminásobkem modrého kanálu na žluté kouli". **Kód říká opak: `SpecularAmbientStrength` na kouli nenastavuje ani jeden ze tří programů, všechny sedí na výchozí 1.** A to „měření" bylo ze dvou snímků z **různých úhlů kamery ve dvou různě zarámovaných oknech**, což není měření ničeho. Lekce je stará a stála další kolo: **dva snímky z různých kamer nejsou A/B.**

**Přeměřeno řízeně** (týž level soubor otevřený v obou programech, týž pohled `D1`, průměr přes 11×11 plošku):

| koule | Testbed | editor |
|---|---|---|
| zelená (Type2) | 63 / 113 / 44 | 31 / 190 / 26 |
| žlutá (Type7) | 199 / 169 / 66 | 224 / 204 / 70 |
| kámen (neutrál) | 117 / 100 / 81 | 106 / 94 / 79 |

**Kámen souhlasí** (do 10 %, uvnitř rozptylu mezi běhy) — `StoneEnvironment = 0,55` platí, špatné bylo jen jeho vysvětlení. Rozcházejí se **barvy**, Testbedovy jsou vybledlé a nadzvednuté, a to je **`SkyLightRig.StepOvercast`**: Testbed ho krokuje každý snímek podle mraků, **Game ho záměrně nekrokuje nikdy** (má to okomentované u vlastního rigu: zatažená paleta je psaná pro denní oblohu a soumračné město by *rozsvítila*) a editor nemá mraky. **Editor se tedy shoduje s Game a odchylka je Testbed** — obráceně, než jsem to napsal poprvé.

**Založeno jako #334, a je to nález o nástroji, ne o kameni.** Testbed je program, ve kterém se v tomhle projektu rámuje **každé** barevné rozhodnutí (`campos`/`camtarget` jsou jeho), a jako jediný ze tří přidává zatažení — navíc proměnlivé běh od běhu s driftujícím mrakem. #246 (přerozestup modrých), #294 (olivová) i #315 (černá vs. hnědá) se měřily přes něj. Neplyne z toho, že jsou špatně; plyne, že žádné z nich nebylo čteno pod oblohou, se kterou se hra prodává. Návrh v issue: `noweather` po vzoru `nopost`, což je nejlevnější správná odpověď, a teprve pak přečíst ta tři rozhodnutí znovu. Neutrální povrch se skoro nehne (kámen do 10 %) — proto to nikdo nechytil dřív: kouše to na sytých barvách, tedy přesně na otázkách, kvůli kterým se ty snímky dělají.

**Ověřeno:** LevelGen exit 0, ScoreSim exit 0, čtyři solutiony čisté, kampaň bajt za bajtem nedotčená, a **devět tvrzení o pravidlech proti reálné knihovně** (odhozený konzolový program ve scratchpadu, není v žádném solutionu): druhy z disku, skupina začatá na kameni je prázdná, **žádná skupina nikde neobsahuje kámen** (největší 72 = žlutá rovina minus blok, čili fill jde okolo, ne skrz), pole samých kamenů se počítá jako vyčištěné, round-trip oběma směry, jen kameny píšou `"k"`, a starý soubor (`Full.json`, 1000 koulí) nezapíše `"k"` vůbec.

**Autorsky:** **K** cyklí druh v editoru (vlastní klávesa, ne další položky v barevném cyklu — jsou to ortogonální osy a mixující picker by tvrdil opak), `Testbed\Maps\Rocks.json` je testovací pole (196 koulí, 13 kamenů: blok uvnitř žluté roviny a čtyři rohy pod ní), zastagováno hned podle pravidla o netrackovaných datech.

**Co zůstává:** #334 (rozhodnutí je majitelovo: má Testbed zatažení krokovat dál?). Devět zbylých speciálů — v doporučeném pořadí je další **#325 Transparent**, jediný šev 3 a bez nové odstraňovací cesty. A pro kohokoli, kdo na nich bude dělat: **#323 je předpoklad všech devíti a je splněný**, kontrakt i oba počty stojí na mainu.

---

**#298 dovřeno: notebookový sweep zaplněné příčky, který vlastní záznam issue jmenoval jako chybějící.** Táž pětice levelů a tytéž podmínky jako původní matice (APU, `level=<jméno> quality=<tier> nocap`, 1600×900, 70 s, mediány, podmínky z `[fps]`), měřeno PO výplni příčky a po oběti hory z #296:

| level | scéna | High | Medium | Low | před (H/M/L) |
|---|---|---|---|---|---|
| Ziggurat | neon | 37,7 | 15,6 | **12,4** | 37,9/16,0/15,0 |
| Turbine | město | 32,7 | 13,4 | **10,6** | 32,3/13,7/12,8 |
| One | louka | 27,2 | 15,8 | **10,8** | 26,0/14,0/14,0 |
| Ten | hory | 38,3 | 18,0 | **13,0** | 39,1/18,7/18,7 |
| Spring | jeskyně | 22,4 | 20,6 | **13,5** | 22,3/20,5/20,5 |

- **Low se vejde do rozpočtu stroje (16,1 ms) na všech pěti levelech — před výplní se nevešel žádný tier nikde.** Díra „stroj, který neudrží Medium, nemá kam jít" je zaplněná a změřená: Low se odděluje od Medium o 3,2 (neon) až 7,1 ms (jeskyně).
- **Jeskyně přestala být scénou, které žebřík nepomáhá** (20,5 napříč tiery → 13,5 na Low) — redukovaný program stěny na ni dosáhne tam, kam počet pixelů nemůže (#155).
- **Desktopové ceny tyhle velikosti nepředpověděly a předpovědět nemohly**: řezy oceněné 0,2–1,0 ms na desktopu berou na APU 3–7 ms — „attribution does not travel" změřené najednou přes celou matici.
- **⚠ Provozní past, která stála čtyři buňky: MEETING.** Čtyři běhy proběhly souběžně s Teams hovorem na tomtéž APU a přišly s divokými rozptyly (min–max 29–88 FPS) a inverzí Low>Medium; přeměřené po hovoru jsou těsné (72,8–75,9). Podpis pasti 14 v novém kostýmu — před věřením číslu se podívej, co jiného na stroji běží, a hovor na sdíleném package budgetu je zabiják měření.

**Matice v `docs/game-shell.md` přepsána** (stará čísla vpletená do bulletu o díře jako historie), žádná změna kódu.
**Nic dalšího si neberu.**

---

## 2026-09-01 — Claude Code (čtvrtý zápis dne)

**Jedenáctý blok „The Mirage" (#323/#325) — deset levelů, kampaň je teď 105 vstupů v 11 blocích.** Majitel si vyžádal novou kapitolu, ve které se uplatní oba nové druhy kuliček, s tvrdou podmínkou **jeden nový druh na level, nikdy oba** („aby toho na hráče nebylo moc a pochopil, jak kuličky fungují"), **průhledná první**, hodně **pyramid** (klidně několik malých spojených), **větší clustery** a **kosočtverce / trojúhelníky / drahokamy**. Rockové levely nechal na mně.

**Blok je dva pětilevelové půlbloky.** Sklo: `Facet` (terasovaný oktaedr s čirým lemem každé terasy), `Trefoil` (tři malé pyramidy srostlé nahoře, obtažené sklem po nárožích), `Harlequin` (dutý argylový buben, čiré prošití po jedné diagonále), `Diadem` (čelenka se čtyřmi pyramidami v čirém lůžku), `Solitaire` (dutý stupňovitý solitér, **celý povrch čirý a barva až o prstenec dovnitř**). Kámen: `Anvil` (dno zvonu je kovadlina), `Seam` (diagonální zeď napříč tělem, chladná půlka proti teplé), `Keystone` (arkáda, tři pilíře končí pod klenbou), `Cairn` (dvě zkřížené zdi = čtyři komory), `Obsidian` (spirálová žíla + kamenný lem kotevní vrstvy — **finále visí na kameni, který nikdo neodstřelí**).

**Scéna je sen** — jediné plně postavené pozadí, které žádný level nejmenoval (poznámka v `docs/scenes.md` tím padá). Není to zbytek: signaturou snu jsou *tvrdá skleněná tělesa, která se do sebe přelévají*, takže kapitola o kuličce, která je sklem, dokud se nestane barvou, hraje v místě, kde tohle dělá už samo pozadí. **Jedenáct bloků = jedenáct scén, po jedné.** Hudba `mural` (poslední jednoblokový kus, repríza dorovnává tally — a je to jediná synkopovaná skladba v setu, tresillo 3+3+2, což pod kapitolu o kuličkách, které nejsou tím, čím vypadají, sedí i uchem). Styl **porcelán podruhé** — deset materiálů na jedenáct kapitol končí éru „každé jiný" aritmetikou; repríza je vybraná **proti druhům, ne proti scéně**: ani jeden speciál nebere materiál levelu, glazura je nejhlubší barva a nejopracovanější povrch v sadě, tedy nejširší odstup od kuličky bez barvy i od té neopracované. Bublina by byla průhlednost vedle průhledného druhu, mramor kámen vedle kamenného (a hlavička žuly sama píše, že ji od šedého mramoru dělí **drsnost**).

**Dvě konstrukční pravidla, a jsou zrcadlová.** *Sklo jde na kůži* — nikdy zazděné, nikdy na kotevní vrstvě. *Kámen si dojde ke stropu sám, takže co na konci stojí, je kámen.* Obojí je zabráněné v generátoru, ne jen zapsané.

**Generátor: `Design.Kind` / `BlockKind` + šest bran, které musely poznat druh — a jedna z nich tiše mazala data.**

1. **`RepairLonelyBalls` teď speciály přeskakuje.** Kámen ani sklo nemají barevnou skupinu, takže `GetConnectedSameTypeCells` vrací **prázdno** = skupina nula = pro ten průchod nejhorší osamocená koule v levelu. A oprava je `PutBallAt`, jehož `kind` **defaultuje na Normal** — bez přeskoku by se každý speciál v bloku „opravil" na obyčejnou barevnou kuličku, potichu, a soubor by se zapsal z výsledku.
2. **`Validate` census jen přes matchnutelné** — kámen by vlezl do `counts` a nikdy do `largestGroup` (jeho skupina je prázdná, test `group > g` nikdy nespustí), a report indexuje jedno klíči druhého: barva, kterou nosí jen speciály, byla `KeyNotFoundException`, ne špatné číslo.
3. **`CountGroups`** — jinak je každý speciál vlastní skupina a nikdy se neodškrtne (fill nemá co značit): Cairnových 253 kamenů = 253 fantomových skupin a poměr rozpočtu čte třetinu skutečnosti.
4. **`FindLonelyBalls`** — totéž, z druhé strany.
5. **Dva jmenovatelé se rozdělily**: co je *výstřel* zač (koule na výstřel, one-shot %) se počítá přes `GetRemovableBallsCount`, kotevní zátěž dál přes celý cluster. Na levelu bez speciálů jsou si rovny a všechny historické figury čtou přesně jako dřív.
6. **Nová brána `FindStrandedGlass`** — sklo bez jediného prázdného souseda nelze nikdy obarvit a přitom drží level neuzavřený, a sklo na kotevní vrstvě je **kotva, která se rozpustí** (#301/#302 s tím jediným, co ty issue neměly: koule nevaruje, protože nemá barvu, kterou by varovala). Obojí odmítnuto. Chytila **dva reálné případy**: Trefoilova nároží uvnitř srostlé zóny (proto `TREFOIL_ARRIS_TOP` končí pod ní) a — ten poučný — **duté těleso přestane být duté, když je dost malé**: Solitérova skořepina se u hrotu (rim 1) stala plnou koulí pět buněk napříč a po vyčištění hrotu zbyla jedna buňka se sklem ze všech šesti stran.

**⚠ Sag probe měřil na sklech úplně jinou hru, a to je nález sám o sobě.** `SagProbe` nespouští `BallContactEventHandler` — pokládá kouli rovnou do mřížky — takže **nikdy neobarvoval sklo**: pět skleněných levelů se dalo dohrát jen *osiřením* skla, což udělalo z Diadem a Solitaire „vady rozvržení" (5 z 5) a z Facetu level, co se čistí v deseti ranách. Doplněno `ColourTransparentNeighbours` mezi attach a group check (pořadí handleru), i do `WouldMatch` (s přesným undo — vrátit kouli jako `Transparent` obnoví buňku, sklo nemá barvu, o kterou by přišlo), jinak probe *nevidí ránu, kvůli které průhledná kulička existuje*. K tomu dvě další: **`LoadedColour` jen přes matchnutelné** (jinak by na pěti kamenných levelech nabíjel břidlici — barvu, kterou v nich nenosí ani jedna matchnutelná koule, tedy zhruba každá pátá rána nematchnutelná konstrukcí) a **konec běhu na `GetRemovableBallsCount`** místo `GetBallsCount` (jinak kamenný level dojede rozpočet a ohlásí OutOfShots na levelu, který hráč dohrál — přesně to udělal na Seam a Cairn).

**⚠ A jedna retrakce vlastní práce: `MIRAGE_ROCK_TINT` jsem napsal, zdokumentoval a nikdy nezavolal.** Každý kámen v bloku vyjel v barvě, kterou mu vrátilo `BlockColour` designu — na Obsidianu tedy v jedné z pěti barev, které level hraje, což je přesně to, proti čemu ten komentář argumentoval. **Žádná brána to nemohla vidět**, census kameny přeskakuje. Přejmenováno na `ROCK_TINT` a **vynuceno v `Emit`**, protože pravidlo o tom, co kámen JE, patří na jediné místo, kterým každý kámen projde.

**Tloušťka zdi je věc mřížky, ne vzhledu.** Krok mezi vrstvami mění `dx + dz` o −1, 0 nebo +1, takže **diagonální** rovina o jedné buňce má dveře v každé vrstvě a potřebuje tři (Seam), zatímco **osově zarovnaná** ne (Keystone, Cairn = jedna). A osová zeď se píše `|dx| ≤ ½`, ne `dx == 0`: mřížka posouvá každou druhou vrstvu o půl buňky, takže rovnost by na posunutých vrstvách nenašla sloupec vůbec.

**Dvě chyby v barvení, obě změřené, obě týž tvar — diagonální svar.** `Anvil` s paletou otočenou o jeden krok na vrstvu měl **jednu barvu v jediné skupině 126 koulí** (pětina clusteru na jednu šťastnou ránu): barva pásu na vrstvě *i* padne zpátky na barvu *sousedního* pásu na *i−1* a ty dvě buňky jsou mezivrstevní sousedé. Otočka o dva to láme. Otočka o dva **na každou vrstvu** ale rozdrolila zvon na 36 skupin po patnácti (rozpočet 90 ran na nejmírnějším levelu bloku) → dva kroky na **dvě** vrstvy. `Keystone` má tentýž `+ 2 * i` a ze stejného důvodu (nad klenbou mezi loděmi není pilíř).

**Co sklo kupuje designérovi: tvar, který barva mít nesmí.** Barevný pás o jedné buňce po diagonále je řetěz koulí, které se navzájem nedotýkají — přesně to, co `FindLonelyBalls` odmítá — a sklo nemá barvu, tedy ani skupinu, takže na něj pravidlo nedosáhne. Tři z pěti skleněných levelů na tom stojí. Výplatní figura se tiskne jako **nejhlubší kapsa**: kolik skel stojí kolem jedné dopadové buňky, tedy kolik jich jedna rána obarví naráz. Pětice čte 4, 4, 4, 3 a **12** — Solitérových dvanáct je dutý hrot, kde jediný dopad do prázdna obarví celou špičku kamene.


**Sag probe přes celý balík (105 levelů, opravená probe): nad prahem 4 z 5 skončil JEDINÝ level, a byl můj — `Diadem`, 5 z 5.** A diagnóza byla dvakrát špatně, než byla správně. Vypadalo to samozřejmě: čtyři kyvadla šest vrstev dlouhá visící na prstenci. **Zkrácení stopek nepohnulo ničím** (pořád 4 z 5), rozšíření kroku stropu taky ne. Co říká trace: nedotčený cluster startuje **7,7 nad čárou** a pod ni jde na ráně, která naráz uvolní šedesát koulí — to není prověšení, to je **rozhoupání**. A čeho má prstenec proti plnému tělesu téže šířky málo, jsou **kotvy ve stropě**. Vnitřní poloměr 3,6 → 2,6, tedy 80 kotev → 100 (Anvil má 120 a čte 0 z 5), a level čte **0 z 5** hned při první probe po tom. Díra uprostřed je pořád pět buněk napříč, takže je to pořád čelenka — jen ne tenká.

**Finální pětice běhů přes všech deset:** Facet 0, Trefoil 0, Harlequin 2, Diadem 1, Solitaire 3, Anvil 0, Seam 0, Keystone 0, Cairn 0, Obsidian 2 — **nikdo nad prahem**. (Diadem četl 0 i 1 ve dvou běhech; je to hraniční kolísač jako Cabinet a Globe, ale hluboko pod čtyřkou.) Nejvýš sedí `Solitaire` a to je čekané: celý povrch čirý znamená, že skoro každá rána nejdřív barví a až potom bere.

**`Seam` a `Cairn` dojedou nejhorší běh na OutOfShots** — a to je společnost, ne vada: totéž dělá `Pagoda`, `Gantry` a `Static`, tedy pět levelů ze 105 a všechny v nejtěžším pásmu. Cairnu jsem přesto přidal (66 → 72 ran, 1,50 → 1,64 na skupinu): čtyři komory jsou účetnictví, ne čtení tvaru, a je to kapitola, ve které se hráč pořád ještě učí pravidla. Na 72 ranách probe pořád nedojde do konce, takže to není o rozpočtu — probe je průměrný střelec a tohle je level, který chce plán.
**Ověřeno:** LevelGen exit 0 (105 levelů, blok 11/11 `Dream` sky 10 `mural` porcelain), ScoreSim exit 0 („All levels rate the right way round" přes všech 105), čtyři solutiony čisté, deset nových `.json` zastagováno hned podle pravidla o netrackovaných datech, **starých 95 souborů bajt za bajtem nedotčených** (`git diff` v `Game/Levels` hlásí jen `Levels.json`). Rozpočty jsou naladěné z tištěných figur a sedí v pásmu balíku (medián 2,57 ran na skupinu): 3,40 / 2,71 / 1,71 / 1,80 / 2,62 pro sklo, 2,38 / 1,80 / 1,50 / 1,64 / 2,78 pro kámen. Kotevní zátěž 3,7–14,7, tedy nejnižší konec balíku (nahoře je Giza 139).

**Co zůstává / co jsem NEudělal:**

- **`aimcheck` na těch deseti neproběhl.** Potřebuje grafické zařízení a je to sweep; v paměti mám od majitele, že se desktop pod zátěží tvrdě resetuje a mám se před sweepem ptát. Všech deset je rámovaných vcelku v poli 16 nebo 17, což je tvar, který ta kontrola nikdy neodmítla (nejstrmější v balíku je Donutových 72,0° na témže 17 širokém poli) — ale je to nedoběhnutá kontrola, ne doložený výsledek.
- **Screenshoty nejsou.** Blok je vizuálně nový (první čirá kulička a první kámen v kampani, první level ve snu) a `.claude/skills/screenshot` je jediné, co řekne, jestli čiré sklo čte proti porcelánu pod mramorovanou oblohou snu. Stejný důvod jako výše.
- Zbývajících osm speciálů z #256 (#326–#333). Blok je stavěný tak, aby další druh znamenal další kapitolu, ne přepsání téhle.

---

## 2026-09-01 — Claude Code (pátý zápis dne)

**#326 Bomb — druhá odstraňovací cesta ve hře, a ta, na které stojí #327 (Zap) a #328 (Acid).** Majitel si ji vybral z nabídky po dokončení Mirage. Issue samo píše „decide deliberately here, not incidentally", protože ty dvě další zdědí, co se tady rozhodne — takže tenhle zápis je hlavně seznam rozhodnutí.

**Kdy bomba bouchne: když rána dopadne do buňky VEDLE ní.** To je pravidlo průhledné kuličky s jiným koncem, a je to jediné čtení „when it is hit", které hráč může sledovat a mířit na něj — hráč míří na mezeru vedle kuličky, nikdy na kuličku samotnou. **Bouchnou všechny sousední**, ne jedna vybraná, ze stejného důvodu jako `ColourTransparentNeighbours`: výběr mezi několika jsou neviditelné kostky, dva stejně vypadající dopady dělají různé věci.

**Pořadí uvnitř dopadu: obarvit sklo → dokončit skupinu → odpálit.** Obě půlky jsou rozhodnutí. Ozbrojené bomby se sbírají **před** releasem (bomba, kterou release osiří, už spadla a nesmí bouchnout ve vzduchu — detonace každou buňku znovu ověří a přeskočí ty, co odešly), a **vlastní shoda běží první**: hráč, který dopadne vedle bomby a přitom dokončí skupinu, si tu skupinu zasloužil, a výbuch, který ji sežere dřív, než se spočítá, čte jako hra odmítající dobrou ránu. Skládají se v tom pořadí, v jakém je hráč udělal.

**Destrukce není release, a to je ta podstata.** Všechno, co hra dosud brala, odešlo přes `ReleaseSameTypeCluster` — skupina jedné barvy plus co našel disconnection walk. Oběti výbuchu nikdy nebyly skupina, takže tvar je **vyber množinu buněk geometrií, odeber je, a pak nech projít disconnection pass přes to, co zbylo** (`BallsConstraintsBuilder.DetonateBombs`). Ten poslední krok není volitelný a je to důvod, proč to nemůže být smyčka na call site: díra otevřená pod půlkou clusteru ji osiří a nikdo jiný by si toho nevšiml.

**Poloměr je procházka mřížkou, ne rozsah indexů.** `BLAST_RADIUS = 2` **ve světových jednotkách**, a to je ta jediná věc, kterou autor levelu rozmýšlí: mřížka je anizotropní (liché vrstvy posunuté o půl buňky, vrstvy 1/√2 od sebe), takže „dvě buňky" znamená dvě různé vzdálenosti podle toho, kterým směrem počítáš — jako *vzdálenost* je to koule, což je to, co hráč vidí. Rozsah indexů jen **ohraničuje** procházku a každý kandidát se pak měří přes `BallsMap.GetRealPosition`.

**Výbuchy řetězí, přes worklist a ne rekurzí** (varování z issue). Worklist je zároveň to, co dělá terminaci zjevnou: bomba zasažená cizím výbuchem se **zařadí do fronty a schválně se nezničí jako oběť**, aby se dostala k tomu bouchnout sama; když ji fronta vydá, zničí sebe (je ve vlastním poloměru na nulové vzdálenosti). Mapa se jen zmenšuje a už zničená buňka se přeskočí.

**Oběti padají, vyhozené ven** (doporučení issue). Koule, co zmizí z existence, zahazuje nejlepší zpětnou vazbu, kterou tahle hra má — padání, drain, zvuk i drop cinematic už existují. Bomba sama je na nulové vzdálenosti a nemá směr ven, takže jde **dolů**: to, co vybuchlo, vypadne z díry, kterou udělalo. (Normalizace nulového vektoru je NaN rychlost, kterou Bepu odnese rovnou do pózy a nikdy se z ní nevrátí — proto ten práh 1e-4.)

**Skóre: třetí kategorie, `Destroyed`, za stejnou sazbu jako `Matched` a schválně ne za sirotčí dvojnásobek.** Hráč na ně mířil přesně tak jako na skupinu, takže poctivá je sazba shody; co bomba platí, je v **počtu**, který je i tak několikanásobek běžné skupiny, a platit ji navíc sirotčí sazbou znamená zaplatit hráči dvakrát za jednu ránu. Sirotčí sazba není „sazba za velký pád" — existuje jako odměna za jedinou ránu v téhle hře, která se musí **vyčíst** místo namířit (přeseknutí podpory), a výbuch je pravý opak: rána, která nepotřebuje vyčíst nic.

**⚠ A dveře „spent shot" musely přestat číst jen `matched`.** `ScoreKeeper.Landed` četlo `matched <= 0` → `Missed()`. Rána, která odpálí bombu a nedokončí žádnou skupinu, by tím byla **minutá**: přetržený streak a nula bodů za třetinu clusteru. Teď je to `matched <= 0 && destroyed <= 0`, a `BallsReleased.Any` se rozdělilo od „dokončilo to skupinu" na „udělalo to něco".

**#173 visí nad tou sazbou a je to poctivě zapsané: je vyargumentovaná, ne změřená.** `ScoreSim` hraje shipnuté levely a v žádném bomba není, takže na tuhle otázku umí odpovědět až ve chvíli, kdy nějaký bombový level vyjde. Shipnutá sada je nedotčená, protože třetí argument `Landed` defaultuje na nulu — ScoreSim projde beze změny a kampaň je bajt za bajtem stejná.

**Vzhled: `BallShading.Bomb`, třináctý člen a třetí bez barvy typu; pátá bucket oblast.** A tady je ta zajímavá věc: **co říká „ozbrojená", není figura na plášti, ale POHYB.** Hlavička kamene si zapsala, proč je kámen čitelný přes celé pole a pod každým dómem — je to jediná koule, která **nedýchá**, a pohyb je první, co oko přečte. Bomba bere druhý konec téhož kanálu: `PulseDepth` 1,0 a `PulseSpeed` 2,6 proti clusteru v klidu na 0,55 a 1,1, při emisi 1,25. Nic nakresleného na plášti tu práci udělat nemůže, protože na herní vzdálenost je figura pár pixelů široká a rytmus není. Je to zároveň důvod, proč to musí být vlastní draw: emise, hloubka i rychlost jsou per-renderer uniformy — argument still roviny (#252) potřetí.

- **Emise se násobí MASKOU ŠVŮ, ne celou koulí.** Plášť je rozřezaný na šest šířkových pásů (rovnoměrně v **úhlu**, přes `acos`, jinak se u pólů shluknou a čte to jako klubko), náboj hoří v drážkách mezi nimi a kolem pasu je věnec nýtů — jediná část figury, která přežije, když se pásy slijí. Koule blikající takhle hluboko a rychle celou plochou by stroboskopovala.
- **Plášť je tmavý a schválně teplý** (0.115, 0.098, 0.092): tmavý, aby měl náboj proti čemu svítit, a teplý, protože neutrálně skoro černá koule je Type8 — táž past, kterou si kámen zapsal ze šedého konce. Černý taky ne: tělo na nule nemá, na čem by seděla obloha, a silueta zmizí pod tmavým dómem.
- **⚠ `DrawBombs` vrací rychlost tepu ručně.** Hloubku vrátí `DrawPlane`, který ji **uvádí** při každém běžném draw (disciplína toho souboru), ale rychlost se nastavuje jednou při stavbě rendererů a nikdo ji per frame neuvádí — bomba v ní ponechaná by posadila celý cluster na svůj tep do konce snímku. Vráceno v `DrawBombs`, a ne naučením `DrawPlane`, protože ten se vrací brzy, když není co kreslit, takže se na něj nedá spolehnout jako na restore.

**Brána generátoru se rozšířila, a jako VLASTNOST místo výčtu.** `FindStrandedGlass` → `FindStrandedSpecials`: „walled in" teď platí pro *odstranitelný, ale nematchnutelný* — tedy „koule, kterou musí dosáhnout dopad vedle ní". Dnes to jsou Transparent a Bomb; další speciál, který na to odpoví ano, je zabráněný v den, kdy vznikne, ne v den, kdy si na tenhle soubor někdo vzpomene. **Půlka „na kotevní vrstvě" zůstává jen pro sklo**, a je to rozhodnutí: bomba tam stojí strop mnohem víc, ale celé to, proč bylo sklo odmítnuto, je že **nevaruje** — a bomba je nejhlasitější koule ve hře a bouchne jen proto, že na ni hráč mířil. To je past, kterou hráč vidí a volí.

**Sag probe dostal tutéž paritu, tentokrát schválně a ne objevem** — a je z toho pravidlo pro všech osm zbylých speciálů: *krok dopadu, který žije v contact handleru, se musí zopakovat v probe, jinak probe měří jinou hru.* Detonace je takový krok, takže `FireOneShot` sbírá ozbrojené bomby před releasem a odpaluje po něm, v pořadí handleru. A `WouldMatch` odpovídá **true na každou buňku, která ozbrojí bombu**, ještě než sáhne na mřížku: výbuch není barevná otázka vůbec, a bez toho by probe hodnotil každou mezeru u bomby jako „drží, ale nematchuje" a dopadal tam jen z nouze.

**Ověřeno — dvacet tvrzení proti reálné knihovně v reálné simulaci** (odhozený konzolový program ve scratchpadu, není v žádném solutionu; `PhysicsWorld`, kinematické sklo, `BuildBallsStructure`, pak `DetonateBombs`). Poloměr je koule (55 obětí proti 55 buňkám spočítaným nezávisle z `GetRealPosition`), nic nad výbuchem mimo něj nezmizí, kámen i sklo uvnitř jdou taky, bomba zničí sebe, **řetěz doskočí na bombu 4,0 daleko (mimo první poloměr) a vezme s sebou VLASTNÍ poloměr** — tedy řetěz je detonace, ne odebrání, disconnection pass běží po výbuchu a sirotci se počítají zvlášť (5 zničených + 6 osiřelých + 1 stojící kotva = 12 ve sloupci), zazděná bomba nemá jediného prázdného souseda, oba predikáty, pole samých bomb se nepočítá jako vyčištěné, round-trip `"k":3`, a **jen deset Mirage souborů v celé kampani nese klíč `k`**.

K tomu: LevelGen exit 0, ScoreSim exit 0, čtyři solutiony čisté, **kampaň bajt za bajtem nedotčená** (`git status` v `Game/Levels` prázdný). `Testbed\Maps\Bombs.json` je testovací pole — 197 koulí, 5 bomb: jedna v otevřeném prostoru pod špičkou (ta, na kterou se míří), dvojice buňku od sebe (řetěz), jedna v kotevní desce a jedna zazděná v těle bez jediného prázdného souseda (případ, který brána v authored levelu odmítá; tady je schválně, jako `Glass.json` drží jedno nedosažitelné sklo). Hint klávesy **K** v editoru přepsán tak, aby nešel zestárnout — jmenuje osu, ne výčet; aktuální druh se stejně tiskne pod tím (a je to táž hniloba jako #320, kterou tenhle commit **neřeší** — `L` pořád jmenuje dva materiály z deseti a `V` sedm scén ze sedmnácti).

**Co zůstává / co jsem NEudělal:**

- **Bomba nebyla vidět.** Žádné screenshoty, žádná změřená cena snímku na hustém clusteru s pevnou kamerou — obojí chce grafické zařízení, a v paměti mám od majitele, že se desktop pod zátěží tvrdě resetuje a mám se ptát. **Tohle je ta polovina issue, která zbývá**: „it must read as *armed* before it is hit, from play distance, on all ten `BallStyle` materials". Argument, proč to má fungovat, je zapsaný (tep jako opak kamene); ověřený není. `Testbed.exe Testbed\Maps\Bombs.json` plus `balls=<styl>` je nejlevnější způsob, jak to projít přes všech deset materiálů.
- **Sazba `DestroyedBallPoints` je vyargumentovaná, ne změřená** — viz výše, `ScoreSim` na to umí odpovědět až s bombovým levelem.
- **Žádný shipnutý level bombu nemá.** To je schválně: kapitola je práce na příště a #326 měl postavit mechaniku, ne ji vysadit do kampaně.
- Zbylých sedm speciálů: **#327 Zap a #328 Acid teď mají, na čem stát.**

---

## 2026-09-02 — Claude Code

**Vizuální průchod bomby (#326) na majitelovo slovo — a našel dvě reálné vady a jednu vyvrácenou domněnku, přičemž ta domněnka byla moje vlastní a stála v komentářích jako fakt.**

Rig: `Testbed.exe Testbed\Maps\Bombs.json`, `campos`/`camtarget` (pevná kamera — moje vlastní zapsané pravidlo, které jsem zpočátku porušil tím, že jsem měřil přes herní kameru z `F10`, která se mezi běhy hýbe), `F5` na zmrazení simulace, aby se cluster mezi snímky nehoupal, `balls=<styl>` pro deset materiálů.

**1. Náboj byl band-limitovaný do neexistence.** `BombPS` násobil emisi **band-limitovanou** maskou švů, takže jakmile pásy klesly pod pixel, zhasl s nimi i žár — a tedy celý „ozbrojený" read zmizel přesně na vzdálenosti, kvůli které existoval. Změřeno: teplo na pixelech bomby spadlo ze 168 na 65 kódů R−B proti detailu. Náboj teď konverguje k **podlaze** (`BombFarGlow`) místo k nule: když figuru nelze rozlišit, nese žár celý plášť. Není to zachování energie — drážky jsou asi patnáctina povrchu, takže poctivé rozprostření jejich světla nechá kouli stejně tmavou — a je to týž typ rozhodnutí, jaký si zapsala hlavička kamene v opačném směru (kámen dostal emisi, kterou fyzikálně nemá, protože bez ní četl jako osmička). Co musí přežít vzdálenost, je **signál**.

**2. Podlaha nemohla jít přes `BallEmission` a žádná hodnota `PulseDepth` by to nespravila.** Při hloubce 1,0 nemá identita `(1−depth)·occlusion² + depth·beat` klidový člen vůbec, a `Heartbeat` je lub-dub — dva úzké gaussiány a pak dlouhý klid — takže při 2,6 tepu/s je svítivé okno asi 30 ms z 385. Šest snímků v náhodných fázích: **pět četlo bombu na R−B 1,9, mrtvě černá, jeden na 10.** Stáhl jsem hloubku na 0,6 a **snímky se nezměnily** — a to je ten nález: klidová polovina se násobí **occlusion na druhou**, což je pravidlo pohřbení z #303 (světlo zahrabané v hromadě se schválně tlumí, aby cluster neseděl na emisivní podlaze, pod kterou ho žádné AO nedostane). To pravidlo je správné a je to přesný opak toho, co bomba potřebuje — bomba uvnitř hromady je ta, kterou je nejvíc potřeba vidět. Takže podlaha musela být člen, na který okluze nedosáhne: `BombRestingGlow`, přičtený v shaderu vedle `BallEmission`, a hloubka zpátky na 1,0, aby tep jel jako čistý tep na rozsvícené podlaze (tep jede neokludovaně už z návrhu `BallEmission`, což je právě to, co ho drží čitelný uvnitř clusteru).

**⚠ 3. A ta vyvrácená domněnka: TEP NENESE READ NA HERNÍ VZDÁLENOST.** Napsal jsem do kódu na třech místech, že bombu přes celé pole pojmenuje pohyb — kámen se pozná tím, že jako jediný **nedýchá**, tak bomba tím, že dýchá nejvíc. Mechanismus funguje a je změřený: pevná kamera na 11 jednotkách, osm snímků v osmi fázích, plášť se houpe **1,35×** a sedí na R−B 68..92, což je živý uhlík. Přes tutéž pevnou kameru na odstupu, ze kterého se level opravdu hraje, těch samých osm snímků houpe pláštěm **1,08×** na plášti s R−B 8..9, tedy prakticky neutrálním. Na té velikosti jsou pásy pod pixelem a světlo, co na nich jede, se zprůměruje pryč. Tři průchody s tím pohnuly (odband-limitovaný náboj, neokludovaná podlaha, `BombFarGlow` na 0,85) a **A/B téhož výřezu před a po se pořád špatně rozeznává.**

**Komentáře jsem opravil, ne obhájil.** CLAUDE.md říká, že špatné „proč" ponechané stát je horší než žádné, a tohle „proč" bylo napsané sebejistě na pěti místech (hlavička techniky, `BOMB_EMISSION`, `DrawBombs`, `BallShading.Bomb`, `docs/rendering.md`) plus jednou v odůvodnění brány generátoru. Všechna teď nesou naměřená čísla a větu, že další páka **není rytmus, ale množství světla** — náboj by musel být několikanásobný nebo figura několikanásobně hrubší, a to je rozhodnutí o vzhledu, ne o aritmetice.

**Co PROŠLO:** sweep přes všech deset materiálů. Bomba je identická a je zjevně ta odlišná na každém z nich — včetně **lávy**, které jsem se bál nejvíc: lávová kůra svítí sítí prasklin, bomba rovnými šířkovými pásy, a spletou se nedají. Detail čte přesně jak byl navržený: tmavý žebrovaný plášť, žhavé drážky, nýty kolem pasu, nezaměnitelně vyrobený předmět a ne obarvená koule.

**Stav #326: mechanika hotová a ověřená, vzhled hotový zblízka a nedodělaný zdaleka.** Nechávám to na majitelovo oko — je to volba o tom, jak křiklavá bomba má být, a v paměti mám jeho vlastní preferenci vážit tyhle věci směrem k dopaminu, ne k zdrženlivosti. Cena snímku pořád neměřená.

---

## 2026-09-02 — Claude Code (druhý zápis dne)

**Majitel se na bombu podíval a pojmenoval vadu přesně: „ty červené linky jsou doopravdy moc úzké, takže bomba z dálky nevypadá jako bomba — není vidět to červené blikání."** Byly to tři vady naráz a všechny tři sedí ve stejné rovině — velikost svítící figury, ne množství světla, což je přesně ta páka, o které jsem v předchozím zápisu tvrdil, že je to množství světla. Tvrdil jsem to špatně.

1. **Drážky byly moc úzké a moc ostré** (0,22 při sharpness 2,4). Komentář, který na nich stál, argumentoval pro úzké — „náboj musí číst jako světlo vycházející ze SPÁRY, a široká drážka je namalovaný pruh". To platí o bombě v ruce a neplatí o kouli dva tucty pixelů široké, která musí říct OZBROJENA dřív, než jde vůbec nějakou spáru rozlišit. **Obě půlky musely hnout, a ta druhá se snadno přehlédne**: šířka je dosah masky, sharpness je mocnina nad ní, takže vysoká sharpness stáhne svítící jádro zpátky, ať je dosah jakýkoli — 2,4 zahazovalo většinu toho, co šířka koupila. Teď 0,35 a 1,5, a pásů je pět místo šesti (šest tenkých je závit, a závit čte jako šroub, ne jako bomba).

2. **⚠ Band limit zeslaboval vzor šestkrát dřív, než bylo potřeba.** Faktor 2,0 na počtu pásů posílal limit na nulu při footprintu 0,1, zatímco pás byl pořád několik pixelů široký a dokonale rozlišitelný — plášť se rozpustil v plochou záři přesně na vzdálenosti, kde měla figura pracovat. Pás zabírá asi π/BombBandCount povrchového parametru, tedy nějakých 0,63 radiánu při pěti pásech, takže aliasovat začne teprve u footprintu blízko toho. Faktor je 0,5, což je zhruba tam, kde Nyquist opravdu je.

3. **⚠ Tep byl moc rychlý na to, aby ho šlo vidět, a to je kontraintuitivní.** `Heartbeat` má svítivé okno pevné jako ZLOMEK cyklu (asi třináctina), takže rychlejší tep nebliká víc — bliká **kratčeji**. Při 2,6 Hz trvá záblesk asi 30 ms, což je na hraně toho, co člověk vůbec zaregistruje; šest snímků v náhodných fázích ho chytilo jednou. `BOMB_PULSE_SPEED` je 0,5 — pomalý, nezaměnitelný tep, pořád nic jako rytmus clusteru. **Střída se tím nezměnila a statistika taky ne**; změnilo se jediné, na čem záleží, totiž jak dlouho jeden záblesk trvá pro oko. Na to v tom souboru není měření, je to úsudek.

**Změřeno po opravě**, pevná kamera na herním odstupu, osm snímků přes jeden cyklus: plášť běží **106 až 151** kódů červené, výkyv **1,43×**, a chytilo ho několik z osmi — zatímco předtím totéž vzorkování chytilo záblesk jednou ze šesti. Čte to jako tmavý žebrovaný plášť, jehož pásy jdou z matně hnědé do jasně oranžové a zpátky. Sweep materiálů znovu prošel (bomba je ta odlišná i proti lávě, která svítí sítí prasklin, a proti plazmě, která svítí celá).

**⚠⚠ A teď to nejdůležitější, co z toho celého plyne: TŘI KOLA „MĚŘENÍ" PŘED TÍMHLE BĚŽELA NA TESTBEDU, KTERÝ NEMĚL EDITOVANÝ SHADER.** Vyvodil jsem z nich, že bomba je „moc široká, oranžová koule", stáhl jsem šířku zpátky, a pak jsem z nich vyvodil retrakci, kterou jsem **zapsal do pěti souborů a commitnul** (`e71fdff`). Všechno to bylo neplatné. Dvě nezávislé příčiny:

- **`-c Release` staví jinam.** Skill i jeho `-Exe` default míří na `bin\net10.0-windows` — to je **Debug**. `dotnet build -c Release` píše do `bin\Release\net10.0-windows`, takže každý „rebuild" projde, hlásí nula chyb, a spouštěná binárka je hodiny stará.
- **MGCB přeskočí `.fx`, jehož `.xnb` je novější — a pak nezkopíruje nic.** Content task kopíruje jen to, co v tom běhu sám postavil. `dotnet build` vypíše `Skipping …\InstancedModel.fx` a ohlásí úspěch. Smazat `Testbed\bin` **nestačí**; teprve smazání `Testbed\Content\bin` (MGCB intermediate) vynutí překlad i kopii.

**Chytil to až obarvený konstantní náboj na ZELENO** — koule zůstala oranžová, takže bylo jisté, že pixely na obrazovce nepocházejí ze souboru na disku. Do té doby jsem tři kola ladil podle měření, která „ukazovala" tři různé věci, a dvakrát jsem si na jejich základě opravil vlastní správný krok. **Zapsáno do `.claude/skills/screenshot/SKILL.md`** jako kontrola před každým snímkem shaderu (`.xnb` musí být novější než `.fx`) a jako zelený test, když je výsledek překvapivý. Komentáře v těch pěti souborech jsou přepsané na to, co doopravdy platí.

**Poučení pro příště, obecnější než tenhle bug:** když měření třikrát po sobě řekne něco jiného a každé z nich vede k jinému ladění, není to hádanka o shaderu — je to signál, že měřicí řetěz nemá zavřenou smyčku. Ověřit, že běží to, co jsem napsal, mělo přijít jako první krok, ne jako pátý.

Ověřeno: čtyři solutiony čisté, LevelGen exit 0, ScoreSim exit 0, kampaň nedotčená. Cena snímku pořád neměřená (zbytek #326).

---

## 2026-09-02 — Claude Code (třetí zápis dne)

**Led (#337) — majitel řekl „vypadá to jako prošívaná kůže" a měl pravdu doslova: vada nebyla velikost figury, ale její PRAVIDELNOST.** Praskliny stavěly tři `SeamLine` pole — tři rodiny rovných, stejně rozestoupených pásů kolem hlavních kružnic. Tři pravidelné rodiny čar přes plochu je přesně konstrukce, na které se prošívá kůže. Žádná šířka ani frekvence to nespraví; jemnější prošití je pořád prošití. Led se láme na nepravidelné mnohoúhelníkové desky, které se stýkají po třech v bodě po hranách nestejné délky — což je Voronoi, tak je z toho Voronoi: `VoronoiEdgeCell3` nově v `Noise.fxh` (27 hashů, vrací i **id buňky**, aby se dala stínovat deska a nejen síť mezi nimi), čtený nad **object-space směrem**, protože koule nemá bezešvou 2D parametrizaci — azimut/elevace se štípne na pólech a trhne na řezu `atan2`, a to všechno leží přesně tam, kde je silueta.

**Tři věci k tomu musely přijít, a každá z nich je vlastní nález:**

1. **Šířka praskliny se vlní po její délce** (0,18× až 1,55× `ICE_CRACK_WIDTH`), takže jedna hrana jde od mezery k vlásečnici a skončí. Síť nakreslená jednou šířkou po celé kouli je *nakreslená síť*, a to je druhá polovina toho, proč staré praskliny četly jako steh.
2. **Deska nese vlastní hodnotu, a to ze DVOU stran naráz.** Deska, která se jen zesvětluje, je neviditelná na bílé (rameno tonemapu ten rozdíl sní); deska, která se jen ztmavuje, je neviditelná na osmičce (tint 0,045 šedá, pod ní nic). Takže `IcePlateShade` bere hodnotu z těla a `ICE_PLATE_CONTRAST` přidává studené světlo zpátky. **Zkoušel jsem nejdřív per-deskový náklon normály a zahodil ho** — po částech konstantní pole nemá uvnitř desky gradient a na hranici má nekonečný, takže přes `PerturbNormalFromHeight`, který derivuje, dostane každá prasklina jednopixelový hrot. To je přesně ta tvrdá šachovnice, před kterou ten soubor o dvě stě řádků výš varuje.
3. **Jinovatka byla JEDNA vlna po jedné ose, tedy pruhy, ne zrno.** Schovávala je hustá síť prasklin; jakmile byly desky velké a hladké, šrafování vylezlo — ve stejném snímku a ze stejného důvodu jako to prošití, na které bylo namalované. Teď čtyři vlny po smíšených směrech.

**⚠ Dvě chyby, které jsem udělal a které stojí za zapsání, protože obě jsou o tom, čím se figura MĚŘÍ:**

- **Počet buněk jde přes PLOCHU, ne přes obvod.** Spočítal jsem plátky jako `2·π·f` po hlavní kružnici a nasadil f = 3,2. Správně je `4·π·f²` přes celou kouli — první ladicí kolo mělo na kouli **padesát oblázků** a četlo to jako mozaiku. Shipuje f = 1,8, tedy nějakých 37 desek na kouli, tucet na přivrácené polokouli.
- **Band limit na TENKOU ČÁRU musí začínat pozdě.** Napsal jsem ho jako rampu od nulového footprintu, což je tvar, který má v tom souboru každý jiný limit — jenže každý jiný jede přes celou VLNOVOU DÉLKU. Prasklina je čára široká `width/frequency`, takže rampa od nuly ji utlumila na **43 % na kouli široké devadesát devět pixelů**, kde byla čára pořád čtyři a půl pixelu silná a dokonale ostrá. Konstanta 1,5 v `IceCrackBandLimit` je právě tohle: drž plnou sílu, dokud čára není asi pixel, a teprve pak zhasínej. Deska se tlumí zvlášť, proti buňce — obě půlky figury jsou od sebe velikostně řádově, a #326 je lekce o tom, co se stane, když se tlumí obojí podle té jemnější.

**Měření (6900 XT, 1600×900, tisíc koulí `Full.json` zmrazených na `campos=0,3,14`, mediány párovaných opakování):**

| | ssaa 2 | ssaa 4 |
|---|---|---|
| led starý | 2,04 ms | 7,63 ms |
| led nový | **2,44 ms** | **8,72 ms** |
| vinyl (kontrola) | 2,05 ms | 7,60 ms |

**Ta cena je reálná a je to 27 hashů na pixel.** Led byl proti vinylu *nepatrně levnější* (to je číslo, které do teď neslo `docs/rendering.md`) a je teď asi **o 19 % dražší**. Na tomhle stroji je to při 410 FPS na tom pinu jedno; **na APU to změřené není** a atribuce mezi těmi dvěma třídami necestuje (#102 vs #250). Nechávám to na majitelovo rozhodnutí — je to jediná konstanta v tom smyslu, že levnější varianta by znamenala pravidelnější buňky, a pravidelnost je přesně ta vada, kvůli které se to dělalo.

**⚠ Rig, na kterém se to dá vůbec měřit, byl třetí pokus.** První dva byly nesmysl a oba by bývaly prošly, kdybych se nedíval: (a) `benchmark.ps1` si skládá jméno logu z argumentů, a s absolutní cestou k mapě a kamerou v `-Extra` to přeteklo MAX_PATH — skript hlásil „no output" a žádný log nevznikl; (b) běžící simulace 3000 koulí je CPU-bound a **A/B se v ní obrátilo mezi běhy** (ssaa 1: led 296 vs vinyl 260; ssaa 2: oba 289). Teprve `F5` na zmrazení clusteru + ssaa 4 dalo rozptyl pod 1 % a znaménko, které drží. Kdo bude měřit cenu koulí: **zmrazit simulaci, jinak se měří fyzika.**

**Ověřeno:** čtyři solutiony čisté, LevelGen exit 0, ScoreSim „All levels rate the right way round", kampaň nedotčená (žádný soubor levelu se nezměnil). Snímky: detail na dómu 1 i 13 přes všech třináct tintů, herní odstup na `Full.json`, a **kontrolní sweep lávy, mramoru a vinylu** — sdílený include do `InstancedModel.fx` nikam jinam nesáhl. Vedle porcelánu (#339) se ty dva teď nedají splést: porcelán je tmavá lesklá glazura s bodovým odleskem a jemnou krakelurou, led bledá studená koule rozlámaná na desky.

**Co z toho plyne pro sousední issues:** #339 (porcelán) tím **není vyřešené** — pořád nečte jako konkrétní materiál — ale přestal se plést s ledem, takže se dá řešit sám za sebe. A **láva (#338) má tutéž konstrukci, jakou tady padla**: její švy jsou pořád `SeamLine`, tedy pravidelné pásy; drží ji, že *svítí*, ne že by ta síť byla lomem. Jestli se #338 bude dělat, tenhle Voronoi je připravený.

---

## 2026-09-02 — Claude Code (čtvrtý zápis dne)

**Láva (#338) — „černá kůra zabírá moc plochy, má být víc vidět barva typu".** Majitelova stížnost je o **ploše**: barva žila jenom ve švech, což je asi patnáctina povrchu, takže dvanáct pixelů ze třinácti bylo černých, ať měla koule jakoukoliv barvu.

**Obě nasnadě ležící opravy jsou špatně a to je celé to rozhodnutí.** *Zesvětlit kůru* zahodí argument, kvůli kterému ten styl vznikl — emisivní šev je jediná barva v téhle hře, která dorazí neředěná, a kůra nesoucí skutečný tint je zase difuzní povrch, což je pod soumrakovým dómem třináct tmavě šedých koulí. *Rozšířit šev*, dokud nepokryje kouli, udělá z popraskané skořápky barevnou kouli s černými flíčky, a ta skořápka **je** ten read. Takže barva jde z **tepla**: kůra do vzdálenosti jedné šířky desky od spáry je tenká a horká a matně svítí — a to je pořád emise, ne difuze.

**Dvě věci to rozhodly, a druhá z nich mě stála jedno kolo:**

1. **Halo je MAX ze tří švových polí, ne jejich součet.** Tři široká pole sečtená saturují přes většinu koule a desky přestanou být deskami; nejbližší z nich je vzdálenost k nejbližší prasklině, což je přesně to, čím se teplo řídí.
2. **⚠ Halo musí mít SPÁD, ne plato.** Napsal jsem ho poprvé jako `široké pole − šev`, což je uvnitř rovnoměrně nasvícený prstenec, a koule se vrátila jako **tlustě pruhovaná neonová klec** s deskami zredukovanými na černé ostrůvky mezi mřížemi. To je **plazma** — přesně ten styl, od kterého se hlavička lávy na půl obrazovky snaží držet dál. Mocnina nad širokým polem z toho udělá gradient: jasně u spáry, tmavě o desku dál. Teprve tohle je chladnoucí kámen.

**Změřeno** (`-Whole` na `Thirteen_Colors`, stejný pevný rig jako u ledu): podíl plochy koule nesoucí skutečnou chromu **48,7 % → 57,8 %**, střední chroma **51,9 → 59,7**, **střední jas beze změny** (74,3 → 75,3) — takže víc barvy, ne víc světla. Nejtěsnější pár z #315, oranžová/hnědá: **8,0 dE před** proti **7,6 a 8,3 na dvou snímcích po** — dva snímky téhož buildu se liší o 0,7, takže to je uvnitř ±0,4 dE, které na jediný snímek dává emisivní tep, a **žádný jiný pár se nezúžil**. Cena: tři sinusy a `pow` navíc, **7,59/7,65 ms před proti 7,67/7,69 po** při ssaa 4 — uvnitř rozptylu mezi běhy.

**⚠ OPRAVA MÉHO VLASTNÍHO ZÁPISU O PÁR HODIN VÝŠ.** V zápisu k #337 a v komentáři k issue jsem napsal, že „láva má tutéž konstrukci, jaká u ledu padla — její švy jsou pořád `SeamLine`, tedy pravidelné pásy". **První půlka je pravda, druhá ne.** Láva má `LavaSeamWander` (0,30) — doménový warp, který ty švy ohýbá, a její vlastní hlavička zaznamenává, že bez něj to *byla* drátěná klec a přesně tak to vypadalo. Napsal jsem to jen z názvu funkce, aniž jsem si přečetl volající kód, a vyznělo to, že je láva stejně pravidelná jako býval led. Není. Voronoi tu proto **nepotřebovala** a nedostala ji — ušetřených 27 hashů na pixel je vedlejší, hlavní je, že by opravovala vadu, kterou ten styl nemá.

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0, kampaň nedotčená. Nafoceno na dómu 1 i 13 přes všech třináct tintů a na herní odstup na clusteru — tam je to největší zisk, protože barvy koulí musí jít od sebe rozeznat, a to je hratelnost, ne vzhled.

---

## 2026-09-02 — Claude Code (pátý zápis dne)

**Porcelán (#339) — a nález nebyl v kódu, byl v komentáři.** Hlavička toho stylu odjakživa říkala „a coloured glaze over a **white ceramic body**". V shaderu žádné bílé tělo nebylo: `color` byl rovnou tint. Obarvená difuzní koule pod ostrým odleskem je **lakovaný plast**, a „it isn't clear what this style is supposed to be at all" je přesně to, jak lakovaný plast vypadá — majitel neuměl ten materiál pojmenovat, protože shader žádný nekreslil. Tělo je teď bílá hlína a tint je vrstva na ní: kde je nátěr tenčí, prosvítá hlína; prasklina krakelury je glazura rozevřená **až na hlínu** a zabarvená hnědou, kterou starý kus za století nabere; a tělo propouští světlo (`PORCELAIN_TRANSLUCENCY`, třetina ledové — nadzvednutý šálek svítí a žádný plast to nedělá, a jde to skrz barvu **hlíny**, protože červený šálek prosvícený zezadu svítí teple bíle, ne červeně).

**Ale to, co ten styl doopravdy pojmenuje, je ornament — a to je majitelův nápad, ne můj.** Řekl to v jedné větě uprostřed práce: „co kdybychom na porcelán nakreslili bíle nějaké vzory, jako bývají na porcelánovém nádobí — třeba zaoblenou Hilbertovu křivku". Je to správně hned dvakrát. Věcně: **čínský porcelán se pozná podle dekorace**, ne podle stínování; o materiálu se dá přít, o malovaném lemu ne. A technicky je Hilbertova křivka ta jediná ozdoba svého druhu, která se dá **vyhodnotit místo uložit** — žádná textura, žádné UV, nic v content pipeline — a k tomu **dlaždicuje**: standardní křivka vstupuje do svého čtverce v jednom rohu spodní hrany a vystupuje v druhém, takže dlaždice položené kolem rovníku srostou v **jeden souvislý meandr, který se uzavře sám do sebe**. To je doslova konstrukce lemu na talíři a padá to z definice té křivky, ne z aranžmá.

**Jak pixel křivku najde, což je ta část, která za zapsání stojí:** křivka navštíví každou buňku mřížky, takže vlastní buňka pixelu stačí. Vezmi index buňky podél křivky (`HilbertIndex`), zeptej se, kde křivka byla o krok dřív a o krok později (`HilbertPoint`), a křivka uvnitř téhle buňky je lomená čára ze středu vstupní hrany přes střed buňky do středu výstupní. Dvě vzdálenosti k úsečce. Sousední buňka nemůže být blíž než půl buňky, pokud do téhle křivka nevstoupí — a pak *je* tím vstupem nebo výstupem a je už započítaná. Takže je to **přesné pro každou stuhu užší než půl buňky**, což je každá stuha, která vypadá jako ornament. První a poslední buňka dlaždice sáhnou pro chybějícího souseda **ven z dlaždice**, a to je ten svar, který dlaždice spojuje.

**Tři věci změřené, ne odhadnuté:**

1. **⚠ Krakelura byla band-limitovaná na VLNOVOU DÉLKU, ne na svou čáru** — dvacetkrát velkoryseji, než měla. A nevypadalo to jako slabá síť, vypadalo to jako **bílé jiskření**: drážka nakloní normálu uvnitř pixelu, zrcadlově hladký Fresnel glazury vystřelí na těch pixelech, které to zrovna chytí, a koule je posetá tečkami jako od špíny. Táž třída vady jako u ledu, jen jindy. Teď se tlumí na čáře, což z krakelury dělá **figuru na blízko** a na herní odstup je pryč — a to je přesně důvod, proč ten styl nemohl dál stát celým readem na ní.
2. **⚠ Čistě bílý pás přes pětinu kotouče stlačí tmavý konec palety.** Zvedne každou kouli k bílé o totéž, a černá proti hnědé spadla z 9,3 dE na **6,0** — pod vlastní nejhorší pár moulded vinylu (**6,4**), což je laťka, kterou podle #315 musí každý styl přeskočit. Napřed jsem podezříval přimíchanou hlínu a stáhl ji z 12–42 % na 6–20 %: **nepohnulo to ničím** (6,0 → 6,2), takže to hlína nebyla, byl to ornament. Oprava je fyzikální: takhle tenká engoba svou podložku doopravdy propouští, takže enamel nese **22 % barvy glazury**. Černá/hnědá je zpátky na **7,1**, červená/oranžová **8,1** proti vinylovým 8,1. Pořád to čte jako bílá — čte to jako bílá **na něčem**.
3. **Cena:** 7,75/7,78 ms před proti **8,01/8,03** po při ssaa 4 na stejném pinu jako led — asi +3 %, což jsou dvě krátké celočíselné smyčky a jeden `atan2`.

**Antialias stuhy jde z footprintu, ne z `fwidth` té vzdálenosti**, a to dvakrát schválně: vzdálenost skáče na 8 mimo pás (rozmazalo by to jeho hranu) a čte se přes `atan2`, jehož řez by jinak udělal jeden vadný sloupec dolů po každé kouli. První pokus měl navíc antialias **širší než stuha sama** (footprint je ddx+ddy, tedy asi dva pixely, a použil jsem celý jako poloviční šířku smoothstepu) — pás vyšel jako měkký vyražený stín místo malované linky.

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0, kampaň nedotčená. Nafoceno na dómu 1 i 13 přes všech třináct tintů a na herní odstup na clusteru — **meandr je čitelný i tam**, což krakelura nikdy nebyla, a to je celý rozdíl mezi „nějaká koule" a „porcelán". Pod tmavým dómem nese ornament spíš reliéf než barva, což je fyzikálně v pořádku a pořád nezaměnitelné.

**Tím jsou hotové všechny tři issues z majitelova playtestu, které se týkaly rozpoznatelnosti materiálu (#337, #338, #339).** Zbývají #335 mramor, #336 kov a #340 kámen — a #336 s #335 se mají navzájem plést stejně, jako se pletly led s porcelánem, takže se vyplatí je vzít spolu.

---

## 2026-09-02 — Claude Code (šestý zápis dne)

**Kov (#336) — a nejzajímavější na tom je, že tenhle nález stál v `docs/rendering.md` už rok jako „to není bug".** Ten odstavec doslova říkal: *„It goes flat on a bright featureless dome, and that is a real limit rather than a bug"* — `SkyRadiance` je dvoubarevný svislý gradient, mirror koule nemá co odrážet, tak je ten styl **scene-bound** a má se nasazovat tam, kde je co zrcadlit. Majitelův playtest („nevypadá to jako kov, má to být lesklejší") je **totéž pozorování, jen jako stížnost** — a ta diagnóza byla o jeden krok krátká.

**Lesklý povrch svoje okolí neINTEGRUJE, on ho UKAZUJE.** `SkyRadiance` je lineární rampa přes celých 180°, což je přesně správně pro *ambientní* člen (to difuzní povrch integruje) a je to důvod, proč kov četl jako plochá, měkce nasvícená koule: hladká koule odrážející hladký gradient nemá nikde nic ostrého. Venkovní scéna má přesně dvě tvrdé věci: **horizont**, což je hrana a ne rampa, a **slunce**, což je malý objekt tisíckrát jasnější než obloha kolem. V gradientu není ani jedno, takže žádná hodnota `MetalReflectance` je tam nikdy vyrobit nemohla — proto se „udělej to lesklejší" odpovídá u prostředí a ne u odrazivosti. `MetalSky` je `SkyRadiance` s obojím vráceným zpátky, a je **lokální pro tenhle styl schválně**: `SkyRadiance` je ambient, který integruje každý jiný povrch ve hře, a zostřit ji tam by nakreslilo linku horizontu přes zem, ostrov, kanón a devět dalších stylů koulí.

**Vypadl z toho jeden vedlejší nález:** `GroundColor` je ambientní **odrazová** barva — co země posílá zpátky nahoru do difuzního integrálu — a je znatelně jasnější než vlastní tvář země. Použitá syrově jako odraz dělala **spodní půlku každé koule jasnější než oblohu nad ní**, což je vzhůru nohama: venku je oblohou zdroj a zem je to, na co dopadá. Půlím ji.

**A druhá půlka stížnosti — „broušení čte jako boule" — měla přesnou příčinu.** Byly to **dvě vlny po směrech pár stupňů od sebe** (26 a 45). Téměř rovnoběžné vlny různé frekvence **se rozezní ve švihy**, a švih je řada boulí. Žádná změna frekvence to spravit nemohla, protože vada byla ta interference, ne měřítko — a komentář u `METAL_BRUSH_FREQUENCY` přitom správně argumentoval, že jednotlivé linky se na herní vzdálenost rozlišit *nemají*; jenže to byla odpověď na jinou otázku. Broušení jsou **rovnoběžné čáry**, a čáry vzniknou z oktáv po **jednom** směru: kolineární sinusovky se sečtou do jednorozměrného profilu, což *je* soustava rýh nestejné hloubky a rozteče. Teď jsou čtyři po `MetalBrushA`; `MetalBrushB` je přesunutý na skutečně kolmý a degradovaný na tu jedinou práci, na kterou je druhý směr dobrý — měnit, jak hluboko kartáč zabral **podél** rýhy, aby čára naběhla, běžela a umřela místo aby obíhala kouli v jedné hloubce.

**Změřeno:** nejtěsnější pár kovu se **rozvolnil** z 15,6 na 12,6 dE (černá/stříbrná) — kov má i tak nejvolnější paletu ze všech stylů, protože tint *je* odrazivost. Cena **7,26/7,28 → 7,39/7,42 ms** při ssaa 4 na stejném pinu jako led, tedy asi +2 % (dva `pow` na slunce, `smoothstep` na horizont a dvě oktávy navíc).

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0, kampaň nedotčená. Nafoceno na dómu 1 i 13 — a **na tmavém dómu 13 je ten zisk největší**, protože tam se sytý eloxovaný odstín s ostrým horizontem a jasným slunečním pruhem konečně čte jako kov a ne jako pastelová koule. Poučení, které přežije tenhle styl: **„to je limit, ne bug" je závěr, který si zaslouží druhý pohled** — tenhle stál v dokumentaci sebejistě napsaný a byl to celou dobu bug.

---

## 2026-09-02 — Claude Code (sedmý zápis dne)

**Mramor (#335) — a celý tvar té opravy je v tom, že konstanta, která tu figuru řídí, zůstala nedotčená.** Majitel četl ty koule jako plochou barvu. Jedna zvlněná sinusovka položí jednu čáru přes každý svůj průchod nulou, a při frekvenci 3,5 byly na přivrácené polokouli dvě tři — což je kámen s pár vlásečnicemi, ne žilkovaný mramor.

**Nabízející se páka je ta špatná, a poznámka u `MARBLE_VEIN_CONTRAST` sama říká proč:** nad 0,6 se bledé typy slijí, protože dotáhnout žílu blíž k minerálu **zesvětluje celou kouli**, a čtyři ze třinácti jsou bledé už teď. Figura tedy musela zesílit, aniž by koule zesvětlela — a to jde dvěma způsoby, oba použité:

1. **Víc žil.** Hlavní švy na 4,2 a přes ně **jemnější síť** na 2,7× po vlastní ose a s vlastním band limitem. To je, co skutečně žilkovaný kámen má: pár výrazných švů a mezi nimi pavučina, ne sada rovnoběžných pásů.
2. **Tmavý lem vedle každého světlého jádra.** Skutečné žilkování není světlá čára na kameni — je to světlá čára s **tmavším lemem**, kde minerál kámen po stranách zabarvil, a právě ta dvojice je většina toho, proč je šev vidět přes celý pokoj. A je to zároveň to, co chrání paletu: **lem si vezme zpátky hodnotu, kterou jádro přidá**, takže lokální skok přes žílu se zhruba zdvojnásobil, zatímco průměr koule se skoro nehnul.

**Změřeno:** nejtěsnější pár přes všech třináct **7,2 → 6,7 dE**, pořád nad vlastním nejhorším párem moulded vinylu (6,4), a `MARBLE_VEIN_CONTRAST` je pořád 0,6. Cena **7,42/7,49 → 7,50/7,54 ms** při ssaa 4 — jeden sinus a dva `pow` navíc, uvnitř rozptylu mezi běhy.

**Ten pár se přestal plést, což bylo v obou issues explicitní požadavek.** Kov nese od #336 tvrdý horizont a šmouhu slunce, mramor větvenou síť žil pod bodovým leskem — nejsou to už dvě měkce nasvícené koule bez figury. Zajímavé je, že se to spravilo z obou stran naráz a ani jedna oprava nebyla o té druhé: kov potřeboval **co odrážet**, mramor **víc figury za stejný jas**.

**Obecná věc, kterou si z těch dvou odnáším:** obě issue nabízely v textu „zjevnou\" páku (kontrast u mramoru, odrazivost u kovu) a **u obou byla ta páka ta špatná**. U mramoru proto, že tlačí na průměr místo na lokální skok; u kovu proto, že násobí prostředí, ve kterém není co zesílit. Když issue sama píše „pozor, tahle konstanta byla naladěna schválně", je to skoro spolehlivá známka, že řešení leží jinde než u ní.

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0, kampaň nedotčená. Nafoceno na dómu 1 i 13 přes všech třináct tintů a na herní odstup na clusteru.

**Tím je vyřízeno pět ze šesti issues z majitelova playtestu vzhledu koulí** (#337, #338, #339, #336, #335). **Zbývá #340 kámen**, a ten nese navíc rozhodnutí, na které je potřeba se zeptat: majitel v něm píše, že kámen smí být viditelně méně kulatý, což je přímo proti dosud psanému pravidlu „silueta zůstává dokonalý kruh" (fx:3137-3142, a #271 to u gemu rozhodl opačně).

---

## 2026-09-02 — Claude Code (osmý zápis dne)

**Kámen (#340) — a hlavní věc na tom je, že jsem si vyžádal rozhodnutí a dostal ho.** Issue psalo, že kámen smí být viditelně méně kulatý, což jde přímo proti pravidlu, které v hlavičce toho stylu stálo napsané („nothing here builds geometry", a celý read stál na stínování dokonalé koule) — a proti #271, které u gemu rozhodlo opačně. **Zeptal jsem se majitele a ten řekl siluetu porušit.** Obě rozhodnutí jsou přitom správná pro svůj styl: gem je **broušený**, takže hranatá silueta je vada výbrusu; kámen je **rozlomený**, a nekulatý obrys je to jediné, co se o něm dá říct — a jediné, co mu na dálku žádný band limit nevezme.

**`InstancedModelStone` je teď jediná technika koulí s vlastním vertex shaderem.** Tři rozhodnutí v něm:

1. **Řeže se jen dovnitř.** Mřížka balí koule přesně dva poloměry od sebe, takže koule, která by narostla, by vlezla do buňky souseda a cluster by srostl sám do sebe. Řezání je navíc to, co kámen dělá kamenem: kámen je to, co **zbylo**.
2. **⚠ Normála je analytická, ne koulová.** Jakmile se poloměr mění po povrchu, směr přestane být normálou — a použít ho dál by nasvítilo vyřezanou kouli přesně jako kulatou, takže celá změna by byla silueta a nic víc. Pro radiální plochu `r(d)·d` je normála `d − ∇ₜr / r`, což je tady uzavřený tvar, protože pole je součet sinů: jeho gradient je týž součet s kosinem a vlnovým vektorem. **Proto je to pole napsané zvlášť a nepoužívá `StoneLumps`** — ten je rektifikovaný (`abs`), a `abs` v nule gradient nemá.
3. **Je hrubší než všechno ostatní na kouli** (1,7–5,7 vln proti 3,5–49 u lumpů), takže se ty dvě vrstvy nepočítají dvakrát: tahle je **tvar**, to, co na ni pixel shader kreslí, je **povrch**.

**První build byl hladká BRAMBORA** — zjevně nekulatá, ale kulatá po částech, a to nebylo, co bylo zadáno („ostřejší, jako opravdový kus kamene"). Součet sinů je jemné vlnění; mocnina nad podílem řezu nechá většinu povrchu blízko plného poloměru a zbytek zažene hluboko a úzko, čímž se z vlnění stanou **výmoly**. Kámen se neodlupuje v čeřinách.

**⚠ Každý kámen nese TÉŽ vyřezání**, protože instance stream nemá volný kanál na per-ball seed (world matrix, okluze, dissolve a ripple ho zaplňují) a jediná dostupná per-instance hodnota — pozice koule — se hýbe, což by tvar rozvlnilo, jak kámen padá. Zachraňuje to, že pole je v **object space**: fyzika dá každému kameni vlastní orientaci, takže hromada ukazuje týž kámen ze sta úhlů, což je mimochodem přesně to, jak vypadá hromada rubaniny z jednoho lomu. Kdyby to někdy mělo být opravdu per-kámen, oprava je pátý instance element, ne změna tady.

**Dvě věci z toho vyplynuly a obě jsou to, co issue žádalo dál:**

- **Zrno zpátky ze 14 na 22.** Argument, který ho kdysi stlačil na 14, byl, že jemná figura se band-limituje do neviditelna na herní vzdálenost — a ten argument **už nemusí nést zrno**, protože ho nese silueta. To zrno osvobodí k tomu být jemné, aniž by dálce cokoliv dlužilo. Plus `StoneLumps` má šest oktáv místo čtyř (čtyři oktávu od sebe nepopisují povrch, interferují do pravidelného tkaní — trap, který si hlavička `SurfaceReliefWorld` zapsala a kvůli kterému používá sedm).
- **Tělo z 0,63 na 0,54.** Prostě ztmavit to nešlo — poznámka u `StoneBody` vysvětluje, proč fyzikálně rozumných 0,42 četlo jako osmička. Ale **vyřezaná koule má skutečný tvar**: vlastní výmoly ji stínují, což je zdroj kontrastu, který hladká koule bez emise a bez zrcadla neměla odkud vzít, a hodnota, která dřív musela jít z albeda, jde teď z tvaru.

**Změřeno** (louka, střední jas kotouče): dóm 1 — dva kameny **84 a 75** proti Type8 (černá) **42** a Type11 (stříbrná) **81**; dóm 13 — **66 a 67** proti **29** a **80**. Pořád vedle stříbrné a pořád na dvojnásobku osmičky na obou dómech, což je přesně to, co ta poznámka žádá. **Cena** 7,47/7,50 → 7,50/7,52 ms při ssaa 4 na `Rocks.json` (třináct kamenů ve 196 koulích) — uvnitř rozptylu mezi běhy.

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0, kampaň nedotčená (pět kamenných levelů Mirage se nezměnilo — je to čistě render). Fyzikální těleso zůstává koule a řez jde dovnitř, takže nakreslený kámen nikdy neopustí buňku, kterou mu simulace dala.

**Tím je hotových všech šest issues z majitelova playtestu vzhledu koulí: #335, #336, #337, #338, #339, #340.**

---

## 2026-09-02 — Claude Code (devátý zápis dne)

**Tři malé issue z majitelova druhého playtestu: #320, #349, #348.** Každá vlastní větev, všechny na mainu.

**#320 — hint u klávesy L v MapEditoru jmenoval dva materiály z deseti.** Napsaný v #258, kdy ty dva byly jediné; #272 přidalo osm dalších. Opravená verze **nejmenuje žádný**: říká, co klávesa dělá, a ukazuje na řádek `Info`, kde už aktuální materiál stojí — což je tvar, který `K` (druh koule) používá odjakživa, a stejný argument, na kterém stojí ten cyklus sám (`BallStyles.Next` bere pořadí z enumu, takže jedenáctý materiál nemůže zůstat nedosažitelný). Nic tam už nepočítá materiály, takže to nemůže zastarat znovu. **Sám jsem si to dnes zhoršil** — pět z těch deseti materiálů jsem předělal.

**#349 — přepínač „Unlock all" v nastavení.** Odpovídá se na **jednom místě**, `IsLevelUnlocked`, kterým prochází všech šest volajících (dlaždice level selectu, dosah kapitol, „next level" na výsledkové stránce a volba backdrop levelu ve front endu) — zkrat jinde by nechal některé z nich nesouhlasit s ostatními. **Nezapisuje nic**: `PlayerProgress` je nedotčený, takže vypnutí vrátí skutečný stav přesně. A **nepersistuje se**: vývojářský přepínač, který přežije restart, jednou zůstane zapnutý, a to jediné, co nesmí, je udělat ze skutečného save něco, co vypadá dál, než je.

**⚠ Ověřit to na tomhle stroji nešlo bez sáhnutí na majitelův save**, protože ten má 214 hvězd a všechno odemčené. `Progress.json` žije **jen** v `Game/bin/net10.0-windows/Levels/` a `bin/` je v `.gitignore`, takže **jiná kopie neexistuje**. Postup: zálohovat na dvě místa, ověřit hash, odsunout, testovat na čerstvém profilu, vrátit, znovu ověřit hash. Vyšlo: s vypnutým přepínačem je v kapitole 1 otevřený level 1 a 2–10 hlásí „Locked · N ★"; se zapnutým a **nula hvězdami** stránka otevře **kapitolu 11 z 11** s levely 96–105 volitelnými. Záznam vrácen bit po bitu (56 levelů). **Kdo bude testovat progress: ten soubor je jediná kopie, zálohovat před ním a hash po něm.**

**#348 — kapitolové pipy jsou klikatelné.** Byly to `Label`y, tedy jen readout, a hlavička té třídy to argumentovala: držet je mimo procházení padem zkracuje seznam vstupů stránky. Majitelův verdikt je opačný a je to rozumné — vidět přesně ten pip, který chceš, a pak pětkrát listovat, je horší obchod. Každý je teď postavený přes `MenuTile` jako šipky, což je celé, co z něj dělá skutečný vstup (tytéž štětce, zvuk kliknutí, `Tag` pro pad). **Hlavičku třídy i `docs/game-shell.md` jsem přepsal, protože obojí od té chvíle tvrdilo opak toho, co kód dělá.**

**⚠ A vypadl z toho obecný nález o Myře, který stojí za zapamatování: TLAČÍTKO MENŠÍ NEŽ LABEL UVNITŘ NĚJ TEN LABEL NEOŘÍZNE — nechá ho přetéct.** Plocha, kterou hráč vidí, a obdélník, který trefí myš, tím potichu přestanou být totéž. Při první velikosti (74) byly od sebe asi půl pipu a klik na viditelný kroužek dopadl **pod** tlačítko, což čte přesně jako mrtvý ovládací prvek — a přitom se nic nerozbilo, nic nezalogovalo a build byl čistý. Poznalo se to až tak, že jsem si na snímku zvětšil pruh pipů a viděl, že plotny sedí jinde než glyfy. Velikost pipu má proto podlahu v **glyfu**, ne ve vkusu.

Ověřeno: čtyři solutiony čisté. #320 nafoceno v editoru, #349 a #348 v běžící hře (klik na pip skočil z kapitoly 11 na kapitolu 2 a hlavička, výpis, mřížka i pipy se přepsaly zároveň). **Dosah padem na pip jsem NEnafotil** — tři stisky Nahoru zůstaly v mřížce dlaždic a hledat cestu procházením by stálo další běhy; plyne to z `CollectNavEntries`, který sbírá **každé povolené `Button`** ve stromu, a pip jím teď je.

---

## 2026-09-02 — Claude Code (desátý zápis dne)

**Průchod repem na majitelovo zadání „navrhni issues" — šest založených (#353–#358), žádný kód.** Zapisuji je sem hlavně proto, že tenhle deník má na duplicitní zakládání vlastní jizvu: dva agenti kdysi založili totéž s hodinovým odstupem.

**#353 save a #354 nastavení jsou jedna a táž díra viděná ze dvou stran: hra nemá kam psát.** `Progress.json` sedí v `Game\bin\net10.0-windows\Levels\` (`BS3DGame.cs:1079-1090`), `bin/` je v `.gitignore`, jiná kopie neexistuje — což si devátý zápis dne sám zapsal jako varování pro toho, kdo bude testovat progress, a je to varování o vadě, ne o postupu. K tomu `Save()` je jediné `File.WriteAllText` **přes ten jediný soubor** (`PlayerProgress.cs:95`): otevři, zkrať, zapiš. **A `Load` je schválně lenient** (`PlayerProgress.cs:64`) — takže useknutý soubor se načte jako čerstvý prázdný progress a další level ho přepíše doopravdy. Ta shovívavost, která chrání první spuštění, je přesně to, co z tohohle dělá **tichou** ztrátu; smazaná kampaň a první spuštění jsou zevnitř hry nerozlišitelné. Na stroji, který se pod zátěží tvrdě resetuje, to není teorie. Nastavení proti tomu nemá ani ten `bin/`: v `Game/` není jediný zápis souboru a `Program.cs` to sám přiznává v komentáři u `mute` („nothing is persisted"). Obě issues míří do `%LOCALAPPDATA%\BS3D`, aby to byla jedna migrace a jedna věc k zálohování.

**#355 alt-tab je #79 o vrstvu výš, a to je na tom to zajímavé.** Větvení na `Game.IsActive` (`GameplayScreen.cs:1081`) je celé o **vstupu** — neaktivní větev pustí kurzor, zruší capture, zneplatní aim, odjistí trigger. `UpdateCeilingDescent`, `StepPhysics` i `CheckLevelLost` visí **pod** tím větvením a běží dál. Strop tedy klesá na hodinách reálného času nad oknem, na které se nikdo nedívá, a hráč se vrátí k prohranému levelu. #79 spravilo přesně tuhle třídu vady pro pause page a udělalo to pořádně (pauza zastaví obrazovku dřív, než se `Update` vůbec dostane ke slovu); ztráta fokusu byla celou dobu napsaná jako problém kurzoru a je to problém **času**.

**#356 kámen bez seedu je dluh, který si osmý zápis dne zapsal sám** — a schválně jsem ho nepsal jako „přidej pátý element", protože ta cena je celé to rozhodnutí: stream jede na **každé** instanci ve scéně a město jich kreslí přes tisíc. V issue jsou tři varianty včetně té nejlevnější (nechat být a napsat argument o object space do hlavičky techniky) a návod, jak to rozhodnout **snímkem hromady** místo názorem.

**#357 barvoslepost je jediná z těch šesti, která nevznikla ze čtení kódu, ale z čtení čísel v tomhle deníku.** Nejtěsnější pár je 6,4 dE u vinylu a 6,7 u mramoru — to je paleta na hraně pro člověka, který **vidí všechno**, a #315 (černá/hnědá) byla reálná vada nalezená okem. Nikdo se nikdy nepodíval, co z třinácti typů zbude pod deuteranopií. Issue schválně **nežádá řešení**, žádá měření: rig existuje (pevná kamera, `F5`, `balls=<styl>`, dómy 1 a 13), a teprve co vypadne, rozhodne mezi doladěním pár tintů a druhým kanálem. Gate jako ScoreSim to být nemůže — stínování je v shaderu, takže se to měří ze snímků, ne z palety.

**#358 rotace tohohle deníku po měsících.** 3533 řádků, 600 KB, a pravidlo nahoře říká číst ho před začátkem práce. Co se reálně děje, je čtení tailu a grep — staré zápisy tedy stojí kontext, aniž by je někdo četl. Návrh je schválně hloupý, aby nezhnil: aktuální měsíc tady, starší **needitované** do `docs/agent-notes-archive/YYYY-MM.md`, rotuje ten, kdo píše první zápis nového měsíce. **Ne sumarizace** — zápisy se citují po měsících zpátky a hodnotu má přesná formulace.

**⚠ Dvě věci na okraj, obě stojí za vědomí:**

- **Mezi mojí kontrolou duplicit a založením přibylo #352** („Survey MonoGame's own API against BS3D's custom code"). Nekolidovalo mi to s ničím, ale znamená to, že na repu byl v tu chvíli souběžně někdo další. Kontrola duplicit má **životnost v minutách**, ne v hodinách.
- **#341 je potvrzená jednořádkovka:** `BombCharge = float3(1.0, 0.46, 0.13)` (`InstancedModel.fx:4377`) je oranžová přesně tak, jak issue tvrdí — #326 se konstanty nedotklo, protože řešilo šířku pásů a rytmus blikání. Kdo na to sáhne, ať se podívá i na `BombFarGlow`: náboj konverguje k němu, takže „červená" se musí změnit na **dvou** místech, ne na jednom.

Dál pokračuji na **#343** (kámen natrvalo na stropě) na majitelův pokyn.

---

## 2026-09-02 — Claude Code (jedenáctý zápis dne)

**#343 — kámen nesmí viset na stropě. Vada nebyla v návrzích, ale v tom, že se na ně nikdo neptal: ta jediná brána, která to mohla chytit, se na kamenných levelech NESPOUŠTĚLA.**

**Dvakrát propadlo síto, a obě propadnutí jsou poučná.** `FindStrandedSpecials` se volalo podmínkou `glass + bombs == 0 ? new StrandedReport() : …`, takže level složený jen z kamene a barvy tu chůzi přeskočil celou. A i kdyby se spustila, uvnitř stojí strážce `if (Matchable(kind) || !Removable(kind)) continue;` — a **kámen je jediný druh, který odpovídá NE na `Removable`**, takže každý řádek pod tím strážcem je pro kámen nedosažitelný. Test kamene proto musí stát **před** ním, a stojí. Obecně: strážce psaný jako „vlastnost místo výčtu druhů" je správný tvar, ale chrání jen otázku, pro kterou byl napsaný — tady „dosáhne na tu kouli výstřel?" — a otázka #343 je jiná, skoro opačná: „zbaví se hráč té koule vůbec někdy?".

**Audit (jednorázový nástroj přes skutečný `Level`/`BallsMap`, ne parsování JSONu): tři levely z pěti kamenných.** `Seam` 26 kotev ze 112, `Cairn` 44 ze 112, `Obsidian` **58 ze 113**. `Anvil` a `Keystone` čisté — a je zajímavé proč: Anvilovo `i < ANVIL_STONE_COURSES` míří na **spodní** kurzy (`fieldLevel = i + offset`, takže `i = depth-1` je kotva), a Keystoneovy pilíře „stop short of the glass" už dávno. **Ten idiom byl v bloku celou dobu**, jen ho tři návrhy ze čtyř nepoužily.

**Oprava je proto Keystoneovo pravidlo aplikované na sourozence: `i < depth - 1 && …`.** Nestojí to ani buňku ani siluetu — o obsazenosti rozhoduje `OccupiedBlock`, `BlockKind` rozhoduje jen druh — takže ty buňky dál visí a dál se kreslí, jen nesou barvu místo žuly. Počty koulí sedí na jednotku (601 / 673 / 606 před i po).

**Změřený dopad, a je asymetrický:**

| | one-shot | anchor load |
|---|---|---|
| Seam | 8 % → 8 % | 6,1 → 6,2 |
| Obsidian | 18 % → 18 % | 5,6 → 6,2 |
| **Cairn** | **4 % → 11 %** | **6,5 → 9,1** |

Cairn je jediná skutečná cena a je pochopitelná: čtyři komory se teď potkávají pod **jednou barevnou střechou**, a ta střecha je jediné místo levelu, kde jde barva brát přes dvě čtvrtiny naráz. Zapsal jsem to do hlavičky návrhu i do `docs/formats-and-tools.md`, protože „quartered top to bottom" tam stálo jako fakt.

**⚠ A hlavní nález dne, který stojí za zapamatování, protože vyvrací argument, na kterém ta vada stála.** Obsidianův kamenný lem byl obhájený takto: kotva, kterou nelze vzít, nemůže `WorstAnchorLoad` zhoršit — tedy pojistka proti propadnutí clusteru pod čáru (#301/#302). **Změřil jsem tu pojistku sondou, která na to jediná je, a nikdy neplatila:** `--sag=Obsidian` čte **3, 1, 2 z 5 s lemem** a **1, 2, 1 z 5 bez něj**. Stejné rozpětí, stejný nejhorší průhyb (≈ −1,0). Poučení: **„tohle brání horšímu případu" je tvrzení, ne argument, dokud ho někdo nezměří** — a tady se za nezměřenou pojistku platilo 58 koulemi, kterých se hráč na poslední úrovni kampaně nemohl nikdy zbavit.

**⚠ Vypadlo z toho ale i něco, co #343 neřeší a co nikdo nenahlásil: Obsidian propadá pod čáru i teď, 1–2 z 5.** Je to táž třída jako #316/#317/#319 (levely nad prahem, které nikdo nehlásil, našla je sonda) a `Obsidian` mezi nimi jmenovaný není. Nezakládal jsem issue bez zeptání — patří to majiteli k rozhodnutí, protože 1–2 z 5 je podle hlavičky `RUNS_PER_LEVEL` čtení „na hraně", které se má opakovat, a já ho opakoval třikrát na obou verzích.

**Vedlejší nález v dokumentaci:** pás one-shotů Mirage tam stál jako „3–25 %" a **měřením vyšel 4–25 % i na nedotčeném HEADu** — byl zastaralý už před touhle změnou. Opraveno včetně toho, co posunulo #343 samo (jen Cairn).

**Ověřeno:** LevelGen 0, ScoreSim 0, čtyři solutiony čisté, audit hlásí 0 kamenů na stropě přes všech 106 levelů. Nafoceno v běžící hře (`play level=…  shot=12`) — Obsidianův kotevní kurz nese barvu a žíla kamene zůstala pod ním, Cairnův kříž stojí a nad ním je barevná střecha. Tělo #324 na GitHubu opraveno v místě, jak issue žádalo: ta věta je teď přeškrtnutá a označená jako retraktovaná, ne smazaná.

---

## 2026-09-02 — Claude Code (dvanáctý zápis dne)

**#347, půlka první: falešné CAMPAIGN COMPLETE. Majitelovo rozhodnutí bylo „vždycky se odemkne jenom jeden následující level", což je oprava u KOŘENE, ne u symptomu — a to je na tom to podstatné.**

**Vada byla jeden výraz, ale její příčina byla ekonomika odemykání.** `_campaignCompleted` se ptalo `_levelIndex + 1 >= LevelSet.Count`, tedy „byl tenhle level poslední položka setu". Komentář nad tím tu zkratku dokonce obhajoval: *„cheap and knowable this early: unlike the block, which has to look at every level of a run"* — a **to byla ta vada napsaná jako úspora**. Blok se ptá draze a správně (`WouldCompleteBlock` prochází celý běh), kampaň se ptala lacině a špatně. Přidal jsem `WouldCompleteCampaign` přesně podle vzoru bloku včetně půlky „a ještě není hotová", která z **repríze** finále dělá obyčejný clear (bez ní by konfety padaly při každém návratu na 105).

**Ale samotné přepsání toho výrazu by díru nezavřelo, jen ohlásilo správně.** Do 105 se dalo dojít s dírami za sebou, protože se odemykalo na **součet hvězd**: čtyři hvězdy za level znamenají, že hráč utíká před sebou samým — level 40 a 41 otevřené, 37 nikdy nehrané. Nové pravidlo je proto **součet hvězd A ZÁROVEŇ nejvýš jeden level za frontierem** (první nevyčištěný level). Hvězdný gate zůstává pod tím, ne místo toho: je to on, kdo pošle slabšího hráče vrátit se a zahrát level líp, a je to ekonomika, proti které jsou `minStars` v setu vůbec napsané.

**⚠ A hlavní věc dne: první verze toho pravidla by majiteli zamkla 28 levelů, které má dohrané.** Napsal jsem `index <= FirstUnclearedLevel` a šel to změřit na skutečném save — **65 hotových levelů, frontier na 38, a 28 hotových levelů ZA ním** (41, 42, 48, 51–53, 61–68, 71, 76, 78–80, 84, 94, 96–98, 102–105). Zpřísnění pravidla, které zavře level, jehož jsi vítěz, se nečte jako pravidlo, ale jako **ztracený postup**. Klauzule „hotový level je vždy otevřený" tam proto je a je nosná, ne kosmetická. Obecně: **nové pravidlo o postupu se musí změřit na existujícím save, ne jen na čerstvém profilu** — čerstvý profil to nikdy neukáže, protože na něm žádná historie neexistuje.

**Zámek má od teď dva důvody a všechny tři plochy musí jmenovat ten správný.** Dlaždice `Locked · #38 first` místo ceny ve hvězdách, detailní řádek „the campaign opens one level at a time; level 3 'Toadstool' is next", a hlavička kapitoly `locked · clear level 10 first` místo `opens at N ★`. Kdyby zůstala cena, dlaždice by hráči nabízela hvězdy, které **už má** — a to se nečte jako pravidlo, ale jako rozbitá hra. Je to táž lekce jako u #349: na „co je otevřené" se odpovídá na jednom místě, ale **„proč je to zavřené" musí umět odpovědět každá plocha zvlášť**.

**⚠ Testováno na majitelově save podle procedury z devátého zápisu** — záloha na dvě místa, hash, umělý save (dva hotové levely, 8 hvězd), tři screenshoty, obnova, hash znovu (`f5c8c4ee…` před i po, a ještě jednou po běhu na skutečném save, protože hra na něj při pouhém prohlížení pickeru nesahá). Umělý save je zároveň důkaz té vady: s 8 hvězdami byly dřív otevřené levely 3, **4 i 5** (minStars 4, 6, 8) — tři otevřené dveře po dvou zahraných levelech.

**Nezahrnuto a je to schválně: druhá půlka #347, „bounded skip".** Issue ho chce jako ventil pro level, který hráče zasekne. Po tomhle rozhodnutí je jeho tvar jasný (skip posune frontier o jedna), ale potřebuje rozšířit formát `PlayerProgress` o „přeskočeno", vlastní ovládací prvek a rozhodnutí, kde se nabízí a kolikrát. `WouldCompleteCampaign` je napsané tak, aby šlo rozšířit na „vyčištěno **nebo** přeskočeno" jedním testem. Issue proto **nechávám otevřené**.

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0, kampaň nedotčená (žádný soubor levelu se nezměnil — je to čistě pravidlo), tři screenshoty z běžící hry: frontier na čerstvém profilu, kapitola za frontierem, a kapitola 7 na skutečném save, kde levely 61–68 stojí otevřené se svými hvězdami a 69/70 hlásí `Locked · #38 first`.

---

## 2026-09-02 — Claude Code (třináctý zápis dne)

**#347, druhá půlka: bounded skip. Rozpočet je jeden skip na KAPITOLU, ne na kampaň, a ten rozdíl je důsledek sekvenčního pravidla z předchozího zápisu.** Dokud se odemykalo na hvězdy, dal se zaseknutý level obejít; teď je to zeď přes celou kampaň, takže jeden skip na 105 levelů by hráče na druhé takové zdi zastavil natrvalo. Na kapitolu je to zároveň přesně to omezení, které issue chtělo („ne skip přes celou kapitolu") — deset levelů, jeden skip. Na setu bez kapitol padá zpátky na jeden na celou sadu, protože blok tam je jeden level a per-blok rozpočet by byl skip na každém.

**Úložiště je zvlášť od `Levels`, ne příznak v `LevelBest`.** `LevelBest` je záznam **nejlepších výsledků** a přeskočený level žádný nemá; navíc `Levels.Count` se loguje jako „cleared level(s)" a příznak uvnitř by to číslo tiše rozbil. `Skipped` je `List<string>` **null až do prvního skipu**, takže save bez skipu se serializuje bit po bitu jako dřív — týž precedens a týž důvod jako volitelné `"k"` v mapě. Starší build klíč ignoruje a najde ty levely nedokončené, což je bezpečný směr: podhodnotí postup, nevymyslí ho.

**Skip není clear a nikde se za něj nevydává.** Žádné skóre, žádné hvězdy, nic do gate. Posune jen frontier — a přeskočený level zůstává navždy hratelný, což je důležitější, než to vypadá: **v pickeru proto dlaždice říká `Skipped`**, protože to je jediné místo ve hře, kde ten level jde znovu najít. Bez toho slova vypadá úplně stejně jako level, ke kterému se hráč zatím nedostal — na stránce, jejíž celá práce je říct mu, kde stojí, a o jediném levelu, který dluží.

**⚠ Dvě díry, které jsem našel až při čtení vlastního kódu, ne v zadání:**

1. **Skip by hráče propašoval přes hvězdný gate.** `SkipLevel` volá `AdvanceLevel` přímo, takže by ho vysadil do levelu, který picker hlásí jako zamčený — dvě plochy nesouhlasící o téže položce, přesně to, čemu má bránit jednomístná odpověď z #349. `CanSkipLevel` proto nabídku odmítne, když další level nemá zaplacené `minStars`. Skip hýbe **sekvenční** půlkou pravidla, cenová stojí dál.
2. **Konfety by se ztratily.** Kampaň je hotová, když je každý level vyčištěný **nebo přeskočený** (formulace z issue), ale `WouldCompleteCampaign` se ptá jen z `CheckLevelCleared`, takže moment vždycky patří skutečnému clearu. Napsal jsem to jako „tenhle level nebyl hotový **před** tímhle clearem" místo „kampaň ještě není hotová" — na kampani bez skipů je to totéž, na kampani se skipy je jen ta první verze správně (jinak by návrat k dodělání přeskočeného levelu konfety vyvolal podruhé). Zbývá jedna mezera a je schválně: kdo si **poslední** nedokončený level přeskočí, konec kampaně bez konfet dostane. Ukončit kampaň přeskočením její poslední zdi není konec, pro který ty konfety jsou.

**⚠ A podruhé jsem spadl do téže pasti, kterou má `docs/game-shell.md` u téhle stránky zapsanou dvakrát:** `MENU_TEXT_DIM` je šedá pro **vedlejší text na tmavé plotně**, a výsledková stránka žádnou plotnu nemá. Řádek s cenou skipu napsaný v ní byl nad tropickou oblohou pod nasvíceným clusterem prostě neviditelný. Chytil jsem to na první fotce (#238 a #199 to chytily až po nasazení), ale poučení je, že to není rada — je to **pravidlo: na téhle stránce mimo plotnu žádná dim není**, a tak jsem to tam teď i napsal.

**Proč cena stojí na vlastním řádku a ne v popisce tlačítka:** tlačítko nese cíl, ne větu — a Myra tlačítko menší než jeho label ten label **neořízne, nechá ho přetéct** (past z #348), takže delší popiska by potichu rozešla viditelnou plochu s obdélníkem, který trefí myš.

**Vyhodil jsem taky vlastní mrtvý stav.** Přidal jsem do `LevelResult` `NextLevelBeyondReach`, abych opravil poznámku „Next level unlocks at N ★" na prohře — a pak zjistil, že ta poznámka je řádek **mřížky rozpisu skóre**, která je na prohře skrytá. Na cleared se frontier už posunul, takže ta vlastnost nemůže být nikdy true. Odstraněno i s parametrem; místo toho má prohra vlastní řádek.

**Ověřeno:** čtyři solutiony čisté, LevelGen 0, ScoreSim 0. Nafoceno v běžící hře: stránka prohry s `Retry / Skip to: Elephant / Main Menu` a řádkem ceny (dvakrát — jednou nečitelně, pak opraveně, obojí nad tropickou oblohou), a picker na umělém save se skipem, kde level 2 hlásí `Skipped`, levely 1/3/4 mají hvězdy, 5 je frontier a 6–10 `Locked · #5 first`. **Majitelův save zálohovaný na dvě místa a hash ověřený před i po** (`f5c8c4ee…`) — testovalo se na umělém profilu.

---

## 2026-09-03 — Claude Code

**#353: save žil v `bin/` a přepisoval se sám přes sebe. Nejzajímavější na tom není ta cesta — je to argument, který ji tam držel, a ten stál černé na bílém v hlavičce třídy.** *„progress through a set belongs with the set it measures"*. Je to hezká věta a je špatně, protože adresář toho setu je **výstup buildu**: `Game\bin\net10.0-windows\Levels\Progress.json`, v `.gitignore`, druhá kopie nikde. `dotnet clean`, smazané `bin` kvůli přestavbě contentu a čerstvý klon na druhém stroji jsou tady všechno rutina a všechny tři ten save berou s sebou. Nové pravidlo je jedna věta a přebíjí tu starou: **save musí přežít výstup buildu**. Sada levelů se nestěhovala — `Levels.json` je content a patří k buildu.

**Druhá půlka vady je horší než ta první, protože je tichá.** `Save()` byl jeden `WriteAllText`: otevři, zkrať, zapiš. Ztrať stroj v tom okně a na disku zůstane krátký, ale **syntakticky bezvadný** soubor — a `Load` je schválně shovívavý (*„a corrupt one must cost the player their stars, never the game"*, a ta věta je pořád správně), takže se vrátí jako čerstvý prázdný postup a další clear tu prázdnotu zapíše doopravdy. **Shovívavost, která chrání první spuštění, je přesně to, co z vymazané kampaně dělá neviditelnou událost.** A tenhle desktop se pod GPU zátěží tvrdě resetuje (#250), takže to okno není hypotéza.

**Zápis je proto atomický a drží jednu generaci zpátky.** `Progress.json.tmp` ve stejném adresáři (`File.Replace` neumí přes svazek) a `File.Replace`, který **v téže operaci** starý soubor degraduje na `.bak`. `Load` sáhne po záloze dřív, než to vzdá, a vrátí objekt navázaný na **skutečný save**, ne na zálohu — jinak by další zápis přistál na záloze a druhá vada by snědla první.

**A `Load` teď říká, co udělal.** `ProgressLoad`: `Fresh` (nebylo tam nic), `Loaded`, `RecoveredFromBackup` (v nejhorším padl poslední clear) a **`Discarded`** — něco tam bylo a nedalo se z toho použít nic. Tři ze čtyř vracejí objekt, který vypadá naprosto stejně; ta jediná otázka, na kterou hra do dneška neuměla odpovědět, je právě `Fresh` proti `Discarded`.

**⚠ Migrace je kopie a starý soubor schválně nechávám stát.** Má pět kilobajtů a do téhle změny byl **jedinou existující kopií** té kampaně — přijít o něj kvůli migraci, která ho má zachránit, by byl mizerný vtip. Odejde, až odejde `bin`, což je přesně to, o čem tohle issue je. Kopíruje se bajt po bajtu, ne přes reserializaci: klíč, který přidal nějaký pozdější build, není tomuhle buildu co zahazovat. A spustí se, jen když v novém domově není **ani save, ani jeho záloha** — save, jehož jediná přeživší kopie je záloha, je pořád save.

**⚠ Poučení o měření, které jsem si uštědřil sám:** napsal jsem si do testu `Check("214 stars", …)`, protože 214 hvězd stálo v těle issue #353. Reálný save jich má **252 přes 65 levelů** — číslo v issue bylo den staré a přebrané z ověřování #349. Jediný „FAIL" v jinak čistém běhu byl **můj předpoklad, ne kód**. Číslo opsané z cizího zápisu je citace, ne měření.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0 („All levels rate the right way round"), žádný soubor levelu se nezměnil. Devět skupin kontrol proti skutečnému souborovému systému (jednorázový konzolový projekt mimo repo, ne v něm): první spuštění, první zápis včetně založení adresáře a úklidu `.tmp`, druhý zápis a že záloha je stav **před** ním, roztržený zápis se zdravou zálohou, oba soubory nečitelné → `Discarded` a ne `Fresh`, samotná záloha bez saveu, verze z pozdějšího buildu, round-trip skipů, a majitelův skutečný save z kopie. **V běžící hře**: migrace proběhla (hash nového souboru `f5c8c4ee…` = starý, starý netknutý včetně časového razítka), druhé spuštění už mlčí, a podstrčený useknutý save vypsal `A save was at '…' and NOTHING in it could be read`. **Majitelův save zálohovaný na dvě místa, hash ověřený před i po každém kroku** (`f5c8c4ee…`).

**Nezahrnuto a je to schválně: #354** (nic z Nastavení nepřežije proces). Chce **týž adresář a touž migraci**, takže `Game/UserData.cs` je psaný pro obojí — ale je to vlastní změna a vlastní větev.

---

## 2026-09-03 — Claude Code (druhý zápis dne)

**#354: nic z Nastavení nepřežilo proces. Zajímavá na tom není perzistence, ale to, jak se zabránilo úniku argumentů do hráčova souboru — a že se to nedá udělat po řádcích.**

**Pravidlo z těla issue je správné a stojí za to ho mít napsané:** *hodnota, která přišla z `Program.cs`, se aplikuje, ale nikdy nezapisuje; hodnota, která přišla z kliknutí, se zapisuje.* Kdyby se vymáhalo řádek po řádku („u kvality si pamatuj, jestli byla pinnutá"), rozpadne se to na prvním novém řádku. **Vymáhá se proto strukturálně:** `_settings` **jsou hráčovy odpovědi**, pole hosta jsou **co dělá tenhle běh**, a zapisují se do `_settings` jenom settings verby. Benchmark s `quality=high` pak může kliknout na jakýkoli jiný řádek a jeho argument se do souboru nedostane, protože po té cestě nevede.

**⚠ Tři místa, kde by to jinak potichu uniklo, a všechna tři jsem našel až při psaní, ne v zadání:**

1. **`fullscreen` a `nocap` byly `bool`, ne `bool?`.** `bool` se implicitně konvertuje na `bool?`, takže to **přeložilo bez jediného varování** — a `false ?? _settings.Fullscreen` je `false`, čili uložený fullscreen by se ignoroval při každém startu. Tichá vada, kterou by chytil až hráč. Zvedl jsem nullable až do `Program.cs`; `false` neumí říct „hráč chce okno" odděleně od „nikdo nic neřekl".
2. **Čtyři hlasitosti sdílejí jeden `CycleVolume(ref float)`, a `ref` neumí říct, který řádek to je.** Zapsat při kliknutí všechny čtyři by bylo nejjednodušší — a bylo by to špatně, protože **`mute` nastaví master na nulu BEZ kliknutí**. Kliknutí na Hudbu v tichém běhu by hráči do souboru zapsalo ticho benchmarku. Každý řádek si proto jmenuje svou položku sám.
3. **`_info.Visible` bylo `true` z výchozí hodnoty `DrawableGameComponent`** — FPS overlay je zapnutý defaultně a nikdo ho nikdy nenastavoval. Default `false` v novém souboru by ho tiše vypnul všem, což není „pamatuj si, co si hráč vybral", ale změna chování schovaná v perzistenci. Default je proto `true`.

**⚠ Kvalitu rozhodl majitel a rozhodl ji jinak, než navrhovalo tělo issue — a ten důvod je obecnější než tenhle řádek.** Issue chtělo ukládat i verdikt adaptivní sondy „jako nápovědu". Jenže **sonda umí tier jen snižovat** (`_qualitySettled` je jednosměrné, a je to tam napsané schválně). Zapamatovaný verdikt by proto byl **ráfna**: jedno nešťastné okno — build běžící na pozadí, teplotní výkyv — by hru zamklo na Low natrvalo a zvedl by to už jen řádek v Nastavení. Ukládá se tedy **jen tier, který hráč kliknul**, a ten se chová přesně jako `quality=`. Poučení: **než něco změřeného uložíš napříč sezeními, zeptej se, jestli se ta veličina umí hýbat oběma směry.**

**Obloha se seedí PŘED `SetScene`, ne po něm — a je to opak toho, co dělá `sky=`.** Šest scén (moře, savana, tropy, sopka, Mars, bouře) si dosazuje vlastní kupoli; `sky=` je testovací override a jde po nich schválně, uložená volba hráče ne — jinak by savana každý start přišla o svůj zlatý horizont. Napsané před `SetScene` to znamená „to je tvoje kupole, pokud si scéna neřekne o svou", což je přesně tak trvanlivé, jak kupole kdy je.

**Atomický zápis jsem vytáhl do `Prazsky.Core.Tools.AtomicFile`**, protože #354 by ho jinak od #353 opsalo den po jeho napsání. `PlayerProgress.Save()` je teď jeden řádek přes něj a harness z prvního zápisu proběhl po refaktoru celý znovu.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, harness `PlayerProgress` ALL PASS i po přepnutí na `AtomicFile`. **V běžící hře, a je to celý řetěz:** podstrčený soubor se aplikoval (`[fps] … Meadow, dome 3, ssaa 1x, low, msaa 2x, detail reduced`); savana si přes uloženou kupoli 3 dosadila svou 14; `quality=high sky=11` obojí přebilo (`dome 11 … ssaa 2x, high, detail full`) **a soubor po tom běhu pořád hlásí `sky 3` / `quality Low`** — to je ten únik, který se testoval. Zápis přes skutečnou klávesu: F10 do běžícího okna (`keybd_event`, VK 0x79 / scan 0x44, extended) překlopil `"fpsOverlay": true` → `false` a vedle vznikl `.bak`. A dvě fotky z běžící hry přes `shot=`: se souborem `false` je roh prázdný, s `true` je tam `FPS: 78`.

**Poznámka o úklidu:** při focení jsem si vyprázdnil `Game\bin\net10.0-windows\Screenshots`, abych poznal, který PNG je nový. Je to výstup buildu a harnessu, ne ručně dělaná data — ale je to týž reflex, před kterým varuje pravidlo o `git clean`, a příště stačí filtrovat podle času.

**Majitelův save:** hash `f5c8c4ee…` ověřený i po všech těchto bězích. Testovací `Settings.json` po měření smazán, takže majiteli naskočí čistý default.

---

## 2026-09-03 — Claude Code (třetí zápis dne)

**#341: bomba blikala oranžově místo červeně. Jednořádkovka, jak desátý zápis včera předpověděl — ale cesta k ní vyrobila dva nálezy o měření, a jeden z nich zneplatňuje kus rigu, kterým se #326 měřilo.**

**Vada byla v ZELENÉ, ne v červené, a proto by „přidej červenou" nepomohlo.** `BombCharge` byla `(1.0, 0.46, 0.13)`; červená už seděla na 1,0 a neměla kam růst. Odstín je na tomhle konci kola `60 · (G−B) / (R−B)`, takže s připíchnutou červenou je jediná páka zelená — 0,46 je v lineárním světle skoro pětina červené. Teď `(1.0, 0.15, 0.05)`. Modrá schválně nejde na nulu: kanál na nule dělá z jádra náboje jednokanálovou barvu, která na hraně drážky tvrdě aliasuje a čte jako nálepka, ne jako světlo.

**Komentář, který u té konstanty stál, byl argument vydávaný za měření — a měření, které ho vyvrací, stálo dvacet řádků nad ním.** Tvrdil, že emise hodnotu vynásobí „hluboko za bílou v červeném kanálu", takže je to „spíš směr než barva" a projde tonemapem jako *žhavá* věc, ne jako oranžová. Jenže vlastní zápis #326 o pár řádků výš měří plášť na 106 až 151 kódů červené a popisuje ho slovy „z matně hnědé do jasně oranžové a zpátky". **Plášť, který kulminuje na 151, neclipuje nikdy**, takže se nic za bílou neposílá a napsaná hodnota JE barva, co dojde k hráči, v plné síle. Úmysl napsaný do komentáře se nestane pravdou tím, že se vynásobí.

**Změřeno oběma směry v jednom sezení**, dvanáct fází jednoho tepu, táž kamera (`Bombs.json`, louka, `sky=1`, `nopost nooverc ssaa=2`, `campos=0,4,30 camtarget=0,5.5,0`, pravá horní bomba, průměr přes kotouč r=8):

| | podlaha | na tepu | švih | odstín |
|---|---|---|---|---|
| oranžová (G 0,46) | 96 / 39 / 17 | 142 / 60 / 19 | 1,48× | 17–20° |
| červená (G 0,15) | 96 / 20 / 15 | 142 / 23 / 15 | 1,48× | **3,8°, plochý přes celý tep** |

**Červený kanál se nehnul o jediný kód, na obou koncích.** Hýbe se jen zelená — tedy celý odstín a, protože zelená nese 0,7152 luminance proti 0,2126 červené, i asi třetina světla náboje. Ta ztráta je reálná a **není vidět**: bomba se čte červenou proti skoro černému plášti, takže co odešlo, byla přesně ta část, co dělala jantar.

**⚠ A na tomhle jsem se spálil: kompenzaci té třetiny jsem postavil a musel vzít zpátky.** `BombRestingGlow` 0,5 → 0,65, s odůvodněním „podlaha spadla ze 106 na 96, a 106 je číslo, které #326 změřilo a schválně trefilo". **Jenže 106 bylo z cizího rigu, jiné scény a jiného sezení — citace, ne měření**, a moje vlastní čerstvé měření obou buildů říká 96 v obou. Kompenzoval jsem propad, který neexistuje. Navíc není zadarmo: ten člen zvedá podlahu i záblesk o **stejný lineární přírůstek**, takže kupuje jas za švih (1,48× → 1,36×), a švih je to, co #326 chránilo. Zapsáno u konstanty i s tím, proč to vypadalo správně. **Je to podruhé v tomhle deníku za tři dny, co číslo opsané z cizího zápisu prošlo jako naměřené** (poprvé „214 hvězd" v #353).

**⚠⚠ A nález, který přežije tenhle issue: `F5` MRAZÍ I HODINY TEPU, takže s ním nejde vzorkovat fáze animace.** Zmrazit simulaci je zavedený trik, aby se cluster mezi snímky nehoupal, a **rig #326 ho používal**. Jenže série běhů se stupňovaným `-Settle` pak fotí pořád jednu fázi. Nepozná se to z obrázků: záblesk zamrzne tam, kam padl stisk, což se běh od běhu trochu liší, takže sweep vypadá, že funguje. Změřeno na jednom buildu: **dvacet snímků přes cyklus s `F5` pokrylo 109–115 kódů, dvanáct bez něj 96–142.** První sadu jsem přečetl jako zhroucený tep a málem si na ni koupil špatnou konstantu. Houpání, kvůli kterému se po `F5` sahá, je na visící mapě po `-Wait 9` neměřitelné. Zapsáno do `screenshot` skillu i do `docs/rendering.md` — **platí pro každou pulzující kouli, nejen pro bombu.**

**Kontrola, kterou jsem po cestě zahodil, a to je taky nález:** podezříval jsem stín oblačné vrstvy (skill před ním sám varuje). Vyvráceno v týchž snímcích — tráva ve stejném rámu stojí na 1 % (151,4 → 149,9), zatímco bomba jede 37 %. Není to počasí, je to tep.

**`BombFarGlow` sahat nebylo potřeba** — desátý zápis včera radil, že „červená se musí změnit na dvou místech". Je to `float`, ne barva: škáluje **množství** téhož `BombCharge`, takže barva má jediný zdroj. Vzdálený read jsem přesto vyfotil (60 jednotek, dvojnásobek herního odstupu) a je to sytý červený kotouč.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0 („All levels rate the right way round"), žádný soubor levelu se nezměnil. **Vyfoceno:** dvanáctifázový sweep obou buildů (viz tabulka), tmavý dóm 13 (podlaha 85, tep 135, `G−B` ≈ 1 — pod tmavou oblohou je to červená ještě čistší a švih 1,60×), a sweep proti **lávě**, které se #326 bálo nejvíc: láva svítí sítí prasklin, bomba rovnými šířkovými pásy, spletou se nedají ani tam, kde je lávová koule červená. Bomba se taky nepřekrývá s **červeným typem koule**, který v `Bombs.json` visí přímo nad ní: koule 181/61/45 (světlá, lesklá, bez pásů) proti bombě 134/23/15 (tmavá, pásovaná, tepe) — asi poloviční luminance.

**Co zůstává:** #326 pořád nemá změřenou cenu snímku a **žádný shipnutý level bombu nemá**. Ani jednoho jsem se nedotkl.

**Dál pokračuji na #344** (transparentní koule barví celou spojenou skupinu) a pak **#355** (alt-tab nepauzuje), na majitelův pokyn v tomhle pořadí.

---

## 2026-09-03 — Claude Code (čtvrtý zápis dne)

**#344: barvení skla teče celým spojeným tělesem. Mechanika je pět řádků; drahé na tom bylo, že #344 sám o sobě obsahuje odůvodnění, které je NEPRAVDIVÉ, a doslovné provedení jeho druhého pravidla by zbouralo shipnutý level.**

**Pravidlo 1 hotové a je to retrakce #325.** `ColourTransparentNeighbours` → **`ColourTransparentGroup`**: seed z každého průhledného souseda dopadové buňky, pak flood fill dál sklem. **Seznam `coloured` je zároveň worklist** — obarvená buňka přestává být `Transparent`, takže se nedá naseedovat podruhé; žádná visited mřížka, žádná alokace navíc, terminace je vlastností toho, co se zapisuje. Krok se **nepohnul** (pořád mezi attach a group checkem), takže census ani group check nikdy nevidí napůl obarvenou tabuli.

**⚠ Pravidlo 2 („sklo se nikdy negeneruje samotné") jsem NEIMPLEMENTOVAL doslova, protože jeho odůvodnění je vyvrácené shipnutým levelem.** Issue argumentuje: *„osamocená tabule nemůže být součástí shody na ráně, která ji obarví"*. **Facet dokazuje opak** — jeho čirý lem je jednobuňkový taxicab prstenec, takže se žádné dvě tabule nedotýkají (64 tabulí = 64 těles po jedné) a **design z toho dělá svůj učicí tah**: buňka o krok vedle diagonálního prstence se ho dotýká DVAKRÁT, takže jedna rána obarví dvě a se střelou jsou tři. Stojí to černé na bílém v hlavičce `FacetKind` od #325. Doslovná brána by odmítla 36 ze 64 tabulí a zbourala design, jehož vlastní komentář vysvětluje, proč je postavený takhle.

**Brána se proto ptá DOPADU, ne tabule: „žádná tabule nesmí jít obarvit sama."** Formálně: těleso, jehož **každý** dopad platí jedna, je těleso, které se dá vyměnit jen za dvojici — a to je přesně ta dřina, kterou `FindLonelyBalls` všude jinde odmítá, jen sem nedosáhne (sklo nemá barvu, ve které by bylo osamělé). Vyjádřeno per těleso, ne per tabule, protože těleso se barví najednou; a jen těleso o jedné může v tom stavu vůbec být. Facet prochází, izolovaná tabule ne. **Obecně: pravidlo z issue se implementuje jako vlastnost, o kterou mu jde, ne jako věta, kterou je napsané** — a ověří se to tak, že se pustí proti tomu, co už stojí.

**`MostAtOnce` musel přestat počítat sousedy** a začít počítat velikost dosažených těles, jinak by validátor tiskl číslo, které po téhle změně není pravda. Dvě RŮZNÁ tělesa u jedné buňky se sčítají (colouring seedne obě), dvě tabule jednoho tělesa jsou jedna platba.

**⚠⚠ A tady je cena, kterou musí vidět majitel: výplata jedné rány vyskočila o řád.** Mirage pětice **4, 4, 4, 3, 12 → 5, 34, 45, 50, 116**. Solitaire má celý povrch jako jedno těleso, takže jedna rána obarví 116 ze 444 koulí. Pět skleněných levelů bylo nakresleno, když sklo stálo za kapsu; teď stojí za tabuli. **`DropTest` to nevidí** (modeluje jen stojící barevné skupiny), takže brána `oneShot` je na tuhle cestu slepá — zapsáno do `docs/formats-and-tools.md` jako otevřená otázka rovnováhy, ne zahrané do autu.

**⚠ Sag probe to chytil a chytil to správně: Diadem 2 z 5 → 5 z 5.** Změřeno **na tomtéž stroji v jednom sezení proti oběma buildům** (poučení z #341 o citaci proti měření): před — Facet 0, Trefoil 0, Harlequin 3, Diadem **2**, Solitaire 3; po — Facet 0, Trefoil 0, Harlequin 3, Diadem **5**, Solitaire 3. Harlequin i Solitaire se nehnuly; jediná změna je Diadem, a přesně z toho důvodu, který má jeho vlastní hlavička zapsaný od #325: **není to prověšení, je to rozhoupání**, a teď se houpe padesátikoulovou ranou místo hrstky. (Mimochodem: základní čísla téhle session jsou o 1 vyšší než finální pětice zapsaná u #325 — probe kolísá ±1 mezi sezeními, což je právě důvod, proč se „před" muselo přeměřit a ne opsat.)

**Opraveno toutéž pákou co minule — a zapsal jsem, že ta páka NESEDÍ na příčinu.** `DIADEM_BAND_INNER` 2,6 → 2,0 (100 → 112 kotev), čte **3 z 5 dvakrát po sobě**, tedy tam, kde sedí Harlequin i Solitaire, a pod prahem. Ale rozšiřování pásu dovnitř **přidává koule skoro tak rychle jako kotvy** (732 → 792 koulí, zátěž kotvy 8,0 → 7,7), takže koupilo mnohem míň než poprvé. Páka, co sedí na novou příčinu, je **výplata** — čtyři nedotýkající se límce by ji zastropovaly na ~25 — a to je změna toho, jak čelenka VYPADÁ, takže je majitelova. Napsáno do designu jako „když se to bude otevírat, začni tady".

**Ověřeno proti skutečné knihovně, čtrnáct tvrzení** (odhozený konzolový projekt ve scratchpadu, v žádném solutionu): řetěz čtyř se obarví celý ze kraje a vzdálená tabule (která NENÍ soused dopadu) vezme barvu střely i `Normal`; skupina se uzavře na téže ráně (5); dvojice je nejmenší platící těleso (3); **dvě tělesa po jedné u jednoho dopadu jdou obě** (Facetův tah); těleso, na které dopad nesáhne, zůstává sklem; tabule berou barvu STŘELY, ne svou uloženou; těleso **stoupá do vrstvy nad sebou** (4 tabule); obyčejný soused se nepřebarvuje; **kámen barvu nevede a zůstává kamenem**; uzavřený prstenec osmi se obarví celý a **terminuje**; scratch list se čistí per dopad. ALL PASS.

K tomu: čtyři solutiony 0 chyb, LevelGen exit 0 (brána ALONE zelená přes všech 105), ScoreSim 0, a **z kampaně se změnil jediný soubor — `Diadem.json`**, tedy ten, který jsem záměrně opravil.

**⚠ A past, do které jsem šlápl podruhé za den, tentokrát z vlastní paměti:** přejmenování v `SagProbe.cs` jsem pustil přes PowerShell `-replace | Set-Content` a **přidalo to do souboru BOM**. Mám to zapsané jako pravidlo („na trackovaný zdroj vždy Edit/Write, nikdy shell round-trip") a stejně jsem to udělal. Vráceno a přepsáno nástrojem.

**Co jsem NEUDĚLAL a je to schválně:** není fotka z běžící hry, jak přejezd barvy běží přes celou tabuli. Fade se nastavuje v téže smyčce per obarvená koule, takže rozšíření seznamu ho rozšiřuje samo, a celou cestu handleru (včetně zrcadlení na fyzikální pole) projela sag probe pětadvacetkrát ve skutečné simulaci na pěti skutečných levelech. Vizuál je přesto neviděný.

**Otevřená otázka na majitele:** má se Mirage pětice převážit na novou sílu skla (Solitaire 116 za ránu), nebo je ta podívaná žádoucí? To je designové rozhodnutí, ne mechanické, a nedělal jsem ho.

**Dál pokračuji na #355** (alt-tab nepauzuje level).

---

## 2026-09-03 — Claude Code (pátý zápis dne)

**#355: alt-tab nepauzoval level. Oprava je pět řádků; zajímavé je, že ODŮVODNĚNÍ v issue je věcně špatně, a opravit ho bylo nutné, protože ta věta by jinak zůstala stát v kódu i v docs jako fakt.**

**Vada i tvar opravy sedí přesně tak, jak issue říká: je to #79 o vrstvu výš.** Větvení na `Game.IsActive` je celé o **kurzoru** (pustí pointer, zruší capture, zneplatní aim, odjistí trigger) a všechno pod ním běželo dál. Neaktivní větev teď pushne pauzu a **vrátí se** — ze stejného důvodu, proč se vrací Escape cesta: manager aplikuje push až příští snímek, takže bez toho by tenhle snímek ještě krokoval svět. **Zůstane zapauzované i po návratu**, což je správně (aim si stejně žádá klik na re-capture, #154). Přední scéna se nepauzuje.

**⚠ Ale „strop klesá na hodinách reálného času" NENÍ pravda, a je to hlavní věta zprávy.** `UpdateCeilingDescent` jen **animuje** krok, který si vysloužila **rána** (`_ceilingStepsPending` / `ReleaseCeilingStep`), takže pole, na které nikdo nestřílí, žádné nové kroky nedostane. Chytil jsem to měřením, ne čtením: na levelu 1 jsem nechal běžet 25 vteřin mimo okno a **profil clusteru se v HUD nehnul ani o pixel** (vrchol na řádku 447 na obou snímcích). Kdybych ta čísla nesbíral, opsal bych tu větu do komentáře jako pravdu — málem jsem to udělal.

**Co doopravdy běží, a dohromady to stačí:** `StepPhysics` (visící mřížka se pořád usazuje a houpe, a prohra se čte z **živých** pozic), **krok, který si vysloužila poslední rána** (dočká `CEILING_STEP_HOLD` a sjede, když se hráč nedívá), a **`ClusterLineWatch` počítá grace pod čárou v `elapsed`, tedy na hodinách** — takže cluster, který už je pod čárou, prohraje na čase samotném. Přepsáno do komentáře u větve, do docu `PauseOnFocusLoss` i do `docs/game-shell.md`.

**Opt-out: `nofocuspause`, a `shot=` ho implikuje.** Issue si vyžádalo rozhodnout, jestli `IsActive` na zamčené ploše umí odlišit alt-tab — **a já ho neizměřil schválně**: zamknout majiteli plochu není měření, které si můžu vzít. Implikace ten dotaz **zneplatňuje**: capture schedule existuje proto, aby vyfotil konkrétní vteřinu, a běh, který si potichu vystrčí pauzu, vrátí fotku pauzy — tiše a vypadá to jako nález (past, před kterou skill sám varuje). Zapsáno i to, že to zůstává neizměřené.

**Ověřeno v běžící hře, čtyři kategorické zkoušky** (kategorické schválně — `docs/game-shell.md` má zapsáno, že skriptovaný vstup není opakovatelné měření, takže „která stránka je navrchu" je to jediné, co z tohohle harnessu unese závěr):

| zkouška | výsledek |
|---|---|
| fokus ukraden a vrácen uprostřed levelu | **PAUSED**, `30 balls left` — žádná rána neutracena |
| totéž s `nofocuspause` | level běží dál bez pauzy (staré chování na témž buildu) |
| `shot=20` s oknem 12 s bez fokusu | snímek je **živá hra**, ne pauza — opt-out drží |
| **F11** uprostřed levelu | fullscreen 1920×1080, **žádná pauza** — přepnutí si aktivaci drží |

Ta poslední byla skutečná otázka, ne formalita: kdyby si `SetGraphics` na chvíli vzalo aktivaci, pauzoval by se level při každém přepnutí na celou obrazovku. Zvažoval jsem preventivní grace okno — **neudělal jsem ho, protože měření říká, že není potřeba**, a spekulativní stav navíc je stav navíc.

**Autorsky:** `F11` přibylo do key mapy `screenshot.ps1` (chybělo tam, přitom je to fullscreen ve všech třech programech), a testovací skript na krádež fokusu zůstal ve scratchpadu, ne v repu.

**Ověřeno dál:** čtyři solutiony 0 chyb. LevelGen ani ScoreSim jsem nepouštěl — tahle větev nesahá na knihovny, generátor ani na jediný soubor levelu (`git status` to potvrzuje).

**Pro majitele:** stálo by za to opravit i **tělo #355**, které tvrdí to o hodinách reálného času; nechávám to na něm, protože komentář do cizího issue při zavírání je jeho gesto.

**Všechny tři dnešní větve (#341, #344, #355) jsou na majitelovo slovo smergované do `main` napřímo přes `--no-ff`, v tomhle pořadí.** Jediné, co při tom kolidovalo, byl tenhle deník — tři zápisy připsané na týž konec souboru ze tří větví ze společného `main`. Pro dalšího, kdo bude mít víc větví naráz: **je to konflikt na jistotu a řeší se ponecháním obou stran**, ne výběrem jedné. Větve po mergi smazané lokálně i na originu.

---

## 2026-09-03 — Claude Code (šestý zápis dne)

**#321: halo nabité koule v precise aim zakrývalo pole, na které se míří. Tady nebyl sporný fix, ale RIG — a stálo to víc než sama změna.**

**Změna je malá a má dvě osy, protože zpráva má dvě půlky.** „Moc velké" a „moc hlasité" nejsou totéž a strength sama by první nevyřešila: **tlumenější disk téže velikosti sedí pořád přes tytéž buňky**. Takže se s `_preciseAim.Blend` stahuje **dosah** (`BallGlow.Draw` ho bere per call, 4 → 2 poloměry koule) i **síla** (na 30 %, podlaha, nikdy nula — #236 nechalo halo jediným 3D vyjádřením příští barvy), a **dech se netlumí úměrně, ale úplně**: co nad čteným polem řve, je POHYB, takže nakloněná čočka dostane klidný prstenec. Že to jde stáhnout tak hluboko, umožňuje HUD proužek magazínu, který tutéž barvu drží ve 2D celou dobu.

**Přehledová kamera je nedotčená KONSTRUKČNĚ, ne podle snímku** — a to je lepší tvrzení než fotka: `PreciseAim.Step` srovná `Blend` na **přesnou nulu**, jakmile se tlačítko pustí, takže oba lerpy jsou tam identity a `(1-Blend)` je 1. Nemusel jsem to fotit a nefotil jsem to.

**⚠ Rig na precise aim je ale problém sám o sobě a nedotáhl jsem ho.** Ten režim je HOLD na pravém tlačítku a odpovídá teprve po zachycení kurzoru, takže skript musí kliknout dovnitř okna a pak držet pravé. To funguje — jenže **aim si pak jde, kam chce**: hra kurzor každý snímek vrací do středu, relativní `mouse_event` se s tím pere, a `NudgeX=200` mi scénu naklonil ve svislici, což nedává smysl a nerozřešil jsem proč. Dvě různá volání skončila se dvěma různými mířeními.

**Zachránilo to pozorování, ne boj s rigem: v precise aim sedí kulička u ústí prakticky na PEVNÉM MÍSTĚ OBRAZOVKY**, protože čočka je v pevném offsetu za ústím. Kam míří hlaveň, je tedy pro měření halo jedno. Odtud A/B: týž skript, týž level, `before` z `main`, `after` z větve, a měří se červený přebytek (R−G) po svislici nad koulí — obloha ani mrak červenou nemají, takže R−G izoluje halo.

| nad ústím | před | po |
|---|---|---|
| těsně nad koulí | 42,8 | 12,5 |
| +160 px | 23,8 | 7,6 |
| +300 px | 26,3 | 2,6 |
| +380 px | 22,5 | **−49,9** (čistá obloha) |

Před opravou halo zvedalo červenou i 380 px nad koulí; po ní je tam obloha beze stopy. Vizuálně je to ještě jasnější než ta čísla: **na starém snímku je celý střed rámu růžově vymytý včetně čelistí hlavně, na novém mají čelisti vlastní šedomodrou a halo je těsný lem kolem koule.**

**⚠ Dvě poctivé výhrady k tomu měření:** pozadí obou snímků není bit za bit totéž (mrak se mezi běhy pohnul), takže absolutní hodnoty nejsou srovnatelné — R−G ano, a proto je použité. A **v „before" snímku je v pravém dolním rohu notifikace Teams**; je daleko od měřených bodů, ale je to přesně ta kontaminace, před kterou skill varuje, a nechávám to zapsané místo abych ji zamlčel.

**Nesahal jsem na základní figury pro přehled** (`MUZZLE_GLOW_BASE`, `BallGlow.RADIUS_IN_BALL_RADII`, `BRIGHTNESS`). Issue je nabízí jako třetí variantu a samo říká, že je to majitelova volba; nahlášená vada je, že se režim neptal vůbec, a ta je opravená.

**Ověřeno:** čtyři solutiony 0 chyb. LevelGen ani ScoreSim nepouštěno — změna se nedotýká generátoru ani jediného souboru levelu.

**Dál pokračuji na #345** (šachovnice na Měsíci) a pak **#327** (Zap).

---

## 2026-09-03 — Claude Code (sedmý zápis dne)

*(Šestý zápis dne je #321 a sedí na větvi `321-muzzle-halo-ads`; tenhle je z `345-moon-checkerboard`. Obě větve jsou z mainu nezávisle, takže se při mergi potkají na konci tohohle souboru — patří oba.)*

**#345: šachovnice na měsíčním povrchu. Nález je obecnější než scéna a stojí za zapamatování: PLOCHÝ HASH NA BUŇKU JE VIDITELNÝ ČTVEREC, kdykoli je ta buňka na obrazovce větší než pár pixelů — a band limit proti tomu nechrání.**

**Zrno regolitu bylo od #208 kaskáda tří oktáv plochého per-cell hashe: ~2 cm, ~1,3 m a ~5,5 m.** Každá má vedle sebe limit, jenže ten oktávu tlumí, když její buňky **zmenší** — to je aliasingový konec. O blízkém konci neříká nic, a tam je buňka 1,3 m široká **sto pixelů** a buňka 5,5 m většina blízké země. To není zrno, to je dlažba. S #240 (mřížka kráterů) to nemá nic společného, ta drží.

**⚠ První diagnóza byla ŠPATNĚ a metoda je na tom to podstatné.** Napsal jsem si, že to bude valounová oktáva 5,5 m — vypadala jako zjevný viník — umlčel ji a **šachovnice zůstala**. Teprve umlčení celého členu rám vyčistilo, a umlčení prostřední oktávy samotné nechalo stát ty větší čtverce: **dělaly to obě velké oktávy.** Každá zabita zvlášť a vyfocena; nedalo se to vyvodit ze čtení kódu, a kdybych se po prvním pokusu spolehl na úsudek, opravil bych půlku vady a odešel.

**Oprava: jedna INTERPOLOVANÁ oktáva na ~2,8 m místo obou plochých.** Hladké tady není kompromis, je to to, k čemu metrová oktáva je — mramorovaná zem v téhle velikosti nemá ostré hrany. Dvoucentimetrová si plochý hash nechává: v té velikosti je buňka podpixelová všude, kde se vůbec kreslí, takže se ta hrana nikdy nerozliší — je to drť a na drť je hash dobrý.

**`GRAIN_SMOOTH_GAIN` je tam proto, aby to byla JEDNA změna a ne dvě.** `NoiseHash22` plní −1..1 rovnoměrně, `GradientNoise2` nad týmž hashem je perlinovské pole, jehož hodnoty se tlačí k nule a k jedničce skoro nesahají; prostá záměna při témž koeficientu by mramorování potichu **taky o polovinu ztlumila**, a výsledek by nečetl jako „čtverce jsou pryč", ale jako „země zplihla".

**⚠ Že jsou z toho DVĚ oktávy a ne tři vyhlazené, rozhodla čísla, ne vkus.** `GradientNoise2` jsou čtyři hashe proti jednomu, takže vyhladit obě velké stálo **49,9 → 46,5/47,4 FPS** (pevná kamera, ssaa 2, 1600×900, `nocap`, mediány z 31 čtení) — **1,1–1,5 ms, asi 6 % snímku**. Jedna sloučená oktáva měří **49,0**, tedy **0,37 ms**, a výřezy se od sebe skoro nedají rozeznat. Na shaderu, jehož vlastní hlavička nese build běžící na 2 FPS, není 6 % za rozdíl, který nikdo nevidí, dobrý obchod. (Měřicí skriptík nad `logfps` je ve scratchpadu, ne v repu.)

**Ověřeno:** čtyři solutiony 0 chyb; vyfoceno před/po ze dvou vantage a **na ssaa 2 i ssaa 1** (nižší tier má větší footprint, takže se limity chovají jinak — čisté na obou). Kampaně ani generátoru se to netýká, žádný soubor levelu se nezměnil.

**Dál pokračuji na #327** (Zap).

---

## 2026-09-03 — Claude Code (osmý zápis dne)

*(Šestý a sedmý zápis dne jsou #321 a #345 a sedí na svých větvích; tenhle je z `327-zap-ball`. Všechny tři vyrostly z mainu nezávisle, takže se při mergi potkají na konci tohohle souboru — patří všechny.)*

**#327 Zap: rána vedle něj sebere JEDNU BARVU z celého pole. Je to bombí destrukční cesta v největším měřítku, jaké hra má, a proto na ní bylo skoro všechno hotové — nové je jen to, čím se ta množina buněk vybírá.**

**Rozhodnutí, které si issue nechalo na tomhle typu: KTEROU barvu. Je to barva STŘELY.** Jediná, kterou si hráč vybral. Nejčastější barva na poli je mocná a úplně mimo jeho ruce; barva napsaná na kouli z toho dělá hádanku, kterou hráč přečte místo aby ji rozhodl; barva souseda je stejně libovolná jako u průhledné koule (#325). Magazín ukazuje tři koule dopředu, takže „co tahle rána vezme" je otázka zodpověditelná **před výstřelem** — a v tom je celý rozdíl mezi speciálem a loterií.

**⚠ Bere jen `Matchable` koule, a není to výjimka — je to pravidlo, které má tenhle repozitář napsané už dvakrát.** Kámen, sklo, bomba i jiný zap nesou `BallType`, který **nikdo nesmí číst**; je tam proto, že to pole má každá buňka, ne protože něco znamená. Zap čistící „bomby té barvy" by jednal na poli, které hráč nevidí. Vedlejší efekt je, že se dva speciály nemůžou sežrat.

**Pořadí uvnitř dopadu je ruling, ne detail: ZAP JDE PŘED VÝBUCHEM.** Oba armuje týž dopad (jedna procházka sousedů, `CollectArmedSpecials`). Kdyby šla první bomba, sežrala by zap jako oběť a jeden ze dvou efektů, které si hráč jednou ranou koupil, by tiše zmizel. Zap bombu vzít nemůže (viz výše), takže „široký, pak lokální" je pořadí, ve kterém proběhnou vždycky oba.

**Sonda to dostala celé** — pravidlo z #326: krok dopadu, který žije v handleru, se musí zopakovat, jinak sonda měří jinou hru. `ArmedBombs` se zobecnilo na `ArmedSpecials(kind)`, `WouldMatch` odpovídá true i na buňku, která spouští zap — a **z jiného důvodu než u bomby**, což jsem zapsal: výbuch není barevná otázka vůbec, zap je jen a pouze barevná otázka. Odpověď je stejná, protože i v nejhorším případě (barva, které na poli moc není) je to slabá rána, ne zahozená.

**Vzhled: jediný skutečně těžký kus, a jeho problém není být vidět, ale být ODLIŠEN od dvou věcí, co už existují.** Obě kolize jsou **strukturální, ne tonální**, takže se ani jedna neřeší jasem:
- **Plazma (#309)** je už teď herní „lezoucí filamenty" a na plazmovém levelu je taková **každá** koule. Rozdíl: plazma svítí celá, ve své vlastní typové barvě, měkké filamenty **driftují** rozsvíceným tělem a střed disku je jasnější. Zap je **tmavá** slupka v jedné pevné studené modrobílé a jeho figura je hrstka **tvrdých tenkých oblouků na pevných hlavních kružnicích**, které **cvakají**, ne bloudí.
- **Bomba** je ta druhá tmavá koule a hráč musí poznat, vedle které přistává: bomba jsou **šířkové pásy** a tepe pomalu a hluboko, zap jsou **šikmé hlavní kružnice**, co se kříží, a bliká rychle a mělce. Protilehlé rohy týchž dvou dialů, plus teplá tma proti studené.
- Vinylové gores jsou poledníky, takže figura od pólu k pólu byla vyřazená dřív, než se kreslila.

**⚠ „Rychle" je tady správně a u bomby to bylo špatně** — vypadá to jako spor a není. `Heartbeat` má svítivé okno jako pevný **zlomek** cyklu, takže rychlejší tep bliká **kratčeji**; to bombu zabilo, protože jejím readem JE ten záblesk. Readem zapu je nakreslená figura, kterou drží rozsvícenou vlastní podlaha (mimo `BallEmission`, z měřeného důvodu #326 — klidový člen se násobí okluzí na druhou a speciál zahrabaný v hromadě je ten, který je nejvíc potřeba vidět). Tep jen přidává jitter na něco už viditelného, a jitter má být krátký.

**⚠ Oblouky byly napoprvé moc tlusté, a oprava je bombí lekce čtená obráceně.** Při šířce 0,17 byl close-up správně a herní odstup byl **kravský vzor** — bílé skvrny na černé, protože takhle tlusté linky se při zmenšení slijí. Na 0,115 je zblízka klec tenkých jasných oblouků a z dálky to spadne na podlahu záře, což je přesně ta architektura, ke které nakonec došla i bomba: figura nese zblízka, barva a blikání nesou zdaleka. Oblouky se skládají přes `max`, ne součtem — součet by na křížení zdvojnásobil světlo a udělal z něj bouli, přitom to, co se tam má číst, je **tvar** dvou protínajících se čar.

**Ověřeno — 29 tvrzení proti skutečné knihovně** (odhozený konzolový projekt ve scratchpadu, v žádném solutionu; mapová i **fyzikální** polovina, ta druhá ve skutečné `PhysicsWorld` s `BuildBallsStructure`, jak to dělalo #326): round-trip formátu s klíčem `"k":4`, obě pravopisné varianty, oba predikáty, **žádná skupina nikdy neobsahuje zap** a je přesně rovna obyčejným koulím té barvy, pole samých zapů se počítá jako vyčištěné, census barvu zapu nedrží naživu — a v simulaci: **zap sebere celou barvu (75 koulí) plus sebe**, ostatní zapy i **bomba s touž uloženou barvou zůstávají**, kotevní vrstva stojí, **mapa a fyzikální pole souhlasí v každé buňce**, sukně visící na sebrané vrstvě osiří (9), zap nedokončí žádnou skupinu, **dva zapy z jednoho dopadu jdou oba**, zap, který už odešel, nespustí nic, zap na nepřítomnou barvu **zničí aspoň sebe** (hráč tu ránu utratil, zap zbylý stát by četl jako odmítnutá rána), a **zapnutí poslední barvy pole vyčistí**.

K tomu: čtyři solutiony 0 chyb, LevelGen exit 0, ScoreSim 0, **kampaň bajt za bajtem nedotčená** (žádný soubor levelu). `Testbed\Maps\Zaps.json` je testovací pole, zastagované hned podle pravidla o netrackovaných datech.

**Co jsem NEUDĚLAL a je to schválně:**
- **Žádný shipnutý level zap nemá**, na precedentu #326: tohle issue mělo postavit mechaniku, kapitola je práce na příště. Znamená to, že magazín ani skóre nejsou vyfocené **ve hře** — přečtené ano: `_magazineTransmute`/`_magazineFrom` jsou pole **per slot**, takže pět slotů přebarvujících se naráz je pět nezávislých časovačů (issue se ptalo správně, odpověď je „bezpečné konstrukcí"), a zapnutí poslední barvy padá na existující stráž `AnyBallTypeAlive`, tedy na cestu #176.
- **Cena snímku neměřená** — táž díra, kterou má otevřenou #326.
- **Skóre**: `ScoreSim` je zelený, ale žádný shipnutý level zap nemá, takže o hvězdných prazích neříká nic. Až vyjde zapový level, je to první věc k přeměření — přesně to, co #173 chytilo.

**Dál nic si neberu**; #321, #345 a #327 čekají na majitelovo slovo.

**Merge všech tří proběhl týž den na majitelovo slovo, `--no-ff`, v pořadí #321, #345, #327; větve pak smazané lokálně i na originu.** Deník kolidoval podruhé týmž způsobem a znovu se to vyřešilo ponecháním obou stran — s tím, že tentokrát se **dvakrát duplikoval i odstavec o tom prvním mergi**, protože ho měly obě strany. **Pro příště:** připsat vlastní zápis a nic jiného v tomhle souboru neupravovat; věta o mergi patří do zápisu té práce, ne na konec souboru, kde se s ní potká každá další větev.

---

## 2026-09-04 — Claude Code

**#322: kamera nesledovala chůzi děla (W/S). Nejzajímavější na tom není ten follow — je to číslo, které vypadlo hned z prvního měření: chůze v nasazené kampani nemá dopřednou půlku.**

**Klidový poloměr je na všech měřených levelech 15,5, a to je přesně `FUNNEL_TOP_RADIUS + CANNON_DRAIN_CLEARANCE`.** Rozsah chůze je pak 15,5..19,5 — tedy klid **leží na blízkém konci**, `W` z klidové pozice nedělá vůbec nic a `S` je couvání, které se pak dá vrátit. Není to náhoda návrhu levelů, je to aritmetika: standoffová mez by dělo postavila na `distance − 15`, a nejširší pole sady (17×17×18, `Donut`/`Elephant`/`Trophy`) se rámuje na 30,5, takže ta mez nikdy nepřeleze 15,5. Měřeno na `One`, `Colossus`, `Cube`, `Onion`, `Ten`, `Column`, `Donut`, `Elephant`, `Trophy` — všude stejný rozsah.

**A couvání jde K OBJEKTIVU, ne od něj.** Kamera stojí za dělem na téže úsečce (28,8 proti 15,5), takže větší poloměr = blíž k čočce: `S` dělo **zvětšuje**. Znaménko jsem měl v půlce návrhu obráceně a přišel na to až na číslech ze hry, ne z kódu.

**Follow je proto jednostranný a ta asymetrie je geometrie, ne vkus.** Vyřešený standoff je *nejmenší* vzdálenost, na které se pole, sklo i dělo vejdou do frusta — objektiv, který by šel s dělem dovnitř, by nejdřív snědl `FIT_MARGIN` a pak pole ořízl. Ven se nemůže oříznout nic, všechno se jen zmenší.

**Podlaha, která na tom byla nejcennější, stála v kódu celou dobu napsaná a nikdy se nevymáhala.** `CANNON_ADVANCE_STROKE` má ve své vlastní dokumentaci větu, že dělo smí přijít na `CANNON_CAMERA_STANDOFF − stroke` = 11 jednotek od čočky a ne blíž — s dovětkem „když ho postavila standoffová mez". Jenže ta ho na téhle sadě nestaví nikde, takže skutečnost byla **8,3 až 9,3**. Follow je tedy `max(0,5 · couvnutí, couvnutí − rezerva k jedenáctce)`: zlomek je pocit (chůzi se nechává půlka její vlastní zpětné vazby), podlaha je odvozená a na těsných levelech přebírá.

**Změřeno v běžící hře dočasnou sondou** (`[walkprobe]`, odstraněna před commitem), na plném couvnutí: `Ten` objektiv 27,84 → **30,50** (dělo 12,34 → **11,00** před čočkou, předtím 8,34), `Colossus` 28,82 → **30,82** (13,32 → **11,32**, předtím 9,32). Na `Ten` bere podlaha 2,66 ze 4, na širokých levelech bere zlomek 2,0.

**⚠ Tabulka změřených fitů v `docs/game-session.md` byla zastaralá a je to poučné čím.** Stálo v ní `Colossus` 12×12×18 → 35,5 out / orbit 20,5 / walk 16,5..24,5; totéž pole dnes měří **28,8 / 15,5 / 15,5..19,5**. Skoro sedm jednotek odstupu. Příčina má jméno: **#135** (`5e427ba`) přestal rezervovat rám na celý barel *pod* čepy a utratil ho za pole, takže objektiv směl blíž — a tabulku po něm nikdo nepřeměřil. Přepsána dnešní změřenou sadou; historické sady (pre-dome, pre-dish) zůstávají, ty jsou záznamem toho, co tehdy měřily.

**Výška se nesleduje dál** a teď je napsané proč: lens je podlážený na `LENS_FLOOR_Y` (stance na arris), dělo je na míse vždycky pod ní, takže follow ve výšce by byl buď nula, nebo přesně ta změřená vada, kvůli které podlaha vznikla (kamení sežere spodní polovinu rámu).

**Ověřeno:** čtyři solutiony 0 chyb, ScoreSim „All levels rate the right way round". Fotky z běžící hry (`play level=… shot=`, `S` držené přes `keybd_event` 8 s, okno ověřeně zaostřené): `Ten` a `Colossus` v trojici klid / couvnuto **před** / couvnuto **po**, plus jedna s **drženým RMB** na couvnutí. Před změnou dělo na plném couvnutí ořezávalo spodní hranu rámu, po ní se do něj vejde i s koly. `LevelGen` jsem **nepouštěl** schválně: přepisuje soubory levelů a tahle změna se levelů ani mířidel nedotýká (klidový poloměr, rozsah chůze ani `aimcheck` se nehnuly — hnul se jen objektiv).

**⚠ Provozní poznámka, ať to nikdo nemusí luštit z historie:** dělal jsem ve **vlastním worktree** `C:\Users\panrd\source\repos\BS3D-322`. V majitelově stromě leží rozdělané **#360** (`Game/Levels/*`, `Tools/LevelGen/Program.cs`, `docs/formats-and-tools.md`) **bez jediného commitu** a dva z těch souborů se mezitím změnily i na mainu — přepnutí větve by je git odmítl přepsat a stashovat cizí nedodělek nepřipadá v úvahu. Na žádný z nich jsem nesáhl.

**Co zůstává otevřené a je to vlastní issue, ne tahle větev:** ta **jednostranná chůze**. `W` je z klidu mrtvá klávesa na každém nasazeném levelu a náprava je buď posunout klidový poloměr ven (mění pocit i mířidla všech levelů naráz), nebo pustit kola nad sklo odvodu (to `CANNON_DRAIN_CLEARANCE` zakazuje z měřených důvodů). To je rozhodnutí majitele, ne vedlejší efekt opravy kamery.

---

## 2026-09-04 — Claude Code (druhý zápis dne)

**Majitel zadal „pracuj dál sám, dokud ti nedojde limit", a je jediný agent na repu, takže tenhle zápis je jen rozcestník — argumenty jsou v komentářích u kódu a v `docs/`, kam patří.**

Zavřeno a na mainu: **#334** (přeměření palety pod světlem, se kterým se hra prodává), **#366** (objektiv vleče otočku děla a doklouže zpátky), **#365** (tmavší sklo okénka), **#363** (oprava těla #355), **#351** (spáry desek přestaly korálkovat). Založeno: **#364** (chůze děla nemá dopřednou půlku) a **#367** (Testbed vs. editor se rozcházely v barvě kuliček víc, než #334 dokáže vysvětlit). Zavřeno bez práce na majitelovo rozhodnutí: **#357** (barvoslepost — „nikdo barvoslepý hru hrát nebude").

**Tři věci, které stojí za přečtení i mimo své issue, protože každá zabila jinak rozumnou opravu:**

1. **#351: normála je konstantní přes 2×2 kvádr.** `PerturbNormalFromHeight` ji staví z `ddx`/`ddy`, a screen derivace je jedna hodnota na kvádr — takže náběh zkosení, který stoupne uvnitř jednoho pixelu, naklopí **celé kvádry naráz**. Odtud korálky. Toksvig (učebnicová oprava) je tady k ničemu, protože ta odchylka je **přesně nula** — vykreslená barvou byla celá deska černá. Oprava je jedno číslo: náběh se roztahuje 2,5× footprintu tam, kde se šířka roztahuje 0,5×.
2. **#334: člen zatažení je na kuličkách malý.** Řízeně měřeno uvnitř jednoho programu při `cover 1.000` (lerp na 0,982, ambient z 0,093/0,315/0,638 na 0,609/0,633/0,679): kulička se hne o **1–2 úrovně**, žádný pár CIEDE2000 o víc než 1,4 dE. Dvouprogramová čísla v tělu #334 tedy nejsou tímhle členem a jsou teď #367.
3. **#365: propustnost není jas na obrazovce.** Půlka propustnosti hnula zakrytou frontou o čtvrtinu — ACES to stlačí, na tomhle okně jde jas zhruba jako `propustnost^0,44`. „O polovinu tmavší" proto stálo alfu 0,62 → 0,92, ne 0,81.

**Provozně:** pracuju ve vlastním worktree `BS3D-322` (majitelův strom drží nedokončené #360 bez commitu a nesahám na něj). Ověřování všude stejné: čtyři solutiony, ScoreSim tam, kde se dotýká skóre, a fotky z běžící hry nebo Testbedu s pevnou kamerou.

---

## 2026-09-05 — Claude Code

**Rotace deníku (#358) je tenhle zápis sám.** Srpen (142 zápisů, 517 KB) je beze změny v `docs/agent-notes-archive/2026-08.md`, tady zůstal jen aktuální měsíc: 600 KB → 147 KB. Ověřeno bajtově — obě části jsou proti originálu `diff -q` čisté a jediné, co ze souboru zmizelo, je oddělovací `---` mezi posledním srpnovým a prvním zářijovým zápisem. Pravidlo je v hlavičce a je schválně hloupé: rotuje ten, kdo píše první zápis nového měsíce, a **hledá se přes obojí**.

**#350 (vlastní kurzor) jsem vrátil z mainu a issue je zase otevřené.** Majitel ho nevidí ani v okně, ani ve fullscreenu, po dvou pokusech o mechanismus. Nechat na mainu funkci, která nic nedělá, a dokumentaci, která tvrdí, že funguje, je horší než ji nemít.

**⚠ A je v tom past, která stojí za přečtení, protože kvůli ní jsem dvakrát hlásil „ověřeno" o něčem, co jsem neviděl:** `CopyFromScreen` **nikdy neobsahuje kurzor** — systém ho skládá až nad plochu obrazovky. Můj rig si ho proto dokresloval sám (`GetCursorInfo` + `DrawIconEx`), jenže to kreslí **ten handle, který je zrovna aktuální, ve vlastním procesu**: dokazuje to, že handle existuje a je nastavený, a neříká to **nic** o tom, co maluje kompozitor. Změřené a platné zůstává: šipka se rastruje správně, MonoGame z ní staví korektní kurzor (1bpp maska + 32bpp barevná bitmapa) a `GetCursorInfo` ho hlásí jako zobrazený. Neplatí nic o obrazovce. Kdo to bude zkoušet znovu: **nejdřív si vyžádej fotku obrazovky** a teprve pak vybírej mechanismus. Všechno ostatní je v issue.

**Hotové dnes a na mainu:** #342 (odpojené kuličky v klidu blednou do popela — sedmý region kbelíků, přechod přes ditherový rozpad, kvalifikace se měří rychlostí i výškou), #290 (řádek *Drop camera* v Nastavení, čtený jen tam, kde se převzetí kamery rozhoduje). Včera #322, #334, #366, #365, #363, #351; založené #364 a #367.

---

## 2026-09-05 — Claude Code (druhý zápis dne)

**Majitel si ze čtyř nabídnutých issue vybral tři a udělal jsem je v pořadí #279 → #356 → #289; všechny tři jsou na mainu (`54374fd`, `bcee2bb`, `7a27ca2`, merge `--no-ff`).** Argumenty jsou v komentářích u kódu a v `docs/`; tady je jen to, co by se z nich nedalo vyčíst, plus dvě věci, které čekají na majitelovo slovo.

**#279 (výběr hudby v Nastavení) — řádek je *náhled*, ne nastavení, a je to strukturální, ne zapamatované.** Nic na té cestě nesahá na `_settings`, takže volba nepřežije běh, a nic nesahá na hudbu levelu. Hodnota řádku se **nečte z uložené volby, ale z `ProceduralMusic.SoundingTheme`** — z toho, co je *chtěné*, ne ze stavu instance, takže přepnutí zachycené uprostřed prolnutí už jmenuje přicházející skladbu. Kdyby si řádek pamatoval, co si naposled vyžádal, musel by se mazat na dvou místech (level si nasadí vlastní téma, front end si vezme zpátky svou smyčku) a jednou by se to zapomnělo. *Auto* je hodnota přetečení a nabízí se **jen na front endu** — v levelu nemá co znamenat. „Nezapisuje se" je změřené, ne tvrzené: hash i čas `Settings.json` beze změny přes šest kliknutí.

**#356 (kámen: jeden kus třináctkrát) — issue si samo řeklo, ať se hromada nejdřív vyfotí, a ta fotka zabila i tu zachraňující úvahu, na které stála hlavička techniky.** V `InstancedModel.fx` stálo, že *„fyzika dá každému kameni vlastní orientaci, takže hromada ukazuje týž kámen ze sta úhlů"*. Změřeno dočasnou sondou v `ClusterCollector` (odstraněna před commitem), level **Cairn, 209 kamenů**, nejhustší z pěti kamenných: **průměrný náklon od identity 0,05°, největší 0,28°** za šedesát sekund visení; jen první sekunda po stavbě dosáhne 6,3°, jak se dotáhnou vazby. Důvod je jednořádkový a stojí v `BallsConstraintsBuilder`: těla se zakládají přes `CreateDynamic(<Vector3 position>, …)`, tedy **na identitě**, a mřížka visící ze stropu žádné neotočí. Na 4× zvětšeném výřezu z hráčské vzdálenosti (3840×1529) má každý kámen ve sloupci **tentýž bledý vír na tomtéž místě** — razítko, ne rubanina.

**A oprava nestála ani bajt streamu, ačkoli issue i hlavička jmenovaly „pátý instance element" jako jedinou cestu.** `ModelInstance` je opravdu plný (88 B), ale **world matrix už ve streamu je** — takže stačí do ní před pózu těla složit pevné natočení odvozené z buňky (`RockTurns`, 128 orientací Shoemakovou konstrukcí nad Haltonovou posloupností, hash buňky → index). Nulový kanál, nulová změna shaderu, nulový dopad na simulaci (kolider je koule, vazby kotví z pózy těla, a kreslicí matice není ani jedno). Klíč je **buňka, ne pozice** — pozice se hýbe a pole z ní osazené by po padajícím kameni plavalo, což je přesně past, kterou hlavička sama jmenuje; `ArrayPosition` se u kamene nastaví při stavbě a nikdy nezmění (kámen je zeď, nikdy se nestřílí). Totéž natočení bere `BallDrawFrame.AddMap`, takže lavice editoru a pozadí menu ukazují tutéž hromadu jako session.

**⚠ Ale u toho vypadlo něco, co jsem *neřešil* a co je na majiteli: úplně stejnou vadou trpí i barevné koule.** Na fotce má **každá** koule tentýž bludišťový vzor v tomtéž místě — třináct barev, jeden reliéf, jeden úhel. Rozhodl jsem se to nechat: koule jsou lisovaný vinyl z jedné formy a to, že série vypadá stejně, je fikce, ne vada v ní — zatímco rubanina z lomu stejná není. Ale je to *rozhodnutí*, ne fakt, a technicky by ho stejná jedna řádka spravila i pro ně (stačilo by v `Collect` vypustit podmínku na `BallKind.Rock`). Kdyby to majitel viděl jinak, je to pět minut práce.

**⚠ A ještě: počty kamenů v `docs/rendering.md` byly u tří z pěti levelů zastaralé** (Seam 181 → 155, Cairn 253 → 209, Obsidian 117 → 59; Anvil 82 a Keystone 132 seděly). Generátor ty levely mezitím přepsal. Opraveno v témž commitu, spočítáno z `Game/Levels/*.json` a nezávisle potvrzeno sondou v běžící hře.

**#289 (vyhlídkové body scén) — `SceneViewpoint` je bod plus recept a to rozdělení je celý návrh.** *Kde* ta zajímavá věc je, ví jen scéna (kráter se hýbe s konfigurací kužele, oheň savany se svou, a panel v editoru hýbe oběma, zatímco se někdo dívá — proto se každé číslo čte z **živé** konfigurace a nikde se nepíše podruhé). *Jak daleko a jak vysoko* stát je věc **kamery** a je to ta půlka, která musí škálovat s **levelem**: vysoký shluk se hraje z větší dálky a jeho úvodní záběr couvne s ním. `SceneRenderer.TryGetViewpoint` se tak řadí k `ReplacesSky` / `IsSolidTerrainScene` / `OpenBelow` a odpovídá **všech sedmnáct** scén.

**Polovina scén má orientační bod a jmenuje ho; druhá polovina ho nemá — a „nemá bod" není „nemá vyhlídku".** Louka je ze všech stran stejná, ale pořád chce **nízký, blízký** pohled na květy tam, kde hory chtějí **daleký, zvednutý** na štíty — a právě tohle bylo na starém záběru generické, ne ten náhodný azimut. Ty scény si bod staví **z toho náhodného azimutu**, který volající stejně losuje, takže savana jmenuje svůj oheň a louka pořád přiletí odkudkoli. `DURATION_SECONDS` 7 → **9,5** na majitelovo „pomaleji".

**⚠ Past, která z toho spadla hned při první fotce a stojí za přečtení i mimo tuhle issue: Catmull-Rom se mezi vzdáleným a blízkým klíčem *prohýbá dovnitř*.** Tečna v klíči je tětiva mezi jeho sousedy, takže noha vedoucí od dalekého klíče k blízkému nejde po oblouku, ale prořízne vnitřek — a protože scénické nohy teď stojí tak daleko, jak si scéna řekne (2,4× na sopce proti 1,5× na louce), místo pevného násobku, sopečný úvod se vyfotil jako **snímek, na kterém není nic než koule na dosah ruky**: objektiv proletěl shlukem. Podlaha na poloměr ve `Frame` (`gameDistance * 0,92`) to řeší a nemůže rozhodit přistání, protože klíč 3 *je* herní póza a stojí přesně na tom standoffu.

**Vyfoceno na každé scéně, ve které kampaň umí otevřít kapitolu — na všech jedenácti, jeden běh na první level bloku.** Dvě vyhlídky změnila fotka, ne úvaha, která je vybrala: jeskyně se v prvním návrhu dívala **nahoru** na strop (protože definiční rys jeskyně je nad hlavou a herní kamera se tam nikdy nepodívá) a vyšla skoro **černá** — světlo je v té scéně celé zdola; sen měl v záběru mramorování a dvě koule a ostrov mimo. Zbylých šest scén (moře, les, outback, tropy, Mars, bouře) má vyhlídky odvozené z jejich konfigurací a **záměrně je netvrdím jako vyfocené**: žádný level ve shipnuté sadě je nejmenuje (`grep '"scene"' Game/Levels/*.json` dá jen jedenáct), takže v žádné z nich kapitola otevřít nejde.

**Ověřeno u všech tří:** čtyři solutiony 0 chyb; fotky z běžící hry (`play level=… shot=…`), u #356 A/B na stejné velikosti okna, stejném levelu, scéně i čase snímku a s netknutým shlukem (72 balls left na obou); u #289 `[intro]` řádek z konzole pro každý z jedenácti bloků. ScoreSim a LevelGen nespouštěny — žádná ze tří změn se nedotkla skórování ani levelů.

**⚠ Provozně, a je to i díra ve hře:** majitel po dvou restartech stroje požádal, ať hru **nespouštím na fullscreen**. Zjistil jsem, že o to **nejde požádat z příkazové řádky**: `Program.cs` zná jen argument `fullscreen` (nastaví `true`) a jinak nechá odpovědět `Settings.json`, ve kterém majitel fullscreen uložený má — takže neexistuje „windowed". Musel jsem `%LOCALAPPDATA%\BS3D\Settings.json` dočasně přepsat a po práci vrátit (obnoveno bajtově, SHA-256 `30eefb41` sedí). Chybějící `windowed` (nebo `fullscreen=0`) je podle mě samostatná malá issue a nezakládal jsem ji — na majitelovo slovo. Okno jde zvětšit přes `ShowWindow(SW_MAXIMIZE)`, swap chain se za klientskou plochou dotáhne, takže nativní hustota jde získat i bez fullscreenu.

**Pracoval jsem ve vlastním worktree** `C:\Users\panrd\source\repos\BS3D-322`. Majitelovo rozdělané **#360** v hlavním stromě jsem nechal na pokoji a na `Game/Levels/*`, `Tools/LevelGen/Program.cs` ani `docs/formats-and-tools.md` jsem nesáhl — proto ze shortlistu vypadly #359, #361, #362 a #364.

**Nic dalšího si neberu**; #367 majitel nevybral a obě otevřené otázky výše (bludiště na barevných koulích, argument `windowed`) čekají na jeho slovo.

---

## 2026-09-05 — Claude Code (třetí zápis dne)

**#364: chůze děla neměla dopřednou půlku. Rozhodnutí bylo majitelovo (2 dopředu, 4 zpět), zajímavé je, KDE se ta dvojka přičítá — a co ukázalo měření o ceně, kterou jsem předem odhadl špatně.**

**Oprava je podlaha na klidový poloměr, ne změna chůze**, a to je celé jádro. Klidový poloměr je největší ze čtyř mezí, blízký konec chůze největší ze tří — a dvě z nich jsou tytéž dva výrazy. Na celé sadě vyhrává obě `CANNON_DRAIN_CLEARANCE`, takže klid i blízký konec seděly na témž čísle (15,5) a `MoveRadial` četl `room = 0`. Nová `CANNON_ADVANCE_FORWARD = 2` se proto přičítá **k těm dvěma clearance**, a ne k vyřešenému poloměru: kola smějí pořád na 15,5 a hráč na tu mez teď **dojde chůzí**, místo aby na ní level začínal. Kde dělo staví mez, která chůzi neomezuje (standoff, elevace), se nepřičítá nic — tam je dopředná vůle už z definice.

**⚠ Cenu jsem majiteli odhadl řádově špatně a měření to opravilo, ne úsudek.** Do rozhodovací otázky jsem napsal „kampaň se rámuje o ~2 jednotky dál". Skutečnost, párovaně na témže okně a témž stroji (9 polí, `[camera]`): **objektiv se hnul o 0,1 až 0,4**. Fit rámuje POLE, a dvě jednotky děla nezmění, co je potřeba na udržení rohů. Pole je na obrazovce prakticky beze změny — co se změnilo, je **dělo**: klidová mezera 12,3–15,0 → **10,7–13,1**, tedy asi o 15 % větší hlaveň. Kdybych to nezměřil, stálo by v dokumentaci číslo, které jsem si vymyslel od stolu.

| pole | objektiv před → po | orbit | chůze | nejstrmější buňka |
|---|---|---|---|---|
| 12×12×16 `One` | 30,3 → 30,5 | 15,5 → 17,5 | 15,5..21,5 | 59,4 → 52,3° |
| 12×12×18 `Colossus`, `Cube` | 28,8 → 29,1 | | | 59,4 → 52,3° |
| 15×15×27 `Onion` | 29,1 → 29,3 | | | 54,5 → 48,1° |
| 11×11×34 `Ten`, `Column` | 27,8 → 28,2 | | | 40,5 → 35,7° |
| 17×17×18 `Donut`, `Elephant`, `Trophy` | 30,5 → 30,6 | | | 73,7 → 64,8° |

**Vada i oprava jsou změřené kategoricky, ne vyfocené — a to je tady lepší důkaz.** Dočasná sonda na orbit radius per snímek (`[walkprobe]`, před commitem smazaná), klávesa držená přes `keybd_event` po kliknutí na titulek: **před opravou 213 snímků drženého W a poloměr nezůstal na 15,500 „skoro", ale přesně** — ani tisícina; držené S ho poslalo 15,500 → 19,500. **Po opravě W jede 17,500 → 15,500** a 95 % té dvojky ujede za ~1,4 s držení, takže `ADVANCE_EASE_ZONE` (1,5) příjezd tlumí, ale stroke nesežere — což byla reálná obava, protože 1,5 ze 2 leží v gumě. `Donut` chodí stejně. **`[aimcheck]` prochází na všech devíti** a nejstrmější buňka žádá o 4,8–8,9° MÍŇ než dřív: dělo stojící dál střílí plošeji.

**⚠ Jedna mez se tím přesáhla a nechávám to napsané místo zaokrouhlené.** `CANNON_ADVANCE_STROKE` má v dokumentaci, že dělo smí přijít na `STANDOFF − stroke` = 11 jednotek k čočce a ne blíž. Klidová mezera je teď na dvou nejtěsnějších polích **10,7** — 0,3 uvnitř. Couvání to nezhorší (follow tam bere celé 4 místo poloviny, takže mezera přes celou chůzi je ta klidová), ale **cena je, že na těch levelech couvání nemění velikost děla vůbec** a zbývá mu jen ujíždějící svět — jediný kousek zpětné vazby #322, kterým se dopředná chůze zaplatila. Kdyby to majiteli četlo špatně, je to jedna konstanta: **1,5 místo 2** drží všech devět polí nad jedenáctkou (na `Ten` 11,08), za cenu tří čtvrtin dopředné chůze.

**Fotky:** klidová dvojice před/po na `Ten` (nejtěsnější) a `Donut` (nejširší) — pole rámované stejně, dělo o kus větší, nikde nic neořezané. **Držené W jsem NEVYFOTIL a je to poznámka pro příště:** rig, který drží klávesu, musí nejdřív kliknout do okna, a hra si pak bere kurzor — na snímku bylo dělo natočené o půl traverzy a v letu rána. Táž past, kterou má zapsanou #321 u precise aim. Čísla ze sondy jsou tu stejně silnější důkaz než fotka posunu o 2 jednotky.

**Nedotčeno a ověřeno, že nedotčeno:** `SagProbe` staví dělo z týchž dvou clearance napřímo (ne z fitu), takže **žádná sag figura se nehnula**; `LevelGen` exit 0 a **ani jeden soubor levelu se nezměnil**, `ScoreSim` „All levels rate the right way round", čtyři solutiony 0 chyb. Dokumentace přepsaná v `docs/game-session.md` (změřená tabulka fitů + co ta dvojka stála) a v `docs/testbed.md` („Cannon controls"). `AboutPage` říká „W/S adjusts depth", což je nově pravda beze změny.

**⚠ Provozní chyba, ať ji nezopakuje nikdo další:** opravoval jsem sondu přes `sed -i` na trackovaném `.cs` a ono to celému souboru přepsalo CRLF na LF — obsah nula změn, diff celý soubor. Vráceno přes `git checkout --`. Na trackované zdrojáky patří editor, ne shell.

**Dál beru #362** (sklo v Mirage) a pak **#360** (tvary čtyř levelů Coilu). Založil jsem dnes taky **#368** (bomba a zap nejsou v žádném shipnutém levelu) a **#369** (Eruption má 5 levelů proti deseti všude jinde).

---

## 2026-09-05 — Claude Code (čtvrtý zápis dne)

**#362: sklo po #344 platí tabuli místo kapsy. Majitel rozhodl „opravit bránu, z designů sáhnout jen na odlehlý Solitaire" — a při tom vypadl nález, který je obecnější než ten level: TAXICAB PRSTENEC NENÍ SÁM V SOBĚ SPOJITÝ.**

**Brána byla slepá, a to je ta půlka #362, co byla vada a ne otázka.** `DropTest` četl skupiny tak, jak stojí v autorském layoutu, takže u Solitaire hlásil „best single shot drops 22 (4 %)" proti skutečným **186**. Brána, která cituje procento mimo o faktor osm, je horší než žádná. Nově bere maximum ze staré cesty a `GlassLandingDrop`, který dopad **odehraje** v pořadí `BallContactEventHandler` (polož kouli, obarvi spojitá tělesa, teprve pak spočítej skupinu a co osiří) pro každou prázdnou kapsu u skla a každou barvu levelu. Čísla Mirage po opravě: **Facet 26 %, Trefoil 29 %, Harlequin 19 %, Diadem 19 %, Solitaire 41 %** — tam, kde dřív stálo pár procent. Žádný level neprošel přes `ONE_SHOT_PERCENT`.

**⚠ NÁLEZ, KTERÝ STOJÍ ZA ZAPAMATOVÁNÍ MIMO TENHLE LEVEL: prstenec v taxicab metrice není spojitý ve vlastním kurzu.** Krok po vrstvě mění taxicab vzdálenost přesně o jedna, takže žádné dvě buňky prstence o poloměru r spolu nesousedí — skleněné těleso je prstenec **poskládaný svisle** přes kurzy schodu. Odtud plyne všechno ostatní: Solitaire měl čtyři tělesa (12, 36, 60, 56) a **116 = 60 + 56**, protože dopad do rizalitu mezi dvěma schody sáhne na obě. Tohle jsem nevydedukoval z kódu — změřil to odhozený konzolový probe ve scratchpadu (v žádném solutionu), který vypisuje tělesa, jejich kurzy a nejlepší dopad. Bez něj bych „rozděloval těleso", které už rozdělené bylo.

**Dvě opravy jsem díky tomu probe zahodil dřív, než se dostaly do generátoru** — každá vypadala rozumně a měřením spadla:
- **obarvit horní kurz každého schodu** (levels 6, 9, 12): výplata 116 → **96**. Můstek přežil, protože kapsa na úrovni seamu sahá na vrstvu POD i NAD sebe, a co je na její vlastní vrstvě, je jí jedno.
- **obarvit spodní kurz každého schodu** (7, 10, 13): výplata → 40, ale **horní schod se rozpadl na 20 samostatných tabulí**, protože mu zbyl jediný kurz (nad ním je kotevní) — a `FindStrandedSpecials` samotné sklo odmítá. Level by neprošel vlastní bránou.

**Co tam nakonec je: ČTYŘI HRANY KAMENE ZŮSTÁVAJÍ BAREVNÉ** (`min(|dx|,|dz|) < 1`), takže prstenec každého schodu se rozpadne na čtyři svisle poskládané **fasety** a dopad do rizalitu vezme dvě fasety jednoho kvadrantu místo dvou celých schodů. **21 obarvených, 86 dolů (19 %)** — přesně tam, kde už sedí Harlequin a Diadem. Práh je exaktní, ne laděný: lichý kurz má buňky na celých číslech, sudý na půlkách, takže pod jedničkou existují jen hodnoty 0 a 0,5 — hrana je široká jednu buňku na lichém kurzu a dvě na sudém a nic mezi 0,5 a 1 nic nemění. Při `<= 1` (tj. 1,1 v probe) se ukrojí buňka navíc a zůstanou osamocené tabule. **Špička je vyjmuta** z vlastního staršího důvodu: těleso široké jednu jednotku nemá co fasetovat, a obarvit ji znamená čtyři kvadrantové barvy po jedné buňce, což je přesně ta osamocená koule, kterou `SolitaireKind` už jednou řešil.

**Vizuálně to je změna, ne úklid, a fotka to říká líp než číslo:** předtím byla nad ostrovem mléčná hmota s barvou uvnitř, teď má kámen nakreslený **řez** — barevné hrany po siluetě a schody čitelné. Doc levelu jsem podle toho přepsal (jeho vlastní věta „whose entire outer surface is clear" už neplatí) a je fér říct, že se tím mění i první tah: pár barevných buněk na povrchu jde trefit rovnou, kdežto dřív se muselo začít obarvením skla. Beru to jako v duchu vlastní věty toho designu („this one makes the body glass and the colour the mark"), ale je to majitelova věc, jestli mu ten posun sedí — je to jedna podmínka v `SolitaireKind`.

**Zvažoval jsem hrany z KAMENE místo barvy** (zachovalo by to zákon „na povrchu není barva") a **zamítl**: rock je druhá půlka téhož bloku (Anvil je hned další level) a Anvil je napsaný tak, aby kámen naučil viditelně a lacino. Potkat první kámen o level dřív, zapečený do hran skleněné sochy, je přesně to učení, kterému se ten blok vyhýbá.

**⚠ Při psaní dokumentace vypadlo, že `docs/formats-and-tools.md` nese zastaralý headline: „one-shot band 6–52 % across the 44 generated levels".** Balík má 104 generovaných levelů a přeměřený pás je **3–85 %** — strop je **Kepler 85 %** (22 kurzů na 25 kotvách), za ním Sail 67, Trophy 61, Fountain 54. **S #362 to nesouvisí** (Kepler nemá sklo, takže nová cesta u něj vrací nulu) — je to číslo, které nikdo nepřeměřil, když kampaň ztrojnásobila. Opravil jsem headline a per-blokové figury pod ním jsem **označil za datované**, ne přepsal: vysvětlují pořád správně proč který blok kde leží, ale jejich absolutní čísla jsou z 44-levelového balíku. Přeměřit je celé je vlastní práce, ne přílepek k tomuhle.

**Ověřeno:** čtyři solutiony 0 chyb; `LevelGen` exit 0 a **změnil se jediný soubor levelu, `Solitaire.json`** (444 koulí beze změny, mění se jen `k`); `ScoreSim` „All levels rate the right way round" a Solitaire drží 4/4/4/3 hvězd; sag proba `--sag=Solitaire` **3 z 5** — táž hodnota, kterou #362 cituje před změnou (a proba mezi běhy kolísá o ±1, takže „nezhoršilo se" je poctivější závěr než „nezměnilo"). Fotky před/po ze hry (`level=Solitaire quality=high shot=9`).

**Ostatní čtyři skleněné levely jsem nechal být** — to je majitelovo rozhodnutí, ne opomenutí: spektákl je to, co #344 chtělo, a Diademu se kvůli tomu **nevrací** kotvy z #325 (jeho výplata 50 stojí dál).

**Dál beru #360** (tvary Pendant/Crane/Mobile/Bridge).

---

## 2026-09-05 — Claude Code (pátý zápis dne)

**#360: Pendant, Crane, Mobile a Bridge nečtou jako tvary. Přepracované jsou TŘI; Bridge zůstal nedotčený a je to výsledek měření, ne vynechaná práce.**

**Nejdřív nástroj, protože bez něj bych to ladil naslepo:** odhozený probe ve scratchpadu (v žádném solutionu) tiskne **čelní elevaci levelu v ASCII**, jeden znak na barvu, přesně z pohledu, ve kterém jsou tyhle „stroje" nakreslené. Regenerace celého balíku trvá deset vteřin, elevace vteřinu — takže se tvar dá ladit v minutách místo v desítkách minut přes hru. **Ale elevace lže o dvou věcech a obě mě chytily**, viz níž.

**Pendant — klenot musí být největší věc v obrázku.** Kámen byl **čtyři kurzy, 4-3-2-1**, tedy přesně tak široký jako kroužek nad ním a třetina výšky kresby; řetízky byly pět úrovní a krokovaly po dvou, takže největší věcí v obrázku byl **řetízek**. Nově: kámen **šest kurzů 6-5-4-3-2-1** (91 koulí místo 30), řetízky čtyři úrovně krokující po jedné, spojnice jedna úroveň místo dvou. Dvě třetiny výšky jsou teď kámen a je o polovinu širší než kroužek. **Cena je jackpot:** kroužek shodí kámen, což je 72 % levelu (dřív 44 %) — pod bránou 90 % a je to schválně: klenot, který je většina levelu, dělá jackpot, který je většina levelu. Kurzy kamene jsem **prostřídal** (dřív jedna bílá faseta ze čtyř), takže druhá velká rána — uříznout krční kurz — bere 61 % místo toho, aby ji jeden ink vzal celou. Sag **0 z 5**, stejně jako předtím.

**Crane — jeřáb se pozná podle toho, že vodorovné rameno je delší než svislá věž.** Bylo 10 sloupců ramene proti 8 kurzům věže (1,2:1) a **vzpěra stoupala stejně strmě jako věž**, takže to četlo jako oblouk s bouličkami. Nově: mřížka 15, rameno 12 sloupců proti 6 kurzům věže (2:1), věž stojí v **třetině** ramene a vzpěra padá **o sloupec na úroveň** (45°) a dosedá na konec ramene přímo nad hákem. Barevně: **jib už není stříbrno-ČERNÝ ale stříbrno-modrý** — černý segment na herní vzdálenost čte jako díra v rameni a půlka ramene v dírách je půlka ramene.

**⚠ Dvě věci mi vrátila fotka, ne elevace, a obě stojí za zapamatování:**
1. **JEDNOKURZOVÝ JIB SE PROHÝBÁ.** Zkusil jsem rameno o jednom kurzu (půlka hmotnosti, sag stejný — 2 z 5 tak i tak), v elevaci vypadalo štíhle a správně; v běžící hře se dvanáct koulí na jednom kurzu **prověsí** a jeřáb, jehož rameno se prohýbá uprostřed, přestane být jeřáb. Vráceno na dva kurzy. Elevace o tuhosti neříká nic.
2. **DELŠÍ RAMENO SI VYNUTILO KOTVY.** První verze s dvanáctisloupcovým ramenem dala **4 z 5 a jednu prohru s klidným sklem** = vada layoutu. Věž i vzpěra proto dostaly na horním kurzu **čtyřsloupcovou hlavu** (16 kotev místo 8, zátěž na kotvu 31,5 → 13,0) a mezi nimi zůstal volný sloupec, aby to byly pořád dva nezávislé závěsy. Pak ještě protizávaží o buňku mělčí a hák o úroveň výš — obojí je konstanta, kterou si **gate watch toho designu sám předepsal** („tune the counterweight's depth and never the jib length"). Konečný stav **2 z 5** proti starým **1 z 5**: nad prahem hlášení (4) to není a probe sám o sobě mezi běhy kolísá, ale je fér říct, že je to o krok horší.

**Mobile — nosník je TYČ, ne deska.** Oba nosníky byly dva kurzy vysoké a bílé, závěsy byly navy+žlutá vedle sebe: šachovnice. Nově je nosník **jeden kurz** (a to je Calder), závěsy jsou **navy+azurová** (studená dvojice čte jako drát, dělená diagonála zůstala, takže žádný ink nepřeřízne závěs) a patra jsou svisle roztažená, takže mezi hlavním nosníkem, druhým a závažími je vidět obloha. **⚠ Změna inku závěsů málem tiše zabila mechaniku:** bobík B byl schválně **slitý** s vlastním lanem přes žlutou diagonálu („jedna rána shodí bobíka"), takže když lana zežloutla na azurovou, přebarvil jsem bobíka B na azurovou taky. Kdybych to nedohledal v jeho vlastním komentáři, byl by z toho tiše mrtvý design. Sag 1 z 5 proti starým 0 z 5.

**⚠ Bridge JSEM NECHAL BÝT a obě zamítnuté varianty jsou zapsané v kódu, ne smazané:**
- **hlubší průvěs lana** ({6,7,8,9} místo {6,6,7,8}) v elevaci otevře denní světlo pod středem rozponu — a **ve hře napěchuje konce lana k pylonům**, takže horní třetina obrazu čte jako jeden oranžový pás od kraje ke kraji místo jako dvě věže s oblohou mezi nimi.
- **širší mostovka** o sloupec na stranu: region má schválně **dva volné sloupce all round** („not to be spent"), takže se ten sloupec vešel jen **dovnitř půdorysu nohou** — žádný převis, o který šlo, zato 16 koulí navíc na koncích rozponu a sag **2 z 5 → 3 z 5**, stabilně přes tři běhy. Změna, která stojí level stupeň průvěsu a oku nedá nic, není změna.
Bridge byl zároveň nejmírnější stížnost („simple", ne „primitive"), takže z něj odchází beze změny a s poznámkou proč.

**Ověřeno:** `LevelGen` exit 0 (všechny brány, mění se **tři** soubory levelů), `ScoreSim` „All levels rate the right way round" (Pendant 147/42, Crane 152/46, Mobile 148/46 — hvězdy sedí), sag proba na všech třech (0 / 2 / 1 z 5, práh hlášení je 4), čtyři solutiony 0 chyb. Fotky před/po ze hry na všech čtyřech (`level=<name> quality=high shot=9`).

**Co jsem NEUDĚLAL:** nesahal jsem na `Web` a `Pagoda`/`Cabinet` (v #360 nejsou) a nepřegeneroval jsem barvy podle #285 — topologie i inkousty jsou tam, kde je #285 nechalo, kromě dvou míst, kde barva byla ta vada: černý segment jibu a horké závěsy Mobilu.

**Všechny tři dnešní větve (#364, #362, #360) jsou na majitelovo slovo smergované do `main` napřímo přes `--no-ff`, v tomhle pořadí; issues zavřené, větve smazané lokálně i na originu.** Po mergi jsem generátor **spustil znovu na smergovaném kódu** a `git status` je čistý — #362 (Solitaire) a #360 (Coil) se v `Program.cs` potkaly bez konfliktu a reprodukují commitnuté levely bajt za bajtem; ScoreSim a čtyři solutiony po mergi znovu zelené.

**⚠ Deník kolidoval u všech tří mergů a poprvé ne sám se sebou:** druhý stroj mezitím na main přidal vlastní „druhý zápis dne" (#279, #356, #289), takže konflikt byl o **totéž číslo zápisu**, ne o konec souboru. Řeší se **ponecháním obou stran a přečíslováním** — moje tři zápisy jsou proto třetí, čtvrtý a pátý. Pro dalšího: když si dva stroje v jednom dni připisují, číslo zápisu je to, co koliduje, a je to konflikt na jistotu.

---

## 2026-09-05 — Claude Code (šestý zápis dne)

**#359: rozptyl „cluster pod čarou" na čtyřech raných levelech. Změřeno všechno, co issue chtělo — a hlavní nález je, že ty čtyři levely NEJSOU jeden jev. „Hra je o štěstí" je špatná jednotná diagnóza.**

**Čísla, o která issue žádala** (`--sag`, jeden běh na level): **Amphora 3 z 5, Saturn 2, Meerkat 2, Giraffe 1.** Tři z nich už v kódu stály a **vrátily se nezměněné**, takže jsou z opravené proby a dá se jim věřit; **Meerkat, který nikdy neběžel, čte 2** — Saturnovo číslo, a přesně sedí na majitelův popis („stalo se mi to několikrát, ale na jiných pokusech ne").

**⚠ Sag nekopíruje ani zátěž kotev, ani velikost skupiny — každý z těch levelů prohrává po svém:**

| level | sag | kotvy → po jedné ráně | zátěž kotvy | největší stojící skupina |
|---|---|---|---|---|
| Amphora | 3 z 5 | 20 → 14 | 33,8 | 64 z 502 (13 %) |
| Saturn | 2 z 5 | **9 → 4** | **64,5** | 128 z 377 (34 %) |
| Meerkat | 2 z 5 | 26 → 22 | 16,2 | **86 z 364 (24 %)** |
| Giraffe | 1 z 5 | 30 → 26 | 15,8 | 30 z 420 (7 %) |

- **Amphora a Saturn jsou o kotvách.** Saturn drží 377 koulí na **devíti** stropních buňkách a jedna rána mu nechá čtyři; Amphora je level, kvůli kterému `WorstAnchorLoad` vůbec vznikl. Lék je šířka/kotvy a **mění tvar vázy a planety** — to je majitelovo rozhodnutí a **nesáhl jsem na ně**.
- **Meerkat byla JEDNA SKUPINA a trace to řekl bez debat.** Ve všech prohraných bězích padl level **na ráně, která sebrala jeho 86kuličkovou hnědou skupinu** (run 2 a 4: shot 4, `87 matched`, čára −1,07 a −1,03); v bězích, kde tatáž skupina odešla později (shot 3 nebo 9), level **dohrál s osmi jednotkami rezervy**. To není nepředvídatelná fyzika, to je jedna rána, která bere čtvrtinu clusteru.
- **Giraffe je podlaha nástroje.** 1 z 5 bez skupiny nad 7 % a se zdravou zátěží kotev je přesně to, čemu jeho vlastní komentář říká „estimator's floor for a 14-course curtain".

**Opraven jenom Meerkat, a to blokovým dialem, který má `docs/formats-and-tools.md` popsaný jako ZADARMO: kolika inkousty je symbol nakreslený.** Hlava je nově **pískově žlutá** tam, kde byla celá figura hnědá — takže kožich je 54 koulí a hlava 32 místo jedné skupiny 86: nejlepší jedna rána **28 % → 14 %**, sag **2 z 5 → 1 z 5** (Giraffovo číslo). **Bitmapa se nehnula ani o buňku** — změnil se ink řádků 2–5. A je to i lepší obrázek: surikata **má** světlejší obličej nad tmavším kožichem, a černá maska, která podle vlastního komentáře „nese ten druh", sedí na světlé hlavě líp než na hnědé.

**Ověřeno:** `LevelGen` exit 0 (mění se jediný soubor levelu, `Meerkat.json`, 364 koulí beze změny), `ScoreSim` „All levels rate the right way round" a Meerkat drží 4/4/4/3, sag po změně 1 z 5, čtyři solutiony 0 chyb, fotka z běžící hry + elevace (hlava `y` s maskou `K`, kožich `n`, pruh na břiše `K`, ocas a nohy `n`).

**Co zůstává na majitelovi** (a je to celý zbytek #359): jestli Amphoře a Saturnu stojí za to sáhnout na kotvy. Obě čísla jsou pod prahem hlášení (4 z 5) a `docs/game-session.md` má zapsáno, že nenulový sag sám o sobě není vada — ale u těchhle dvou je to **struktura**, ne náhoda, a je to jediné, co po tomhle zápisu z issue zbývá.

---

## 2026-09-05 — Claude Code (sedmý zápis dne)

**#361: Diabolo a Shuttle jsou nejhezčí levely, které hráč zatím viděl, a nepřiměřeně lehké. Změřeno to jde říct přesně — a u Diabola to nebyl dojem, ale aritmetika.**

**Rampa se na pozici 5 OBRACELA.** „Balls a shot at par" (= koulí na skupinu) a rozpočet (= ran na skupinu) přes prvních sedm levelů:

| # | level | skupin | koulí na skupinu | rozpočet |
|---|---|---|---|---|
| 3 | Toadstool | 9 | 43,2 | 4,89 |
| 4 | Pinwheel | 8 | 51,6 | 5,50 |
| **5** | **Diabolo** | **5** | **91,0** | **6,80** |
| 6 | Shuttle | 7 | 61,4 | 5,43 |
| 7 | Amphora | 14 | 35,9 | 2,43 |

**Diabolo mělo PĚT stojících skupin na 455 koulí** — nejvolnější rozpočet ze všech sedmi, a hned za ním Amphora s 2,43. To je přesně ta „překvapivě nízká obtížnost", jen v číslech.

**Příčina u Diabola je `Band` přes tři inkousty na šesti sektorech:** sektor k a k+3 dostaly týž ink, takže každá barva byla **dvě protilehlé desky od skla ke špičce**, a navíc se obě půlky sektoru **slévaly skrz plný krk**. Oprava má dvě části a obě jsou barvy, ne tvar: **čtyři inkousty** (čtyřka nedělí šestku, takže protilehlé sektory už nesdílejí barvu) a **spodní kužel má paletu pootočenou o sektor**, takže se horní a dolní půlka sektoru přes krk nesejdou. Výsledek: **5 → 7 skupin, 91,0 → 65,0 koulí na skupinu, rozpočet 6,80 → 4,86, největší jedna rána 33 % → 25 %.** Silueta se nehnula ani o buňku — a to je právě to, co se na tom levelu majiteli líbilo; pootočení je vidět jako **zkrut přes pas** a je to podle mě lepší kresba než původní zrcadlo.

**⚠ U Shuttlu tatáž úvaha NEPLATILA a málem jsem to zapsal jako opravu, která nic neopravila.** Šest per taky sedělo na třech incích, jenže **protilehlá pera se nedotýkají**, takže to bylo šest samostatných skupin už předtím: čtvrtý ink počet skupin **nezměnil vůbec** (7 → 7). Co mění, je **magazín** — level teď losuje z pěti barev místo ze čtyř, což je skutečné ztížení, ale jiné, než jsem čekal. Zbytek stížnosti byla vůle v rozpočtu: **38 ran → 34**, tedy 5,43 → **4,86**, což je Diabolovo číslo.

**Rampa po zásahu:** 4,89 → 5,50 → **4,86** → **4,86** → 2,43. Inverze na pátém místě je pryč a oba levely teď sedí na úrovni Toadstoolu a stupňují se dolů k Amphoře.

**⚠ Poctivě k tomu, co se NEZMĚNILO: sag proba pořád oba levely „dohraje" v šesti ranách** (Diabolo 5 → 6, Shuttle 7 → 6 z 34). To je ta samá věc z jiné strany: když je level postavený z několika velkých desek, pár dobře mířených ran ho sundá kaskádou bez ohledu na rozpočet. **Jestli je má majitel chtít opravdu těžké, je to přestavba skupinové struktury** (Amphora má 14 skupin po 36 koulích) a to už je zásah do tvaru levelů, které si pochválil — tuhle hranici jsem záměrně nepřekročil a hlásím ji místo abych předstíral, že rozpočet vyřešil všechno.

**Ověřeno:** `LevelGen` exit 0 (mění se `Diabolo.json`, `Shuttle.json` a sada kvůli počtu ran), `ScoreSim` „All levels rate the right way round" — hvězdy obou beze změny (4/4/4/3, 2), jen skóre kompetentní hry kleslo (7,13 → 7,04 a 7,17 → 6,75), což je přesně to zamýšlené utažení; **sag 0 z 5 u obou** (beze změny), čtyři solutiony 0 chyb, fotky z běžící hry — Diabolo drží přesýpací siluetu a Shuttle pořád čte jako badmintonový míček s červeným korkem.

---

## 2026-09-05 — Claude Code (osmý zápis dne)

**#368 + #369 jsou jedna práce a je to ta, na kterou obě issue samy ukazují: PĚT NOVÝCH LEVELŮ SOPKY, které přivádějí bombu a zap do kampaně.** Eruption měl pět levelů proti deseti všude jinde (#369) a bomba se zapem byly postavené, odargumentované a **v žádném shipnutém levelu** (#368). Volcano je jediné místo, kde bomba nemusí nic vysvětlovat — blok už má level jménem Volley o balistických bombách. Kampaň má **110 levelů v jedenácti blocích po deseti**.

**Pořadí učení je celý návrh a je to Anvilův precedent (#324):** každá mechanika dostane nejdřív level, kde je **nepřehlédnutelná a laciná**, teprve pak level, kde je nástroj.

| # | level | co dělá | speciály | sag |
|---|---|---|---|---|
| 6 | **Vent** | bomba, učená — kužel s ústím dolů, nálože v ústí, kam se dívá dělo první | 8 bomb | 1 z 5 |
| 7 | **Sill** | bomba, použitá — studená deska, kterou nejde rychle vybarvit, otevřená čtyřmi štěrbinami | 5 bomb | 0 z 5 |
| 8 | **Fume** | zap, učený — pět trubic, každá jiná dvojice horkých inkoustů, každá končí jedním zapem | 5 zapů | 0 z 5 |
| 9 | **Caldera** | zap, použitý — prstenec na lavici, barvy dithered tak, že jedna nikde netvoří skupinu | 6 zapů | 2 z 5 |
| 10 | **Paroxysm** | obojí a POŘADÍ mezi nimi | 6 bomb + 8 zapů | 0 z 5 |

**⚠ Finále tvrdilo o vlastní geometrii něco, co nebyla pravda, a chytila to sonda, ne úvaha.** Doc říkal „šev u paty, kde jedna rána nabije jednu od každého" — jenže boky sloupce jsou od sebe **4,6 buňky**, takže žádný dopad nemohl být u obou. Přidal jsem skutečný šev (na nejnižším kurzu leží bomba a zap **vedle sebe napříč hloubkou**) a **zeptal se mřížky**: odhozená sonda hledá prázdné buňky sousedící s bombou I zapem — **Paroxysm jich má pět**, ostatní čtyři levely nula (jak mají). Tvrzení je teď změřené, ne napsané.

**⚠ Tři věci vrátily brány nebo fotka a každá zabila jinak rozumný návrh:**
1. **Caldera 5 z 5 „SAGGED WITH THE GLASS AT REST" = vada layoutu.** Podlaha byla plný disk visící za vlastní okraj — trampolína. Rezurgentní dóm uprostřed (což skutečná kaldera má) ji dostal na 4; **teprve zúžení podlahy na lavici mezi dómem a stěnou na 2**. Sto koulí nepodepřeného středu byl ten problém.
2. **Sill byl neviditelný.** Nálož v šachtě uvnitř desky sedí uprostřed hloubky a hra se hraje z podhledu, takže před ní byla deska: level četl jako holý blok bez cesty dovnitř. Štěrbina teď **prochází deskou skrz** a v její hlavě stojí **jen ta nálož** — jedna koule ve světle vlastního náboje. Fotka to potvrzuje: čtyři štěrbiny, v každé rudě pruhovaná nálož.
3. **Štěrbiny nesmí nechat proužek užší než buňka.** Při rozteči 2,5 a šířce 2,2 zbyly mezi nimi třetiny buňky → **22 koulí viselo ve vzduchu** (proužek má sloupec na jedné paritě a na druhé žádný, takže spodek nemá nad sebou co ho unese). Rozteč 2,3, štěrbina 1,2, nejužší proužek 1,1.

**⚠ A dvakrát jsem umístil speciál do CELÉHO průřezu místo do jedné buňky:** Sill vyšel na 30 bomb a Fume na 26 zapů, protože predikát se ptal „je buňka v šachtě", ne „je to hlava šachty". U Fume to navíc vyžadovalo **srovnat paritu**: střed trubice musí být na půlce na sudém kurzu a na celé na lichém, jinak na něm neleží žádná buňka a čtyři jsou od něj půl buňky daleko.

**První věc po vygenerování byla ScoreSim, protože přesně o to #368 žádalo** (lekce #173: hvězdy byly na všech levelech pozpátku a žádné jednotlivé číslo to neukázalo). **„All levels rate the right way round"** a všech pět nových sedí na 4/4/4/3 a 2 hvězdách jako zbytek balíku. Je fér říct, co to NEDOKAZUJE: cesta destrukce se do skóre promítá přes obyčejné uvolnění skupiny, takže ScoreSim ji vidí, ale žádný z těch pěti levelů nepotřebuje bombu k dohrání — kdyby někdy měl, je to první číslo k přeměření.

**Blokové zákony jsem nerozvolnil:** studená hmota dithered na třech incích, horký člen banded na dvou, každý level se stal v nějakém směru (kužel se trhá jedním, štěrbiny pochodují po větru, trubice visí ve vějíři, kaldera je proražená na jedné azimutu, sloup se naklání) a každý speciál má prázdného souseda **konstrukcí** — na nejnižším kurzu, v hlavě štěrbiny nebo na patě trubice. Žádný není na kotevním kurzu.

**Ověřeno:** `LevelGen` exit 0 (všechny brány včetně `FindStrandedSpecials` — a ta odmítla první verzi Paroxysmu se dvěma zazděnými speciály, což je přesně to, k čemu je), `ScoreSim` zelený, sag proba na všech pěti (1/0/0/2/0, práh hlášení je 4), čtyři solutiony 0 chyb, fotky všech pěti z běžící hry. **Pět nových souborů levelů jsem zastagoval hned, jak vznikly** — pravidlo tohohle repa o netrackovaných datech.

**Co zůstává otevřené:** #256 pořád čeká na zbylých šest speciálů (Acid, Frozen, Wildcard, Infectious, Gravity, Heavy) a tenhle blok ukazuje, kam by šly — ale to je práce na příště, ne přílepek k téhle.

---

## 2026-09-07 — Claude Code

**#219 + #223: hrom a rachot erupce postavené, jedním mechanismem — dluh dvou issues splacený naráz.** Větev `219-thunder`, worktree `BS3D-322`. #223 svůj zvuk odložilo s výslovnou podmínkou, že přijde spolu s hromem a bude sdílet jeden mechanismus; tohle je to přistání. Dva bakey v `ProceduralAudio` (`BakeThunder`, `BakeEruption`) vedle výstřelu ohňostroje a nová `Game/Audio/SceneEventSounds.cs`, tikaná z `BS3DGame.Update` hned za ambientem.

**Nosná úvaha, a je to celý rozdíl mezi „funguje" a „zní to rozbitě": zvuk jede po ROZVRHU světla, nikdy po jeho jasu.** Obálky obou úkazů uvnitř jedné události **schválně blikají** (blesk má zpětné výboje), takže hranový detektor na jasu by jeden úder slyšel jako čtyři. Rozvrhy se proto vytáhly do `StormStrikeSchedule` / `VolcanoBurstSchedule` — jedna funkce na jev, ze které čte i shader i zvuk, takže spolu nemůžou driftovat — a `SceneRenderer.TryGetSceneEvent` podává **index události a vteřinu, kdy její světlo začalo**. Index je i ochrana proti dvojímu spuštění přes víc snímků.

**Zpoždění je skutečné, ne autorské.** Svět je metrický z konstrukce (koule 1 jednotka, ostrov 26 v poloměru), takže je to `vzdálenost / 343` a nic se neladí rukou. Měřeno v běžící hře: bouře 45 s na front endu — **7 úderů, vzdálenosti 155–418 jednotek, zpoždění 0,45–1,22 s**, každý naplánován jednou a vysloven jednou; sopka 70 s — **4 výbuchy**, kráter na 266 jednotkách, tedy plochých **0,78 s**, velikosti 0,85–0,96.

**Hrom NEUMÍSTĚNÝ, rachot UMÍSTĚNÝ**, a obojí je geometrie té scény: úder jde uvnitř buňky decku, který arénu obklopuje a podtéká, a než se zvuk po tom decku rozleze, žádný směr v něm nezbyl; kráter naopak *někde* je a říct kde je půlka toho, k čemu rachot slouží.

**Hlasitost visí na řádku Ambience, ne Efekty** (`ProceduralAudio.WeatherGain`, píše ji tentýž `ApplyVolumes`): kdo si stáhl atmosféru, už řekl, co si o počasí myslí, a věšet hrom na efekty by ho stahovalo spolu s dělem. Autorsky **tiše** a hnaně, ne peak-normalizovaně: hrom 4,60 s / RMS **0,132**, erupce 5,00 s / RMS **0,144** — proti 0,30 výstřelu ohňostroje, protože tyhle hrají **pod** ambientním lůžkem.

**⚠ Do lůžka se to zamíchat NESMÍ, a to je celý důvod, proč vznikla vlastní třída** místo pár řádků v `ProceduralAmbience`: lůžka jsou zapečetěné 16sekundové smyčky, takže událost zapečená do lůžka se opakuje v pevném intervalu — metronom, přesně to selhání, kvůli kterému je rozvrh hashovaný.

**Sondy pryč:** bakey se dumpovaly do .wav a změřily (délka, peak, RMS, obálka po půlvteřinách), pak se sonda odstranila — postup výstřelu ohňostroje. Majiteli jsem oba .wav poslal, protože **ucho je jeho**; doladění `targetRms` je na jeho slovo a je to jednořádková změna na obou místech.

**Ověřeno:** všechny čtyři solutions build 0 chyb / 0 upozornění; `docs/scenes.md` (bouře i sopka — obě místa nesla „zvuk není postavený" jako stav) a `docs/game-feedback.md` (sekce zvuku) přepsané v témž commitu.

**⚠ Provozní nález, který nikdo nezapsal: hra nemá argument na okno.** `Program.cs` zná jen `fullscreen` (nastavuje na true) — opak neexistuje, takže když je v `Settings.json` uloženo `true`, není jak z příkazové řádky spustit hru v okně. Kvůli tomu jsem musel majitelův `%LOCALAPPDATA%\BS3D\Settings.json` zazálohovat, přepnout a vrátit byte za bytem (ověřeno SHA-256). Na stroji, který se pod zátěží tvrdě restartuje a kde majitel proto říká „radši nespouštěj fullscreen", je to skutečná díra v harnessu — `windowed` nebo `fullscreen=0` je pár řádků. **Issue jsem nezakládal**, je to na majiteli.

**Beru si #346** (přepsat skladbu „mural"). Nic dalšího.

---

## 2026-09-07 — Claude Code (druhý zápis dne)

**#346: „mural" přepsaný, a verdikt je majitelův — schváleno a na mainu.** Issue říká výslovně, že se to musí potvrdit uchem; slyšet neumím, takže jsem oba .wav (starý i nový) poslal majiteli a větev `346-mural-recompose` nechal ležet, dokud neřekne. Řekl „mergni to" a šla nahoru. Kód i doky hotové, čtyři solutiony čisté, `MusicBake` změřený.

**⚠ A tohle je postup, který se tu vyplatil dvakrát za den:** u věci, kterou neumím posoudit (zvuk, hudba), nechat hotovou práci na pushnuté větvi, poslat majiteli rendery a k tomu měřením podložený rozbor toho, CO jsem změnil a proč — ne prosbu o názor naprázdno. Verdikt pak přišel na jednu zprávu. U #219 to bylo totéž s .wav hromu a erupce.

**Pět vad, každá měla vlastní příčinu, a všechny sedí na majitelových slovech („primitivní až jako vtip, smutné, občas vyloženě falešné, pomalé, špatný rytmus"):**

1. **PRIMITIVNÍ = melodie se skládala jen z tónů akordu.** Riff má tři výšky (`MURAL_RIFF_INTERVAL` = základ–kvinta–oktáva) a *všechny* ostatní linky se vypisovaly ze čtyř tónů právě znějícího akordu, takže v celé skladbě nikdy nezazněl tón, který už pod ním nezněl — žádný průchodný, žádná zádrž, žádný citlivý tón. Horší je, že se každá melodická buňka **přehláskovala na každý nový akord**, takže se jeden tvar opakoval čtyřikrát za kolo ve čtyřech vypsáních. To není melodie, to je cvičení. Nově melodie indexují **stupnici** (`MURAL_SCALE`, G dur přes dvě oktávy od G4) v absolutní výšce a harmonie se hýbe pod nimi. Přízvučné tóny jsou pořád tóny akordu; mezi nimi jsou průchodné.
2. **SMUTNÉ = jeden akord na jednom místě.** Progrese byla I–vi–IV–V a moll seděla ve **druhém** taktu, takže každé kolo šlo do mollu jako první tah. Nově I–IV–vi–V: stejné čtyři akordy, stín přijde ve třetím taktu jako průchodná barva, ne jako odpověď na toniku.
3. **⚠ FALEŠNÉ = obyčejná chyba, jeden řádek.** Závěrečný push klouzal z `target - 2`, pevného celého tónu, ať v tónině leželo cokoli. Proti G dur to znamená B klouzající do C — a hlavně **F klouzající do G pod D akordem, jehož pad drží F#**. Sekunda proti harmonii, dvakrát na čtyři takty, celou skladbu. `MURAL_APPROACH` teď dává diatonický stupeň pod každý základní tón (u dvou ze šesti půltón, u zbytku celý tón), takže z toho je H→C a F#→G. **Obě laděné tomové nápřahy byly mimo tóninu taky** (pevný žebřík od 96 Hz, čtvrtá příčka ~138 Hz = cis) — teď se hrají na tónický kvintakord. Tom je bicí, ale je to bicí s výškou, a laděný úder pod laděným subem s ním tluče.
4. **⚠ ŠPATNÝ RYTMUS = kopák si odporoval s vlastní mřížkou skladby, v každém taktu.** Mřížka je tresillo 3+3+2 (0-3-6, 8-11-14) a kopák hrál 0 a **10**. Desítka v té mřížce vůbec není — je to „a" třetí doby z rovné osminové popové figury a padá přesně do jediné mezery, kterou basa mezi 8 a 11 nechává. Takhle zní „špatný rytmus", i když je každý part sám o sobě správně. Nově **0, 6, 11**, tři vlastní buňky basy, a pořád nikdy všechny čtyři doby.
5. **A pod tím vším nebyla melodie slyšet.** Měřeno proti ostatním pěti kusům měl mural v setu **nejméně melodické energie** — 7,1 % v pásmu 500 Hz–2 kHz a 0,5 % nad 2 kHz proti Emberovým 16,5/0,7 — protože pad i stab hrály akord na `arp - 12` (98–330 Hz) a naskládaly harmonii na basu. Teď hrají na `arp` (196–659), což uvolní 98–196 Hz basě samotné.

**Tempo 120 místo 108.** Cítěná doba je tady půltaktová houpačka, takže 108 se houpalo na 54 — pod klidovým tepem.

**Změřeno po (`MusicBake`):** 162,2 → **146,0 s**, RMS −15,2 dBFS (ostatní −14,9 až −15,1, takže na přechodu není schod), peak −1,0 bez klipu, balance 0,0, mono 0,0. Pásma **52,7 / 25,6 / 8,9 / 11,8 / 0,9 / 0,2 %** — melodické pásmo ze 7,1 na **11,8 %**, obě basová pásma prakticky beze změny (53,6/25,9 → 52,7/25,6). To je celý záměr ve dvou číslech: podlaha se nehnula, melodie vyrostla o dvě třetiny. Dva rendery = jedna SHA1, takže „authored" pořád platí. Ostatní kusy bit za bitem stejné.

**Co přežilo, protože nic z toho vadné nebylo:** tresillo, log drum a jeho **bezterciový** riff (dva basové tóny v tercii pod 100 Hz jsou bahno — táž fyzika drží Emberovy power chordy bez tercie), marimba odpovídající v buňkách, které riff nechává prázdné, break, chant a G dur s čistými kvintakordy plus jedna nóna v padu. **Basa je pořád podlaha a pořád podpis skladby — jen po ní už nikdo nechce, aby byla písnička.**

**⚠ Poučení, které si zaslouží přežít issue:** starý zápis v `docs/game-feedback.md` končil větou *„Not claimed: the tune — … the piece was arranged against the numbers rather than at a monitor."* Ta věta byla poctivá a byla to zároveň varování, které se vyplnilo: **každé číslo v tom zápisu bylo správně a skladba byla pořád pětkrát vedle**, a ucho to chytlo na jedno přehrání. Nechal jsem tu doložku stát i pro přepis.

**Nic dalšího si neberu.**

---

## 2026-09-07 — Claude Code

**Majitel rozhodl, že se Testbed rušit nebude, a vyzval mě, ať si ho přizpůsobím jako vývojový přístroj. Z toho je odpověď na #100, šest nových issues (#371–#376) a první z nich hotové.**

**Verdikt k #100 je zapsaný přímo v issue** (komentář, issue jsem nezavíral): Testbed zůstává, ale zužuje se mu role — je to **přístroj, ne druhá hra**, a nikdy nemá dostat menu, HUD ani levelový tok. Argument, který to rozhodl, není sentiment, ale to, co ty dva kandidáti na jeho nahrazení neumějí: **Game neumí stát** (front end se točí, level si přepisuje scénu, žádná libovolná póza nad libovolnou mapou — `campos`/`camtarget` jsou Testbedu a přes ně se tady rámuje každé barevné i výkonnostní rozhodnutí) a **MapEditor neumí hrát** (žádná simulace, dělo, výstřel ani kontaktní cesta). Zbývá jedna věta v CLAUDE.md — *„where every system was built and is still tuned"* mluví o minulosti; až ji někdo přepíše na to, čím Testbed je dnes, může se #100 zavřít.

**⚠ Před založením issues jsem prošel journal i zavřená issues a jedno z mého seznamu vypadlo: #334 (overcast) je vyřešené a zavřené**, `nooverc` existuje, magnituda byla přeměřena (uvnitř jednoho programu ≤1,4 dE, ne původně tvrzených +2 až +6) a navazující #367 taky. Kdo bude sahat na barvy přes Testbed, ať čte #334 a #367, ne můj původní návrh.

**Zbylých pět, v pořadí, v jakém bych je bral:** #372 (žádný běh neřekne, jaký je to build — MGCB přeskočí `.fx`, jehož `.xnb` je novější, a *nezkopíruje nic*; stálo to tři kola měření a retrakci commitnutou do pěti souborů), #373 (vstup jde do Testbedu jen zvenčí procesu — na zamčené ploše se ztracená klávesa čte jako nález), #374 (`alt=` umí jen arénu, každé jiné A/B je pořád dva procesy a dva běhy jedné nezměněné varianty daly 33,6 a 25,7 ms), #375 (kamera jde zadat, ne přečíst), #376 (nápověda NumPad2 jmenuje sedm scén ze sedmnácti — táž hniloba jako #320).

### #371: Testbed fotí sám sebe

**Mechanismus je vytažený do `Prazsky.Core.Render.ScreenshotWriter`**, takže je v jedné kopii pro Game i Testbed a `Game/Program.cs` parsuje `shot=` přes tutéž metodu — pravopis, řádek `[shot]` ani jméno souboru se nemají jak rozejít. Game se chová beze změny (ověřeno: pořád píše `bs3d-<stamp>-<scene>.png`), Testbed dostal `shot=` v témž pravopisu, vlastní `shotframe=` a klávesu **F8** (F12 je tady textový overlay a zůstává jím).

**Proč `shotframe=` a proč ho Game schválně nemá:** plán v sekundách měří **sampler**, ne efekt, jakmile se fotografovaná věc hýbe — u #175 dalo osm požadovaných časů tři snímky a dva pokusy s rozestupem půl periody se pohnuly o 4 % a pak o 2 %, **opačným směrem**. Index snímku je přesný jen tam, kde se běh dá zopakovat, a to je právě Testbed: `F5` zmrazí simulaci, `campos`/`camtarget` drží kameru. Game nemá ani jedno a index snímku by tam sliboval opakovatelnost, kterou nedokáže dodržet.

**Dvě umístění jsou nosná a jsou okomentovaná v kódu:** servis je **poslední** příkaz `Draw` — až za `base.Draw`, za řádkem `[fps]` a za `CapFrameRate` —, protože readback stáhne pipeline a `SaveAsPng` kóduje na tomhle vlákně (~0,1 s na 1600×900) a snímek s tímhle nákladem nesmí být ten, který počítá benchmark; a běží na `_pulseSeconds`, tedy na téže nástěnné hodině jako mraky a tep koulí, takže plánovaný snímek padne ve stejný okamžik, ať simulace běží, jede zpomaleně, nebo stojí.

**Ověřeno za běhu, všechny tři spouště:** jeden běh (1280×720, louka, dóm 13, `nopost`, pevná kamera) dal snímek z `shotframe=60` i z `shot=5`; **F8** přes zaostřené okno dal třetí; a Game s `shot=12` píše dál svoje. Čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, `Game/Levels` beze změny. Dokumentace srovnaná v témž commitu: `docs/testbed.md` (nová sekce), `docs/game-session.md`, `CLAUDE.md`, a hlavně **oba skilly** — `screenshot` tvrdil *„The Testbed has no such writer"* a radil přilepit `GetBackBufferData` do `Draw` a **necommitnout to**, což byla ta věta, kvůli které se ta záplata psala a zahazovala opakovaně; `verify` popisoval `CopyFromScreen` jako jedinou cestu.

**⚠ Drobná past ze skriptování (stála jeden běh):** `Add-Type -MemberDefinition … -PassThru` vrací **pole** typů, když deklarace obsahuje i `struct` — `$u::GetWindowRect(…)` pak padá na *„[System.Object[]] does not contain a method"*. Vyfiltrovat `Where-Object { $_.Name -eq 'U' }`.

---

## 2026-09-07 — Claude Code (druhý zápis dne)

**#372: každý běh teď na začátku řekne, jaký je to build — dva řádky `[build]` z `Prazsky.Core.Tools.BuildStamp`, ve všech třech exáčích včetně editoru.**

```
[build] Testbed.dll 2026-09-07 21:37:16 50145c15
[build] shaders 28 set 54512ed6, newest Glare 2026-09-07 21:38:53, oldest Sky 2026-09-02 16:17:17
```

**Proč to vzniklo, je v repu draze zaplacené:** tři kola „měření" běžela na Testbedu **bez editovaného shaderu** a vyrobila závěr, změnu podle něj a retrakci zapsanou do pěti souborů a commitnutou (`e71fdff`). Jedna z těch dvou příčin je trvalá — **MGCB přeskočí `.fx`, jehož `.xnb` je novější, a pak nezkopíruje nic**, přičemž `dotnet build` vypíše `Skipping …` a hlásí úspěch; smazat `bin` nestačí, teprve `Content\bin` vynutí překlad i kopii.

**Co je na tom nejdůležitější a co jsem měřil, ne odhadl: `set` je autorita na obsah, časy nejsou.** Změnil jsem jednu konstantu v `Glare.fx`, přestavěl a hash se hnul **54512ed6 → 8b72d45a** (`newest Glare`, pár vteřin staré); vrátil jsem konstantu, přestavěl a **vrátil se 54512ed6** — z čerstvě zapsaného `.xnb` s novým časem. Takže dva běhy se stejným `set` jedou na stejných shaderech, ať hodiny říkají cokoli. To je přesně to čtení, kvůli kterému má smysl hash psát vedle každého čísla, které se zapisuje do `docs/`.

**Tři věci, které jsem u toho zvolil vědomě:**

1. **První řádek jmenuje `.dll`, ne `.exe`.** `.exe` je apphost, kód je v managed assembly vedle něj — a otázka zní „dorazila moje C# změna do toho, co běží". Není to ani timestamp z PE hlavičky: deterministické buildy (default SDK) tam mají hash, ne datum.
2. **Nic to nesoudí.** Shader starší než exe je úplně normální (`.xnb` se mění jen když se změní zdroj), takže tu není žádný verdikt, který by mohl být špatně — řádky vezou důkaz do téhož logu jako snímek a čtení nechávají na čtenáři. Všechny výjimky uvnitř se polykají: běh nesmí spadnout kvůli diagnostice.
3. **Jeden hash přes celou sadu, ne 28 řádků s hashi.** Otázka bývá o jednom souboru a ten pojmenuje `newest`; 28 řádků v každém logu je šum.

**Počty se mezi exáči liší a je to obsahovými projekty, ne vadou:** Game 32 shaderů, Testbed 28, MapEditor 26 (ten nemá `Sky.fx` vůbec, viz CLAUDE.md).

**Zrušilo to kus rituálu ve dvou skillech**, a to je vlastní přínos vedle kódu: `screenshot` měl celou sekci *„Prove the exe is running the change before you believe a single pixel"* s ručním `Get-Item` porovnáním časů, `verify` neměl nic a `benchmark` dostal past č. 15 (měřit build, který nemáš — s pokynem citovat `set` vedle zapsaného čísla). Popis v plném znění je v `docs/formats-and-tools.md`.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, všechny tři exáče ty řádky opravdu tisknou (spuštěné a odečtené, ne odvozené z kódu). Levely nedotčené.

---

## 2026-09-07 — Claude Code (třetí zápis dne)

**#373: Testbed si mačká klávesy sám. `at=<t>:<klávesa>` stiskne akci z vlastní tabulky ovládání ve vteřině nástěnných hodin, `hold=<klávesa>:<od>:<do>` drží W/A/S/D přes interval, obojí uvnitř procesu.**

```
Testbed.exe Maps\Full.json scene=meadow at=8:F10 at=9:F12 hold=A:10:14 shot=11,13 at=16:Escape
```

Ten běh vejde do hracího režimu, schová overlay, čtyři vteřiny orbituje dělem, dvakrát se přitom vyfotí — a **sám se ukončí**. `Escape` je v tabulce jako každá jiná klávesa, takže skriptovaný běh se už nemusí zabíjet zvenčí.

**⚠ Ověřeno se ZMINIMALIZOVANÝM oknem (`IsIconic` true), a to je celý ten důkaz:** všechny čtyři položky padly ve svých časech, oba `shot=` snímky přišly se světem uvnitř (ne černé, ne lock screen) a **kamera se mezi nimi viditelně otočila** — na prvním je slunce a jiný výsek louky, na druhém ne. Na zminimalizované okno nedojde žádná syntetická klávesa a externí capture by z něj nedostal nic.

**Jména se tu nevymýšlejí.** Tap se hledá v Testbedím `ButtonAction[]`, takže `at=2:F10` dělá přesně to, co stisk F10, klávesa přidaná do tabulky je skriptovatelná v den, kdy vznikne, a pravopisy jsou tytéž, co má nápověda v overlayi a `-Keys` v `screenshot.ps1`. Co nepojmenuje žádnou akci, se **zahodí a vypíše** (`dropped, no such action`); celý plán se navíc tiskne na jeden `[script]` řádek dřív, než běh cokoli udělá. Překlep je tak vidět, místo aby chyběl stisk, který nikdo nepostrádá.

**Čtyři rozhodnutí, která stojí za zápis:**

1. **Držení je ORované se skutečnou klávesnicí, ne náhradou za ni** — ruka u stroje a skript mohou řídit tentýž běh.
2. **Držet jdou jen W/A/S/D**, protože nic jiného tenhle program jako držené nečte (orbit a chůze, jen v hracím režimu). Cokoli jiného parser odmítne, aby z toho nebyl tichý no-op — což je přesně ta třída selhání, kvůli které issue vzniklo.
3. **Tik je mimo obě brány** — mimo simulační (skript smí stisknout F5 a běžet dál proti zamrzlému světu) a mimo `IsActive` (běh, u kterého nikdo nesedí, je celý ten případ). Jede na `_pulseSeconds`, tedy na téže hodině jako `shot=`, takže se `at=` a `shot=` dají psát proti sobě.
4. **Neobsluhovaný běh** (`at=`, `hold=`, `shot=`, `shotframe=`) si nastaví `InactiveSleepTime` na nulu. Bez toho by ztráta fokusu srazila běh na ~50 FPS, na kterých MonoGame drží okno na pozadí — a z frame rate vychází jak indexy pro `shotframe=`, tak náběh, který `hold=` měří.

**Dvě držení přes týž interval jsou přesně současná** (jedny hodiny, čtené po snímcích), zatímco externí cesta uměla jen poslat oba downy před sleepem a doufat. Pro diagonální chůzi, na které se pozná rozklad pohybu všesměrového kola, to je rozdíl mezi měřením a přibližně.

**⚠ Co timeline neumí a je to napsané i ve skillu:** `at=…:F2` otevře modální dialog a běh tam stojí (Win32 okno, ne herní stav), a **myš skriptovatelná není** — míření a přesná mušta (RMB) pořád potřebují externí cestu.

**Drobnost, kterou jsem si sám způsobil a stála jeden běh:** logoval jsem časy v kultuře stroje, takže z toho lezlo `[script] 3,00 tap F10` — číslo, které zkopírované zpátky do `at=` neparsuje. Řádky jsou teď invariantní, stejně jako parser.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0. Dokumentace v témž commitu: `docs/testbed.md` (nová sekce), CLAUDE.md (řádek tabulky), `verify` a `screenshot` skilly — v `screenshot` je `-Hold` sekce nově uvozená tím, že `hold=` je lepší cesta, a zůstává tam, co pořád platí (náběh chůze, editor, myš).

---

## 2026-09-07 — Claude Code (čtvrtý zápis dne)

**#374: `alt=` už neumí jen arénu. Varianta je teď malá příkazová řádka — čárkou oddělený seznam týchž `<páka>=<hodnota>` pinů, co berou samotné argumenty, takže žádný druhý slovník neexistuje.**

```
alt=scene=meadow;scene=savanna;scene=forest
alt=ssaa=1;ssaa=2;ssaa=4
alt=arena=all,capprobe=0;arena=all,capprobe=6
alt=all;all,-cap;none            (stará aréní podoba, čte se dál)
```

Starou podobu jsem nechal číst schválně: v ní jsou zapsané **všechny sweepy #151 v tomhle journalu i v `benchmark` skillu**, a kdo reprodukuje zaznamenané měření, nemá si ho překládat. Dvojznačnost to nestojí nic — obecný pin vždy nese `=`, seznam členů arény ho nést nemůže.

**Co se smí střídat, rozhoduje hysterezie a nic jiného.** Přepnutí nesmí nic nechat za sebou, jinak okno po něm měří přechod, ne variantu. Berou: `arena`, `capprobe`, `scene`, `sky`, `balls`, `ssaa`, `msaa`, `rscale`, `detail`, `exposure`, `nopost`. **`nooverc` je odmítnutý** — overcast lerp si nese svou polohu přes přepnutí, takže varianta, která ho vypne, by se celým dalším oknem teprve sunula. Scéna proto při střídání **snapuje počasí** místo fadování (`SetScene(…, immediately: true)`); to je jediný rozdíl proti stisku NumPad2. Běh vypíše plán na `[alt]` řádek a **pojmenuje, co odmítl** — pin zahozený mlčky by ze sweepu udělal dvě čtení téhož buildu, což se čte jako „ta změna nic nestojí".

**`capprobe=` a `arena=` ztratily „#151 PROBE - TEMPORARY", které nesly od svého vzniku: graduovaly** jako dvě páky obecného mechanismu. Izolovat průchod uvnitř jednoho procesu není lešení k jednomu issue — je to to, čím se #151 vůbec dalo odpovědět na stroji, jehož běhy se nedají srovnávat.

**Milisekundy jsou na `[fps]` řádku obou exáčů** (`[fps] 148,9 (6,72 ms) — …`) a `benchmark.ps1` je bere odtamtud místo přepočtu průměrné frekvence: `1000/průměr(fps)` **není** průměr(ms), a do docs se cituje ms.

**⚠ Naměřeno a je v tom lekce o přístroji, ne o kódu** (referenční desktop 6900 XT, louka, dóm 13, `nopost`, pevná kamera, 1600×900, devět cyklů, mediány): pod `fpscap=400` čte `alt=ssaa=1;ssaa=2;ssaa=4` **ssaa 4 = 6,69 ms, zatímco ssaa 1 i ssaa 2 sedí na stropě 2,52 ms**. Pod `fpscap=150` čtou **všechny tři strop** a sweep neřekne nic. Strop se tedy musí dát **pod** frekvenci, kterou měříš, jinak přístroj měří sám sebe — a capnuté čtení je „levnější než tohle", nikdy cena.

**⚠ Jednu vadu našel až běh, ne překlad:** první varianta se seedovala uvnitř `BuildCity`, kde ji #151 mít mohlo (byly to dvě přiřazení na ostrově). Obecný pin ale sahá na pipeline, scene renderer i ball set a `BuildCity` běží dřív, než dva z těch tří existují — první `alt=ssaa=1;ssaa=2` proto spadl `NullReferenceException` z `LoadContent`. Seeduje se teď **na konci `LoadContent`**, nad hotovým startovním stavem. To je i sémanticky správně: varianta se aplikuje přes to, co ostatní argumenty postavily.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0; aréní podoba střídá `arena All`/`None` jako dřív; běh s `nooverc=1,bogus=3` vypsal obě odmítnutí; `benchmark.ps1` nový řádek parsuje (dojel a vypsal `MsPerFrame 6,67`). Sweepy jsem držel pod `fpscap` a v okně — majitel dal dnes svolení riskovat zátěž, ale desktop se pod ní historicky tvrdě resetuje (#250).

---

## 2026-09-07 — Claude Code (pátý zápis dne)

**#375 a #376, obojí drobné, obojí na vlastní větvi. Tím je série #371–#376 celá zavřená a Testbed je hotový přístroj: běh bez člověka u klávesnice, který sám řekne, jaký je to build, sám se odřídí, sám se vyfotí, sám změří varianty a sám skončí.**

### #375 — kamera se dá přečíst, ne jen zadat

**`C`** (nebo `at=<t>:C` ze skriptu, což je hned první užitečné použití timeline z #373) vypíše živou kameru v pravopisu, který bere příkazová řádka:

```
[campin] campos=0.00,-4.00,30.00 camtarget=0.00,-8.00,0.00 fov=72
```

**Nejcennější je, že čte ŽIVOU kameru, takže odpoví i v hracím režimu** — tedy pózu, kterou žádný argument neumí vyrobit, protože ji `GameCameraFit` počítá z mapy. `[camera]` tiskne figury toho fitu (odstup, výška záměru, orbit); tohle tiskne to, co jde vložit zpátky. **Ověřeno oběma směry:** ve free módu vypíše přesně to, co se předalo; po `F10` vypsalo `campos=0.00,-7.40,36.20 camtarget=0.00,1.65,0.00`, což sedí na `[camera] … camera 36,2 out, aim Y 1,6`; a běh spuštěný s těmi čísly je vypsal beze změny.

`fov=` je na řádku jen ve free módu — jen tam ten argument dosáhne. Hrací režim má vlastní `GAME_FOV` a tisknout ho by nabízelo pin, který nedrží.

### #376 — nápověda kláves, která nemůže zestárnout

Hint u NumPad2 zněl `Switch scene (city/sea/savanna/desert/mountain/meadow/neon)` a **jmenoval sedm scén ze sedmnácti** ode dne, kdy přibyla osmá. Je to #320 v druhém exáči a platí jeho pravidlo: **jmenovat osu, ne členy**. Hinty se teď staví (`Cycle sky dome (1-20)`, `Cycle scene (7 of 17; scene= reaches the rest)`) z `SKY_DOME_COUNT` a `Enum.GetValues<SceneKind>().Length`, takže se hýbou samy.

**Druhá půlka pravidla je živá hodnota** — overlay má nově řádek `Scene: Space   Dome: 1   Balls: beach` nad počty koulí, čtený z běžícího stavu, takže není druhou kopií ničeho. Kvůli tomu musel dirty flag overlaye přestat být jen o koulích: `_ballCountsDirty`/`InvalidateBallCounts`/`RefreshBallCounts` → `_overlayDirty`/`InvalidateOverlay`/`RefreshOverlayText`, a značí ho i změna scény, dómu a materiálu.

**⚠ Drobnost při ověřování, která se čte jako nález a není jím:** první snímek overlaye jsem vzal nad neonovým městem a řádek `L Cycle ball material` na něm **nemá vidět klávesu** — písmeno zaniklo v jasné fasádě za ním. Přefotil jsem to nad vesmírem (tmavé pozadí) a je tam. Overlay se prostě nedá číst nad každou scénou; kdo fotí nápovědu, ať volí tmavou.

**Ověřeno:** čtyři solutiony 0 chyb, ScoreSim 0, obojí nafoceno vlastním writerem Testbedu přes `shot=` a odřízeno `at=…:Escape` — tedy celý řetěz #371–#375 použitý na ověření #376.

---

## 2026-09-08 — Claude Code

**#330 hotové a na mainu. Wildcard je jediný druh koule, který hráč STŘÍLÍ — a to je celý jeho návrh: nesahá na formát mapy ani na flood fill, sahá na magazín, ústí a HUD. Nová `BallKind.Wildcard`, `WildcardCycle`, `BallsMap.TryChooseWildcardColour`, klíč `wildcardEvery` v setu a testovací argument `wildcard=<n>`.** Větev `330-wildcard`, merge `--no-ff`.

**Nejdůležitější rozhodnutí, které z kódu není vidět: `Magazine` jsem NECHAL BEZ ZMĚNY.** Druh slotu drží Game (`_magazineKind`) a veze se přes tytéž dva háky, které už vozí transmute (`slotCarried`/`slotLoaded`). Sedí to na to, co má `Magazine` napsané v hlavičce — fronta a její posun jsou její, *co* se nabíjí je pravidlo o levelu — a znamená to, že Testbed o wildcardu neví vůbec (#100: přístroj, ne druhá hra). Kadence se proto rozhoduje v okamžiku rozdání, ne při výstřelu, a počítají se **rozdané koule, ne výstřely**: level rozdá plný magazín dřív, než padne první rána, takže při počítání ran by první wildcard přišel o celý zásobník později, než pravidlo slibuje.

**Cyklus je HRY, ne koule, a to je půlka issue.** Wildcard je vidět na pěti místech naráz (fronta v hlavni, rána v ústí, její halo, duch dopadu, koule v letu). Per-ball fáze by je nechala cyklovat rozházeně, což je #175 (dva testeři přečetli barvu ze špatné koule) s barvami v pohybu. Jeden `WildcardCycle` na session, jedna metoda pro všechny tinty (`LoadedColour`) a přechod nastavený na render setu jednou za snímek vedle stylu koulí.

### ⚠ Tři pasti, každá stála jeden běh

1. **Vzorkování si změřilo samo sebe.** První důkaz cyklování jsem bral ze snímků po `shot=6,9,12` a všechny tři vyšly **bajt v bajt stejně** — vypadalo to jako mrtvý cyklus. Jenže `CROSSING_SECONDS` je 0,5 s a tři sekundy jsou přesně šest přechodů: perioda a rozestup snímků se potkaly. Je to táž třída chyby, kterou má #371 zapsanou u `shotframe=` („plán v sekundách měří sampler"), jen z druhé strany — tady stál svět a hýbalo se to, co jsem fotil. **Kdo fotí cyklující věc, ať volí rozestup, který NENÍ násobkem periody**: `shot=6,6.2,6.4` ukázalo červená → zelená → modrá, a hlavně ukázalo, že se hýbe **jen slot 2** (třetí rozdaná koule při `wildcard=3`) a čtyři ostatní stojí. To je ten důkaz, ne ta pěkná fotka.

2. **Intro kamera sežrala celý běh a neřekla ani slovo.** Rig, který po 9 s pošle Space, střílí do `[intro] … 9.5s` — a `Shoot` se během převzetí kamery odmítá, takže v logu **nebyl jediný `[shot]` řádek** a vypadalo to na vadu v dopadu. Čekat 13 s. Pro příště obecně: než se u Game prohlásí „nic nepřistálo", přečti si `[intro]` řádek toho běhu.

3. **Sousedů je dvanáct, ne osm.** Kandidátní pole jsem nadimenzoval podle „čtyři na úrovni a čtyři na sousední" a to je špatně o celou jednu úroveň (4 + 4 nahoře + 4 dole). Chytl to až vlastní průchod diffem, ne kompilátor a ne hra — devět různých barev kolem jedné buňky je vzácné, ale `Span` by na tom dopadu spadl. Číslo je teď pojmenované (`MAX_TOUCHING_CELLS`) s poznámkou, že se hádá špatně, a **je změřené**: sonda staví buňku s dvanácti různobarevnými sousedy a projde.

**Ještě jedna past, kterou jsem nezaplatil, protože ji chytla úvaha u kódu, a proto je zapsaná i tam:** duch dopadu se NESMÍ posílat jako kind. Přechod zapisuje `Dissolve`, a duch si ten kanál už bere na vlastní blikání (`PREVIEW_BLINK_DEPTH`) — routovaný duch by byl plný a nehybný, tedy slib na buňku, kterou trefí 3 z 10 ran.

### Jak jsem ověřoval, když se hra nedá skriptovat

Pravidlo dopadu bez skriptovatelné myši (#379, dnes založeno) nemá jak nastavit remízu na objednávku. Rozdělil jsem to:

- **Co jde vystřelit, je vystřelené**, zvenčí posílanou mezerou do zaostřeného okna, a čte se to **z logu, ne z fotky**: `[shot] wildcard landed as Type2, group of 29`, `group of 2` (zůstala viset, pod `MINIMUM_CLUSTER_SIZE`) a `beside nothing matchable, kept Type1`. Všechny tři větve pravidla viděné za běhu. Při `wildcard=1` (celý magazín wildcardy) padl level One čtyřmi ranami (90+120+90+16 z 385) a nic po cestě nepředpokládalo pevnou barvu — proběhl i drop cinematic a transmute.
- **Co vystřelit nejde, prošlo jednorázovou sondou** proti knihovně ve scratchpadu (repo nemá testovací projekty): největší skupina, remíza → nejvíc dotyků, dokonalá remíza → nižší `BallType`, nezávislost na pořadí, kámen a bomba nejsou kandidáti, prázdné okolí, `PutBallAt` zamkne wildcard, `Next()` ho přeskočí, dvanáct sousedů. Osm kontrol, všechny PASS. Sonda je **zahozená**, v repu není — je to táž věc, co udělalo #356 se sondou v `ClusterCollector`.
- **Shoda ústí, fronty a HUDu se ověřovala v HŘE s doopravdy drženým RMB**, jak káže pravidlo. Při celém magazínu wildcardů je všech pět disků v každém snímku **bajt v bajt stejných** (103/140/207, pak 207/45/45, pak 54/207/54) a mění se společně: jedny hodiny přečtené pětkrát, ne pět hodin, které se náhodou shodly.

### Co zůstává na majiteli

**Žádný shipnutý level wildcard nerozdává**, schválně a na precedentu #326/#327 — mechanika je postavená, kapitola, která ji používá, je vlastní práce (#368 je, jak to vypadalo u bomby a zapu). Proto ten `wildcard=<n>`: mapou se to obejít nedá, je to koule děla. **A z toho plyne poctivá mez ověření:** `ScoreSim` hraje shipnuté levely, žádný z nich `wildcardEvery` nenese, takže jeho verdikt platí beze změny — ale konfigurace S wildcardy tím ohodnocená není a být nemůže, dokud si o ni nějaký level neřekne. Skórování jsem záměrně nechal na pokoji: dokončená skupina přijde do `Released.Matched` a boduje jako každá jiná, žádný člen navíc.

**Dnes jsem taky založil tři issues** (majitel si vybral „založ chybějící a pak vezmi 330"): **#377** (gamepadem se dělo neotočí ani nerozejde — `Orbit`/`Advance` visí jen na klávesnici, levá páčka je v hraní volná), **#378** (rumble na dvou tělových motorkách; #188 už doměřilo, že fungují a trigger parametry se tiše zahazují) a **#379** (timeline Testbedu nedosáhne na myš — míření a držené RMB jsou jediná osa, kterou neobsluhovaný běh neumí, a externí cesta už dvakrát vyrobila falešné „ověřeno").

**⚠ Drobnost do hlavičky tohohle souboru, kterou NEOPRAVUJI, protože pravidlo zní neupravovat tu nic než vlastní zápis:** stojí v ní *„squash-merge s `(#NNN)` v subjektu"*, zatímco `CLAUDE.md` od 2026-08-12 výslovně říká `git merge --no-ff` a že tak vypadá každý merge v logu. Řídil jsem se CLAUDE.md. Je to na majitele, jestli tu větu srovnat.

**Ověřeno:** čtyři solutiony 0 chyb / 0 upozornění, ScoreSim 0, LevelGen 0, `Game/Levels` beze změny (nový klíč nepřepsal ani jeden shipnutý soubor, což je celý smysl toho, že je volitelný).

**⚠ Čtvrtá past, chycená až vlastním průchodem diffem po commitu, a je to past na celý repo:** `BallsMap.cs` je v repu uložený s **CRLF**, zatímco okolní soubory mají LF a repo nemá `.gitattributes`; `core.autocrlf` je na tomhle stroji `true`. Můj zásah ten soubor převedl na LF a commit tím **přepsal celých 1633 řádků místo mých 153** — v `git diff` v pracovním stromě to vidět NENÍ (ten normalizuje za běhu a hlásil poslušně 145), objeví se to teprve při porovnání dvou stromů: `git diff --stat <předchozí main> HEAD`. Merge jsem proto odtočil (`update-ref` + `git restore`, nikoli `reset --hard`, který je tu zakázaný), soubor vrátil na CRLF a zapsal ho přes `git -c core.autocrlf=false add`, protože jinak ho `add` znormalizuje zpátky na LF. Diff je teď 153 řádků, 0 smazaných.

Dvě věci k tomu, obě na majitele: **(1)** ten soubor je v repu jediný svého druhu z těch, co jsem kontroloval — anomálie, ne konvence; **(2)** repo nemá `.gitattributes`, takže tohle čeká na každého, kdo ten soubor upraví nástrojem píšícím LF. Jeden řádek `* text=auto` by to zavřel, ale je to rozhodnutí na celý repo a jedním commitem by přeuložil kdeco, takže jsem na to nesáhl. **Pro příště, a je to levné:** po commitu, který sahá na starý soubor, se vyplatí `git diff --stat` proti předchozímu mainu — ne jen ten, co ukazuje pracovní strom.

---

## 2026-09-08 — Claude Code (druhý zápis dne)

**#379 hotové a na mainu: timeline Testbedu dosáhla na myš. `aim=<t>:<elevace>:<traverz>` postaví hlaveň do zadané polohy, `rmb=<od>:<do>` drží přesnou mušku, `C` obojí přečte zpátky (`[aimpin]`), `at=…:F2` je odmítnuté a pojmenované.** Větev `379-timeline-mouse`, merge `--no-ff`. Tím je uzavřená poslední vstupní plocha, kterou neobsluhovaný běh neuměl — a je to zrovna ta, na které se soudí dělo.

**Vzniklo to z včerejší práce, ne z plánu:** u #330 jsem musel do hry střílet externím rigem (SendKeys do zaostřeného okna) a **mířit nešlo vůbec**. Přesně ta cesta, o které má repo dvakrát zapsané, že vyrobila falešné „ověřeno" (#321 a nevyfocené držené W: rig musí kliknout do okna, hra si vezme kurzor, a vrátí se snímek jiné pózy, než se žádala).

### ⚠ Jediný skutečný nález, a je to past, která by prošla review i kompilátorem

`adsHeld` se v `Testbed.Input.cs` počítá s bránou `IsActive`. Kdybych skriptovaný náklon **ORoval dovnitř** té podmínky:

```csharp
bool adsHeld = IsActive && ... && (PreciseAim.ButtonHeld(mouse, pad) || _script.IsPreciseAimHeld());
```

…zkompiluje se to, přečte se to naprosto přirozeně a **na zminimalizovaném okně to nedělá nic** — tedy na tom jediném běhu, kvůli kterému celá věc existuje. Brána `IsActive` je tam pro **zařízení**: XInput hlásí držený trigger i nezaostřenému oknu, takže alt-tabnutý běh nesmí zůstat nakloněný. Skript ale není zatoulané zařízení, je to běh řídící sám sebe — týž argument, kterým #373 dalo tik mimo obě brány. Musí jít **vedle** toho testu:

```csharp
bool adsHeld = !_freeModeAnimStarted && _map != null
    && ((IsActive && PreciseAim.ButtonHeld(mouse, pad)) || _script != null && _script.IsPreciseAimHeld());
```

Je to okomentované na místě i v `docs/testbed.md`, protože příští člověk, který bude tu podmínku „uklízet", ji zjednoduší zpátky.

### Rozhodnutí, která stojí za zápis

1. **`aim=` NASTAVUJE pózu, nesyntetizuje pohyb myši.** Míření je rychlost integrovaná z delt proti překreslenému kurzoru, takže cokoli deltového by bylo stejně neopakovatelné jako rig, který to nahrazuje. Nový `Cannon.AimTo` je ocas existujícího `AimAt` bez jeho world-space hlavy.
2. **Úhly jsou stupně všude, kde na ně sahá člověk** (argument, plán, log, odečet) a na radiány se převádějí na jednom místě — v delegátu předaném do `InputScript`. Dělo se kvůli diagnostice druhou jednotku učit nebude.
3. **Traverz je nula tam, kde hlaveň míří na střed pole**, ne na světovou osu. Znamená to totéž po orbitu i po chůzi, kde by světový azimut neznamenal nic.
4. **Klamp se hlásí, nemlčí.** `aim=9:95:70` vypíše `aim clamped to 80.2/45.0 deg` (což jsou `MaxElevation` a `MaxTraverse`), protože oříznutá póza je pin, který nereprodukuje framing, ze kterého se opsal — a zjistit to z fotky je přesně ten okruh, kvůli kterému tohle vzniklo.
5. **Náklon vyžádaný mimo herní režim se pojmenuje.** `rmb=` bez `at=<t>:F10` by byl tichý no-op a snímek by vypadal jako rozbitá funkce místo chybějícího stisku.
6. **F2 se odmítá dřív, než se sáhne do tabulky ovládání**, aby zněl užitečný důvod: „otevírá modální dialog", ne „taková akce není". F2 je platná akce; problém je, že ji skript neumí zavřít.

### Ověřeno

Všechno na **zminimalizovaném okně** (`IsIconic` true) — tam nedojde žádná syntetická klávesa ani externí capture:

- `aim=6:30:20` a hned `at=6.5:C` vrátilo `[aimpin] elevation 30.0 deg, traverse 20.0 deg -> aim=<t>:30.0:20.0`, tedy **přesný round-trip**.
- Tři snímky vlastním writerem kolem `rmb=8:14`: v 7 s přehled, v 10 s **nakloněná čočka s křížem a velkou ránou v ústí**, v 16 s zase přehled s mířením tam, kde bylo postavené.
- `at=5:F2` → `dropped, opens a modal dialog a script cannot dismiss: F2`; `rmb=3:5` před F10 → řádek o chybějícím herním režimu; `aim=9:95:70` → klamp na 80.2/45.0.
- Čtyři solutiony 0 chyb, ScoreSim 0, LevelGen 0.

**Dvě drobnosti, obě moje a obě levné:** `Console.WriteLine(CultureInfo.InvariantCulture, $"…")` **takové přetížení nemá** (repo používá `string.Create(CultureInfo.InvariantCulture, …)`, jak to dělá `Vec` v `LogCameraPin`) — tři chyby na jeden zásah; a `Testbed.Input.cs` neměl `using System;`, takže první běh s varováním o herním režimu jel na starém binárce a ten řádek v logu chyběl. Obojí chytil build, ne fotka.

**Co zůstává a je to majitelovo rozhodnutí, ne opomenutí:** **Game timeline nemá** — bere `play`, `level=`, `result`, `shot=` a nic víc, schválně (argument #373 o opakovatelnosti). Pravidlo „vizuály kolem míření se ověřují ve hře s drženým RMB" tedy pořád nemá přístroj a externí cesta u něj zůstává. Issue to říká a nechal jsem to na majiteli, protože Testbed umí zastavit kameru a Game ne.

**⚠ Mimochodem, cizí nález při plném rebuildu:** `Tools/LevelGen/Program.cs:16788` hlásí **CA2014 — `stackalloc` uvnitř smyčky** (`Span<int> seen = stackalloc int[12]`, z #368). Je to varování, ne chyba, a není moje; ale `stackalloc` ve smyčce je přesně ta věc, která se projeví až na velkém vstupu. Nesahal jsem na to, patří to k LevelGenu.

---

## 2026-09-08 — Claude Code (třetí zápis dne)

**#328 (kyselina) hotové a na mainu. Pátý druh koule: spustí se ranou vedle sebe jako bomba a zap, a žere DOLŮ — vyvrtá šachtu shlukem, dokud nenarazí na mezeru.** Větev `328-acid`, merge `--no-ff`. Nová `BallKind.Acid`, `BallsMap.CollectAcidShaft`, `BallsConstraintsBuilder.DissolveAcids`, technika `InstancedModelAcid`, osmý region kbelíků, `Testbed\Maps\Acid.json` — a `SagProbe`, který se to musel naučit taky.

### ⚠ Dva geometrické nálezy, oba změřené, a oba jdou PROTI zadání

Issue má celou sekci o tom, že „dolů" v týhle mřížce není sloupec, a nabízí dvě cesty. **Obojí je vedle:**

1. **Varování issue je špatně.** Tvrdí, že procházka `level--` při pevném `(x,z)` „vrtá diagonální šachtu nakloněnou jedním směrem", a označuje to za nejpravděpodobnější způsob, jak tohle udělat blbě. Jenže **posun parity se STŘÍDÁ**: pevný indexový sloupec sedí 0,707, 0,000, 0,707, 0,000 … od osy. Houpe se o půl buňky a odchylka je **omezená** — změřeno přes třicet úrovní, nejhorší 0,707 —, kdežto naklonění by rostlo bez omezení.
2. **Doporučení issue je naopak opravdu špatně.** „Vrhni svislou přímku a vezmi každou buňku do půl koule od ní" v tomhle balení nefunguje: **všechny čtyři** buňky o úroveň níž sedí 0,707 od osy a buňka o dvě úrovně níž 0,000 — svislá přímka středem koule prochází **mezi** čtyřmi koulemi pod ní. Válec o poloměru půl koule tedy bere každou DRUHOU úroveň a nechá tečkovanou díru s koulemi visícími uvnitř; válec dost široký na 0,707 bere všechny čtyři naráz, což je tvar bomby.

Šachta se proto **prochází po buňkách**: každý krok bere obsazenou buňku níž, nejbližší **ose té kyseliny** (ne předchozí buňky — to je, co dělá procházku samoopravnou), s mezí `ACID_SHAFT_DRIFT` 0,75, která brání tomu, aby díra v shluku z procházky udělala tu nakloněnou šachtu.

**Pravidlo zastavení pak vypadne z geometrie, místo aby se volilo:** dolů ze SUDÉ úrovně existuje uvnitř meze jediná buňka (ta na ose), takže díra tam šachtu ukončí i když sloupec pod ní pokračuje; dolů z LICHÉ jsou všechny čtyři na 0,707, takže jedna chybějící koule se obejde o půl buňky a šachta jede dál. To je poctivé čtení díry v tomhle balení: vrták se zastaví o podlahu, ne o jednu chybějící kouli vedle svého okraje.

### Vzhled: první figura koule v týhle hře kreslená ve WORLD space

Kyselina je jediný speciál s **osou**, takže vzhled musí říct „dolů" dřív, než se na ni vystřelí — a figura, která by se otáčela s tělem, neříká nic. Na kouli je world normála zároveň radiála, takže `AcidPS` čte `-n.y` a stružky visí správně, ať těleso leží jakkoli. Devět pruhů s per-pruhovou délkou stéká do kaluže na spodku; kaluž se **neband-limituje** (to je ta část, co zbyde na hráčskou vzdálenost — koule se svítícím spodkem, což je pořád ten směrový signál). Reliéf je **kladný**, kde má zap záporný: kapka leží NA skořápce, oblouk je světlo vyříznuté DO ní.

### Ověření, a je v něm jedna poctivá mezera

- **Procházka šachty: 11 kontrol jednorázovou sondou** (plný sloupec, nic pod tím, díra na osové i čtyřcestné úrovni, odmítnutí sousedního sloupce o celou buňku, žere kámen/bombu/sklo, 30 úrovní hluboko). Sonda nepotřebuje fyziku — proto ta procházka sedí na `BallsMap` a ne u odebírání.
- **Odebrání a pád: 9 kontrol ve SKUTEČNÉ Bepu simulaci** (headless, `PhysicsWorld` + `BuildBallsStructure`): kyselina nad pětikoulovým sloupcem s příčkou u paty **zničila 6 a osiřely 3**, mapa i fyzikální pole se vyprázdnily současně, deska nad tím zůstala netknutá (sloupec, ne okolí) a simulace po tom odkrokovala 60 snímků bez pádu.
- **Vzhled nafocen** vlastním writerem Testbedu na vesmíru s `nopost`, vedle obyčejné zelené koule — jsou nezaměnitelné.
- **⚠ Co se mi NEPODAŘILO: nastražit v Testbedu ránu, která dopadne vedle kyseliny.** Přes tři přestavby testovací mapy a asi čtyřicet mířených ran. Důvod je poučný a je zapsaný i v `docs/game-session.md`: **kyselina potřebuje koule POD sebou a volnou buňku VEDLE sebe** — kyselina uvnitř plného bloku je kyselina, ke které se rána nikdy nedostane. Mapa je proto blok se **schodem** a kyseliny stojí v jeho stupnici. Spouštěcí cesta samotná je dvouřádkový přídavek do procházky, kterou bomba a zap už používají (`CollectArmedSpecials`, jeden walk pro všechny tři) — ověřeno čtením a tím, že řetězec za ní je proměřený výše, ne výstřelem. Kdo na to sáhne dál, ať začne odtud.

**⚠ A jedna past, která mě stála hodinu a týká se každého, kdo pouští Testbed z `bin`:** `Testbed.exe Maps\Full.json` **NENAČTE nic** — v output složce žádné `Maps\` není (csproj je nekopíruje) a program tiše spustí vestavěnou mapu. Pozná se to jedině podle `[camera] Field …`, které pak hlásí rozměr vestavěné mapy. CLAUDE.md má v příkladu absolutní cestu právě proto; **oba skilly (`verify`, `screenshot`) mají ale relativní** a jsou tím zavádějící. Nesahal jsem na ně v téhle větvi, ale stojí za jeden `sed`.

**Užitečná odbočka:** `aimcheck` je jediný nástroj, který řekne skutečné číslo — čepy děla jsou na **Y = −6,3**, ne u nuly, takže elevace počítaná „od země" je o dvacet stupňů vedle. Kdo bude mířit `aim=` na konkrétní buňku, ať si nejdřív pustí `aimcheck` a odečte z něj rozsah.

**`SagProbe` se to musel naučit v téže změně** a je to jeho vlastní zapsané pravidlo: přistává koule rovnou do mřížky, takže každý krok dopadu, který žije v handleru, se tam musí zopakovat, jinak měří jinou hru. Šachta je navíc ta hmotová změna, kterou nejvíc chce vidět — bere nosný sloupec uprostřed levelu.

**Ověřeno:** čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, `Game/Levels` beze změny (žádný shipnutý level kyselinu nenese, precedent #326/#327).

---

## 2026-09-09 — Claude Code

**#329 (zmrzlá koule) hotové a na mainu. Šestý druh: barevná koule v kostce ledu, roztaje vyčištěním skupiny VEDLE ní.** Větev `329-frozen`, merge `--no-ff`. Nová `BallKind.Frozen`, `BallsMap.ThawFrozenBesideGroup`, technika `InstancedModelFrozen` s vlastním vertex shaderem, devátý region kbelíků (a první, který je celá **barevná rovina**), `PlayIceBreak`, brána v LevelGenu a `Testbed\Maps\Frozen.json`.

### Rozhodnutí, které stojí za zápis nad rámec issue: pravidlo NEPATŘÍ do volajícího

Všech pět speciálů přede mnou sbírá a odpaluje **volající** — handler nasbírá bomby, zapy a kyseliny kolem `ReleaseSameTypeCluster`. Cena toho tvaru je v tomhle souboru zapsaná několikrát: `SagProbe` se musel naučit sklo, pak bomby, pak zapy, pak kyseliny, jedno po druhém, a Mirage se pět skleněných levelů změřilo špatně, protože to první ještě neuměl.

Spouštěč ledu je ale **odchod skupiny**, ne dopad. A skupina odchází v celé hře na **jednom** místě. Tak jsem pravidlo napsal tam — a Testbed, Game i sonda tají, aniž by do nich přibyl jediný řádek. Zapsal jsem to i do `docs/formats-and-tools.md` jako tvar, po kterém má sáhnout každý další druh, kterému to spouštěč dovolí.

### Vzhled: kostka řezaná ve VERTEX shaderu, ne druhá mesh

Issue doporučuje „geometrický overlay" — druhou mesh kolem koule — a jmenuje kolizi s `BallStyle.Ice` (zmrzlá koule vs. celoledová koule) jako důvod, proč to nesmí být jen stínování. **Ta kolize je skutečná a řeším ji tvarem; druhá mesh k tomu ale není potřeba.** Kámen (#340) už dokazuje, že se v tomhle projektu dá vyřezat nekulová silueta ve vertex shaderu z téže koule. Superelipsoid `(|x|^p+|y|^p+|z|^p)^(-1/p)`, znormovaný svou hodnotou v **rohu**, dá zaoblenou kostku, jejíž rohy leží na kouli a všechno ostatní uvnitř — dovnitř, takže kreslený blok nikdy neopustí buňku. Mřížka má mezi sousedy přesně 1,0 ve všech směrech, takže rohy na kouli o r=0,5 je dotyk a ne průnik.

**Normála vyjde přesně** a je to hezčí než u kamene: v `n = d − tangenciála(∇r)/r` se pro tohle `r` oba členy vyruší (`∇r/r = −g/S`, `g = |d|^(p−1)sign(d)`, `g·d = S`), takže normála **je** `g`. Žádný řetízkový faktor, žádná pojistka u pólů, a stěny vyjdou rovné místo mírně vypouklých.

Vyfoceno: třináct barev v ledu vedle třinácti obyčejných koulí (světlý dóm) — každá barva čitelná —, a hlavně **řada zmrzlých kostek pod řadou koulí ve stylu `Ice`**: nespletitelné na první pohled a na jakoukoli vzdálenost. To je ta kontrola, kterou musel druh projít dřív než cokoli jiného.

### ⚠ Past, kterou skrývá slovo „frekvence", a stála mě dvě kola

První verze jinovatky měla `FrozenFrostFrequency` 26 a amplitudu 0,010 a vyfotila se jako **pravidelný diagonální manšestr** na každém bloku. Nejdřív jsem to přičetl „pírku" (jedna sinusová vlna = pruhy, ta past je v repu zapsaná) — opravil jsem pírko na tři oktávy a **nezměnilo se skoro nic**. Teprve aritmetika řekla proč: `ReliefOctave` se počítá na **jednotkovém směru**, takže 26 je 26 radiánů přes celou kouli, tedy **čtyři cykly**, a čtyři cykly při desetině poloměru reliéfu jsou hřebeny, ne zrno. Styl `Ice` jede 30–72 na čtvrtinové amplitudě a vypadá jako jinovatka právě proto. Kalibrováno podle něj: 41 s poměry 1,0/1,43/0,71/1,87 při 0,0028, čtyři vlny místo tří.

**Poučení pro příště: u figury na kouli je „frekvence" v radiánech přes celý směr, ne na jednotku světa. Vydělte 2π, než uvěříte, že je něco jemné.**

### ⚠ Testbed neuměl ránu, dokud jsem nepřestal mířit dělem

Kyselina (#328) si zapsala, že se jí nepodařilo v Testbedu nastražit ránu vedle speciálu. Narazil jsem na totéž a **je to jinde, než to vypadá**: šedesát ran přes `at=:F10` + `aim=` + `Space` nevypsalo ani jedno `bounced`, tedy vůbec nedopadly. Ve **free módu** ale `ShootBall` střílí z kamery na `camtarget`, což se zamíří přesně:

```
Testbed.exe <mapa> campos=0,1.5,7 camtarget=0,6.5,0 at=4:Space at=5:Space …
```

a hned první běh vypsal `[shot] thawed 9 frozen ball(s) beside a group of 43`. Vyfoceno před a po: dvě kostky uprostřed clusteru jsou na druhém snímku oranžové koule. **Kdo bude potřebovat v Testbedu doopravdy trefit shluk, ať nestřílí dělem** — herní mód se dá zamířit jen úhly a shluk visí vysoko a malý.

Proč `aim=`ované rány míjejí, jsem **nedovyšetřil** a nepatří to k #329; je to otázka na geometrii herního módu, ne na led.

### Ověření

- **44 tvrzení proti skutečné knihovně** (odhozený konzolový projekt ve scratchpadu, v žádném solutionu): mapová strana (vlastní barva, jeden prstenec hluboko, blok u tří buněk téže skupiny taje jednou, flood fill se o led zastaví a po roztátí projde, kámen/sklo/bomba/zap/kyselina vedle skupiny netknuté, `GetRemovableBallsCount`, wildcard vedle samého ledu nenajde nic), fyzikální strana ve **skutečné** `PhysicsWorld` (mapa i pole souhlasí v každé buňce, blok o dvě buňky dál zůstává, přechod nastartovaný jen na roztátých, **sirotci ledem netají**), a **cesta handleru**: skutečná rána, skutečný kontakt Bepu, `BallContactEventHandler` doresolvoval dopad, `BallLanding.Thawed` sedí a další dopad nehlásí starý počet.
- **⚠ Past v té sondě, která by prošla i zkušenému:** `PhysicsWorld.Step(dt, perStepWork)` volá `perStepWork` **po** flushi kontaktů, a `perStepWork` **je** `handler.ProcessQueuedContacts`. Sonda krokující s prázdným delegátem simuluje celou hru s vypnutou cestou dopadu a rány tiše prolétnou clusterem. Stálo mě to kolo a vypadalo to úplně stejně jako mlčení Testbedu výše.
- Čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, `Game/Levels` beze změny (žádný shipnutý level led nenese, precedent #326/#327/#328).

### Co jsem přibral a je to oprava cizí mezery, ne rozšíření

1. **Editor neuměl pojmenovat kyselinu.** #328 přidalo druh a ne hint; `switch` v `MapEditor.CycleBallKind` na to má vlastní komentář, který přesně tuhle hnilobu předvídá. Přidal jsem řádek kyseliny i ledu.
2. **LevelGen kyselinu vůbec nepočítal do rozhodnutí, jestli se má sonda `FindStrandedSpecials` spustit.** Seznam byl `rocks + glass + bombs + zaps`, takže level z kyseliny a barev **přeskočil celou bránu** — to je doslova bug #343, který dorazil o druh později. Doplněno o kyselinu i led.

### Co zůstává a je to poctivá mezera

**Zvuk rozbití ledu jsem NESLYŠEL.** `PlayIceBreak` je navržený aritmetikou proti spektru `PlayRelease` (praskla kolem 5 kHz, střepy 3,4–8,6 kHz, protože release sedí 140 Hz–2,8 kHz a oddělit se mají **rejstříkem**, ne hlasitostí) a ověřený jen tím, že bake proběhne a hra nastartuje. Slyšet ho nejde: žádný shipnutý level led nenese a Testbed zvuk nemá — chce to nakreslit level v editoru (`K` na druh) a uložit do `Game/Levels`. Je to zapsané i v `docs/game-feedback.md`, protože je to přesně ta část hry, kterou nelze soudit ze snímku.

**Nic dalšího si neberu.**

---

## 2026-09-09 — Claude Code (druhý zápis dne)

**#331 (infekční koule) hotové a na mainu. Sedmý druh, poslední z „match" skupiny — a první pravidlo v téhle hře, které TIKÁ.** Větev `331-infectious`, merge `--no-ff`. `BallKind.Infectious`, `BallsMap.SpreadInfection`, `BallsConstraintsBuilder.SpreadInfection`, technika `InstancedModelInfectious`, desátý region kbelíků (druhá barevná rovina), tik v herní sekvenci i v Testbedu, brána v LevelGenu, `Testbed\Maps\Infection.json`.

### Návrh: issue nechává pět rozhodnutí na stavitelovi, tady jsou i s důvody

1. **Nemocná koule JE matchovatelná** — první druh vedle `Normal`, který na `Matchable` odpoví ano. Issue to říká za mě: infekce, kterou nejde vystřílet, není mechanika ale časovač. Bez tohohle nemá hráč protihru.
2. **Šíří se, ale zároveň sama tuhne v kámen.** Populace tím může jen stagnovat nebo klesat — a klesá, když ohnisko nemá koho nakazit. To je „stopping rule zabudovaná, ne doladěná později", kterou issue chce, a **nepotřebuje čítač na kouli, tedy ani nový klíč ve formátu**. Roste kámen za ní: přesně jedna trvale ztracená koule na jedno ohnisko na jednu ránu odkladu.
3. **Bere jen OBYČEJNOU kouli.** Kámen, sklo, bomba, zap, kyselina, led ani jiná nemocná ne — a není to sedm výjimek, je to jedno pravidlo (infekce bere zdravou kouli, a všechno ostatní zdravá koule není). **Led je úkryt**, dokud si ho hráč sám nerozbije.
4. **Šplhá.** Bere zdravého souseda nejblíž stropu, s totálním tie-breakem. Náhoda by byly neviditelné kostky; takhle hráč vidí, kam to jde a kolik má času.
5. **Zpevnění na kámen zůstává, druhá prohrávací podmínka ne.** Issue nabízí tři cesty; beru „kámen ano" a pole samého kamene je podle **už shipnutého pravidla #324 VYČIŠTĚNÉ** — spadne za nula bodů. Cena za ignorování je nesená **skóre a hvězdami**, což je lepší nosič než druhé dveře na konci levelu.

### ⚠ Pozice tiku, a proč je to ta těžká část

Po uvolnění skupiny, před census. **Po uvolnění**, protože skupina, kterou hráč právě dokončil, je jeho — tik, který by jí napřed zkamenil kouli, mu tiše sebere ránu, kterou si zasloužil. **Před censusem**, protože z něj čte všechno v témž snímku: jaké barvy se smí nabíjet, jestli je level vyčištěný, jestli je prohraný. Po censusu jsou všechny tři **o ránu pozadu** a ta chyba je nepravidelná a svede se na fyziku.

**Dokázáno, ne předpokládáno** (issue si to vysloveně žádá): na poli, jehož jediná koule jedné barvy je ta nemocná, čte barva **před** tikem jako živá a **po** tiku jako mrtvá — koule ztvrdla v kámen, jehož barvu nikdo nesmí číst.

**Mine tiká taky.** Dva argumenty a shodují se: strop klesá podle `ShotsFired % ceilingStep`, tedy podle **vystřelených** ran, takže per-ran tlak v téhle hře mine vždycky počítal; a kdyby bylo mine zadarmo, zaseknutý hráč může čekat donekonečna. **⚠ Co schválně NEDĚLÁM: tikat při VÝSTŘELU**, což je místo, kde počítá strop. Rána letí desetinu sekundy — tik u ústí by mohl zkamenit kouli, na kterou hráč míří, v letu, takže rána, ke které se upsal, je po upsání špatně. Tik je na **doresolvování** rány. Při té příležitosti jsem obě cesty mine (dopad na kámen a propad pod kill plane) svedl do `OnShotSpent`, aby pravidlo přidané k jedné nechybělo u druhé.

### ⚠ Past, kterou otevřelo právě to „matchovatelný", a je to #325 z opačné strany

`RepairLonelyBalls` v LevelGenu má guard `if (!Matchable(kind)) continue;` — dnes tím vypadnou všechny speciály. Jakmile je infekce matchovatelná, **projde**, a oprava přebarvuje přes `PutBallAt`, jehož `kind` **defaultuje na Normal**. Každá oprava by nemocnou kouli tiše **vyléčila**, a soubor levelu by se zapsal z výsledku. To je doslova ta ztráta dat, kterou má #325 zapsanou, jen dorazila druhými dveřmi. Kind se teď protahuje i zkušebními přebarveními, jinak by se skupina nemocné koule měřila na poli, které oprava už změnila.

### ⚠ Tři figury vzhledu byly špatně a každá stála kind jeho protihru

Fotil jsem to přes všech třináct barev, dvakrát znovu:

1. **Práh pokrytí nastavený, jako by měl obor střed v půlce.** Je to součet **usměrněných** sinů s amplitudami do jedničky a střední hodnota `|sin|` je `2/π = 0,64` — práh 0,42 tedy leží **pod** průměrem a propustí čtyři pětiny povrchu. **Cokoli, co řeže usměrněné pole, se poměřuje proti 0,64, ne proti 0,5.**
2. **Hrana skvrny byla tak měkká, že film rozmazala přes celou kouli.** Při `InfectEdge` 0,12 pokryl pás smoothstepu většinu rozptylu pole, takže film byl **částečně přítomný skoro všude** — nádech a ztmavení místo skvrn.
3. **Tep dosazoval barvu slizu MÍSTO barvy koule, ne k ní.** Každá obyčejná koule tady září **vlastní** barvou a ta zář je velká část toho, jak se třináct barev pozná; předáním slizové barvy jsem ji vzal a všech třináct se vyfotilo jako **tmavě zelené koule se žlutými puchýři** — červená jako hnědá, navy i stříbrná jako tmavě zelená. Teď je to `lerp(primary, slime, film)`. **Tohle je ta, co si zaslouží přežít issue**: vypadá jako detail a je to celá věta „obyčejná koule, která je nemocná" buď pravdivá, nebo jen tvrzená.

### Ověření

- **44 tvrzení proti skutečné knihovně** (odhozený projekt ve scratchpadu): švy (a že se **nepohnulo** sedm ostatních druhů), mapová strana (šplhá, bere jen zdravou, imunity, vyhoří, koule nakažená týmž tikem uvnitř něj dál nešíří, dvě ohniska si nevezmou téhož souseda dvakrát, **determinismus dvou průchodů**), fyzikální strana ve skutečné `PhysicsWorld` (mapa i pole souhlasí, oba přechody nastartované), **celý průběh levelu** a **důkaz pořadí**.
- **Změřená bilance, kterou jinak nemám kde vzít:** jedno ohnisko na stovce koulí, hráč nedělá nic → **94 tiků, 94 koulí v kameni, pak se to samo zastavilo** a nechalo 6, na které si nedosáhlo. Infekce tedy **vyhoří o vlastní stopu** a pole se nutně nezmění celé v kámen.
- **Tik v běžícím Testbedu**: `[infection] 2 hardened to stone` třikrát, se stopou kamene na fotce.
- Čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, `Game/Levels` beze změny.

### Co zůstává a je to poctivá mezera

- **Nevyfotil jsem samotné šíření**, jen jeho stopu. Infekce šplhá nahoru do shluku a mizí za předními koulemi; z venku to zvenčí nejde zarámovat. Kdo na to sáhne, ať postaví mapu s ohniskem na **kraji** slabu.
- **Nemocná koule nemá vlastní zvuk.** Rozbití ledu (#329) ho dostalo, protože jeho účinek není vidět; tady je vidět — puchýře a kámen. Kdyby se ukázalo, že tik uniká pozornosti, patří sem krátké mokré prasknutí, ne další záře.
- **`RockTurns` se na čerstvě ztvrdlou kouli aplikuje okamžitě**, takže při zpevnění koule skokem změní natočení. Nezkoumal jsem, jestli to je na obrazovce vidět — obě poloviny přechodu ho dostávají, takže se nerozjedou, ale ten skok tam je.

**Nic dalšího si neberu.**

---

## 2026-09-09 — Claude Code (třetí zápis dne)

**Nejdřív k tomu, odkud tahle práce je: majitel mě pustil na průzkum repa a chtěl issues. Založil jsem osm (#380–#387) a pak dostal pokyn vzít #381 a opravit ho.** Zbylých sedm nechávám ležet a nic si z nich neberu; kdo je bude číst, ať ví, že vznikly čtením kódu proti trackeru (otevřené i zavřené issues), ne z běhu hry. **⚠ A jedno z nich mělo v těle špatné číslo — viz níže.**

**#381 hotové na větvi `381-neighbour-cells-without-enumerator`: `BallsMap.GetNeighboringCells` přestal alokovat.** `FillNeighboringCells` píše buňky do `Span` volajícího, `GetNeighboringCells` vrací `NeighboringCells` (obyčejný struct, který si buňky najde v konstruktoru a pak jen jede indexem). **Ani jedno z dvaceti volání v repu jsem nemusel sáhnout** — `foreach` se váže vzorem, ne rozhraním.

### Proč to nikdo nechytil dřív, a co si z toho vzít

Iterátor byl v pořádku, dokud se na sousedy ptal **dopad**. #70 nechalo náhled míření položit **tutéž otázku**, a od té chvíle tentýž `yield return` běžel **jednou za snímek** z `TryFindEmptyCellNextTo` a — když je první prstenec plný — z `TryFindEmptyCellInSecondRing`, **jehož průchod je vnořený**. To je doslova incident, který má `BestPractices.md` §3 zapsaný (`SkyLitRenderers`, per-frame až kvůli overcast lerpu), podruhé a **s už napsaným pravidlem**.

**Poučení, které jsem tam dopsal: nebezpečný není iterátor, ale volající, který se přestěhuje na snímek.** Revidovat se má nové volání, ne stará metoda — u staré metody není co vidět.

### ⚠ Pořadí je nosné a je to celé riziko téhle změny

`CollectAcidShaft` (#328) na pořadí **láme shody** — na kroku v ose jsou všechny čtyři kandidáti stejně daleko a vyhrává první. Kampaň na disku je proti tomuhle pořadí vygenerovaná. Proto je nová `FillNeighboringCells` **doslovný přepis** starého těla (`yield return X` → `into[count++] = X`, řízení netknuté) a proto je to teď **napsané na metodě**, ne ponechané k objevení.

### Změřeno (odhozená sonda ve scratchpadu, v žádném solutionu)

- **Pořadí a obsah: 8 211 buněk přes osm polí** — obě parity, každá stěna, každý roh, degenerované 2×2×2 i 13×13×34 — nová `foreach` forma i nová `Fill` forma proti **verbatim přepsanému starému iterátoru**, buňka po buňce, **0 neshod**.
- **Alokace: 104 B na jeden průchod → 0 B.**
- **Cesta snímku, oba prstence plné: 1 456 B na snímek → 0 B.**

**⚠ A tady je ta oprava vlastního issue: napsal jsem do #381 „až deset enumerátorů na snímek". Skutečné maximum je ČTRNÁCT** — jeden první prstenec, jeden vnější druhý a jeden vnitřní na každého z až dvanácti obsazených sousedů. Odhadl jsem osm vnitřních místo dvanácti. Chyba je směrem k podcenění; do issue jsem to dopsal komentářem, ať tam nezůstane stát nižší číslo.

### Co ještě jsem sáhl a proč to není rozšíření

`CountOccupiedNeighbors` si nechal vlastní průchod, ale **jeho komentář tvrdil jako důvod tu alokaci** — a ta je pryč. Přepsáno na skutečný důvod: nechce buňky, chce `dX`/`dZ` kroku, který už hotová buňka zahodila. To je pravidlo o nosných komentářích, ne kosmetika: nechat tam stát starý důvod znamená, že příští čtenář ten průchod zruší jako zbytečný.

### Ověření

- Čtyři solutiony 0 chyb.
- **LevelGen exit 0 a `Game/Levels` po plném přegenerování BAJT ZA BAJTEM stejné.** To je ten nejsilnější důkaz, jaký na pořadí existuje: přegenerování projede kyselinovou chůzi, sag sondu na skutečném Bepu, `FindStrandedSpecials` i opravný průchod, a 110 levelů vyšlo identických.
- ScoreSim „All levels rate the right way round".
- **Živě v Testbedu**: `aimshoot` na `Full.json` odpovídá `[shot] bounced: no free cell in either ring` — to jsou **oba prstence prochozené za běhu**, tedy přesně ten nejhorší případ, co jsem měřil; na `Eight_Colors.json` **kaskáda 22 spadlých koulí**, tedy dopad + shoda + uvolnění + průchod odpojených.
- Hra: `BS3D.exe level=Ten shot=6,9` — level Ten vyfocený, běží. **Majitelův `Progress.json` netknutý** (mtime 26. 8., hash `9a66b7e9…`).

### Co zůstává

- **Merge na slovo majitele.**
- Nevyzkoušel jsem `aim=`ovanou ránu do shluku — recept z #329 (`campos`/`camtarget` + `Space`) mi na `Frozen.json` čtyřikrát minul a všechny čtyři koule spadly. **Nešel jsem to vyšetřovat**: je to táž otevřená otázka, kterou si #329 zapsalo jako nedovyšetřenou, a k #381 nepatří. Pozitivní cestu dopadu mám z `Eight_Colors.json` jinou cestou.
- **Nic dalšího si neberu.**

**Nic dalšího si neberu.**

---

## 2026-09-09 — Claude Code (čtvrtý zápis dne)

**#380 hotové na větvi `380-editor-all-scenes`: scénový cyklus jde přes všechny scény, v obou programech, které ho mají.** `SceneRenderer.NextScene` chodí po enumu, `CycleLength` je zrušený (jiného volajícího neměl).

### Co bylo doopravdy špatně, a je to jiná věta než „deset scén nešlo zobrazit"

Prefix sedmi scén měl důvod — „scény, proti kterým se kreslí mapa" — a ten důvod **umřel při stavbě kampaně, aniž to kdokoli rozhodl: padesát ze sto deseti shipnutých levelů je autorováno ve space, dreamu, cavernu, na Měsíci a v sopce**, všechny za prefixem. Horší než nedostupné pozadí je ale tohle: **po načtení takového levelu leží index za koncem cyklu, takže první V restartuje na City a k vlastní scéně levelu se nedá vrátit jinak než znovunačtením souboru** — v programu, jehož jediná práce je ukázat mapu proti pozadí, ve kterém pojede. #73 kdysi opravilo *aritmetiku* toho restartu; prefix samotný opravit nemohlo.

### ⚠ Rozšíření cyklu odhalilo tabulku, která byla správně jen proto, že byl cyklus krátký

`Testbed.SetScene` dosazoval vlastní kupoli **moři a savaně** a víc nejmenoval, zatímco parse `scene=` při startu jmenoval **všech šest** scén, které kupoli mají. Kdo by dojel cyklem na tropy, sopku, Mars nebo bouři, dostal by je pod tím, co zrovna viselo. Obě ramena se teď ptají jedné `Testbed.DefaultSkyDome`. **Hra drží třetí kopii téže šestiřádkové tabulky se stejnými šesti čísly** (`BS3DGame.SetScene`) — dnes souhlasí a nic je k tomu nenutí; sloučit všechny tři je práce na vlastní větev, ne přílepek k cyklu.

### ⚠ Editor kupoli scény ZÁMĚRNĚ nedosazuje, na rozdíl od Testbedu i hry

Zjišťoval jsem to, protože první instinkt byl opačný: náhled má vypadat jako hra, tak ať V dosadí kupoli scény. **Je to past.** V editoru je kupole **data** — F4 ji píše do levelu — a ve hře se sky levelu aplikuje **až po** scénině výchozí (`GameplayScreen.Session`) a **vyhrává**. Dosadit ji při cyklu by tedy kvůli náhledu přepsalo autorovu volbu, tedy zničilo jeho práci. Napsáno na místo, ať to příště nikdo „neopraví".

### Rozhodnutí, která issue nechalo na stavitelovi

1. **Testbed jsem rozšířil taky**, ne jen editor. Nechat jeden na sedmi znamená nechat žít konstantu, jejíž doc tvrdí mrtvou premisu — a Testbed je nástroj, kterým se scény soudí; srovnat sopku s Marsem nešlo klávesou, jen restartem procesu.
2. **Zpětná klávesa NE.** `B` cyklí dvacet kupolí na jedné klávese, takže sedmnáctka je idiom tohohle programu, ne nová zátěž; a modifikátor by chtěl stav klávesnice, který `CameraInputHelper` nevydává (a druhý `GetState()` zakazuje BestPractices §5). Kdyby to v praxi vadilo, je to samostatná drobnost.
3. **Hint Testbedu ztratil počet.** #376 ho stavělo z enumu, aby nemohl zestárnout; klávesa, která dosáhne na všechny členy, nemá na co ukazovat. **Nepotřebovat počet je lepší než mít nestárnoucí.**

### Ověření

- **Testbed vlastní časovou osou** (`scene=city` + 18× `at=N:NumPad2`): sedmnáct scén v deklarovaném pořadí a přetečení `Storm → City → Sea`. K tomu `[sky] Dome` 13 / 14 / 1 / 9 / 19 / 20 na moři, savaně, tropech, sopce, Marsu a bouři — **prostřední čtyři jsou přesně ty, které staré rameno nikdy nastavit nemohlo** — a každá ostatní scéna si nechá zděděnou kupoli.
- **Editor dvakrát vyfocen z volcano levelu** (`Caldera.json`, harness ve scratchpadu: fokus klikem na titulek, scan-code klávesy, `CopyFromScreen` — editor nemá ani časovou osu, ani klávesu na snímek): **jedno V jde na Mars**, ne na City, a **sedmnáct se vrátí na Volcano**, s panelem scény přebindovaným v každém kroku.
- Čtyři solutiony 0 chyb, LevelGen 0 s `Game/Levels` beze změny, ScoreSim zelený.
- Dokumentace přepsaná v témže commitu: `CLAUDE.md`, `docs/scenes.md` (osm míst — včetně „Fifteen `SceneKind`s exist", což byla vlastní hniloba: je jich sedmnáct), `docs/testbed.md` a `docs/formats-and-tools.md`. Historické záznamy #73 a #376 jsem nechal stát a dopsal k nim, že cyklus, který popisují, už neexistuje.

### Co zůstává

- **Merge na slovo majitele.**
- `screenshot.ps1` ve skillu nemá v `KeyMap` ani `V`, ani `NumPad2` — proto ten vlastní harness. Dva řádky by to spravily, ale je to cizí soubor a jiná práce.
- **Nic dalšího si neberu.**

---

## 2026-09-09 — Claude Code (pátý zápis dne)

**#332 (gravitační studna) — na větvi `332-gravity`, NEmergnuto: čeká na majitelovo ruční ověření, které je jediná část, kterou neumím udělat sám.** Osmý druh: obyčejná koule své barvy, která **ohýbá rány letící kolem ní**. Nová `GravityWells`, hák `PhysicsWorld.PerStepForces`, `ShotPlacement.TryFindFirstHitCurved`, technika `InstancedModelGravity`, jedenáctý region kbelíků, brána v LevelGenu, `SagProbe` s křivkou, argument hry `levelfile=` a dvě mapy.

### Jádro: jeden snímek studní čte simulace i náhled

`ShotPlacement` existuje proto, aby se duch a dopad nemohly rozejít. Křivka tu jedinou odpověď rozbíjí, a issue nabízí tři východiska — integrovat křivku v náhledu, náhled u studny schovat, nebo kreslit rovnou čáru a lhát. Vzal jsem první. Náhled integruje **týž výpočet, týmž krokem, z téhož snímku** jako simulace (`v += a·dt`, pak `p += v·dt`), a pole má **hranu** (nulová hodnota i sklon na `RANGE`), takže dráha je mimo studny přesně rovná a náhled rovné úseky přeskočí jedním segmentem místo šedesáti krátkých.

**Beam sleduje let**, ne rovnou čáru k dopadu — rovná čára ke konci křivky říká pravdu o konci a lže o celém průběhu, což je horší než obojí. Kreslí se jako řetěz segmentů s fází nesoucí už nakreslenou vzdálenost, aby čárkování v kloubech nezačínalo znovu.

### ⚠ Konstanta síly byla čtyřikrát vedle a spočítat ji nestačilo

Aritmetika dala 900 u/s². Změřeno: **ohne ránu o třetinu buňky** — studna, kterou hráč nikdy nemusí obcházet. Odhad měl správný řád a špatnou otázku: hráč nevidí deflexi u studny, ale deflexi **tam, kde rána dopadne**, a po minutí studny visící pod clusterem zbývá pár jednotek letu. Deflexe je v té konstantě lineární, takže se dala vyřešit místo prohledat: při **3600**, změřeno ve volném letu osm jednotek za studnou, **1,62 / 1,33 / 0,86 / 0,38 / 0,08 buňky** pro průlet 1,5 / 2,0 / 2,5 / 3,0 / 3,5 jednotky od ní. To je gradient, podél kterého se dá mířit.

**Kolik z toho dorazí až k dopadu, je věc levelu, ne konstanty** — proto testovací mapa věší studny na stopky šest vrstev pod slabem a proto generátor odmítá **zazděnou** studnu (bez volného prostoru ve vlastním dosahu neohne nic).

### ⚠ Skutečná chyba, kterou odhalilo zaseknutí a ne selhání

Když hranice pole ležela blíž než jeden integrační krok, řešitel posunul hodiny letu o nulu a **zacyklil se**. Sonda visela místo aby spadla. Hranice bližší než jeden krok se teď počítá jako „uvnitř pole", což je i fyzikálně poctivé čtení.

### ⚠ A past, do které jsem spadl DVAKRÁT za hodinu

`_world.PerStepForces = …` jsem napsal do konstruktoru vedle `_processContacts` — nejdřív v Testbedu, pak v Game. **Ani jeden z těch dvou programů nestaví `PhysicsWorld` v konstruktoru** (Testbed v `Initialize`, Game v `SetUpPhysics`), takže to bylo pokaždé `NullReferenceException` při startu. **Cokoli visí na `_world`, patří vedle `_world`.**

### ⚠ Čtyři chyby v měřicím rigu, každá vypadala jako vada gravitace

Tohle je hlavní poučení dne a stálo mě většinu jednoho sezení:

1. **Střílel jsem do té gravitační koule.** Odstup 1,0 při součtu poloměrů 1,0 je zásah, ne průlet — tělo letělo na 43 % rychlosti a vypadalo to jako rozbitý silový model.
2. **Pověsil jsem shluk 27 jednotek pod strop.** Kotevní sokety cluster vytáhnou nahoru, mřížka se roztáhne a všechny rány se odrážejí.
3. **Měřil jsem let skrz stropní desku**, kterou model nezná — 18 jednotek rozdílu, a znovu to vypadalo jako vadný model.
4. **Terč měl jedinou barvu**, takže první rána shodila celý slab a všechny další letěly do prázdna („náhled nic nenašel").

Po opravě model sedí s letem na **0,0375 jednotky za 10 kroků**.

### ⚠ A tvrzení, které jsem musel opravit, ne kód

Akceptační test „duch se shodne s dopadem" jsem psal jako 6/6. **Rovný, nezměněný náhled se v tom rigu shodne 3 ze 6** — je to vlastní zapsaná tolerance `ShotPlacement` („duch, ne slib"), ne regrese. Správné tvrzení je **„křivka nestojí přesnost"**, měřené proti vlastnímu baseline: rovný 3/6, zakřivený **4/6**.

### Ověřeno

- **26 tvrzení proti skutečné knihovně**: pole (hrana, hladkost, konečnost ve středu, `DistanceToField`), let proti modelu, deflexní křivka, shoda ducha a dopadu proti baseline, švy, formát (`"k":9`), a že bez studní je zakřivený řešitel byte za byte ten rovný.
- Čtyři solutiony 0 chyb, LevelGen 0, ScoreSim 0, `Game/Levels` beze změny.
- Vyfoceno v Testbedu i ve **hře** (`levelfile=`).

### Co jsem přidal, protože to chybělo všem šesti druhům přede mnou

**`levelfile=<cesta>`** ve hře: přehraje level mimo set a připne na něj celý běh. Set **je** kampaň, takže level postavený na vyzkoušení mechaniky do něj nepatří — a dosud byl jediný způsob, jak vzít speciál do ruky, upravit kampaň, kterou LevelGen při příštím běhu přepíše. Je to týž tvar, jaký má `preview=` pro front end.

### Co zbývá a proč to nemergnuju

**Nemám jak sám ověřit to, na čem issue trvá:** že s drženým RMB duch sedí tam, kam rána doopravdy dopadne. Repo má vlastní pravidlo, že se vizuály kolem míření ověřují **ve hře**, a Game nemá timeline. Majitel nabídl, že to vezme myší — čeká to na něj, i s napsaným scénářem.

**Nezměřil jsem cenu snímku** integrace náhledu (issue ji chce s pevnou kamerou a párovými opakováními). Mám v paměti majitelovo „desktop se pod zátěží tvrdě resetuje, ptej se před delší GPU seancí", takže se ptám než to spustím.

**Nic dalšího si neberu.**

---

## 2026-09-10 — Claude Code

**Obě větve, které čekaly na slovo, jsou na `main`:** `332-gravity` a `380-editor-all-scenes`, obojí `--no-ff`, na majitelův pokyn. Zápisy výše zůstávají stát tak, jak byly psané — včetně vět „NEmergnuto" a „Merge na slovo majitele", které tímhle přestaly platit.

**⚠ Seam, který ten merge otevřel, a proč byl skutečný:** `332-gravity` odbočilo **před** #381, takže se v něm potkal starý `GetNeighboringCells` (iterátor) s novým (struct), a `380-editor-all-scenes` sahalo do týchž tří souborů kolem scén. Textově prošlo obojí, konflikt byl jen v tomhle žurnálu (obě větve psaly na jeho konec). Ověřeno **čtyřmi solutiony bez chyby**, **LevelGenem s exit 0 a `Game/Levels` beze změny** — což je na pořadí sousedů ten nejsilnější důkaz, co existuje, protože přegenerování projede kyselinovou chůzi, sag sondu se zakřiveným řešitelem i `FindStrandedSpecials` — a ScoreSim „All levels rate the right way round".

**Pořadí zápisů z 9. 9. jsem srovnal podle času commitu, ne podle pořadí merge:** #380 (18:23) je čtvrtý, #332 (20:58) pátý. Obě větve si samy říkaly „třetí"/„čtvrtý", protože o sobě navzájem nevěděly; přečíslovaný je jen nadpis #332.

### Co po nich zůstává otevřené a nikdo si to nebere

- **#332 nemá to ruční ověření, na kterém samo trvá:** že s **drženým RMB** duch sedí tam, kam rána doopravdy dopadne. Merge tuhle otázku nezodpověděl, jen ji přestal blokovat. Scénář je napsaný v zápisu výše (`levelfile=` na `Testbed\Maps\GravityLevel.json`).
- **Cena snímku zakřiveného náhledu není změřená** (pevná kamera, párová opakování).
- **`CA2014` v `Tools/LevelGen/Program.cs`** (`stackalloc` v trojité smyčce) **není z merge** — stojí to na `main` i před ním, jen se posunulo číslo řádku. Nechávám stát; patří to k #386, ne sem.

---

## 2026-09-10 — Claude Code (druhý zápis dne)

**#384 hotové na větvi `384-mouse-sensitivity`: citlivost myši je řádek v Nastavení, a nájezd v ADS konečně škáluje rychlost kurzoru.** `GameSettings.Sensitivity`, `SENSITIVITY_LADDER`, `BS3DGame.CycleSensitivity`/`NearestSensitivityRung`, záhlaví `CONTROLS` na stránce, `PreciseAim.CursorRateScale` a `rateScale` parametr na `MouseAim.ApplyCursor`.

### Tvar, který jsem zvolil, a proč zrovna ten

`ApplyCursor` bere **jeden** parametr `rateScale` a volající si do něj násobí své vlastní členy. Zvažoval jsem property `Sensitivity` na `MouseAim` vedle per-snímkového parametru pro nájezd — **zahodil jsem to kvůli synchronizaci**: `_mouseAim` patří `GameplayScreen`, ale stránku Nastavení lze otevřít **z pauzy**, takže se řádek může pohnout pod běžícím levelem a kopie předaná session při startu by byla ta zastaralá. Čte se to tedy per snímek z `BS3DGame.MouseSensitivity` — což je **vzor `IsDropCinematicEnabled` (#290)**, jen tady na něm záleží víc.

**Dial hráče se do Testbedu nedostane a nesmí.** Testbed nemá soubor nastavení a je to měřicí přístroj: rig, jehož citlivost může driftovat, nejde porovnat sám se sebou mezi dvěma běhy. Nájezd v ADS ale škáluje **v obou** — jinak by se přístroj a hra rozešly přesně v tom, kvůli čemu se přístroj používá.

### ⚠ Poměr je z tangent poloúhlů, ne z FOV — a je to čtená vs. neuvažovaná fyzika

Na obrazovku úhel mapuje **projekce**, takže co drží stejnou dráhu kurzoru po obraze, je `tan(FOV/2)/tan(GAME_FOV/2)` = **0,828**. Prostý poměr FOV by řekl 0,840, což je **1,5 % vedle**. Malé — a taky zadarmo správně, a tangentový tvar zůstane správný, i kdyby se některé z těch dvou FOV přeladilo.

**Změřený zbytek, protože ten tvar jeden má:** měřítko násobí **úhel**, kdežto na obrazovku dopadá jeho tangenta, takže rovnost je přesná jen v limitě. Proti skutečnému dělu, jako podíl poloviny obrazu, který ruka přejede: **0,004 % vedle při 10 px, 0,15 % při 60px korekci — proti 20,8 %, které tam stály předtím a stály tam v každé vzdálenosti.** Zavřít i těch 0,15 % by znamenalo přemapovat *polohu* kurzoru místo škálování rychlosti, za cenu vlastnosti, která tohle dělá bezpečným: být v klidu **přesně 1**, tedy přesně dnešní míření.

### ⚠ Dvě věci, které jsem měl v prvním kole špatně

1. **Aritmetika ve vlastním komentáři.** Napsal jsem k `NearestSensitivityRung`, že „1,2 by podle rozdílu spadlo na 1,5 (0,3) místo na 1,0 (0,2)". **0,2 < 0,3, takže rozdílové pravidlo vybere 1,0 taky** — ten příklad nic neukazoval. Sonda to shodila hned. Skutečné tvrzení je obecné a silnější: **rozdílové pravidlo má hranici v aritmetickém průměru, poměrové v geometrickém, a geometrický nikdy není větší — takže rozdíl táhne každou mezihodnotu o příčku DOLŮ.** Ověřeno na 3000 hodnotách: rozdílové pravidlo ani jednou nevybere vyšší příčku než poměrové. Doložený příklad je 2,47 (hranice 2,5 a 2,449).
2. **Sonda měřila NaN.** `Cannon(Vector3.Zero)` je degenerovaný — `OrbitCenter` slouží zároveň jako vektor míření, takže `AimTarget == Position` a `Normalize(0)` je NaN. Není to vada `Cannon`, je to past pro toho, kdo si ho postaví v konzoli.

### ⚠ A past v běhovém ověření, která by prošla i zkušenému

Chtěl jsem to dokázat **kolečkem**: sweep tam s drženým RMB, stejný sweep zpátky bez něj — bez škálování se musí dělo vrátit přesně domů. **Napoprvé jsem vzal 800 px, což je 92° traverzu, a to naráží do dorazu**: cesta tam se ořízla, cesta zpět jela plných 92°, a kontrolní běh **bez ADS se nevrátil domů**. Vypadalo to jako vada škálování a byl to doraz. Při 240 px (27,5°) sedí obojí.

**Druhá past ve stejném harnessu: `shot=` počítá HERNÍ čas, ne čas skriptu** — mezi startem procesu a prvním snímkem je ~5 s načítání. První snímek, který měl být „před jakýmkoli pohybem", byl ve skutečnosti až za sweepem, takže baseline byl pohnutý.

### Ověření

- **27 tvrzení proti skutečné knihovně** (odhozený projekt ve scratchpadu, v žádném solutionu): `CursorRateScale` (blend 0 je **bitově přesná 1**, blend 1 je tangentový poměr, monotonie přes celý blend, půlka je půlka, `overviewFov == FOV` je 1 i při plném držení), `ApplyCursor` (rateScale 1 je shipnutý pocit `0,001 × SENSITIVITY × px`, půl je půl, trojnásobek je trojnásobek, nula nepohne, a **nezávislost na snímkové frekvenci při 30/120/240 fps zůstala**), a **skutečná privátní `NearestSensitivityRung` v postavené `BS3D.exe` reflexí** — ne replika: 2,47 → 3, 0,62 → 0,75, 0 / záporná / NaN → 1, absurdních 5000 → 3, každá příčka sama sebou.
- **Ve hře, s doopravdy drženým pravým tlačítkem** (majitelovo pravidlo, že se míření ověřuje ve hře a ne v Testbedu): kontrolní kolečko 240 px tam a zpět bez ADS **vrátilo dělo přesně doprostřed**, totéž kolečko s drženým RMB na cestě tam skončilo **viditelně natočené doleva** — cesta tam byla za držení levnější, přesně jak má být. Kontrola zároveň dokazuje, že harness neztrácí události.
- **Stránka Nastavení vyfocená**: `CONTROLS / Sensitivity 100 %`, dvě kliknutí → 150 % → **200 %**, na disku `"sensitivity": 2`, a po restartu procesu se čte zpátky 200 %.
- **⚠ Past #138 (řádky utekly pod obraz i s tlačítkem Back) jsem musel vyloučit, protože pravý sloupec vyrostl z devíti řádků na jedenáct** — je to ten jediný skutečný risk téhle změny. Vyfoceno: Back stojí, celá stránka se vejde.
- Čtyři solutiony 0 chyb, LevelGen exit 0 s `Game/Levels` beze změny, ScoreSim „All levels rate the right way round".
- **Majitelův `Settings.json` jsem na dobu měření přepsal** (potřeboval jsem okno místo fullscreenu — hra pořád nemá argument `windowed`, viz starší zápisy) **a vrátil bajt za bajtem**: `30eefb41…`, `.bak` `4a7debca…`, `Progress.json` `f5c8c4ee…` — všechny tři hashe sedí s těmi, které jsem si vzal předem.

### Co zůstává a je to poctivá mezera

- **Padu jsem se nedotkl, schválně.** `PAD_RATE` zůstává konstanta a issue to tak chce: výchylka páčky je rychlost a nejsou v ní pixely, takže se jí žádný z argumentů pro dial (rozlišení, DPI myši) netýká. Napsal jsem to na `PAD_RATE` samotné, ať to příští čtenář nesloučí do jednoho řádku.
- **Nájezd čte blend z MINULÉHO snímku** v obou programech (`_adsHeld` se nastavuje ve vstupu a `PreciseAim.Step` běží až v kameře). Nechal jsem to a napsal proč: snímek zpoždění na členu, jehož vlastní náběh je `BLEND_TAU` 0,08 s, je pod tím, co ruka pozná — kamera čtoucí nájezd, na který dělo ještě není napózované, není.
- **Neměřil jsem cenu snímku** a není co: dvě tangenty za snímek na cestě, která už dělá `Normalize` a `Lerp`.
- **Nic dalšího si neberu.**

---

## 2026-09-10 — Claude Code (třetí zápis dne)

**Na majitelovo slovo: každá mergnutá větev je smazaná, a je z toho pravidlo v `CLAUDE.md`.** Větev `delete-merged-branches-rule`.

**⚠ Pro druhý stroj, ať to nevypadá jako nehoda:** z GitHubu zmizelo **osmnáct** větví a lokálně tady **stodvacet pět**. Všechny byly mergnuté — ověřeno dvakrát a dvěma způsoby (`git branch -r --merged` a pak `git merge-base --is-ancestor` větev po větvi, se zapsanými SHA), otevřené PR nula, nemergnutého nezůstalo nic. **Neztratilo se nic a nejde o nic přijít**: každý commit je dosažitelný z `main` a **název každé větve stojí v jejím merge commitu**, takže zpětně se kterákoli dohledá přes `git log --oneline --merges | grep <název>` a obnoví z toho SHA.

**Proč to pravidlo vzniklo, a je to argument o čitelnosti, ne o pořádku:** merge commit už název větve i její jednořádkové shrnutí nese, takže po přistání **ref na větvi nedrží nic, co historie nemá** — pořád ale něco *říká*, a říká „tohle se právě dělá". Na repu, jehož celý smysl je, že si `main` dělí několik strojů, je seznam větví způsob, jak se jeden stroj ptá, co má druhý rozdělané; seznam hotové práce se čte jako seznam nehotové. Odpověď na „co se teď dělá?" se musela **počítat, ne číst**.

**Dvě výjimky, obě dočasné a ne povolené:** větev vyzvednutá ve worktree se smazat nedá, dokud se ten worktree nepřesune jinam (`git worktree list` řekne které — tady zůstaly `234-first-level-pyramid` a `384-mouse-sensitivity`), a větev, kterou si druhý stroj po mergi nestáhl, tam prostě přijde o upstream, což je neškodné.

**Mazat vždy `git branch -d`, nikdy `-D`.** To malé `-d` odmítne cokoli, co doopravdy mergnuté není — a to je ta kontrola, ne formalita.

**Nic dalšího si neberu.**

---

## 2026-09-10 — Claude Code (čtvrtý zápis dne)

**#382 hotové na větvi `382-ads-converge-on-impact`: přesné míření konverguje na skutečnou vzdálenost dopadu.** `PreciseAim.ConvergeDepth` + `CONVERGE_TAU`, `Step` bere cílovou hloubku, `LensTarget` bere hloubku místo středu clusteru, `DepthToClusterCentre` jako pojmenovaný zbytek staré aritmetiky.

### ⚠ Issue navrhovalo instalatérství, které merge #332 mezitím udělal zbytečným

#382 chce protáhnout `nearest` ven z `TryFindFirstHit` jako `out float distance`. **Nebylo potřeba sáhnout na jedinou signaturu v `ShotPlacement`:** #332 už `TryFindFirstHitOnSegment` `out float distance` dalo, a hlavně — hra si **kontaktní bod každý snímek drží** v `_previewBeamEnd`. Hloubka je `Dot(_previewBeamEnd − muzzle, aim)` a je zadarmo.

**Promítnuto na aim, ne vzato jako bod**, a to je rozhodnutí, ne detail: `LensTarget` má ve smlouvě, že vrací bod **na** dráze střely, a parallax je o **hloubce**. Drží to zároveň zakřivené lety #332 poctivé — kontakt oblouku leží mimo aim, jeho hloubka ne.

### ⚠ Co jsem musel ověřit, protože na tom celá změna stojí

Že `UpdateShotPreview` **vždy** běží před `UpdateCamera`. Mezi nimi není žádná větev — a co je silnější, `UpdateShotPreview` si shazuje `_previewReachesCluster = false` **na svém začátku, před každým early returnem**. Zastaralý `_previewBeamEnd` tedy branou projít nemůže, ať se náhled ukončí kudy chce.

### ⚠ Dvě pravidla kolem easingu, bez kterých by to bylo horší než původní stav

1. **Hloubka se sleduje PŘESNĚ, dokud je čočka venku, a easuje se jen když je vevnitř.** Venku hloubka nekreslí nic (pose je `Lerp(overview, leaned, 0)`), takže easing by tam znamenal jen **špatný příjezd**: stisk po přejetí míření jinam by se otevřel zakonvergovaný na to, kam hráč mířil dřív, a teprve dojížděl.
2. **Když sonda nic nenajde, fallback je dnešní projekce a NE strop clampu.** Míření vyvezené nad prázdné nebe nemá na co konvergovat, a skok look-atu na 90 jednotek je přesně to cuknutí, kvůli kterému ten easing existuje.

`CONVERGE_TAU` je 0,3 s a **schválně to není `BLEND_TAU`** (0,08 s): náklon je tlačítko, které hráč zmáčkl a chce ho vidět, kdežto hloubka následuje událost, o kterou nikdo nežádal — přejetí siluety.

### Ověření

- **43 tvrzení proti skutečné knihovně** (rozšířená sonda z #384, jejích 27 tvrzení tam zůstalo jako regresní pojistka a prošla): clamp na obou koncích, `DepthToClusterCentre` bit za bitem stará projekce, nová cesta `LensTarget` dopadá tam co stará, obě pravidla easingu výše, **~90 % skoku za 0,7 s (změřeno 90,3 %)**, `Reset`, a hlavně **že při blend 0 se přehledová pose vrací BIT ZA BITEM** — vlastnost, na které stojí „přerušené držení nikdy neškubne".
- **Změřený přínos: chyba kříže vůči dopadu 1,438° → 0,00000°.**
- Čtyři solutiony 0 chyb.

### ⚠ Co se mi ověřit NEPODAŘILO, a proč to říkám takhle natvrdo

**Chtěl jsem A/B ve hře — starý a nový build, týž záběr, oční kontrola proti známé signatuře („duch sedící kousek nad křížem"), jak si to issue výslovně žádá. Nedotáhl jsem to na srovnatelné snímky.** Postavil jsem `main` ve worktree `BS3D-322` a fotil obě binárky týmž harnessem; oba běhy se korektně zaklonily a duch se v novém buildu kříže **dotýká**, kdežto ve starém sedí zhruba o průměr koule níž, což ta signatura je. **Jenže srovnatelné to není**: `GameplayScreen` má neosetý `private static readonly Random RANDOM = new()`, takže fronta má v každém běhu jiné barvy — úvodní klik, kterým se bere kurzor, jednou minul a podruhé sebral tři koule, a shluk se tím rozešel. Kdo na to sáhne, ať **nejdřív vyřeší determinismus** (osít `RANDOM`, nebo najít cestu, jak vzít kurzor bez výstřelu); bez toho je oční A/B v téhle hře anekdota.

**Kvantitativní důkaz je proto sonda, ne snímek.** Uvádím to takhle, protože repo má pravidlo, že uvedené číslo je změřené — a 1,438° → 0° je měřené proti skutečné komponentě, ne odečtené z obrázku.

### ⚠ Provozní: stroj se mi pod tím restartoval, a byla to moje chyba

Devět spuštění hry po sobě bez zeptání, přesně proti tomu, co má majitel zapsané. **Nic se neztratilo** — `Settings.json`, jeho `.bak` i `Progress.json` po restartu hashují bajt za bajtem stejně (atomický zápis #353) a rozpracované úpravy přežily v pracovním stromu. **Majitelova rada, která z toho vzešla a patří do každého dalšího běhu: `fpscap=75`** (jeho monitor je 3840×1600 @ 75 Hz) — *„potom to tak nepadá"*. Oba běhy A/B pak jely s ním a proběhly. Explicitní argument je lepší než řádek „FPS limit: Monitor", protože ten závisí na souboru nastavení, který harness mohl přepsat.

**Nic dalšího si neberu.**

---

## 2026-09-10 — Claude Code (pátý zápis dne)

**#383 hotové na větvi `383-pause-restart`: pauza umí Restart, ne jen Resume.** `PausePage` dostala šestou položku (`Restart` → `Game.RetryLevel`), dva řádky pod Resume a seskupenou s Main Menu/Quit; `docs/game-shell.md`'s výčet položek pauzy (byl zastaralý) opravený.

### Tři rozhodnutí, která issue nechávalo otevřená, a proč takhle

1. **Odstup místo potvrzovacího dialogu.** V repu není nikde žádná Dialog/Popup infrastruktura a Main Menu i Quit — obojí drastičtější než Restart — dnes potvrzení taky nemají. Stavět jednorázovou dialogovou mašinerii pro jediné tlačítko by bylo přesně to, co CLAUDE.md zakazuje. Restart sedí dva řádky pod Resume (tím, co se mačká bez čtení) a vedle Main Menu/Quit, se kterými sdílí sémantiku „konec rozehraného pokusu".
2. **Žádná klávesa.** WASD patří gameplay obrazovce, Retry na výsledkové stránce je taky jen myš/pad, a žádná položka pauzy dosud klávesu neměla. Napsáno jako komentář na místě, ne jen rozhodnuto mlčky.
3. **Skóre: řečeno jednou, ne na obou místech zvlášť.** Restart z pauzy a Retry z výsledkové obrazovky volají doslova tutéž `RetryLevel()` — takže sémantika stojí na jejím doc-komentáři, ne na dvou kopiích, které by se časem mohly rozejít. `RecordLevelResult` běží výhradně z `ShowResultScreen`, kam zahozený pokus po definici nikdy nedojde — takže se restart, ať spuštěný odkudkoli, nezapočítává jako nic.

### ⚠ Co jsem NEověřil naživo, a proč

Chtěl jsem vyfotit pauzu v běžící hře — tenhle stroj je ale ten s neopraveným hard-resetem pod zátěží (viz starší zápisy), takže jsem se zeptal, než bych pustil GPU. Místo focení jsem spočítal řádkový rozpočet ze `MENU_DESIGN_HEIGHT` (2160), `MENU_FONT_BODY` (80) a `MENU_COLUMN_SPACING` (26): nadpis + šest tlačítek vychází kolem **1170 z 2160** jednotek — bezpečná rezerva, a hlavně jiná liga než #138, což byla stránka Nastavení s třinácti řádky, ne tahle. Majitel diff prošel a řekl mergnout na tomhle základě — živé foto tedy chybí a je to poctivá mezera, ne skrytá.

### Ověření

- Game.sln: 0 chyb.
- Žádný jiný soubor v repu položky pauzy nepočítá ani neindexuje pevně — jediné další zmínky byly `docs/game-shell.md`'s výčet, teď opravený.

**Nic dalšího si neberu.**

---

## 2026-09-10 — Claude Code (šestý zápis dne)

**#385 hotové na větvi `385-next-star-cost`: výsledková obrazovka teď řekne, co stojí další hvězda.** `LevelResult.LevelBalls` (nové pole, plněné z `_initialBallCount` přesně tam, kde se dnes bere rating) plus dvě odvozené vlastnosti, `NextStarScore`/`NextStarGap` — `StarRating.Rate`ova aritmetika puštěná pozpátku, -1 jako sentinel na čtyřech hvězdách i na poli bez podlahy (`levelBalls <= 0`, stejná výjimka, jakou má `StarRating.Rate` sama). Nový řádek v `ResultPage`'s breakdown gridu, mezi součtem a poznámkou o zámku dalšího levelu: „Next star at 7 200 (+2 380)", jen pod čtyři hvězdy a jen na CLEARED.

### Dvě otevřené otázky z issue, rozhodnuté

1. **Cíl i rozdíl, ne jedno nebo druhé** — přesně podle mock-upu v issue: na řádku je místo na obojí a hráč se podle každého z nich rozhoduje jinak (jeden říká „zkusit to znovu", druhý „jak moc").
2. **Picker číslo nedostal.** Issue to nechávalo otevřené („may or may not be wanted") — jeho vlastní ask byl výsledková obrazovka, picker je jiná stránka s jinou informační hustotou a čtvrtá zamčená hvězda s číslem pod ní je svébytné UI rozhodnutí, ne přirozený vedlejší produkt týhle změny. Nechávám to jako budoucí a samostatné.

### ⚠ Čísla v mock-upu issue nejsou v notaci, kterou hra používá

Issue píše „28,900" — čárkou. Hra od #284 skupinuje MEZEROU (`ScoreText.cs`: „12 340", ne „12,340") schválně, kvůli konzistenci napříč HUD, popupem a breakdownem. Nová poznámka jde přes `ScoreText.Of` jako všechno ostatní na stránce, takže reálně čte „Next star at 7 200 (+2 380)". Mock byl próza ilustrující tvar věty, ne specifikace formátu — stálo za to si to ověřit v `ScoreText.cs`, než bych ho okopíroval doslova.

### Ověření

- **41 733 kontrol proti skutečné `StarRating`/`ScoreKeeper` knihovně** (sonda ve scratchpadu, žádný solution, `ProjectReference` na skutečný `Prazsky.BS3D.csproj`): swept grid přes 9 velikostí pole × všechna hvězdná pásma × skóre 0–8× podlahy, plus 20 000 náhodných případů (levelBalls 1–999, skóre 0–9× podlahy) — na obojím dvě neměnná tvrzení, `Rate(next) == stars+1` a `Rate(next-1) == stars`, tedy že `NextStarScore` je PŘESNĚ ten práh a ne jen nějaké číslo nad ním. Nula chyb na 41 733 kontrolách. Degenerovaný 0-koulový a záporný `levelBalls` case zvlášť. Test-cesty vlastní čísla (120 koulí, skóre 4820) taky prošla a vyšla `stars=3` — shoduje se s výchozím `testStars=3`, takže se ta věta na focení opravdu ukáže, a ne mlčky schová.
- Game.sln: 0 chyb. LevelGen exit 0, `Game/Levels` beze změny. ScoreSim: „All levels rate the right way round."
- `docs/game-shell.md`'s odstavec o stránky headlinu (byl by jinak zastaralý) aktualizovaný.

### ⚠ Co jsem NEověřil naživo, ze stejného důvodu jako u #383

Živé foto výsledkové stránky jsem nedělal — tenhle stroj je pořád ten s neopraveným hard-resetem pod zátěží. Přidaný řádek je ale jen další `Auto` řádek gridu ve stylu, který `_unlockNote` (o řádek níž) už dokazuje funguje — stejný font, stejná barva, stejné rozpětí přes grid — takže riziko je nižší než dodání šestého tlačítka na pauzu. Stojí za oční kontrolu při příštím hraní, hlavně na levelu blízko hranice hvězdy.

**Nic dalšího si neberu.**

---

## 2026-09-10 — Claude Code (sedmý zápis dne)

**#387 hotové na větvi `387-ci-build-check`: `main` má konečně CI, co se ozve, když nejde postavit.** `.github/workflows/build.yml`, jeden job na `windows-latest`, na každý push (repo nemá PR, takže push JE to, jak merge do `main` vzniká — jeden trigger pokrývá obojí, co issue chtělo zvlášť). Tři `dotnet tool restore` (Testbed/MapEditor/Game, každý svůj manifest), čtyři `dotnet build` (přesně ty čtyři solutiony, co CLAUDE.md jmenuje), LevelGen + `git diff --exit-code Game/Levels`, ScoreSim.

### ⚠ Issue psalo `Game/Levels.json` jako by to byl sourozenec `Game/Levels/` — není

`LevelSet.DefaultFileName` je `"Levels.json"` a bydlí **uvnitř** `Game/Levels/` (`BS3DGame.cs:1214`, `Path.Combine(..., LEVELS_DIRECTORY, LevelSet.DefaultFileName)`), ne vedle něj na úrovni `Game/`. Jeden `git diff --exit-code Game/Levels` pokrývá obojí — všech 111 levelů i pořadí — takže issue's dvě cesty byly ve skutečnosti jedna. Ověřeno v repu (`ls Game/Levels/Levels.json`), ne převzato z popisu.

### To, co issue samo nabízelo jako únik, se nakonec nepoužilo

„Pokud bude content-pipeline build na runneru pomalý nebo nemotorný, postav jen `BS3DLibs.sln` plus nástroje" — nebylo potřeba. Celý čtyřsolutionový build včetně MGCB (MonoGame content pipeline, tři okenní executably) proběhl na `windows-latest` **čistě, 4m13s** — grafický adaptér není pro kompilaci shaderů potřeba (FXC běží softwarově). Plný rozsah, jak issue primárně chtělo.

### ⚠ Node.js 20 deprecation z prvního běhu

`actions/checkout@v4` a `actions/setup-dotnet@v4` byly nucené běžet na Node 24, i když cílí Node 20 — neškodné teď, ale zbytečné dědictví hned od prvního commitu. Povýšeno na `@v7`/`@v6` (aktuální major tagy, ověřeno přes `gh api .../releases/latest`), druhý běh bez anotace.

### Ověření — a tohle byl ten důležitý kus

Dva zelené běhy na GitHubu samy o sobě nedokazují, že kontrola něco chytá — zelená, co nikdy nemůže zčervenat, není kontrola. Na zahazovací větvi (`throwaway-ci-negative-test`, založené na `387-ci-build-check`, smazané po testu na obou koncích) jsem ručně přepsal `Cairn.json`'s `"sky": 10` na `3` — hodnotu, kterou LevelGen sám tiskne za běhu (`block 11/11 'The Mirage' Dream, sky 10`), takže jde jistě o generovaný, ne ručně psaný kus. Push, reálný běh, a **spadl přesně na kroku "Regenerate levels and check they match what is committed"** — ne dřív na buildu, ne později na ScoreSimu (`gh run view … --json jobs -q '…conclusion=="failure").name'` řekl který). To je ten jediný skutečně nový kus logiky v celém workflow; zbytek (build padá na chybu, `git diff --exit-code` padá na rozdílu) je chování, na které se dá spolehnout, aniž by to tenhle projekt musel dokazovat znovu.

### Co jsem NEudělal

Žádné required-checks pravidlo ani branch protection — issue to výslovně nechce („nic tu nemá stavět bránu před majitelovy vlastní merge"). Žádné cachování NuGetu — nebylo žádané a čtyři minuty jsou levné.

**Nic dalšího si neberu.**

---

## 2026-09-11 — Claude Code

**Beru si #386.** `Tools/LevelGen/Program.cs` (dnes 17 511 řádků) se rozpadne na `partial` třídu: návrhy po blocích do `Designs/Block01_Meadow.cs` … `Designs/Block11_Mirage.cs`, pomocníci, které používají návrhy víc než jednoho bloku, do `Designs/Shared.cs`, a v `Program.cs` zůstane orchestrace, `Emit` s branami a `Design`. Kam co patří, rozhoduje graf referencí ze sémantického modelu Roslynu, ne odhad. Jeden commit, který kód jen přesouvá; důkazem je kampaň přegenerovaná bajt za bajtem a nové soubory složené zpátky do původního. Majitelův cíl, ke kterému to má vést: **univerzálně použitelný generátor, který umí dělat nové originální levely** — proto dělím podle toho, co je obecné (typ `Design`, brány, sdílený slovník tvarů a barev), a co je jedna konkrétní kampaň.

**Prosím do merge nesahat na `Tools/LevelGen` ani `Game/Levels`.** Přesun je skriptovaný a nad novějším `main` se dá zopakovat, ale rozpracovaný návrh by se pak musel přenášet ručně. Nic dalšího si neberu.

---

## 2026-09-11 — Claude Code (druhý zápis dne)

**#386 hotové a na `main` (`185cb0a`): `Tools/LevelGen/Program.cs` je `partial` třída rozdělená po blocích.** `Program.cs` je generátor sám — `Main`, tabulky bloků, set a jeho odemykací rampa, `Emit` se všemi branami a typ `Design` — a má místo 17 511 řádků 2 294. Návrhy každého bloku s pomocníky, které používá jen ten blok, jsou v `Tools/LevelGen/Designs/Block01_Meadow.cs` … `Block11_Mirage.cs`, pomocníci, které používají návrhy víc než jednoho bloku, ve `Designs/Shared.cs`. Dva commity schválně: `c8fb89e` kód **jen přesouvá** (dá se revidovat výstupem), `167906a` opravuje, co tím přestalo platit, a dopisuje rozvržení do `docs/formats-and-tools.md`. **Zámek z prvního zápisu dne tímhle padá** — na `Tools/LevelGen` a `Game/Levels` se zase dá sahat.

### Kam co patří, rozhodl graf, ne jména

Odhozený nástroj v Roslynu (scratchpad, žádný solution) spočítal ze sémantického modelu pro každého z 1 539 členů třídy, návrhy kterých bloků ho tranzitivně používají: jeden blok → soubor toho bloku, víc bloků → `Shared.cs`. Regiony se stěhovaly celé, kde se jejich členové shodli; vnější `#region The designs` po vyprázdnění zmizel a to jsou jediné tři zahozené řádky. **Graf opravil i samotné issue:** to tipovalo jako sdílený `Picture` — ten je jen Galerie; sdílený je `PixelAt`, který čtou i Arcade a Reveal. Sdílených je 14, mezi nimi `ONE_WALLS`, které si Mirage (Trefoil, Diadem) bere od One celé. Tři konstanty Quarry (`HOPPER_*`) ležely v regionu geometrie Reveal a šly domů. Bloková tabulka (`BLOCKS`, `MUSIC_*`, `BALLS_*`) zůstala pohromadě v `Program.cs`, přestože každá konstanta patří jednomu bloku — je to tabulka, jejíž komentáře argumentují celou sadou najednou (reprízy, jeden materiál na kapitolu).

### ⚠ Past `partial` třídy: pořadí statické inicializace mezi soubory je nespecifikované

Kdyby inicializátor v jednom souboru četl `static readonly` pole z jiného, může dostat `null` a nic to neohlásí. Změřeno před splitem: 166 statických polí má inicializátor, jen **čtyři** čtou jiné pole (`BOLT_TIER_ODD`/`_EVEN` → `VOLT`, `PLEAT_TIER_SHIFT` → `AURORA`, `CUBE_GLYPHS` → pět `CUBE_*`) a každé čte pole deklarované dřív ve stejném bloku — skončily ve stejném souboru ve stejném pořadí. **Pravidlo je teď v hlavičce `Program.cs` i v dokumentaci**, protože nové návrhy přidávají `static readonly` tabulky pořád a `CUBE_GLYPHS` ukazuje, že i tabulka z tabulek je normální.

### ⚠ `git blame` se o přesun zarazí, pokud se mu neřekne jinak

Výchozí Myers diff na souborech téhle velikosti přesun nenajde: `git blame -C -C` nechá na split commitu **1 550 z 1 553** řádků Eruption (a `-C -C -C` taky). S `--diff-algorithm=histogram` nebo `--minimal` (nebo jednou nastaveným `diff.algorithm=histogram`) tam zůstane **12** — přesně nová hlavička a patička souboru. Kdo bude hledat historii návrhu sahající před 11. 9., potřebuje tohle.

### Ověření

- **Nezávislý ověřovač**, který neví nic o tom, jak se dělilo, puštěný i proti stagnutým blobům: `Program.cs` = původní řádky 1–968 a 16194–17511 bajt po bajtu plus `partial`; těla nových souborů = původní řádky 969–16193, řádek po řádku a v rámci souboru v původním pořadí, bez přesně těch tří deklarovaných. Povrch třídy stejný (1 540 členů, signatury i hodnoty konstant). Splitter dvakrát po sobě zapsal bajtově totéž.
- **LevelGen: celý stdout (2 040 řádků) a všech 111 zapsaných souborů bajtově shodné s během před splitem**, po obou commitech; `Game/Levels` beze změny. ScoreSim „All levels rate the right way round". `Game.sln` 0 chyb, varování stejná (CA2014 se jen posunulo, dnes `Program.cs:1605`). **CI na větvi zelené** (běh 34649374507: čtyři solutiony, přegenerování + `git diff --exit-code`, ScoreSim) a merge je strom bit za bitem ten, na kterém prošlo.
- Každý nový soubor má jen `using`y, které potřebuje (CS8019 z kompilátoru). `Program.cs` o jeden přišel (`Prazsky.Core.Tools`) až v druhém commitu.

### Co jsem po přesunu opravil, protože to přestalo platit

Čtyři komentáře říkaly „this file" a myslely celý generátor; jeden z nich (Mirage: „every other gate in this file passes it") byl po přesunu **nepravdivý**, brány zůstaly v `Program.cs`. Dva ve `Shared.cs` říkaly „this block" o pomocnících, které dnes sdílí čtyři bloky. Hlavička `Program.cs` přestala tvrdit, že nástroj píše „pattern levels (Three to Seven)". Hledal jsem to heuristikou (poziční slovo + jméno člena, který teď bydlí v jiném souboru): 375 kandidátů, 78 po zúžení, šest skutečných.

### ⚠ Provozní chyba, kterou jsem udělal a nic nestála

**`git merge -F -` nečte zprávu ze stdinu** (na rozdíl od `git commit -F -`) — spadne na „could not read file '-'" a merge se neprovede. Můj řetězec pokračoval přes `;`, takže **smazal vzdálenou větev dřív, než byla mergnutá**. Nic se neztratilo: `git branch -d` správně odmítl smazat lokální větev, protože mergnutá nebyla, a merge jsem zopakoval se zprávou ze souboru a se `set -e`. Pro příště: zprávu merge commitu dávat přes `-F <soubor>` nebo `-m`, a řetězec, který končí mazáním větve, psát tak, aby se na první chybě zastavil.

### Co zůstává

- **Majitelův cíl, ke kterému tohle směřuje: univerzálně použitelný generátor, který umí dělat nové originální levely.** Rozdělení to umožňuje, samo nic nevymýšlí. Navrhl jsem majiteli další krok: režim, který skládá nové `Design`y ze sdíleného slovníku (tvary, barvení, druhy koulí) a nechá existující brány, sag sondu a ScoreSim rozhodnout, které kandidáty jsou levely — do scratch adresáře ke screenshotům, nikdy rovnou do `Game/Levels`.
- `CA2014` (`stackalloc` ve smyčce) nechávám stát — přesun kód neměnil a oprava patří jinam.
- `#region Colour helpers` ve `Shared.cs` drží i geometrii (`Centred`, `Untwist`, `WrapAngle`…); nesedělo to už v původním souboru, přesun jen zachoval název.
- `Program.cs` začíná čtyřiceti BOMy za sebou (objevily se v jednom commitu po `85b40fb`, 19. 8.); kompilátoru to nevadí a nesahal jsem na to.
- `SceneRenderer.cs` (5 500 řádků) issue výslovně nechává na potom.

**Nic dalšího si neberu.**

---

## 2026-09-13 — Claude Code

**Tři věty v dokumentaci, které kampaň mezitím přerostla** (větev `docs-bombs-in-shipped-set`, bez issue). Majitel se ptal, jestli existuje mapa s bombou; při hledání vyšlo najevo, že dokumentace pořád tvrdí opak.

- `docs/formats-and-tools.md`, parita sag sondy u #326, končila *„Nothing in the shipped set has bombs in it… the parity is in place for the day one ships."* Ten den byl **7. 9.** (#368/#369) — tedy šest dní po napsání té věty. Kampaň veze **Vent (8 bomb), Sill (5) a Paroxysm (6 + 8 zapů)**, takže sag odpovědi, proti kterým se ty tři levely ladily, pocházejí ze sondy, která odpaluje. Parita už není pojistka do budoucna, ale nosný prvek.
- Tamtéž, kaveát formátu u klíče `"k"`, tvrdil, že soubory kampaně žádné druhy nenesou. **Nese je patnáct ze sto deseti** — rock (Anvil, Cairn, Keystone, Obsidian, Seam), sklo (Diadem, Facet, Harlequin, Solitaire, Trefoil), bomba (Vent, Sill, Paroxysm), zap (Fume, Caldera, Paroxysm). Sečteno z `Game/Levels`, ne odhadnuto.
- `docs/game-session.md` u sazby za zničenou kouli říkal, že `ScoreSim` odpoví, *až* nějaký level s bombami vyjde. Vyšel — **a sazba je pořád neměřená z jiného důvodu**: ScoreSim pokládá každý zásah jako `keeper.Landed(balls, 0)` (`Tools/ScoreSim/Program.cs:166` a `:198`), takže `destroyed` zůstává na defaultní nule a model ty tři levely hraje, jako by v nich bomby nebyly. Změřit to znamená naučit ten model výbuch, ne čekat na level.

### ⚠ Past, kterou tohle ilustruje

Zastaralá věta se neopravuje přepsáním na opak. První dvě byly prostě nepravdivé, ale třetí měla **správný závěr ze špatného důvodu** — kdybych ji jen otočil („už změřeno"), zapsal bych do dokumentace nepravdu, kterou nic v repozitáři nevyvrací. Než jsem ji přepsal, ověřil jsem, co ScoreSim dnes opravdu volá.

### Ověření

- ScoreSim puštěn dnes: *„All levels rate the right way round."* Vent/Sill/Paroxysm jsou v běhu vidět, takže verdikt platí i s nimi — jen o samotné sazbě za blast nevypovídá nic.
- Datum #369 (`f1115d9`, 7. 9.) i datum té věty (`459cc15`, 1. 9.) jsou z gitu, ne z paměti; první formulace tvrdila „eleven days" a byla škrtnuta před commitem.
- Žádný kód se neměnil, takže nic se nestavělo.

### Co zůstává

- **`ScoreKeeper.DestroyedBallPoints` je pořád neměřená sazba.** Buď se ScoreSim naučí `destroyed` (nejspíš v jednom z těch průchodů jako podíl zásahů, protože přesně na tohle je ten nástroj), nebo se to přizná jako trvalá mezera. Majiteli to nenavrhuji jako hotové — je to issue, které nikdo nezaložil.

---

## 2026-09-13 — Claude Code (druhý zápis dne)

**Beru si #333 (Heavy).** Desátý a poslední speciál z #256 — ostatních devět je na `main`u, takže tímhle se to trackovací issue zavírá. Větev `333-heavy`.

**Pořadí prací mi diktuje samo issue a nemíním ho obracet:** nejdřív se rozhodne, co s **sag sondou**, teprve potom se staví hmota. Těžká koule je *záměrný* průvěs na projektu, jehož vlastní brána levely s průvěsem zahazuje; kdybych stavěl hmotu první, první level s mechanikou spadne na vlastní bráně a pokušení bude povolit práh všem. Ten práh se povolovat nebude — sonda se naučí druh, nebo se level měří proti vlastnímu baseline.

**Z čeho čerpám:** #332 (gravitační studna) je nejbližší předloha — taky sahá do kroku simulace — a jeho zápis výš nese čtyři pasti měřicího rigu, které platí i tady. `BALL_MASS` je dnes jedna hodnota pro celý cluster (`BallsConstraintsBuilder.cs:100`, jedna `BodyInertia` pro všechna tělesa), takže per-ball setrvačnost je první skutečná změna.

**Jak chci ověřovat stabilitu, a proč ne okem:** poměr hmot má někde na škále útes a issue chce, aby se **našel**, ne odhadl. Sag sonda věší cluster v reálné Bepu simulaci **bez grafiky**, takže poměr i krok integrace se dají projet po mřížce v konzoli a odečíst čísla — to je silnější důkaz než dívání se na obrazovku a nepotřebuje to dlouhou GPU seanci (majitelovo „desktop se pod zátěží tvrdě resetuje" platí). Na obrazovku se půjde až s vybraným poměrem.

**Rozsah:** staví se **průvěs**, ne trhání. Issue to samo doporučuje a důvod je, že trhání je práh nad průvěsem, ne druhý mechanismus.

**Nic dalšího si neberu.**

---

## 2026-09-13 — Claude Code (třetí zápis dne)

**#333 (těžká koule) je na větvi `333-heavy` (`b48236a`), NEmergnuto: čeká na majitelovo oko.** Desátý a poslední speciál z #256. `BallKind.Heavy = 10`, `HEAVY_MASS_RATIO = 12`, dvanáctý region kbelíků a technika `InstancedModelHeavy`, brána v LevelGenu, atribuce v sag sondě, `--sagfile=` a dvě testovací mapy.

### Jádro: je to hmota, ne pravidlo

Druh se dotkne **jediného řádku hry** — `BodyInertia`, kterou tělesu dá `BuildBallsStructure`. Kontaktní handler, match rule, uvolňovací cesta ani skórování o něm nevědí. Proto jako jediný z deseti nepotřeboval **nic** zopakovat v `SagProbe` (#329 stálo nula řádků, tohle nemá ani ten krok), a proto zpětná vazba nic nestojí: větev pod ním visí níž a to je celé vysvětlení mechaniky.

### ×12 je změřeno a útes je STRETCH, ne průvěs

Odhozený rig ve scratchpadu (`MassRig`, bez grafiky) věší dvě fixtury v téže Bepu simulaci: holý pramen (7×7 deska, osm koulí pod ní, těžká na špičce — nejvíc zátěže, co jeden řetěz soketů kdy nese) a skutečný level s nejnižší koulí těžkou. Rozhodující číslo není deflexe, ale **nejdelší vzdálenost dvou spoutaných sousedů**: skutečný cluster samých obyčejných koulí má **1,044**, pramen dává 1,027 při ×12, 1,031 při ×20, pak 1,057 (×25), 1,083 (×35), 1,154 (×60) a **1,402 při ×100 — viditelně roztržená mřížka**. Hustý level usne při každém poměru do ×100 a **přestane usínat při ×200**. ×12 je tedy faktor dva pod deformací a řád pod rozpadem; koupí **0,46 jednotky** průvěsu na prameni a **0,372** na dodávané testovací mapě. Táž ×12 uvnitř hustého clusteru pohne koulí o 0,02 a je neviditelná — to je druh, který se chová správně, a důvod, proč se hlubší průvěs dělá **zavěšením většího břemene, ne zvýšením konstanty**.

### ⚠ První verze rigu měřila fázi, ne polohu

Nic tu netlumí (integrátor přičítá gravitaci a nic víc), takže zavěšená mřížka kmitá, dokud ji nezhasne měkkost řešiče a Bepu neuspí. Snímek v pevné 4. sekundě proto čte **fázi**: první tabulka ukázala průvěsy, které s rostoucí hmotou zase **stoupaly**. Oprava: běžet, dokud cluster neusne (to *je* otázka stability), a když neusne, průměrovat výkyv místo vzorku.

### ⚠ Dvě chyby ve vzhledu, obě odhalila až fotka

1. **Samotné ztmavení nestačí.** Odstín přežije beze změny, takže těžká žlutá se vyfotila jako **čokoládově hnědá koule** — obyčejný Type10 se zhasnutými světly. Odsycení je osa, po které se „kov" pozná; mramor to má v hlavičce napsané už dávno a já to objevoval znovu.
2. **`SurfaceSpecular.Highlight`/`.Environment` jsou NÁSOBKY toho, co dostane každá jiná koule** (1 = beze změny). Napsal jsem 0,86 a 0,80, tedy si vyžádal **míň světla než obyčejná koule**, a vyfotilo se to jako matný plast. Leštěný odlitek zvedá oblohu: 1,30 a 1,25.
3. **Zrno odlitku jsem vyhodil.** Tři síly, a ani na jedné nebylo na herní vzdálenost vidět; při nejsilnější se z jedné koule opodál četlo jako **fasety**, ne jako písek. Figura, která se projeví jen jako artefakt, je horší než žádná a platí se za ni na každém pixelu. Nahradil jsem ji **dělicím švem formy** (`SeamLine`, jeden prstenec, vystouplý) — tvrdá čára přežije i šířku jednoho pixelu a pak se sama vytratí.

### ⚠ Past, kterou mám v paměti a stejně jsem do ní spadl

Tři konstanty v `.fx` jsem změnil přes `python … io.open(encoding='utf-8-sig')` a **přidal tím souboru BOM** (diff skočil na „152 insertions, 1 deletion"). Opraveno binárně, `git diff` je zase čistý přírůstek. Na sledované zdrojáky patří Edit/Write, ne shell.

### Sag sonda: atribuce, ne tolerance

Issue se ptá, co dělat s bránou, která hlásí průvěs, když je průvěs záměrný. **Ani práh, ani čára smrti se nehnou** — větev pod čarou level prohrála bez ohledu na to, co autor zamýšlel. Přidal jsem druhé pověšení s **neutralizovanou hmotou** (`SagProbe.HeavyBaseline`; hmota je celý druh, takže přepsání `Heavy`→`Normal` *je* tentýž level s vypnutou mechanikou) a brána tiskne obě čtení i rozdíl: sedá-li i baseline, vinen je **layout**; sedá-li jen zatížený běh, návrh **pověsil na těžkou kouli moc**. Číslo psané rukou vedle levelu (druhá nabídka issue) jsem odmítl — zastará při první změně návrhu, druhý běh téže simulace ne.

**Vyzkoušeno na skutečném levelu**, protože ručně stavěné prameny se vyčistí dřív, než se stihne projevit hang: Pylon se čtyřmi nejnižšími koulemi těžkými čte `sagged 1 of 5, closest −1,00` proti baseline `2 of 5, −1,04` → hmota stojí **−0,04** clearance, což je uvnitř vlastního šumu sondy, takže verdikt je „layout". Na to byl potřeba `--sagfile=<cesta>`, který věší **soubory** mimo kampaň (set *je* kampaň; do té doby se level na zkoušku mechaniky nedal sondě předhodit jinak než úpravou kampaně, kterou příští běh přepíše).

### Ověřeno

- Čtyři solutiony 0 chyb; **LevelGen exit 0 a `Game/Levels` beze změny**; ScoreSim „All levels rate the right way round".
- Poměr přeměřen při **dt 1/240, 1/120 a 1/60 s**: do ×30 se shoduje na ~0,01 jednotky. Hra stejně krokuje pevných 1/120 z akumulátoru, takže obnovovací frekvence na řešič nedosáhne — sweep je rezerva, ne mechanismus.
- **Cena snímku: žádný per-step průchod nepřibyl**, takže není co měřit; issue ji chce pro případ, že by trhání přidalo procházení impulsů, a to se nestaví.
- Vyfoceno v Testbedu: všech třináct odstínů (scratch mapa) a A/B mapa `Testbed\Maps\Heavy.json` — čtyři stejné prameny, dva se závažím.

### Doměřeno s grafikou (na majitelův dotaz, tentýž den)

**Testbed je na tohle nástroj právě proto, že krokuje jinak než hra: jeden krok za snímek o délce snímku.** `fpscap=` je tedy **číselník dt** — u hry by akumulátor držel 1/120 bez ohledu na cap. Táž A/B mapa, kamera pod clusterem (prameny stojí proti čisté obloze, takže se dno dá segmentovat jedním kanálem: obloha má B ≥ 219, zelená koule 64, odlitek 32), **bez střelby**, sedm snímků přes ustálené okno, měřítko **38 px na jednotku**:

| režim | medián snímku | průměrný pokles | v jednotkách |
|---|---|---|---|
| `fpscap=120` | 9,3 ms (herní krok je 8,3) | 11,1 px | **0,331** |
| `fpscap=20` | 50,0 ms (šestinásobek) | 15,0 px | **0,387** |
| kontrolní mapa | — | −1,4 a +0,3 px | **−0,038 / +0,008** |

Bezgrafický rig říká **0,372**. Takže **průvěs na obrazovce je ten, co změřil rig, a při šestinásobném kroku neuteče**; kontrolní mapa (tytéž čtyři prameny bez závaží) drží obě pásma v rovině na čtyři setiny jednotky, což je šum přístroje.

**Stabilita, tentokrát dívánm:** šest ran během 42 s při `fpscap=20` — cluster celý, žádný třes, nic neuletělo. A ve **hře** (`BS3D.exe levelfile=`) při **2–15 FPS** na `quality=high`, tedy s akumulátorem trvale na stropu `PHYSICS_MAX_STEPS_PER_FRAME` a světem běžícím ve zpomaleném čase, level hraje a odlitky visí normálně až do konce běhu. Pomalý snímek stojí hru **čas, ne stabilitu**.

### ⚠ Jeden snímek tohle změřit neumí a první grafický průchod na to doplatil

Cluster se houpe: přes sedm snímků jednoho režimu šel pokles **−2 až 24 px** kolem průměru 11 px, takže jediný snímek přečte cokoli od nuly po dvojnásobek. Je to varování z palety v `.claude/skills/screenshot` — „jeden snímek neurovná rozdíl pod ~10 dE" — jen v geometrii místo barvy. **A druhá chyba téhož průchodu:** střílel jsem před focením, takže každý pozdější snímek držel koule, které tam přistřelily rány. To je jiný cluster, ne jiná fáze houpání; průvěs je vlastnost **nerozhoupaného** zavěšení.

### ⚠ A první verze segmentace četla jako kouli celou oblohu

Porovnával jsem každý pixel s oblohou **daleko vlevo** ve stejném řádku. Kopule je ale gradient i **napříč** snímkem, takže reference 600 px stranou je sama o sobě větší než práh a všech 31 snímků vyšlo „dno v posledním řádku". Vzorkování ukázalo, že modrý kanál odděluje oblohu od koulí sedmdesáti kódy — reference není potřeba vůbec.

### Co zbývá a proč to nemergnuju

- **Vkus majitele na vzhled odlitku** a případně vlastní pohled na to, jak se to hraje. Měření výše zodpovědělo, co issue chtělo číselně; „jak to vypadá v ruce" je věc, kterou za majitele neudělám.
- **Vzhled je věc vkusu majitele.** Odlitek se dnes čte jako tmavá hutná koule své barvy se švem; pokud má být kovovější, je to jedna konstanta (`HeavyEnvironment`), ne přestavba.
- **Trhání se nestaví** (záměrně, viz claim výše). Až bude, je to práh nad tímhle.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code

**Prstence gravitační studny se rozkládají přes kotouč, ne do okraje.** Větev `gravity-rings-wider` (`53cb4a3`) z `origin/main`, samostatně od #333.

**Majitelovo hlášení:** „modré pruhy jsou příliš úzké — z větší dálky skoro neviditelné." Šířka byla ta menší polovina problému.

### Příčina: parametr, ne šířka

Prstence byly rozložené v **grazing členu** `1 − dot(normal, eye)`, jenže nakreslený poloměr koule je `sin θ`. Rovnoměrně v tom členu tedy znamená **natlačeně k limbu**: při třech prstencích seděly na **55 %, 87 % a 99 %** poloměru a každý další byl tenčí. Vyfoceno na 14, 26 a 40 jednotkách po pěti fázích (prstence se pohybují, jeden snímek fotí fázi): za zhruba dvaceti jednotkami zbyl jediný tenký srpek na okraji a na čtyřiceti byla studna plochá fialová koule.

**Oprava:** parametr je poloměr kotouče, `sqrt(1 − facing²)` z téhož skalárního součinu (nestojí nic navíc), počet **2 při šířce 0,42** místo 3 při 0,30, zisk 0,85 → 1,0, aby širší pás nečetl měkčeji než tenký. Nic jiného se nehnulo — pohyb je pořád dovnitř, barva, čočka, ztmavení okraje i band-limit zůstávají.

### Co měření umí a co ne

Kontrast figury (sm. odchylka jasu přes pixely studny, průměr přes fáze): **19,8 → 23,3** na 26 jednotkách a **18,9 → 20,5** na 40. ⚠ **Ale číslo neoddělilo obě kandidátky:** prosté rozšíření (2 prstence, 0,42, bez změny parametru) skórovalo stejně, takže volbu mezi nimi rozhodl obrázek — jen tahle drží terč **uvnitř** kotouče i na 40, druhá má pořád jen lem. Majitel vybral tuhle.

### Nerozbliká se

Pět **po sobě jdoucích snímků** (`shotframe=`) na 40, 60 a 80 jednotkách: figura dobíhá na band-limitu bez crawlování. Studna se mezi snímky mění zhruba dvakrát víc než obyčejná koule v týchž snímcích (12–16 kódů proti 7–9) — to jsou **prstence v pohybu, ne speckle**; zvětšeno osmkrát je vzdálená studna hladká koule, na které už žádná figura není. Reference obyčejnou koulí je tu podstatná: samotné číslo „14 kódů mezi snímky" neříká nic.

### Ověřeno

- Testbed, MapEditor i Game 0 chyb (shader staví všechny tři).
- Fyziky se to netýká vůbec — `GravityWells.RANGE` ani síla se nehnuly, takže sag sonda ani ScoreSim nemají co říct.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (druhý zápis dne)

**Obě větve, které čekaly na slovo, jsou na `main`u:** `333-heavy` (`83e7b5b`) a `gravity-rings-wider` (`8ccf014`), obojí `--no-ff`, na majitelův pokyn. Zápisy výše zůstávají stát tak, jak byly psané — včetně vět „NEmergnuto" a „čeká na majitelovo oko", které tímhle přestaly platit. Obě větve smazané lokálně i na originu; při té příležitosti šly pryč i dvě staré lokální (`380-editor-all-scenes`, `381-neighbour-cells-without-enumerator`), dávno mergnuté, jejichž vzdálené protějšky už neexistovaly. Repozitář má teď na originu jedinou větev, `main`.

**Šev mezi nimi byl skutečný a je ověřený:** obě sahaly do `InstancedModel.fx` (#333 přidává techniku na konec, studna mění konstanty a tělo `GravityPS`) a obě psaly na konec tohohle žurnálu — textově kolidoval jen žurnál, vyřešeno zachováním obou zápisů v pořadí podle data. Po mergi: **čtyři solutiony 0 chyb**, **LevelGen exit 0 a `Game/Levels` beze změny**, ScoreSim „All levels rate the right way round", a v shaderu stojí obě změny vedle sebe (`InstancedModelHeavy` i `float across = sqrt(...)`).

**#333 tím zavírá #256** — deset speciálů z rozpadu je hotových. Zavření obou issue jsem nechal na majiteli.

---

## 2026-09-14 — Claude Code (třetí zápis dne)

**Beru si #222 (polární led).** Osmnáctá scéna: plochý ledovcový příkrov — závěje a sastrugi, ledovcová čela s trhlinami a nad tím **nízké slunce**. Větev `222-polar-scene`.

**Co scéna je a co není:** issue samo píše „led, rozloha a nízké slunce jsou ta scéna". Beru tedy **scénu**, ne kapitolu — žádných pět návrhů do LevelGenu, žádné zařazení do kampaně a žádné zvukové pozadí; to je práce, která patří k bloku, ne k backdropu (scén je dnes sedmnáct a kampaň jmenuje jedenáct, takže scéna bez bloku je normální stav).

**Z čeho čerpám:** pouštní scéna je pojmenovaný vzor pro pevný terén (mřížka `CreateGridMesh` posunutá ve vertex shaderu, **normála po pixelech z gradientu výškového pole** — to je ta lekce o Machových pruzích, kvůli které se poušť mohla vrátit), moře umí Fresnelův odraz oblohy v uzavřeném tvaru a podpovrchový rozptyl, koule nesou `TranslucencyStrength`. Materiál ledu je právě tohle složené dohromady: **bílá v odrazu, azurová v průsvitu**.

**Na co si dát pozor** (issue to pojmenovává a scenes.md to potvrzuje): ⚠ nesmí to číst jako „hory, akorát placaté" — hory jsou kotlina s reliéfem a sněžením, tohle je rovina a materiál; a ⚠ **past ACES kontrastu** u bílé plochy — celobílé pole se slije do jednoho tónu, pokud barva stínu a průsvit nenesou skutečné oddělení.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (čtvrtý zápis dne)

**#256 a #333 zavřené na GitHubu — kód byl hotový, chybělo jen zavření.** Majitel požádal o kontrolu #256. #323–332 byly CLOSED, ale #333 (těžká koule) běžela dál jako OPEN, přestože je od `83e7b5b` na `main`u — přesně ten stav, který zápis výš ("druhý zápis dne") nechal na majiteli: "zavření obou issue jsem nechal na majiteli." Žádný kód se neměnil, jen ověření (`BallKind.cs` nese všech deset druhů s plnou dokumentací, deset merge commitů #324–333 je v historii `main`u) a dvě `gh issue close` s komentářem, co bylo změřeno a ověřeno.
---

## 2026-09-14 — Claude Code (pátý zápis dne)

**#222 (polární led) je na větvi `222-polar-scene` (`10b442b`), NEmergnuto: čeká na majitelovo oko, protože je to scéna a ta se schvaluje pohledem.** Osmnáctý `SceneKind`, `Polar.fx` + `PolarSceneConfig` na strojovně pouště, vlastní kopule, ambience, viewpoint, JSON diskriminátor, tři content projekty.

### Tři opravy, které si vynutila fotka

Každá je zapsaná tam, kde vznikla — tohle je jen jejich seznam:

1. **Sastrugi jsou POVRCH, ne terén.** Postavené jako výtlak v měřítku, které sastrugi mají (jednotky metrů), padly na zhruba jednu buňku mřížky, takže je síť neudržela a pole se vyfotilo jako hrubé duny s fasetami. Ledovcový příkrov **je** placatý; co oko čte, je textura. Geometrie nese jen dlouhé vlny a čelo, sastrugi jsou perturbace normály — přesně dělba, kterou má poušť mezi dunami a vlnkami.
2. **Čelo jsou KRY, ne hřeben.** Vyhlazené se vyfotilo jako **řada lámajících se vln** — což je to, co jakýkoli hladký hřbet pod azurovým materiálem čte, a jediná věc, kterou si tahle scéna nemůže dovolit (zamrzlé moře je v oku hned vedle). Tlakový led je rubanina: pás je kvantovaný na desky, každá zvednutá vlastním hashem.
3. **A je to FRONTA, ne prstenec.** Zavřený dokola četl jako zeď arény a scéna přestala být rozlohou, což je přesně to, co issue chce nade vše. Teď kryje výseč a zbytek kruhu je otevřený led k obzoru.

### ⚠ Past bílé plochy (ACES) se neřeší jedním číslem

Celobílé pole se slije do jednoho tónu. Drží ho tři věci **dohromady**: albedo sněhu není 1, barva stínu je barva **oblohy** (a tedy modrá), a sastrugi mají vlastní stínování prohlubní nezávislé na slunci. To třetí je nosné, ne dekorace: při **vysokém** slunci má plochá pláň skoro konstantní `ndotl`, takže reliéfní člen neříká nic — při ±0,12 se pole vyfotilo jako list bílého papíru. Je ±0,42.

### Kopuli vybrala fotka, ne vkus

Obsah scény je materiál a materiál ukazuje to, co na něj svítí — takže kopule s touhle scénou hýbe víc než s kteroukoli jinou. Vyfoceny čtyři: **11** (slunce 55°) polární poledne, ale sastrugi drží jen stínováním prohlubní; **13** (13°, tyrkysový obzor do indiga) **ta pravá** — nízké slunce hrabe sastrugi do reliéfu, čelo svítí azurově, trhliny čtou jako štěrbiny světla; **16** (4°, krémový obzor nad skoro černým zenitem) led vezme teplé světlo a čte **zlatohnědě**, což skutečný příkrov při západu dělá, ale tahle scéna to není; **17** (42°) bílá na bílé, azurová nevystřelí vůbec, protože průsvit potřebuje slunce **za** ledem. Scéna si tedy říká o 13 v Testbedu i ve hře. Obecné pravidlo: **nízké slunce a studený obzor**.

### Cena a jedna hloupost v ní

Proti poušti (táž kamera, ssaa 2, 1600×900, `fpscap=400`, mediány z 24 oken): **polar 30,49 ms proti desert 28,26**, tedy **+7,9 %**. Bylo to 31,70, dokud `CrevasseField` vzorkovalo tlakový hřbet samo: trhliny potřebují vědět, kde je pláň napjatá, takže jedno vyhodnocení výšky stálo dva hřbety, tři tapy na normálu šest a pixel shader další dva. Předání hodnoty dolů srazilo pixel z dvanácti hřbetů na čtyři a ušetřilo 1,2 ms.

### Co jsem si nevzal a je to napsané i v docs

Pět návrhů do LevelGenu a místo v pořadí kapitol (scéna není kapitola — jedenáct z osmnácti scén jmenuje nějaký level, backdrop bez bloku je normální stav), létající diamantový prach (na zemi jiskření je) a polární noc s polární září, kterou vlastní #205.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (desátý zápis dne)

**#205: svislý šev v auroře při otáčení kamery — potřetí stejná chyba (`6f3954a`).** Majitel: *„Vidím výrazný svislý lem na auroře. Podobný problém se řešil i u jiných scén. Ach ty švy 2D plochy."* Měl naprostou pravdu a dokonce trefil, kde to hledat — `docs/scenes.md` už jednou popisuje přesně tuhle třídu chyby u pouštních trhlin Marsu: *„co rib count vzatý z `atan2` bearing neumí … chyba, se kterou se jednou dodala žíly jeskyně."* `atan2(dir.x, dir.z)` skáče o 2π na jedné pevné čáře ve SVĚTĚ (ne s kamerou) a šev do noise funkce vjel s ním — stál na místě a kamera ho přejela.

**Oprava: úhel se nepočítá vůbec.** `AuroraDriftSpeed` teď točí SMĚREM kolem svislé osy místo přičítání k azimutu (otočený vektor nemá kde přeskočit), stuhy jsou `Fbm3` na tom otočeném směru se svislou osou stlačenou `AuroraCurtainWarp` (Fbm2Combed's protažení, ve 3D). 3D noise na směru nemá pod sebou žádnou 2D mapu, na které by mohl mít šev.

**Ověřeno přímo v problémovém směru** (kamera mířící přesně na −Z, přesně tam, kde `atan2` skáče) a z druhého, kolmého úhlu — čisté oboje. Tři exe stavějí. Cena přeměřená: 710–728 FPS proti dřívějším 758–762 — reálný, ne dramatický pokles (3D noise stojí víc na oktávu než 2D), zapsáno jako změřené, ne odhadnuté.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (devátý zápis dne)

**#205: zmenšená listnatá koruna z minulého zápisu byly houbičky (`fec97bc`).** Majitel po dalším screenshotu: *„Na scéně jsou taky nějaké stromečky, co vypadají jako malé houbičky s hnědou nohou a zeleným kloboukem."* Přesně to to bylo — kmen nechaný na denní výšce, koruna zmenšená na kouli 0,55/0,9, a kulatá koruna na tyčce je hříbek, ať je jakkoli malá. Řešení není další zmenšování (čtení se tím nemění, jen se stěhuje mezi „malý strom" a „hříbek"), ale druh z výsadby úplně vynechat — `ConiferFraction` 0,94 → 1. `ForestScatterRenderer` nemá bezlistou mesh variantu, takže poctivá odpověď na „opadané listnáče" je žádné listnáče, ne přiblížení, co samo vypadá jako jiná věc.

**Ověřeno:** tři exe stavějí, nový capture ze země ukazuje čistý smrkový les bez cizích siluet. LevelGen/ScoreSim znovu nespouštěny — izolovaná změna jednoho stromového configu, žádný sdílený kód dotčený.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (osmý zápis dne)

**#205 přeladěno na majitelovu zpětnou vazbu (`6de9ec7`), hned po prvním capture.** Vlastními slovy: *„Aurora vypadá dobře, les moc ne. Měly by v něm být hlavně jehličnany, mělo by jich tam být víc a les by měl být celkově mnohem tmavší, možná v něm i trochu sněží — je zima. Listnaté stromy jsou opadané."*

**Tma měla dvě různé příčiny a první capture opravil jen jednu.** `AuroraGlowColor` (zem + přes `ApplySkyTint` stromy) byl už jednou sražen na 0,22×, ale koule/ostrov/děl/stromová základní barva berou svit z `AuroraLightingConfig`u — a ten pořád nesl konvenci „~1 na kanál" pro `KeyTint`/`BackTint`, co mají Měsíc/dream/cavern, a žádná z těch tří scén nesvítí na osazený `ForestScatterRenderer`. Obě světla srazena zároveň (rig o dalšího zhruba půl, `AuroraGlowColor` z 0,22× na 0,09×) — schválně spolu, protože jedna stížnost na jednu fotku neumí rozlišit, které z nich to bylo.

**Zbytek byl v `AuroraSceneConfig.Terrain` a nikde jinde** — `ConiferFraction` 0,94, `Count` 380, listnatá koruna zmenšená z 3,1/5,6 na 0,55/0,9 (pahýl bez listí — poctivý limit editace configu, žádná zimní/bezlistá mesh varianta v `ForestScatterRenderer` neexistuje, a při 94 % jehličnanů je to skoro jedno). Ani strom se nehnul v denním lese — nezávislá výsadba (#205's own design decision) se právě teď vyplatila.

**Sníh sdílený s horskou scénou, ne druhá kopie.** `DrawSnow` bral `_mountainConfig.Snow` napřímo; teď bere `SnowConfig` jako argument a vzhledové uniformy tlačí každý snímek místo jednou při apply configu — sdílený efekt/buffer žádá "co se má KRESLIT teď", ne "co se naposledy aplikovalo". `ApplySnowParameters` tím zcela zbytná, smazána. Buffer zůstává horský (`BuildSnowBuffers` pořád podle `_mountainConfig.Snow.FlakeCount`), aurořin vlastní `FlakeCount` je useknutý na kapacitu bufferu.

**Ověřeno:** tři exe stavějí, LevelGen/ScoreSim exit 0, MapEditor kouřový test čistý. Cena přeměřená, ne předpokládaná stejná: 758–762 FPS proti 761–765 před přeladěním — v šumu, i s 58 % víc stromy a sněhem navíc. Nový capture (herní kamera i ze země) poslán majiteli přes `SendUserFile`.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (sedmý zápis dne)

**#205 hotovo a na `main`u (`54e253f`), po rebase na `222-polar-scene`.** Osmnáctou scénu vzal Polar dřív — merge je proto **devatenáctá**, ne osmnáctá, jak zápis níž ("pátý zápis dne") čekal. Přečíslování šlo přesně tak, jak jsem si tam napsal: `git rebase origin/main`, osm konfliktů v `SceneRenderer.cs` (enum, `SCENE_NAMES`, `IsSolidTerrainScene`, `TryParseScene`, `TryGetViewpoint`, `GetSceneConfig`, `DrawEnvironment` switch — `ReplacesSky` a `TryGetLightRig` bez konfliktu, protože Polar do nich vůbec nesahá, není `ReplacesSky`), plus `SceneConfig.cs`, `CLAUDE.md` a `docs/scenes.md` (tři konflikty — hlavička, viewpoint tabulka, a spojení obou nových sekcí na konci souboru). Dvě věci git sám tiše smíchal špatně a stálo by to za přehlédnutí: obě větve nezávisle přepsaly „sedmnáct"→„osmnáct" na týchž řádcích (`CLAUDE.md`, `docs/scenes.md`, `docs/testbed.md`, `docs/formats-and-tools.md`), takže se to smergovalo BEZ KONFLIKTU na „osmnáct" — algebraicky náhodou správně, ale ve skutečnosti zastarale, protože pravý součet je devatenáct. Dohledáno greppem na „eighteen" po dokončeném rebase, ne důvěrou v čistý merge.

**Jedna nesrovnalost v Polarově vlastní próze našla se cestou a opravena tady, ne tam:** věta tvrdila „jedenáct z osmnácti fotografováno; sedm ne" a pak vyjmenovala jen šest jmen (chybělo samo „polar" v seznamu nevyfocených — logicky muselo být mezi nimi, žádný level ho nejmenuje). Opraveno na jedenáct z devatenácti, osm nevyfoceno, s polárním ledem i aurorou v seznamu. A Polarova vlastní sekce měla dopřednou poznámku "polární noc s aurorou, kterou vlastní #205" — hádali, čím #205 bude, než to existovalo, a uhodli špatně (čekali ledovou variantu, ne les). Opraveno na jednu větu, co říká, co #205 doopravdy je, a proč se to neshoduje.

**Co scéna je:** noční les pod silnou, zřetelně pulzující barevnou polární září, hvězdy skrz ni vidět. Zemní podrost je `Forest.fx`ova redukovaná podlaha (bez triplanar/stromových stínů) portovaná verbatim, druhá nezávislá výsadba `ForestScatterRenderer`. Obloha je Měsícův sky-quad vzor verbatim, `Stars.fxh` potřetí, stuhy polární záře (`Fbm2Combed`, roztažené podél stuhy) přičtené — ne composeitované — nad hvězdy.

**Dvě věci, co ukázal až skutečný capture a žádné čtení shaderu by je nenašlo:** první pulz (0,35 rad/s, cyklus 18 s) vyfocený s pětisekundovou mezerou vyšel k nerozeznání — fáze se posunula, ale ne dost na to, aby to oko chytlo. Zrychleno na 0,9 (cyklus 7 s), stejná mezera je teď jednoznačná. A zem nejdřív četla jako osvětlená sluncem, ne nocí — `AuroraGlowColor` nesla oblohovu vlastní `Intensity` napřímo, jenže ten člen integruje přes celou polokouli místo tenkého pruhu. Sraženo na 0,22×; stromy samy se pohnuly málo, protože berou svit ze statického rigu (`AuroraLightingConfig`), ne odsud — zapsáno jako nedotažené dál, ne jako vyřešené.

**Ověřeno:** Testbed/MapEditor/Game všechny stavějí čistě (i po rebase), LevelGen a ScoreSim exit 0 (i po rebase), MapEditor a Game oba naběhnou bez pádu (kouřový test, konstruktor s novou `_auroraScatter` větví). Jedno hrubé srovnání výkonu v Testbedu (ne párová alternace, řečeno tak i v docs): aurora 761–765 FPS proti denímu lesu 365–366 FPS na týž stroji a kameru — levnější, ne dražší, což sedí s redukovanou dlaždicí. Scéna není v žádném shipped levelu a záměrně — issue jmenuje scénu, ne kapitolu, stejně jako si to Polar rozhodl pro sebe nezávisle.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (šestý zápis dne)

**#222 přepracováno na majitelovo zamítnutí** (`23564a6`, pořád na větvi `222-polar-scene`, nemergnuto). Obě výtky byly přesné: *„ty křivé sloupy vzadu ani není zřejmé, co to je — vypadá to spíš jako grafický glitch"* a *„nikde nevidím ten popraskaný led, co prosvítá světle modře"*.

### ⚠ Obě vady byly konstrukční, ne ladicí

- **Sloupy:** kry kvantované v **polárních** souřadnicích = klíny rostoucí se vzdáleností, jejichž výšky se potkávaly na radiálních švech; při výšce 26 nad kry 10–20 širokými byla deska sloup. Teď mřížka ve **světových** souřadnicích, každá buňka **nakloněná rovina**, fronta vysoká 11. Širší než vyšší = kry.
- **Chybějící prosvítající led:** trhliny byly vázané na napětí fronty 300 jednotek daleko, kam žádná kamera nechodí, a plochá pláň průsvitu nedala žádný silný led. Teď pole trhlin přes celou pláň (mýtina pod ostrovem zůstává celá) a **modrý led** — holý ledovec, odkud vítr odvál sníh.

### ⚠ Puklina prošla třemi špatnými čteními, než četla jako puklina

**Val** (světlejší než sníh kolem), pak **rampa** (mělké koryto se světlým dnem, protože záře vrcholila v hrdle), pak teprve puklina: úzká (~3 jednotky), hluboká 8 (aby měla stěny, které slunce zastíní), ztmavené hrdlo, průsvit **na stěnách** a na nich utlumený Fresnel — strmá stěna viděná z pláně je pod klouzavým úhlem a zrcadlila obzor místo aby ukázala led.

### ⚠ Past při posuzování téhle scény

**Testbedova F10 kamera stojí tak nízko za dělem, že pláň schová ostrov** — z ní trhliny nejsou vidět vůbec, i když ve hře jsou. Ověřeno v `BS3D.exe levelfile=` z herní kamery. Kdo tuhle scénu bude soudit v Testbedu, ať použije volnou kameru výš.

### Cena

Proti poušti, párové běhy: **polar 32,56 / 32,70 ms, desert 27,98 / 28,36** → asi **+16 %** (bylo +7,9 %) za ~6 šumových vyhodnocení na pixel navíc. Jedno `BlueIce` místo dvou pohnulo mediánem o 0,14 ms, tj. v šumu — zapsáno jako úklid, ne jako úspora. Další páka, kdyby bylo potřeba: maska trhlin má tak nízkou frekvenci, že tři tapy na normálu mohou sdílet jeden vzorek.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (pátý zápis dne)

**Beru si #205 (polární záře nad nočním lesem).** Issue: noční obloha nad zalesněnou krajinou se silnou, zřetelně pulzující barevnou polární září, hvězdy částečně vidět skrz/vedle ní. Větev `205-aurora-scene`.

**⚠ Kolize se zápisem výš ("třetí zápis dne", #222): obě issues si říkají o osmnáctou scénu současně, na dvou různých větvích ze stejného `main`u.** `222-polar-scene` je na originu, nemergnutá — nesahám na ni, jen ji tu zmiňuju, aby si toho všiml, kdo mergne druhý. Až budu mergovat, natáhnu `origin/main` znovu; pokud #222 dorazí první, moje scéna vyjde devatenáctá, ne osmnáctá, a přečíslování prózy (`docs/scenes.md`, `CLAUDE.md` „sedmnáct" → „devatenáct" atd.) je pak na mém merge kroku, ne na #222.

**Návrh (než začnu psát kód):** druhá scéna v obou rodinách zároveň (po Měsíci, #125) — `SceneRenderer.IsSolidTerrainScene` (sdílený lesní podrost + `ForestScatterRenderer`, ale **druhá, nezávislá výsadba** přes stejný, už obecný `SceneRenderer.ForestTerrainHeight(x,z,config)`) i `ReplacesSky` (žádná dome, vlastní celoobrazovkový sky pass, `Stars.fxh` sdílené s Měsícem/Space, nad hvězdami aditivní stuhy polární záře — aditivně přesně proto, aby hvězdy nikdy nebyly plně zakryté, bleskova stuha bouřky má stejné zdůvodnění). Výškové pole podrostu portované verbatim z `Forest.fx` (jako Mars portoval krátery Měsíce), ale bez těžkého detailu druhého průchodu (FBM mottle/triplanar/stíny stromů) — v noci se neuplatní a scéna má vlastní důvod nechat ho lehčí, ne kopírovat cizí rozpočet. Pulz žije v shaderu oblohy a v `ForestScatterRenderer.ApplySkyTint` přes prahovaně přepočítávaný barevný posun v čase; `TryGetLightRig` zůstává **statický** autorovaný rig (žádná nová `SceneLights` větev) — obojí zdůvodním v `docs/scenes.md`, až bude co psát, ne jenom tvrdit.

**Běží subagent, co má vyjmenovat každé místo v repu, co přepíná/vyjmenovává `SceneKind`/`SceneConfig`** (přesně ten druh chyby, co si tenhle žurnál sám pamatuje — les chyběl ve dvou nezávislých `IsSolidTerrainScene` seznamech, `% 7` cyklus se našel dvakrát). Čekám na výsledek, pak jdu psát.

---

## 2026-09-14 — Claude Code (jedenáctý zápis dne)

**Beru si #389 (bomba nemá vlastní výbuch a většina toho, co shodí, jen spadne).** Větev `389-bomb-detonation`.

Dvě části, přesně jak je issue dělí:

1. **Vyhodit i sirotky výbuchu.** `BallsConstraintsBuilder.DetonateBombs` dnes hází jen oběti v poloměru; to, co pak najde disconnection pass, padá s nulovou rychlostí a čte to jako obyčejný kolaps. Dostane vlastní, jemnější postrčení od nejbližšího středu výbuchu. Je to knihovna, takže to dostane Testbed i hra naráz, a `SagProbe` volá tutéž metodu — paritu tedy nemusím psát, ale **změřím**, že se výstup LevelGenu nehne (vyhozené koule můžou narazit do toho, co visí dál).
2. **Okamžik detonace.** `BallLanding` dnes neumí říct, *kde* co bouchlo (`Destroyed` sdílí bomba se zapem a kyselinou), takže nejdřív to, pak ve hře: otřes kamery (rachot bez směrového zpětného rázu děla), vlastní zvuk (oddělený od release registrem; bake vysypaný do WAV a změřený, protože zvuk se ze screenshotu soudit nedá) a záblesk v místě výbuchu.

⚠ **Vizuální část chce GPU seanci na desktopu → zeptám se majitele předem.** Fyziku a zvuk dělám nejdřív, bez grafiky.

**Prosím do merge nesahat na** `BallsConstraintsBuilder.cs`, `BallContactEventHandler.cs`, `BallLanding.cs`, `GameplayScreen.Rules.cs` a `ProceduralAudio.cs`.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (dvanáctý zápis dne)

**#389 je na větvi `389-bomb-detonation` (`53117b8`, `47f3a92`, `307de65`), NEmergnuto: čeká na majitelovo oko a ucho**, protože vzhled a zvuk výbuchu se schvalují pohledem a poslechem (precedens #222 a #333).

### ⚠ Skutečná příčina hlášení nebyla ta, kterou tipovalo issue

Issue píše, že „it just falls" dělají hlavně sirotci výbuchu, kteří padají s nulovou rychlostí. **Změřeno: z nedotčeného clusteru bomba ve Ventu, Sillu i Paroxysmu osiří 0–1 kouli.** Viníkem byly oběti samy. `Throw` bral střed výbuchu z `BallsMap.GetRealPosition`, tedy ze surového rámce mřížky, jenže tělesa leží centrovaná (`Center()`) a posunutá o `worldOffset` — na všech třech levelech o (−7,5; −5,4; −7,5), dvanáct jednotek proti poloměru dva. Každá oběť tak „ležela za okrajem", dostala okrajových 2,46 j/s a všechny jedním směrem: **koherence směru 0,99–1,00, průměrný kosinus ven ~0** — deska koulí driftující do rohu arény. Po opravě (odhoz od polohy **těla** bomby, poloměr dál v rámci mřížky) 3,40–3,75 j/s, kosinus ven +0,68 až +1,00, koherence 0,18–0,47 (zbytek je geometrie: bomba na spodku má oběti hlavně nad sebou), nic na kamenu ostrova, zbylý cluster se neotřese víc. Změřeno odhozeným rigem ve scratchpadu (reálná Bepu simulace bez grafiky, každá z 19 bomb zvlášť). Sirotci dostali odhoz taky (okrajová rychlost × poloměr/vzdálenost, podlaha `BLAST_ORPHAN_MIN_SPEED` 0,8 j/s) — ale je to druhá, menší půlka. **LevelGen řádek po řádku stejný**; dva výchozí běhy předem potvrdily, že je deterministický, takže nula rozdílů něco znamená.

### Okamžik detonace

`Detonation` (světová poloha těla, článek řetězu, kolik vzala) na `BallLanding.Detonations` — dřív nešlo říct, *kde* co bouchlo, `Destroyed` sdílí bomba se zapem a kyselinou a `World` je buňka rány, ne bomby. Nad tím: `Blasts` + `Blast.fx` (záblesk, ohnivá koule, 96 jisker, jeden draw call, idiom ohňostroje), `SceneLights.SetFlash` (jedno světlo na jeden snímek do volného slotu, lampu scény nikdy nevyhodí), `PlayBlast` s `RenderBlast` (měřitelné bez audio zařízení, šev `MusicBake`), `CameraShake.Rumble` (třetí kanál, 9 Hz, ~0,6 s, bez zpětného rázu). Řetěz se hraje po článcích o 70 ms; efekt běží na **simulačních** hodinách, takže se zpomalí s drop cinematicem. Testovací páka `detonate=<t>` na hodinách `shot=`.

### ⚠ Past, kterou ukázal až první snímek ve hře

**Rázový prstenec (tenké mezikruží) četl jako halo nakreslené přes cluster** — dokonalý kruh, na řetězu dva jako ikona. Nic na výbuchu není kruh; quad se stal ohnivou koulí roztrhanou šumem. Týž snímek našel záblesk moc malý a jiskry bílé (třpytky, ne oheň) a světlo (5; 2,2; 0,7) na koulích skoro k nenalezení. Druhé kolo opravilo všechno tři.

### ⚠ Dvě provozní pasti

1. **`GameplayScreen` se staví dřív než `ProceduralAudio`** (`BS3DGame.LoadContent`, ř. 1117 proti 1121). Audio předané do konstruktoru efektu by bylo navždy `null` a výbuch tiše němý. Předává se do každého `Update`.
2. **PowerShell 5.1: here-string s dvojitými uvozovkami do `git commit -m` rozseká zprávu na pathspecy** — commit se neprovede („pathspec … did not match") a řetěz pokračuje dál. Zprávu dávat přes `git commit -F <soubor>`.

### Herní kamera proti cinematicu

První výbuch (26 i 117 koulí) spustí drop cinematic, který během zlomku sekundy vystoupá nad cluster. **Pohled herní kamery jsem proto fotil na druhé detonaci**, která rekord 1,25× nepřekoná. Bomby uvnitř clusteru (Sill je má ve sloupcích) jsou z výšky cinematicu schované za koulemi a čte jen světlo mezerami. Na jednom snímku ke konci cinematicu byl **objektiv uvnitř kola děla** — cizí vada, nesahal jsem na ni.

### Ověřeno

- Game, Testbed, MapEditor i BS3DLibs 0 chyb; LevelGen exit 0 a výstup beze změny; ScoreSim „All levels rate the right way round"; `Game/Levels` beze změny.
- 5 GPU běhů (majitel povolil 6), okno 1600×900, `fpscap=75`, `quality=high`, `mute`, `[build]` řádek zkontrolován při každém (poučení z #326). Vent, Sill, a na Ventu i `balls=lava` a `balls=plasma` — záblesk vede snímek i na svítících materiálech.
- Zvuk: RMS 0,202, crest 4,81, 97,8 % energie pod 150 Hz (report ohňostroje 94 %), první 10ms okno na 75 % maxima, −40 dB za 1,19 s.

### Co zůstává

- **Zvuk ve hře nikdo neslyšel** (všechny běhy `mute`), WAV poslán majiteli. Dunění kamery ze snímku posoudit nejde.
- **Merge až na majitelovo slovo.**
- Cinematic: rychlé stoupání nad cluster u výbuchu uvnitř clusteru a objektiv v kole děla — kandidáti na issue, nezakládal jsem.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (třináctý zápis dne)

**Beru si #390 (přepínač adaptivní kvality v Nastavení).** Větev `390-adaptive-quality-toggle`, **v samostatném worktree `BS3D-390`**: hlavní checkout drží #389 (`389-bomb-detonation`), na které souběžně píše jiná seance, takže na něj nesahám a nepřepínám mu větev.

Sahám na `Game/GameSettings.cs`, `Game/BS3DGame.Quality.cs`, `Game/Screens/SettingsPage.cs`, `docs/game-shell.md` a na **jeden blok** `Game/BS3DGame.cs` (pin sondy při startu — nejbližší hunk #389 je o 26 řádků výš, merge se nepotká).

⚠ **Ověření dočasně podmění `%LOCALAPPDATA%\BS3D\Settings.json`**: zálohované, po testu vrácené bajt za bajtem a zkontrolované otiskem. Kdo by v tu chvíli pouštěl hru, dostane testovací nastavení. Majitel schválil čtyři krátké běhy v okně s `fpscap=40`.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (čtrnáctý zápis dne)

**#390 je na `main`u (`1b1f6a7`, merge `0ebe472`): v Nastavení je řádek Auto quality, hned pod Quality.** Větev je smazaná lokálně i na originu. Pracoval jsem ve worktree `BS3D-390`, hlavní checkout s #389 zůstal nedotčený. Issue je zatím otevřené, zavření nechávám na majiteli.

### Co to je a proč právě takhle

Ruční výběr kvality vypínal sondu navždy už dřív (pin v `CycleQuality`, uložený tier pinuje při každém startu), jen to nikde nebylo vidět. Řádek proto **nemá vlastní příznak**: čte `!_qualityPinnedByPlayer`, takže nemůže tvrdit nic, co sonda nedělá. Klik na Quality ho viditelně přepne na Off a běh s `quality=`/`ssaa=` ukazuje Off.

- **Off** zapíše `"adaptiveQuality": false` a k tomu tier, který řádek Quality právě ukazuje, i když ho dosáhla sonda. Není to ratchet z #354: hráč sondu vypnul s tím tierem před očima a řádek, kterým ho zvedne, má hned nad přepínačem.
- **On** uložený tier smaže (další start = High a měření jako po čisté instalaci) a hned otevře jedno okno sondy od tieru, který platí.
- **Start:** klíč `false` pinuje stejně jako uložený tier, i když žádný uložený není, a pak hraje High. Starý soubor s tierem a bez klíče pinuje dál a řádek ukáže Off, takže se nikomu chování nezměnilo.

### ⚠ Chyba, kterou nová cesta zpřístupnila na jedno kliknutí

Re-open se neptá, na jakém tieru sonda stojí, takže okno pod floorem na **Low** „snížilo Low na Low": řádek `[quality]` a oznámení v menu pro změnu, která se nestala. Narazit na to šlo i dřív (level postavený na Low na stroji pod floorem), teď by stačilo zapnout Auto quality nad pinnutým Low. Krok na Low teď jen zavře latch.

### Jak ověřit sondu na rychlém desktopu

Desktop pod floor (68 při 75 Hz) nikdy nespadne, takže test „vypnuto" by sám nic nedokazoval. **`fpscap=40` sondu spustí pokaždé**, a to s menší zátěží než cap na refresh. Čtyři krátké běhy v okně (majitel schválil čtyři), `mute`, `logfps`, `[build]` z worktree zkontrolovaný:

1. Level 1 s nastavením beze změny: High → Medium → Low, první krok u patnáctého sekundového odečtu. Stejně jako main.
2. Totéž s `"adaptiveQuality": false`: **31 odečtů pod floorem, z toho 8 ve fullscreenu 3840×1600 mezi dvěma F11, a ani jeden řádek `[quality]`.**
3. Front-end, klávesami do Nastavení: zapnutí pod otevřenou stránkou dalo High → Medium → Low do deseti vteřin. Klik na Quality pak řádek přepnul na Off a zapsal Medium + `false`.
4. Z uloženého Medium: hraje od prvního odečtu bez sondy. On dal jeden řádek Medium → Low. Off zapsal `"quality": "Low"` (vidět v `.bak`). On zapsal `true` bez tieru a deset vteřin pod floorem na Low nedalo řádek ani oznámení.

Snímky stránky potvrzují každý stav. Nový řádek srovnal výšku obou sloupců, Back zůstává na obrazovce a panel měří 817 px při 1600×900 (komentář ve `SettingsPage` říkal 805, přepsáno změřeným). `Game.sln` 0 chyb a 0 upozornění. Změna je jen v projektu Game, který žádný jiný solution nestaví.

### ⚠ Hra nemá argument na jiný soubor s nastavením

Běhy proto podměňovaly skutečný `%LOCALAPPDATA%\BS3D\Settings.json`. Oba soubory (i `.bak`) jsou vrácené bajt za bajtem i s časy zápisu a otisky jsou ověřené.

**Nález, který není můj:** `Progress.json` se změnil ve **20:20:02**, čtyři minuty před mým prvním během. Přibyl `"Vent.json": { "score": 28180, "stars": 4 }`. Nejspíš jsou to testy #389 na Volcano levelech (snímky v hlavním checkoutu 20:02–20:05; `detonate=` bombu opravdu odpálí). Nevracel jsem to, `Progress.json.bak` drží stav z 2. 9. Jestli ten záznam v kampani chce, rozhodne majitel.

**Mimochodem:** `docs/formats-and-tools.md` odkazuje na „The settings page" v `docs/game-shell.md`, jenže taková sekce tam není (nastavení je bullet v „The front end"). Nechal jsem to být.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (patnáctý zápis dne)

**#389: majitel si výbuch pustil ve hře. Vzhled prošel, zvuk ne. Pětkrát jsem ho předělal a teď je na větvi `389-bomb-detonation` jen s basy (`955d2e4`).** Pořád NEmergnuto, čeká se na majitelovo ucho. Založil jsem **#394** (barvy levelu 80). Majitel si ho vyžádal.

### Zvuk bomby: pět verzí a verdikt ke každé

1. **„Myška upustila křišťálový hrníček."** 97,8 % energie pod 150 Hz jsem mylně četl jako hloubku. Jenže **89,8 % leželo pod 60 Hz** (31 Hz a sub na 15,5 Hz), kde repro nehraje nic. Slyšitelné pásmo mělo −24 dBFS a v něm cvakání úlomků s tóny 2,4–5,2 kHz. **Poučení: rozdělení energie přes celé spektrum ránu od cvaknutí neodliší. Slyšitelné pásmo 60 Hz–8 kHz se musí měřit zvlášť** (harness to od teď dělá).
2. **„Hrozně digitální a málo dunivé."** Hukot ze šumu, sutina, tanh drive a gate po 75 ms zvedly slyšitelné pásmo o 10,6 dB. Každá z těch vrstev je ale učebnicový zdroj „digitálna". K tomu verdikt: **jde o příjemnost a basy, ne o fyzikální věrohodnost** (uloženo do paměti).
3. **Jen teplá rána** (sinus bez driveru, kaskádové low-passy, strop 2,8 kHz): „příjemnější, ale málo výrazný, vrať krátké pištivé střepiny".
4. **Rána + devět klesajících svistů střepin** vyvážených RMS: **„Ne. Pištění úplně pryč, jen maximální basy."** Pozdější verdikt platí.
5. **Teď:** rána, dunění, „whump" pod 650 Hz a úder, vše pod 1,4 kHz, komprese + look-ahead limiter a žádný drive. **RMS 0,282, crest 3,37**, tedy nejhlasitější a nejhutnější z pěti; 84,7 % pod 60 Hz, nad 2 kHz nic.

### ⚠ Dvě pasti míchání, obě změřené dřív, než šly ven

- Vyvážení dvou polovin **podle špičky** nechalo svisty na 0,4 % energie, protože špičku horní poloviny dělalo prvních pár vzorků praskotu.
- **Kompresor sleduje průměrnou úroveň**, takže šumové špičky jím proletěly a určily finální normalizaci: crest 9,47, na papíře hlasitější mix, v uchu tišší zvuk. Proto vznikly `Compress` (look-ahead) a `Limit` (look-ahead peak limiter) v `ProceduralAudio`.

### #394: barvy levelu 80 (Paroxysm)

Majitel: barvy jsou si moc podobné, v dělu mají jiný odstín a nevíš, co střílíš. Zjištěno ze souboru a generátoru, vše je v issue:

- láva pod dómem 9 a sedm inkoustů (krusta černá/hnědá/stříbrná, jádro červená/oranžová/žlutá/bílá), takže obě známé těsné dvojice (#315) jsou v jednom levelu;
- **#315 lávu měřilo pod scénou The Reveal, ne pod sopkou**, kde se teď hraje (a sopka tlačí na koule vlastní rudá světla);
- blokový zákon Eruption je „horké členy na dvou inkoustech", jádro Paroxysmu jich má čtyři;
- za sklem v dělu je podle měření #365 hnědá tmavší než černá;
- **čtyři barvy jádra jsou na startu úplně zazděné (z 65 koulí jádra není odkrytá ani jedna)**, a zásobník je přesto nabízí. Majitel se na to ptal jako první („nabízí se mi červená, není to kvůli bombě?"). Bomby to nejsou, `BallKinds.Matchable` je ze sčítání vylučuje. Jestli nabízet jen odkryté barvy, je rozhodnutí o obtížnosti a nechávám ho majiteli.

### ⚠ Oprava k čtrnáctému zápisu: Vent v `Progress.json` nejspíš nejsou moje testy

Zápis výš připisuje `"Vent.json": { "score": 28180, "stars": 4 }` testům #389. **Mých pět GPU běhů (20:02–20:05) v logu nemá jediný řádek `[level]`**, žádný level tedy nedohrál a nezobrazil výsledek. Soubor se změnil ve **20:20:02**, patnáct minut po posledním z nich a poté, co jsem majiteli poradil, jak si výbuch zkusit (`level=Sill detonate=10`). Nejspíš to je majitelovo vlastní hraní. Nic jsem nevracel.

**Nic dalšího si neberu.**

---

## 2026-09-14 — Claude Code (šestnáctý zápis dne)

**Založil jsem #395 na majitelův pokyn: barvy koulí ve všech deseti lávových levelech (The Eruption) se špatně rozeznávají a kulička v dělu svítí světleji než stejná barva na mapě.** #394 (level 80) je jeden konkrétní případ. Do #394 jsem napsal komentář, že rozhodnutí v #395 ho má zavřít nebo zúžit.

- **Těsné dvojice nejsou výjimka, jsou paleta celého bloku:** černá+hnědá v 9 z 10 levelů, oranžová+hnědá v 8, červená+oranžová v 8, černá+stříbrná v 6. Sečteno ze souborů levelů.
- **Světlejší kulička v dělu je potvrzená v kódu, ne jen tušená.** `LavaPS` násobí svit švů `breath = lerp(1 − PulseDepth, 1, beat)` a lineárně okluzí. Cluster se ve hře kreslí s `PULSE_DEPTH_RIPPLING` 0,38, takže mezi údery svítí na 0,62 plného svitu. Zásobník jde přes still plane s `pulseDepth: 0` a `BallRenderSet.UNOCCLUDED`, takže svítí vždy na 1. Nabitá koule tedy svítí zhruba 1,6× víc než odpočívající koule clusteru a v ústí má navíc halo (#236). Všechna tři rozhodnutí jsou záměrná (#236, #252, #303) a na vinylu neškodná; na emisivním stylu ale mění barvu.
- ⚠ **Na obrazovce to změřené není.** Issue navrhuje nejdřív snímky ve hře s drženým RMB a paletu pod sopkou (#315 měřilo lávu pod scénou The Reveal).

Nic jsem neopravoval. **Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code

**Beru si #393 (dvacátá scéna: časná 80. léta výpočetní grafiky, mřížka z pojmenované matematiky — Tron), na majitelův výslovný pokyn.** Větev `393-tron-grid-scene`, založená z aktuálního `origin/main` (tenhle zápis jde přímo na `main` z worktree `BS3D-322`, hlavní checkout stojí na `389-bomb-detonation` a nesahám na něj).

Rozhodnutí z issue, než padne kód — cituju je tu, aby je nikdo nemusel dohledávat, kdyby se do toho měl zapojit ještě někdo jiný:

- **`SceneKind.Grid`** (issue navrhuje přímo tenhle název), tvar Měsíce/Aurory — `ReplacesSky` **i** `IsSolidTerrainScene` zároveň (třetí scéna v obou rodinách po #125 a #205), žádná dome, vlastní světelný rig.
- **Jen pozadí.** Koule, dělo i ostrov zůstávají na běžné osvětlené cestě `InstancedModel.fx`, jen podbarvené studeným cyan rigem téhle scény — to je issue's vlastní doporučená výchozí volba ("the smaller, cheaper, more consistent change"), ne můj zkrat.
- **Podlaha je doopravdy plochá** (konstantní výška, žádné pole a žádný součet oktáv, žádný gradientní normál — normála je vždy nahoru), takže mříž samotná stojí skoro zadarmo. Nad ní **Hilbertova křivka** jako obvodová kresba: bitová rekurze `xy2d` na souřadnicích buňky (dlaždice 64×64, modulo tak aby se opakovala přes celou plochu), hrana mřížky svítí jasněji tam, kde odděluje dvě po sobě jdoucí buňky křivky. Žádná derivace uvnitř té rekurze, takže je bezpečná vedle `fwidth` na antialiasing čáry.
- **Vědomě ne azimutální motiv (atan2/spirála/úhel).** Deník nese tři nezávislá nahlášení téhož švu (Mars, kaverna, a potřetí aurora #205 — `atan2` nakrmený rovnou do šumu). Hilbertova křivka a mříž samy o sobě žádný úhel nepočítají, takže tomuhle švu nemůžou podlehnout — první řez jde jen na tenhle motiv.
- **Obloha je prázdná černá s ditherem proti bankování, bez hvězd.** Hvězdná mřížka je pohled vesmíru/Měsíce/Aurory; tahle scéna má číst jako "nic nevysíláno", ne jako další noční obloha, a je to i nejlevnější varianta z pěti scén nahrazujících oblohu.
- Credit čtyřem CG studiím Tronu (1982) a hlavně procesu podsvícené optické kompozice (odkud je "černé tělo, svítí jen švy") půjde do `docs/scenes.md`, jak issue výslovně žádá — do shaderového komentáře taky, ať přežije i bez dokumentu.

**Cíl je změřit, ne odhadnout**: issue klade laťku "nejlevnější scéna ve hře" (pod kavernou, 5,96 ms) a tvrdí, že plochá podlaha + mříž na to má nárok algoritmicky, ne škrtem v kvalitním tieru. Změřím na konci stejným postupem jako Aurora (Testbed, `nopost nocap logfps`, jedna kontrolní scéna vedle).

**Kampaň, druhé motivy (Life na oknech, spirála, Mandelbrot) a černé tělo sahající na kouli/dělo jsou mimo rozsah** — issue to sama odděluje jako následné kroky.

**Nic jiného si neberu.**

---

## 2026-09-15 — Claude Code (druhý zápis dne)

**#393 je na větvi `393-tron-grid-scene` (`57b2eca`), NEmergnuto: čeká na majitelovo oko** — nová scéna, nová estetika, stejný precedens jako bomba (#389) a polar (#222). `SceneKind.Grid`, `Grid.fx` + `GridSceneConfig`, `scene=grid`/`scene=tron` v Testbedu.

**Tvar podle issue vlastních doporučených výchozích voleb**: `ReplacesSky` **i** `IsSolidTerrainScene` (třetí scéna v obou rodinách po Měsíci a auroře), podlaha jen pozadí (koule/dělo/ostrov zůstávají na běžné osvětlené cestě, jen podbarvené studeným cyan rigem — issue's vlastní "smaller, cheaper, more consistent change"), žádná mřížka výšek (podlaha je doopravdy plochá — konstantní `TerrainHeight`, žádný součet oktáv, žádný gradientní normál), obloha prázdná černá s ditherem a bez hvězd.

**Motiv: Hilbertova křivka jako obvodová kresba, vědomě ne úhlová (spirála/soustředné kruhy).** Deník nese tři nezávislá nahlášení téhož švu (Mars, kaverna, aurora — `atan2` nakrmený rovnou do šumu); buňková mřížka a test "jsou si sousedé po sobě jdoucí na křivce" nepočítají žádný úhel, takže tomuhle švu nemůžou podlehnout vůbec.

**⚠ Dva nálezy, které ukázal až skutečný capture, oba zapsané do `docs/scenes.md`:**

1. **Dlaždice křivky se nesbaluje, a naivní sbalení dalo šev přesně křížem přes arénu.** Hilbertova křivka nemá index 0 a index N²−1 sousední, takže sbalení do `[0, N)` je skutečná nespojitost na každé hranici dlaždice — a hranice padaly na world `x, z = 0`, tedy přesně tam, kde stojí ostrov. Oprava: posun o půl dlaždice před sbalením, takže na počátku světa je **střed** dlaždice, ne její šev. Chyceno okem na debug průchodu (barvení podle "je nejbližší hrana na křivce"), který ukázal vzor zrcadlený přesně podle `x=0` a `z=0`.
2. **Stejně široká, jen jasnější čára se z hráčské vzdálenosti změřila jako žádná čára.** Debug izolace potvrdila, že porovnání souvislosti funguje (zapíná se na skoro polovině hran, přesně jak křivka navštěvující každou buňku dvakrát predikuje) — ale v běžné hře byla stopa vizuálně příliš tenká na to, aby se v hustém poli čar odlišila. Širší záběr ji ukázal jasně. Oprava: `GridAccentWidthScale` (2,4) — stopa je teď širší, ne jen jasnější, vybráno PŘED anti-aliasing maskou, ne po ní.

**Ověřeno:** všechny tři executables staví čistě (`dotnet build` na všech čtyřech .sln), Game a MapEditor naběhnou bez pádu na nové konstrukční cestě (kouřový test, oba killnuté hned po startu, žádný zásah do majitelova `Progress.json`), LevelGen a ScoreSim exit 0 beze změny výstupu. **Výkon: jednorázová kontrola na referenčním desktopu (6900 XT)**, Testbed, pevná kamera, `nopost nocap logfps`, 1600×900 ssaa 2: Grid **0,45–0,46 ms** proti kaverně (dosud nejlevnější změřená scéna) **1,08–1,09 ms** za stejných podmínek — issue's vlastní laťka splněná bez jediné redukované techniky, protože plochá podlaha nemá co redukovat. Není to párový `alt=` sweep a není to #209's vlastní 5,96 ms z reálného levelu — psáno v `docs/scenes.md` jako jednorázová kontrola, ne měření.

**Mimo rozsah, schválně (issue to sama odděluje jako následné kroky):** kampaňové zařazení, druhé motivy (spirála, Conway's Life na oknech, Mandelbrot), černé tělo sahající na kouli/dělo.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (třetí zápis dne)

**#393, druhé kolo: majitel se podíval a řekl "chybějí tam další objekty, co na scéně mít má být".** Zeptal jsem se na konkrétní volbu (AskUserQuestion) — vybral obojí z issue's vlastního seznamu: **vzdálené monolity** a **okna s Conway's Game of Life**. Pořád na `393-tron-grid-scene`, pořád nemergnuto, commit `0d1c6c4`.

**Devět jednoduchých hranolů na kruhu kolem arény** (`GridTowerConfig`: `Count`=9, poloměr 160–380 — daleko od hratelné plochy), deterministicky ze `Seed`=393, aby Hra/Testbed/editor stavěly stejné věže na stejná místa (stejný důvod, proč je mapa sdílená mezi třemi). `BuildGridTowers` staví jen čtyři svislé boční stěny (zrcadlí `BoxMesh.AddFace`'s vlastní čtyři parametry i vinutí) — žádná střecha/podlaha, protože ji hráčská kamera z nízkého postoje nikdy neuvidí. Geometrie je zapečená rovnou ve world-space do vlastního vertex bufferu, žádná world matice, žádné instancování — pár quadů na draw je přesně to, co si plamen ohně (flame billboard) už dovolil.

**Okna čtou jeden sdílený Game of Life 32×32, ne simulaci na věž.** `StepGridLife` (obyčejná toroidální pravidla) kroká na CPU, `LifeStepInterval`=0,5 s — issue's vlastní "pár generací za sekundu, ne za snímek, má to číst jako hodiny, ne blikání" — a jen dokud je Grid opravdu kreslená scéna. Upload na texturu jen když se generace opravdu změnila (`UploadGridLifeTexture`, jeden opakovaně použitý buffer, žádná alokace za krok). Každá stěna čte stejnou desku na vlastním pevném náhodném offsetu, takže žádné dvě stěny v celé scéně neukazují identický výřez.

**⚠ Neporušená deska zvadne, a oprava je levnější než detekce.** Náhodný Life na malé toroidální desce se během pár set generací (pár minut při defaultním intervalu) usadí do statické směsi still lifes a oscilátorů — což by četlo jako "okna se prostě zastavila" — a vymření je ještě horší. `StepGridLife` proto **každou generaci** převrátí tři náhodné buňky bez ohledu na verdikt pravidel — levnější než detekovat stagnaci nebo vymření, a odpověď na obojí najednou.

**⚠ Uprostřed session spadl desktop (Kernel-Power).** Přesně vzorec z paměti "desktop-hard-resets-under-load" — spustil jsem víc běhů Testbedu po sobě, poslední s `nocap`. Working tree přežil beze ztráty (jen needitované soubory na disku, nic v paměti procesu). Zeptal jsem se majitele, jestli pokračovat — řekl ano, ale jen s `fpscap=75`. **Výkon proto NEPŘEMĚŘENO** po přidání věží — `fpscap=75` na scéně běžící v tisících FPS je plošina, ne číslo. `docs/scenes.md` to říká rovnou, ne že by starý údaj (0,45 ms) nesl dál jako by pořád platil.

**Ověřeno:** všechny čtyři solutions staví čistě, Hra i editor naběhnou bez pádu na nové konstrukční cestě (kouřové testy, `fpscap=60`/výchozí, killnuté hned po startu), dva capture osm sekund od sebe potvrzují, že se deska Life opravdu hýbe, LevelGen a ScoreSim exit 0 beze změny výstupu. Vizuálně z hráčské kamery (`campos=0,-4,30 camtarget=0,-8,0`, `fpscap=75`) jsou dvě věže vidět za dělem a čtou se dobře i v běžném herním záběru, ne jen z širokého ustavujícího záběru.

**Pořád na majitelovo oko** — obě kola teď na téže větvi, žádný merge.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (čtvrtý zápis dne)

**#393, třetí kolo, dva požadavky z téhož druhého review.** Pořád `393-tron-grid-scene`, commit `6761335`.

1. **"Těch budov je tam málo a některé by měly mít krychlový tvar, aby to nevypadalo jako budovy, ale jako abstraktní objekty z digitálního světa."** `Count` 9→18, `CubeFraction` 0,4 — čtyřicet procent je teď velká, skoro-krychlová (`CubeSizeMin/Max` 45–75, tak aby jedna stěna ukázala skoro celou 32×32 Life desku najednou, ne jen výřez), zbytek zůstávají věže (`TowerHeightMin/Max`, `TowerFootprintMin/Max`, beze změny). Krychle navíc dostává i horní stěnu (věž ne — hráčská kamera z nízka strop věže nikdy neuvidí). Retry přes kružnice odstupu (`placed` list, 20 pokusů), aby se hustší pole nepřekrývalo.
2. **"Stav Conwayovy hry by se měl lišit i mezi jednotlivými věžemi/objekty — jiný seed, různé hezké varianty."** Každý objekt teď nese **vlastní nezávislou** `GridLifeBoard` (vlastní current/next mřížka, vlastní textura, vlastní hodiny kroku) místo čtení jednoho sdíleného pole na offset. Jeden sdílený `Random` stream pořád seeduje všechny desky, ale každá spotřebuje jinou část streamu — Life je dost chaotický na to, aby dva nesouvisející starty do pár generací úplně rozešly. **⚠ Důsledek: sdílený vertex/index buffer zůstává jeden, ale kreslení teď stojí jeden draw call na objekt místo jednoho na všechny** — draw call drží jen jednu texturu najednou a každý objekt má teď svou. `BuildGridTowers` si navíc pamatuje (startIndex, počet trojúhelníků) na objekt.

**Ověřeno:** všechny 4 solutions staví čistě, Hra i editor naběhnou bez pádu, `fpscap=75` screenshot z dálky ukazuje věže i krychle vedle sebe s viditelně ODLIŠNÝM stavem Life (ne výřezy ze stejného obrázku), LevelGen a ScoreSim exit 0 beze změny.

**Výkon pořád nepřeměřeno** (stejný důvod jako minule — `fpscap=75` je plošina na tomhle rozsahu FPS), `docs/scenes.md`'s poznámka platí dál a teď se vztahuje i na víc geometrie a víc draw callů — pořád odhad, ne číslo.

**Pořád na majitelovo oko, všechna tři kola na jedné větvi.**

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (pátý zápis dne)

**#393 je na `main`u** (merge `ed08046`, `--no-ff` přes `git -C BS3D-322`, tři commity `393-tron-grid-scene` dovnitř beze změny). Majitel po třetím kole řekl "Mergni to". Větev smazaná lokálně i na originu, hlavní checkout přešel na `origin/main` (main sám drží worktree `BS3D-322` — checkout proto detached, ne branch, aby šla stará větev smazat). `BS3DLibs.sln` po mergi staví čistě na `BS3D-322`. Issue nechávám otevřené, zavření na slovo majitele.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (šestý zápis dne)

**Beru si revizi #393 (Grid) na majitelův pokyn: "zkontroluj, navrhni, co by šlo vylepšit, a vylepši to".** Větev `393-grid-review` z `origin/main`, hlavní checkout. Nálezy ověřené offline sondou (C# ve scratchpadu, jen CPU), které jdou do kódu:

1. **Hilbertova "stopa" není křivka.** Shader rozsvěcí hranu mezi dvěma po sobě jdoucími *buňkami* — to je hrana, kterou křivka *překračuje*, kolmá na její směr — takže na podlaze vzniká bludiště nesouvislých příček: na 301×301 vrcholech 5 390 izolovaných, 18 027 slepých konců, 28 549 T-křižovatek. Oprava za stejnou cenu (tři `xy2d`): uzly křivky jsou *vrcholy* mřížky a svítí úsečka mezi dvěma po sobě jdoucími vrcholy. Hilbertova křivka začíná v (0,0) a končí v (N−1,0), takže se dlaždice v ose x řetězí (N²−1 → 0) — sonda: **každý vrchol má stupeň přesně 2**, jedna souvislá křivka a žádný šev.
2. **Antialiasing čar** bere izotropní `length(fwidth(xz))` pro obě osy a maska neztrácí energii, když stopa pixelu přeroste šířku čáry → v klouzavém pohledu čáry v dálce tloustnou a zjasňují. Oprava: derivace po osách + zachování pokrytí.
3. **Objekty nemají svítící hrany** — tělo je skoro barva voidu, siluety se ztrácejí; přitom "světlo jen ze švů" je hlavní pravidlo issue.
4. **Life je náhodná 28% polévka + 3 náhodné buňky za generaci**, majitel chtěl "hezké varianty". Oprava: pojmenované vzory s ověřenou periodou (glider, LWSS/MWSS/HWSS, pulsar P3, pentadekatlon P15, Kokova galaxie P8, osmička P8, tumbler P14, octagon 2 P5; metuzalémy R-pentomino a diehard), nový vzor při stagnaci (perioda ≤ 2 nebo vymření — 28% polévka stagnuje v mediánu po 365 generacích), řádky jako `uint` s bitovou sčítačkou (bit-exact proti naivním pravidlům na 500 deskách × 200 generacích; 18 desek 0,0019 ms místo 0,18 ms), rozfázované hodiny desek a dosvit fosforu.
5. **Zastaralé komentáře a doc** po třetím kole ("one shared Game of Life", "flips three cells" — ve skutečnosti přiřazuje náhodnou hodnotu, atd.).

Ambience beze změny (majitel: nechat). GPU session povolená s `fpscap=75`, zhruba 10–12 krátkých spuštění.

**Nic jiného si neberu.**

---

## 2026-09-15 — Claude Code (sedmý zápis dne)

**Revize #393 je na větvi `393-grid-review` (`63a63ad`), pushnuto, NEmergnuto — čeká na majitelovo oko** (nový vzhled scény, stejný precedens jako tři kola #393). Detail v `docs/scenes.md`, "The Grid".

- **Hilbertova křivka po vrcholech mřížky**, `TraceStride` 2, dlaždice řetězené (N²−1 → 0): každý uzel má stupeň přesně 2, žádný šev. Oba ⚠ nálezy prvního řezu (šířka stopy, šev přes arénu) byly tentýž žebřík příček viděný ze dvou stran.
- **AA čar po osách se zachováním pokrytí** (`GridAxisFootprint`/`GridLineMask`) — jasný pás tlustých čar u horizontu v širokém záběru zmizel.
- **Svítící hrany všech objektů** (`FaceLocal`, `EdgeWidth` 0,9), střecha na každém, cull zadních stěn.
- **Nový `Prazsky.Core/Render/GridLife.cs`**: pojmenované vzory (periody ověřené ve všech 8 orientacích), stagnace → další vzor, rozpočet 360 generací, řádky `uint` (2,9 µs na 18 desek, 0 alokací), rozfázované hodiny, dosvit fosforu (`PhosphorDecay` 0,12 s), deska obtočená kolem objektu a vystředěná na stěnu k aréně. Seed z placement streamu se stejným počtem tahů jako dřív → žádný objekt se nepohnul.

**Ověřeno:** všechny čtyři solutions staví; 5 spuštění celkem (4× Testbed, 1× Game menu), všechna v okně s `fpscap=75`, bez incidentu; snímky před/po z herní kamery i ze širokého záběru; dosvit potvrzen na dvou snímcích 0,25 s od sebe; `Settings.json`/`Progress.json` i `.bak` po běhu Game bajtově stejné. **GPU výkon nepřeměřen** (pod `fpscap=75` to nejde), `docs/scenes.md` to říká rovnou. Issue nechávám otevřené.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (osmý zápis dne)

**Nová dávka playtest poznámek (obecné postřehy ze hraní, ne tabulka Zábavnost/Obtížnost jako v [[playtest-log-triage]]) protříděna do třinácti položek → jedenáct nových issues + komentář.** Postup jako u první dávky: `gh issue list`/`search` a čtení deníku napřed, ať se nic nezaloží zdvojeně. Majitel řekl výslovně "založ issues na základě těchto poznámek" — takže na rozdíl od první dávky, kde se netriviální podíl řádků nezakládal (byly to jen pozitivní/neutrální postřehy), tady byla založena issue na každou jednotlivou položku, včetně jedné, co je jen návrhem alternativy k už jednou schválenému a zavřenému řešení (#412) — o tom, co založit a co ne, rozhoduje majitelovo slovo, ne moje čtení "už to bylo jednou odsouhlaseno".

**Nové:** #401 (Space hvězdný třpyt jak diskotékový stroboskop), #402 (chybí motion blur na rychlém pohybu děla/koule), #403 (nohy lafety jsou jen kvádry `AddBox`, chtějí detail jako kola), #404 (brainstorm: vlastní vzhled ostrova pro každou scénu — dnes jeden sdílený `ArenaIsland`), #405 (výběr levelu v menu nerespektuje scénu/styl kuliček — `LevelSelectPage` se vůbec nedotýká `BackdropScreen`u), #406 (chybí možnost přehrát si `ChapterIntro` tour scény ze `ScenePage`), #407 (drop cinematic se u scén s otevřeným trychtýřem (`OpenBelow`) nejdřív moc přiblíží k trychtýři a pak odskočí), #408 (menu kamera občas prolétá skrz mapu při orbitu — `BackdropScreen`/`FrameOrbitFor` zřejmě nemá klíčování na shluk jako drop cinematic na ostrov), #409 (Space's `ChapterIntro` končí nepříjemným pohledem shora dolů do ostrova), #410 (koule občas odskočí bez přichycení i když náhled slibuje zásah — nová instance třídy bugu #70/#265, ne reopen), #412 (popelavá značka odpojených koulí z #342 nečte se dobře, majitel navrhuje stabilní průhlednost s plynulým fade-inem místo desaturace — nová issue, ne reopen #342, protože mechanismus #342 funguje jak byl navržen, jde o iteraci na vzhledu).

**Komentář na #395** (existující, otevřené): majitelův konkrétní požadavek na opravu lávových barev — silnější linky, které nejdou až do `LavaIncandescent` bílé, ale do primární barvy koule — je konkrétní verze kroku 3 issue's vlastního návrhu, proto komentář a ne nová issue.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (devátý zápis dne)

**Revize #393 je na `main`u — merge `cf1c22e`** (`--no-ff` přes `BS3D-322`, `BS3DLibs.sln` po mergi staví čistě). Majitel po sedmém zápisu: "Vypadá to dobře! Jenom objekt, na kterém je úzká hra života, by potřeboval ještě jeden pixel nalevo."

**Ta poznámka byla obecná chyba, ne jedna věž (`43deede`).** Deska mapuje svůj střed (hranici mezi buňkami 15 a 16) na střed stěny, ale vzor s lichou šířkou na tu hranici vycentrovat nejde — ležel o půl okénka vedle, takže jeden okraj byl o celé okénko širší. Sonda: **96 ze 128** kombinací vzor × orientace nebylo na středu desky. `GridLife.CentreX/CentreY` hlásí, kde střed vzoru opravdu přistál (proti orazítkovaným buňkám 0 neshod ze 128), a `GridLifeCentreOffset` posune desku o rozdíl v obou osách. Ověřeno snímkem téže herní kamery ve stejných časech (desky jsou deterministické): pentadekatlon i jeho široká fáze mají teď po obou stranách stejně okének.

Celkem 6 spuštění za celou revizi, všechna `fpscap=75`, bez incidentu. Všechny čtyři solutions staví. Větev smazaná lokálně i na originu, hlavní checkout detached na `origin/main` (main drží worktree `BS3D-322`). Issue nechávám otevřené, zavření na slovo majitele.

**Dodatek:** majitel řekl "393 zavři, je to hotové" — **#393 zavřeno** s komentářem (co je na `main`u a co zůstává mimo rozsah podle issue samotné: kampaň, spirála/Mandelbrot, black-body mód pro koule a dělo).

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (desátý zápis dne)

**Založeno #420: nová kapitola (dvanáctý blok, deset levelů) ve scéně Grid**, na majitelův pokyn. Duplicitu jsem hledal na GitHubu (kapitola, grid, tron, aurora, polar) i v deníku: kapitolu nemá Grid, aurora (#205) ani polar (#222) a issue na ni neexistovalo.

Issue navrhuje styl "tvar levelu je pojmenovaná matematická konstrukce", tedy tezi samotné scény. Kandidáti: generace Life naskládané jako patra (čas jako výška), 3D Hilbertova křivka, Mengerova houba a Sierpińského pyramida, drátěná tělesa. Vyjmenovává existující mantinely: 13 barev, pravidla bran, 35 s visení v Game pro tenké tvary (Trellis, Bolt), `palette.ps1` pod cyan rigem Gridu, `[aimcheck]` v Game, ScoreSim. Majiteli nechává rozhodnutí: pořadí (append nebo vložení; odhad rampy při 120 položkách 236 proti 476 hvězdám, v issue označený jako přepočítat), materiál (reprise), hudbu, jméno bloku a to, jestli blok přinese do kampaně některou z hotových, ale nikde nepoužitých koulí.

**Změřeno při psaní:** v 110 shipnutých levelech je `"k"` 1–4 (kámen, sklo, bomba, zap) ve 3–5 souborech, **wildcard, kyselina, led, nákaza, gravitace a těžká koule v žádném**. ⚠ Grep podle jmen druhů (`"Rock"` apod.) vrací nulu i pro kámen: level ukládá druh číselně jako `"k": N` (`BallKind`).

**Nic si neberu** — #420 je volné.

---

## 2026-09-15 — Claude Code (jedenáctý zápis dne)

**Beru si #397 (výsledková obrazovka: „Next level unlocks at 150 ★ — you have 306“).** Větev `397-result-sequence-note` z `origin/main`, hlavní checkout. Na #389 (`389-bomb-detonation`, pořád nemergnutá) nesahám: jeho hunky v `GameplayScreen.Rules.cs` jsou na řádcích 58–200, můj je u `ShowResultScreen` (~745).

**Příčina ověřená na majitelově `Progress.json` (jen čtení), ne odhadnutá:** „Unlock all“ zapnuté nebylo — po každém startu je vypnuté a nikam se neukládá, a kdyby zapnuté bylo, `||` v `IsLevelUnlocked` by Next Level ukázal. Vent (#76) byl už dohraný (4★), frontier je **#58 Highwall**, Sill (#77) nedohraný → zamyká ho **sekvence**, hvězdy ne. Komentář v `ResultPage` tvrdil, že sekvence na clear dosáhnout nemůže („frontier se už posunul za tenhle level“), což platí jen když dohraný level *byl* frontier. Replay levelu dohraného mimo pořadí (autorův save jich má víc) to vyvrací.

Oprava: `LevelResult.UnlockNote` jmenuje zámek, který opravdu drží (sekvence napřed, slovy výběru levelů), poznámka se zalamuje do šířky plátu (v play přetékala z pravého okraje), nový testovací argument `nextlocked=<stars|sequence>` na stránce `result`. Majitel povolil 3 krátké běhy Game (`fpscap=75`, okno, `mute`).

**Nic dalšího si neberu.**

**Dodatek: #397 je na `main`u — merge `6f4fe09`** (commit `df5dfb4`, `--no-ff` přes `BS3D-322`, `Game` po mergi staví s 0 chybami). Větev smazaná lokálně i na originu, hlavní checkout detached na `origin/main`. Issue nechávám otevřené, zavření na slovo majitele.

- **Poznámka je teď dvouřádková, pravidlo nad čísly:** `Next level unlocks at 216 ★` / `You have 214`, nebo `Levels open one at a time` / `Level 3 · Toadstool is next`. První řez byla jedna věta a zalomení ji rozlomilo uprostřed jména („Level 3 ·“ / „Toadstool is next“). Explicitní `\n` Myra (FontStashSharp) v zalamovaném labelu respektuje, ověřeno snímkem.
- **Proč přetékala:** mechanismus jsem neověřoval, viděl jsem jen výsledek (text začíná u levého okraje popisků a pokračuje ven z plátu). Komentář u `MinWidth` tvrdil, že poznámka „smí být delší než sloupec, místo aby se ořízla“, a v praxi to znamenalo text ven z plátu. Teď má `Wrap` a `Width = BS3DGame.MenuColumnPlateContentWidth` (sloupec minus odsazení plátu; odsazení je nově pojmenovaná konstanta místo literálu 106/67).
- **Doc drift opravený cestou:** `docs/game-session.md` jmenovalo frontier `FirstUnclearedLevel` („první nedohraný“), v kódu je `FirstUnfinishedLevel` (nedohraný **ani nepřeskočený**).
- **Ověřeno:** 3 běhy Game (sequence dvakrát — jednou jako jedna věta, jednou dvouřádkově —, stars jednou), všechny v okně s `fpscap=75`, bez incidentu, `[build]` kontrolovaný. `Settings.json`, `Progress.json` a obě `.bak` mají po bězích stejné SHA-256 jako před nimi. Kontrolní běh `result` bez zámku jsem vyměnil za druhý pokus o znění: prázdná poznámka je `Visible = false` stejně jako dřív.
- **Nerozhodnuté, pro majitele:** hráč, který dohraje level za frontierem, dostane jen Retry a Main Menu. Tlačítko „Go to: Level 58 · Highwall“ by bylo akčnější než věta, ale mění, co výsledková stránka nabízí, tak jsem ho nedělal.

---

## 2026-09-15 — Claude Code (dvanáctý zápis dne)

**Beru si #403 (nohy lafety jsou jen kvádry `AddBox`) na majitelův pokyn „vem další issue, klidně grafické“.** Větev `403-trail-legs` z `origin/main`, hlavní checkout. Sahám na `BS3DLibs/Prazsky.Core/Render/GunCarriageMesh.cs`, podle potřeby na `MeshBuilder.cs` a figury lafety v `Prazsky.BS3D/GameObjects/CannonRig.cs`, a na `docs/testbed.md`. Nemergnutá #389 nemění ani jeden z těch souborů.

⚠ Issue odkazuje na `GunWheelMesh` jako vzor detailu. Ten od #129 neexistuje, kola jsou `OmniWheelMesh`/`OmniRollerMesh`. `docs/testbed.md` ho i se „spoked wheels“ ještě jmenuje v odstavci o `CannonRig`, to je drift a opravím ho v téže změně.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (třináctý zápis dne)

**#403 je na větvi `403-trail-legs` (`a349e7a`), pushnuto, NEmergnuto: čeká na majitelovo oko** (nový vzhled děla, stejný precedens jako #389 a #393). Majitel během práce napsal „teď kanon začíná vypadat k světu“ a v rámci téhož issue přidal hranaté bloky, do kterých nohy vstupují, tedy líce lafety.

- **Nohy (`241bee7`):** zúžený skříňový nosník se zkosenými hranami a dvěma objímkami, kloub s čepem na vnější straně líce, skloněná radlice s broušenou hranou a výztuhou, zvedací madlo. Z původního kvádru zůstala přesně vnitřní a horní stěna, noha rostla jen ven a dolů. Pro závěr hlavně, který při velké elevaci klesá mezi nohy, je to konstrukcí stejně bezpečné jako kvádr, bez proměřování všech elevací. Objímky vystupují na všechny strany, a proto stojí jen tam, kde to výpočet dovoluje. Vnitřní stěna nohy leží asi na 0,75 + 0,70·t od osy (t je poloha podél nohy od kořene, 0 až 1), nejširší ocel hlavně (základní prstenec) na 0,845. Kolize je tedy vyloučená od t = 0,16 a objímky sedí na 0,34 a 0,49.
- **Líce (`7cbc0fc`):** oblouk kolem osy čepu s poloměrem `CHEEK_TOP_Y` (0,2 → 0,34, konstanta dostala nový význam), tečné přechody do ramen, sražení vnější stěny, ložiskové prstence čepu a nápravy, dva šrouby a žebro. Vnitřní stěna zůstala na místě, přiléhá k hlavni.
- **Výsledek:** 4 běhy Testbedu z povolených 6, všechny s `fpscap=75`, bez incidentu. Snímky z herní kamery ve 25°, 40°/40° (stejný záběr jako výchozí stav) a 80° (závěr mezi nohama bez kolize). Všechny čtyři solutions staví s 0 chybami. Opravený drift: `GunWheelMesh` a „spoked wheels“ v `docs/testbed.md` i v CLAUDE.md jsou teď `OmniWheelMesh`/`OmniRollerMesh`.

### ⚠ Dvě pasti snímkování, obě změřené

- **Řádek `[build]` v Testbedu razítkuje jen `Testbed.dll`.** Změna v knihovně se na něm neukáže: běh 3 měl stejný hash jako běh 2, a přesto kreslil nový mesh. Důkazem je čas zápisu `Prazsky.Core.dll` vedle exe proti času zdroje.
- **Opakované `shot=` se nesčítají.** Tři argumenty `shot=` v jednom běhu daly jediný PNG. Časy patří do jednoho seznamu, třeba `shot=9,12,14.5`.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (čtrnáctý zápis dne)

**#403, další kolo: majitel viděl, že noha „viditelně tuneluje“ plech lafety.** Pořád na větvi `403-trail-legs` (tip `ef22675`), pořád NEmergnuto, čeká na majitelovo oko.

- **Příčina je geometrická a v kódu už byla: noha je širší než plech, do kterého vchází** (0,18–0,22 proti 0,14), takže jím prošla oběma stranami. Dovnitř mezi plechy vyčuhoval proužek, a to je místo, kam se herní kamera dívá. Zvenku a na zadní hraně vznikal zkosený osmiúhelník průsečnic. Žádná šířka nohy se do tak tenkého plechu nevejde.
- **Oprava (`7ae767a`):** kořen nohy je usazený tak, že jeho nejvnitřnější roh leží 0,005 za vnitřní rovinou plechu. Poloha je dopočítaná v kódu, protože u nohy, která se rozbíhá a zároveň klesá, je horní osa (upright) nakloněná o 0,13 v x a spodní vnitřní roh tak sahá o 0,03 dál dovnitř než samotná stěna. Dolní zadní čtvrtina plechu je navenek zesílená do patky (socket) se sraženými hranami, ložiskem kloubu a čepem. Místo nohy na výstupu z rovné zadní stěny zakrývá objímka. Starý kloub na plechu je pryč. Každá část nohy se tím posunula dál od hlavně, takže vůči závěru je to jen lepší.
- **Kola:** nejblíž vnitřnímu plechu kola je čep kloubu, s rezervou 0,02 (1,09 proti 1,11). Objímka má nejvýš 1,066. Poloviční šířka kořene nohy klesla z 0,11 na 0,09.
- **Ověřeno:** běh 5 z povolených 6 (`fpscap=75`, bez incidentu). Detail zezadu mezi nohama, herní kamera rovně a s traverzem ±40° ukazují, že noha nikde neprochází a vychází z patky přes objímku. Testbed i Game staví s 0 chybami, `docs/testbed.md` je aktualizovaný.

**Nic dalšího si neberu.**


---

## 2026-09-15 — Claude Code (patnáctý zápis dne)

**Beru si #396 (osiřelá bomba vybuchne).** Dnes se bomba odjišťuje dvěma způsoby, oba geometrické: zásah vedle ní (`CollectArmedSpecials`) a dosah cizího výbuchu (`DetonateBombs`, řetězení přes worklist). Chybí třetí, čistě **konstrukční**: bomba, které `GetCellsDisconnectedFromCeiling()` sebere poslední cestu ke stropu, dnes spadne jako obyčejná koule. Větev `396-orphan-bomb`.

**⚠ Není to opomenutí, je to obrácení dosavadního pravidla** — a to je na tomhle úkolu to podstatné. `BallContactEventHandler` u sběru odjištěných bomb doslova píše: „bomba, kterou release OSIŘÍ, už spadla a nesmí vybuchnout ve vzduchu". Issue říká opak („je oddělená, takže jde"), takže se nemění jen kód, ale i ten komentář — jinak by v repu stálo špatné „proč".

**Beru trigger, ne efekt ani model:** flash/ohnivá koule a zvuk jsou #389, aktivační/nábojový model je #392.

**Nic dalšího si neberu.**


---

## 2026-09-15 — Claude Code (šestnáctý zápis dne)

**#396 hotové na větvi `396-orphan-bomb` (`abc15fa`, `f86369b`), NEmergnuto.** Osiřelá bomba vybuchne. Větev přerebasovaná na aktuální `origin/main` (byla odbočená uprostřed #393), čtyři solutions 0 chyb.

### Pravidlo je napsané jednou, a to je na tom to podstatné

Čtyři odstraňovací cesty (match, zap, acid, blast) končily **doslovnou kopií** téhož `foreach (cell in GetCellsDisconnectedFromCeiling()) ReleaseBall(cell)`. Kdyby pravidlo „co dělá odpojená buňka" šlo do nich, psalo by se čtyřikrát — a popáté v `SagProbe`, který si celé přistání opakuje. Místo toho je jeden sdílený `ResolveDisconnected`, do kterého ty čtyři ústí. **Sonda nedostala ani řádek**, přesně jak to v #329 udělal thaw; co se v ní měnit muselo, byl komentář.

**Chůze běží ve smyčce, ne jednou na konci**, protože výbuch osiřelé bomby může podetnout další podporu. Končí na `queued` množině: nejvýš jedno kolo na bombu v poli.

### ⚠ Obrátil jsem pravidlo, které stálo napsané pětkrát

„Bomba, kterou release osiří, už spadla a nesmí vybuchnout ve vzduchu" stálo v `BallContactEventHandler`, v `SagProbe`, v `BallKind.Bomb`, v `docs/game-session.md` a v `docs/formats-and-tools.md`. Všech pět přepsáno v témž commitu — nejen kód. Argument proti nim: **chůze je flood fill nad mapou, ne fyzikální událost**, najde bombu v okamžiku, kdy jí podporu podetnou, a koule se ještě ani nehnula. Žádný „vzduch" tam není.

### Skóre: rozhodnuto per oběť, a je to invariant, ne odhad

Oběť výbuchu, kterou chůze **už předtím** našla jako visící na ničem, zůstává `Orphaned` (dvojnásobná sazba) — odešla proto, že jí někdo podetnul podporu, výbuch jenom vybral, kterým směrem letí. `Destroyed` je jen to, co výbuch vezme z **pořád stojícího** clusteru. Alternativa (celý rádius jako Destroyed) byla spočítaná a zamítnutá: koule, které už vydělávaly dvojnásobek, by spadly na sazbu matche, takže **efektnější výsledek by platil míň**. Tak vznikl invariant, který je teď napsaný v `BallsReleased`: *výbuch osiřelé bomby může k hodnotě výstřelu jen přidat.*

Na první kolo `DetonateBombs` je množina „už padajících" prázdná, takže **odjištěná bomba se počítá přesně jako před #396** — žádná existující cesta se nehnula.

### ⚠ `Destroyed > 0` přestalo být testem „vybuchla bomba"

Výbuch uvnitř už padající oblasti nezničí nic — a přesně na tenhle test visela `[shot]` řádka. Teď počítá bomby, které **vystřelily** (`detonatedInto`, vzorem `thawedInto`). ⚠ **Ta řádka se nečistí v žádné ze čtyř metod, ale jednou za přistání v handleru** — detonace patří přistání, ne jednomu jeho kroku.

### ⚠ Seam s #389 je skutečný a **změřený**, ne odhadnutý

`origin/389-bomb-detonation` (hotová, čeká na majitele) přepisuje **tutéž smyčku** a přidává `Detonation` záznam s pozicí těla, hloubkou řetězu a počtem. Zkušební merge: **10 konfliktních hunků / ~199 řádků ve dvou souborech** (`BallsConstraintsBuilder.cs`, `BallContactEventHandler.cs`), všechno ostatní se slučuje samo.

- **Kvůli tomu jsem `BallLanding.Detonated` zase zahodil**, i když jsem ho už měl napsaný: #389 dává na totéž místo bohatší `Detonations`, takže můj holý počet by byl druhá, horší odpověď na stejnou otázku — a hlavně by přidal konflikt do souboru, který sahá do Hry. Po zahození se `BallLanding.cs` sloučí bez konfliktu.
- **Instrukce pro toho, kdo bude mergovat:** vzít smyčku z #396 (`ResolveDisconnected`) a dovnitř ní vložit z #389 `links`, `blasts`, `Throw` z těla místo z buňky a `ThrowOrphan`; `detonationsInto?.Add(...)` patří přesně tam, kde teď stojí `detonatedInto?.Add(bomb)`. **Bez toho posledního kroku osiřelá bomba po mergi #389 vybuchne beze záblesku a beze zvuku.**

### Ověřeno

- **Čtyři solutions 0 chyb; LevelGen exit 0 a `Game/Levels` beze změny; ScoreSim „All levels rate the right way round".** LevelGenovy statické brány bombu vůbec nemodelují (`Program.cs:1618-1624` to říká samo), takže se výstup změnit ani nemohl.
- **Bezgrafický rig** (scratchpad, referencuje tři knihovny, sonda `SagProbe`ova tvaru bez kroku simulace), pět scénářů, čísla před/po:

| scénář | před | po |
|---|---|---|
| A: bomba pod řezem | 3 m, 5 o, **0 vystřelilo** | 3 m, 5 o, **1 vystřelila** |
| B: stojící cluster v dosahu | 3 m, 5 o | 3 m, **6 o, 3 zničené** |
| C: dvě bomby týmž řezem | 3 m, 8 o, 0 vystřelilo | 3 m, 8 o, **2 vystřelily** |
| D: druhá bomba osiřelá **výbuchem první** | 3 m, 1 o, **bomba zůstala stát** | 3 m, **3 o, 3 zničené**, obě vystřelily |
| E: `Testbed\Maps\OrphanBomb.json` | — | 18 m, 63 o, bomba v (2,4,7) |

D je ten, který odděluje jednokolovou odpověď od smyčkové: druhá bomba není v rádiusu první a osiří až tím, co první výbuch sebral. A na A i C je vidět, proč byl potřeba počet vystřelených — čísla se **nezměnila vůbec**, a přitom bomba vybuchla.

- **Sonda na třech ostrých levelech s bombami** (`--sag=Vent,Sill,Paroxysm`, před i po): všechny tři pořád **sagged 0 of 5, worst: Cleared**. Verdikt se nehnul. Jednotlivá čísla ano (Vent 23 ran místo 26 a 2,68 od čáry místo 0,90; Sill 41/33 a −0,43/−0,12; Paroxysm 10/10 a −0,97/−0,75) — ⚠ **to není A/B, jsou to jiné průchody**: první osiřelá bomba změní pole a každý další výstřel model vybírá proti levelu, který druhý běh nikdy neviděl.
- **Za běhu v Testbedu** (`Maps\Bombs.json`, `at=`/`aim=`/`Space`): `[shot] 1 bomb(s) armed, ... 2 bomb(s) fired, destroyed 44, orphaned 0` — handlerová cesta, čištění seznamu i nová řádka ověřené v reálné smyčce. (Dvě vystřelené z jedné odjištěné je řetěz rádiusem, který je tu od #326.)

### Co NENÍ ověřené a nebudu to předstírat

**Osiřelou detonaci se mi nepodařilo vyvolat skriptovaným výstřelem.** `aim=` mířím naslepo bez pohledu na obrazovku a z ~30 pokusů na nové mapě nepřistála ani jedna rána (`bounced` nebo nic). To je omezení, které `docs/testbed.md` samo přiznává („no scripted firing by mouse"), ne vlastnost změny — mapa visí správně, ověřeno snímkem, a rigem se řeže přesně tak, jak má. **Mapa je pro majitelovu ruku a myš, ne pro skript.**

### `Testbed\Maps\OrphanBomb.json` — proč je postavená takhle

235 koulí, **jediná matchovatelná barva v celé mapě** (zbytek obarven greedy tak, že se žádné dvě sousedící neshodují — generátor si to sám dokazuje flood fillem), a **bomba zahrabaná**: každá buňka, která se jí dotýká, je obsazená, takže ji výstřel **nemůže** odjistit tak, jak se bomba odjišťuje od #326. Co v ní vybuchne, může být jen nový trigger.

### Co si neberu

Efekt (#389), aktivační model (#392) a **naučit `ScoreSim` výbuch**. To poslední je pojmenované i v docs: #396 tu díru **rozšířilo, ne prohloubilo** — přibyla přistání, která jsou zčásti orphan a zčásti destroyed, a ten model je neumí vyrobit ani v principu. Sazba je pořád *zdůvodněná, ne změřená*, stejně jako před #396.

**Bezgrafický rig zůstal ve scratchpadu** (tj. zmizí). Jestli ho má být čtvrtý nástroj vedle LevelGenu, ScoreSimu a MusicBaku, je to rozhodnutí majitele — nabízím, nedělám.

**Merge na slovo majitele; zavření issue taky.**

---

## 2026-09-15 — Claude Code (sedmnáctý zápis dne)

**#396 je na `main`u** (merge `ea43ea4`, `--no-ff`) **a zavřené** — majitel řekl "Mergni, uzavři". Větev smazána lokálně i na originu. Zápisy výše zůstávají stát tak, jak byly psané — včetně "NEmergnuto" a "merge na slovo majitele", což tímhle přestalo platit. Šev s #389 je pořád otevřený a změřený (10 hunků / ~199 řádků), instrukce pro merge je v zápisu výše.

**Beru si #395 (lávové barvy).** Majitel: *"Tam je to vážně problémové a potřebujeme dramatické vylepšení."* Větev `395-lava-colours`. Issue má dvě půlky a **tři majitelovy komentáře**, které směr určují přesněji než tělo issue:

1. **Barvy se na Eruption bloku slévají.** Deset levelů, osm inkoustů; černá+hnědá v devíti z deseti, oranž+hnědá v osmi, červená+oranž v osmi. ⚠ **#315 měřilo Lavu pod JINOU scénou a jinou kopulí** — pod sopkou a jejími červeno-oranžovými světly, kde se těch deset levelů skutečně hraje, paletě nikdo nikdy nezměřil.
2. **Konkrétní zadání na opravu palety (komentář 12:41):** linky mají být **tlustší** a **nemají jít až do bílé** — nejteplejší bod má pořád číst jako vlastní barva koule, jen jasnější. Dnes `LavaCorePower` nese jádro do `LavaIncandescent` (1.0, 0.86, 0.62), jedné sdílené skoro-bílé, ať je koule jakkoli barevná — což je přesně důvod, proč červená, oranžová a žlutá konvergují.
3. **Kulka v děle svítí světleji než táž barva na clusteru** — tři mechanismy, každý s vlastním důvodem (#252 zásobník nedýchá, #303 cluster je zastíněný, #236 halo v notchi). ⚠ Majitelův komentář (07:42) říká, že to **není problém jen Lávy/Plasmy** — též tři mechanismy platí pro všechny styly a jen *emisní* příspěvek byl odůvodněn jako malý; obyčejný difuzní rozdíl změřený nebyl. Měřím tedy i na vinylu.
4. **A má to herní následek, ne jen estetický** (komentář 12:43, Volley): majitel měl za to, že se stejnobarevný cluster nerozpojuje — tedy že je to bug v pravidle — když ve skutečnosti střílel do barvy, která jen vypadala podobně.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (osmnáctý zápis dne)

**#395 (lávové barvy), první polovina hotová a na `main`u. ⚠ ISSUE NEZAVÍRÁM — druhá polovina zůstává a je pojmenovaná níž pro toho, kdo přijde po mně.** Dochází mi týdenní limit, majitel řekl „dostaně co máš na main a napiš komentáře pro ostatní agenty“ — takže tohle je předavka, ne hotová věc.

### ⚠ Nejdůležitější věc z celého úkolu: **paleta se změřila dobře a to byla past**

`palette.ps1 -Whole` pod sopkou a kopulí 9 — pod scénou, kde se těch deset levelů **opravdu hraje** a kterou #315 nikdy neměřilo (měřilo Lavu pod Reveal) — dává nejtěsnější pár **orange/brown 7,4 dE** proti vinylové kontrole 7,9. Tedy: nic. A přitom si majitel stěžuje právem.

**Průměr přes disk je pro tenhle styl špatný přístroj.** Michá svítící síť s kůrů, která je na všech třinácti stejně černá, a hlásí barvu, kterou oko nikdy neizoluje. Co oko na lávě čte, je **síť**. Změřeno přes **nejjasnější desetinu disku**, čtyři snímky na build: původní styl měl střední sytost jader **0,283** přes osm inkoustů bloku — silver **0,00**, black 0,06, blue 0,07, white 0,11 — tedy osm ze třinácti koulí nosilo **neutrální** síť, a nejtěsnější pár byl **yellow/white 7,6 dE** na všech čtyřech snímcích. To je `LavaIncandescent`: přenos do skoro-bílé byl **nezastropovaný**.

**Skript na to je ve scratchpadu a zmizí** (`cores.py`). Jestli se v tomhle bude pokračovat, patří to jako třetí režim do `palette.ps1` vedle výchozího a `-Whole`; nabízím, nedělám — skill je majitelův nástroj.

### Co je opraveno (tři páky, všechny v `InstancedModel.fx` + jedna konstanta v `BallRenderSet`)

- **`LavaCoreCarry` 0,30** — zastropuje, jak daleko jde jádro do bílé. **`LavaCoreLift` 1,4** vrací jako **jas** to, co dosud říkala ztráta barvy. Majitelova věta doslova: *nejteplejší bod má pořád číst jako barva koule, jen jasnější.* ⚠ Carry **není nula** záměrně — jádro přesně vlastní barvy čte jako čára namalovaná v drážce, což je právě to, kvůli čemu carry vzniklo.
- **`LavaHuePower` 1,7** — řeže normalizovaný odstín hlouběji. ⚠ **Ne `SaturateTint`**, což je ta zřejmá volba a **nedělá nic**: její první krok je `primary/peak`, což styl už měl. Stálo mě to jeden pokus, ať to nestojí dalšího.
- **`LavaValuePower` 1,0** — rozevírá odmocninu v `TintEmission`, která stlačovala jedinou osu, co na teplé inkousty zbývá. ⚠ Je to **lávina vlastní kopie** křivky, ne zásah do sdílené — tu volá i **plasma**, kterou jsem neměřil.
- **Šířka švů 0,36 → 0,46 a `LavaHeatWidth` 1,9 → 1,55 v jednom kroku.** ⚠ **Nelze zvednout jedno bez druhého**: šířka halo je **násobek** švu a `SeamLine` porovnává |sin| proti šířce, takže součin blízko 1 rozsvítí celou kouli. Halo drží 0,684 → 0,713.

**Změřeno** (čtyři snímky na build, týž pin): střední sytost jader **0,283 → 0,510, +80 %**, každý inkoust nahoru (brown 0,58→0,76, red 0,40→0,58, orange 0,36→0,52, yellow 0,24→0,36).

### ⚠ CO TO NEOPRAVILO — a tady je čára pro dalšího agenta

Nejtěsnější pár se pohnul jen **7,6 → 8,0 dE**. Ale **změnil identitu**: z yellow/white (neutrální splynutí, co dělal bílý carry — to je pryč) na **brown/orange/red**. Ty tři jsou **jedna barevná rodina** a odstínem se rozdělit nedají; jediná osa je hodnota, `LavaValuePower` ji už utratil, a co zbývá, jsou **inkousty bloku** — tedy krok 3 samotného issue, v `LevelGen`, kde zákon Eruption zní tři studené a dva teplé a **Meander, Plume a Paroxysm nesou víc**. To je podle mě příští krok a je to změna, která přegeneruje level soubory (pozor: brány LevelGenu, ScoreSim, sonda).

### Druhá půlka issue: kulka v děle — **dýchání opraveno, zastínění NE**

Nový uniform **`StillEmission`** (default 1, no-op). Still plane ho dostává `1 - PulseDepth`. Důvod: **#252 „nabítá koule nedýchají“ bylo uděláno jako `PulseDepth = 0`, jenže to neznamená „klid“, ale „trvale na VRCHOLU kývu“** — všechny emisní výrazy jsou `lerp(1 - PulseDepth, 1, beat)`. Kulka tedy svítila ~1,6× proti klidové kouli v clusteru. ⚠ **Záměrně samostatný uniform a ne menší `PulseDepth`** — ten by kulku rozdýchal, což #252 na majitelův pokyn právě odstranilo.

**Still plane používají JEN čtyři řádky** v `Game/Screens/GameplayScreen.Draw.cs` (488, 495, 496, 498) — Testbedův zásobník ani aim ghost na něm nejsou (obojí `still: false`). Blast radius je tedy úzký; ověřeno čtením, ne odhadem.

**⚠ Co zůstává z téhle půlky:**
1. **Zastínění.** Kulka dostává `UNOCCLUDED`, koule v clusteru ne. Po mé opravě je poměr cca 2,5× → **~1,35×**, zbytek je právě tohle. Issue navrhuje dát nabítým koulím „typické povrchové zastínění“ — **to je ale rozhodnutí vkusu, ne aritmetiky**: kulka v hlavni kolem sebe opravdu nic nemá a `UNOCCLUDED` to říká pravdivě. Nechávám majiteli.
2. **Majitelův komentář ze 7:42 žádá změřit i NEemisivní styl** (vinyl na obyčejné scéně), protože těch tři mechanismů platí pro všechny styly. **Neuděláno.** Aritmetika ovšem říká, že to není jen Láva: v `BallEmission` je to `(1-PulseDepth)*occ² + PulseDepth*beat`, takže still plane s occ=1 dostával 1,0 proti klidové kouli 0,62·occ² — při occ 0,8 je to **2,5× i na vinylu**. `StillEmission` to sráží všem stylům najednou.
3. **Vizuálně NEOVĚŘENO před/po pro kulku.** Udělal jsem jen kouřový test (`BS3D.exe play level=Vent shot=9` — hra běží, cluster čte dobře), ne párové snímky notche proti kouli též barvy, které issue žádá s drženým RMB.

### Ověřeno

Čtyři solutions 0 chyb; LevelGen exit 0 a `Game/Levels` beze změny; ScoreSim „All levels rate the right way round“; hra na Ventu naběhne a hraje. **Neměřený výkon** — přibyl jeden `pow` a jeden násobek na pixel v `LavaPS`, což je řádově to, co #338 změřilo jako šum, ale **změřeno to není a nemám to vydávat za změřené**.

### Ostatní styly

`StillEmission` je default 1 a dechájící plane ho dostává 1, takže **mimo still plane je to identita**. Ale je to čtyři místa v shaderu (`BallEmission`, bubble, plasma, lava) — kdo bude sáhat na emisi, ať to čte.

**Nic dalšího si neberu — dochází mi limit. Kdo vezme pokračování, začíná u „CO TO NEOPRAVILO“ výše.**

---

## 2026-09-15 — Claude Code (zápis k #403, tunelování hlavně)

**#403, další kolo: majitel viděl, že hlaveň při zvedání prochází boky lafety.** Pořád na větvi `403-trail-legs` (tip `6966491`, kód `1fcc954`), pořád NEmergnuto, čeká na majitelovo oko.

- **Změřeno na CPU, ne odhadnuto.** Výpočet ve scratchpadu projde každou elevaci od −6° do 87° (vůči lafetě; na misce se lafeta naklání až o ~6,4°, takže relativně až ~86,6°) a zdvih zpětného rázu 0–1,15. Proti tomu testuje každou část lafety. **Základní prstenec závěru (r 0,845, nejširší ocel děla) procházel vnitřní stranou plechů (0,78) od ~36° až nahoru, až 0,065 hluboko. Stejně tak kořeny nohou a od 52° i celou tyč nápravy (0,84 hluboko).** **Původní kvádry dělaly totéž**, takže vada je starší než brackety z #403.
- **Oprava je pravidlo, ne kontrola póz.** Lafeta se otáčí s míříkem, hlaveň se tedy jen zvedá kolem osy čepů a klouže podél své osy. Plechy proto obepínají hlaveň (`CHEEK_INNER_X`) jen v **nábě**, tedy do vzdálenosti, na kterou se širší ocel nikdy nepřiblíží, zpětný ráz započítaný (`CannonMesh.NearestSteelWiderThan` − `CHEEK_HUB_CLEARANCE`, na shipnutých figurách 1,25). Jinde stojí na nejširší oceli + `CHEEK_RELIEF_CLEARANCE` (0,865). Obojí se čte z postavené hlavně. Nohy jsou usazené za touto rovinou. **Náprava jsou dva čepy končící v plechu**, protože závěr při velké elevaci zabírá celý prostor mezi plechy a žádná příčka tam stát nemůže.
- **⚠ Úzké hrdlo jsou kola, ne hlaveň.** Mezi odsazenou rovinou a vnitřním plechem kola zbývá tam, kde noha vychází z patky, jen ~0,245. Výpočet kontroluje i kola: noha s poloviční šířkou 0,09 zajela do plechu kola o 0,011, při 0,075 má vůli 0,021. **Průřezy nohou jsou proto svislé**, ne kolmé na nohu, protože kolmý průřez nohy, která se rozbíhá i klesá, naklání vnitřní stěnu dole o 0,03 dovnitř. Kloub se přestěhoval na horní hranu patky, protože na vnější stěně patky pro něj u kola není místo.
- **Kola jsem schválně NEposunul.** Rozchod 1,5 vstupuje do `GameCameraFit.CANNON_DRAIN_CLEARANCE` (vnitřní kolo při plném traverzu stojí na hraně zlatého lemu), takže posun ven by změnil parkování děla na všech levelech.
- **Ověřeno:** 1 běh ze 2 povolených (`fpscap=75`, bez incidentu). Herní kamera ve 25°, 40°, 60°, 80° a v 60° s traverzem 35°: závěr klesá mezi plechy bez průniku, pod ním už není žádná tyč. Testbed, Game i MapEditor staví s 0 chybami, `docs/testbed.md` je aktualizovaný.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (třináctý zápis dne)

**Další dávka playtestu (tabulka Zábavnost/Obtížnost, Plume až Obsidian — Eruption's ocas plus celý Spectrum, Arcade a Mirage, 30 levelů) protříděna do dvou nových issues + pěti komentářů**, stejnou disciplínou jako první a druhá dávka ([[playtest-log-triage]] v paměti — tahle dávka dotáhla tabulku k Obsidianu, `Levels.json`'s poslednímu ze 111 levelů, takže celá shipnutá kampaň teď má projetý jeden playtest): komentář, ne nová issue, kde už existující issue kryje totéž; nová issue jen tam, kde nic nekrylo. `gh issue list` a čtení tohoto deníku napřed a ještě jednou těsně před zakládáním (dvě samostatné kontroly, ne jedna).

**Nové:** #421 (Donut: barvy — čokoládové těsto/bobulová poleva nečtou se jako donut, majitel navrhuje těstovou barvu, růžovou polevu, tříbarevné posypání), #422 (`Block11_Mirage.cs`'s hlavičkový komentář o skle je doc drift — pořád popisuje pre-#344 mechaniku "jedno políčko obarví jen sousední tabule", kód dávno barví celou spojenou skupinu přes `ColourTransparentGroup`; ověřeno přímo v `BallsMap.cs`).

**⚠ #422 jsem si po založení musel sám opravit komentářem** — teprve při psaní TOHOTO zápisu jsem našel `2026-09-03, čtvrtý zápis dne` (řádek ~588 výš), kde #344's implementace řeší přesně Facetův případ a ověřuje ho čtrnácti tvrzeními proti skutečné `BallsMap`, ne mockem: Facetových 64 skleněných tabulí se navzájem vůbec nedotýká (64 těles po jedné), trik je v tom, že jedna dopadová buňka se diagonálně dotýká DVOU different izolovaných tabulí najednou, takže `ColourTransparentGroup` proběhne dvakrát a obě vyjdou obarvené z jedné rány — a přesně tohle "Facetův tah" je jedno z těch čtrnácti ověřených tvrzení. Moje issue's bod 2 ("ověřit ve hře, jestli se Facetovy tabule opravdu párují") tedy míří na něco, co už bylo ověřeno proti knihovně samotné — zůstává jen ten doc drift v hlavičkovém komentáři, ne přehrání ve hře. **Poučení pro příště: hlubší historie v deníku (ne jen ocas) může vyvrátit vlastní issue dřív, než ji stihne přečíst majitel** — grep na klíčové jméno přes CELÝ soubor (a archiv), ne jen `tail`, patří před založení stejně jako `gh issue list`.

**Komentáře:** #395 (otevřená, lávové barvy) — Plume/Sill/Fume/Caldera jako další potvrzení, a nové vodítko: Sill a Fume táhne černá/hnědá/stříbrná (studená rodina), zatímco issue's vlastní "what's left" jmenuje jen teplou (hnědá/oranžová/červená) jako nevyřešenou; Caldera's "celá kapitola se bude muset předělat" jako nejsilnější verdikt dosud. #389 (otevřená, bomba nemá detonaci) — Vent jako přímý repro (bomby jen spadnou); Paroxysm's poznámka o novém typu kuličky na konci kapitoly, co se stejně bude předělávat, jako poznámka k pořadí zavádění speciálů (ověřeno v `Block08_Eruption.cs`: Bomb i Zap se v kapitole objevují už dřív, takže nejde o technicky nový druh, spíš o první čitelné rozpoznání). #359 (zavřená) — Cube a Cabinet jako dva další "reached the line" reporty, oba už měřené pod issue's vlastním prahem 4 z 5 (Cube 2/5, Cabinet 3/5 — "Amphora's own reading"), ale nejsilnější subjektivní reporty dosud (feedback po 10 a 20 pokusech) — mezera mezi prahem a skutečnou tolerancí, neřešeno, jen zapsáno pro majitelovo rozhodnutí. #360 (zavřená) — Cabinet's tvarová nečitelnost, přesně ten level, co #360's vlastní uzavírací komentář výslovně vynechal ze scope. #413 (otevřená, pořadí levelů) — rozšířeno o Spectrum (Trellis/Pleat swap, Bolt na konec kapitoly, Kiln blíž začátku), Arcade (Ziggurat vs. Cube jako opener), Mirage (Trefoil vs. Facet's záměrný "učí mechaniku" opener — konflikt, neřešeno; Keystone dřív; Obsidian — `Levels.json`'s opravdu poslední ze 111 levelů, ne jen kapitoly — jako antiklimaktický konec celé kampaně).

**Pozitivní/neutrální řádky (drtivá většina dávky) nepotřebovaly issue, ale majitel výslovně chtěl zachytit i to, PROČ jsou dobré levely dobré**, aby další generování v `Tools/LevelGen` dál opakovalo totéž místo jen reagování na stížnosti. Nová paměť `level-design-fun-patterns` (odkazovaná z `level-design-rules` a `levelgen-universal-generator-goal`): strategické riziko/odměna jako nejčastější "tohle je skvělé" vzorec (Pleat/Trellis/Reel/Tetra), flexibilní strop dovednosti (Ghost — 3 rány nebo pomalé patlání, obojí legitimní), jedna silná čitelná vizuální myšlenka nad pouhou správnou okupací (Seam — "Extrémní" zábavnost, strop škály celé dávky; kontrapříklady Cabinet a Plume, kde je vzor v pořádku, ale téma se nečte), rytmus kapitoly (lehký/atraktivní opener, těžký closer, ale ne anticlimatický konec celé kampaně — Obsidian), náhoda v tahu kuliček je přijatelná do měřené míry (Globe potvrzuje #359's vlastní práh, ale Cube/Cabinet ukazují, kde ta míra končí).

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (čtvrtá dávka poznámek z playtestu, #423-438)

**Majitel poslal další volný seznam postřehů z hraní (~18 položek — vizuál, chování kamery, pár bugů), s výslovnou instrukcí založit issue na základě těchto poznámek.** Stejný tvar úkolu jako dávka zdokumentovaná v [[playtest-log-triage]] (#401-#412) — tedy žádné filtrování na „jen to, co je skutečně problém": jedna položka na issue, i když se překrývá s něčím starším. Před založením zkontrolován `gh issue list` (běžel až po #422, oba nové) a tento deník.

**Jedna položka se do issue nedostala:** tráva v lese (nečte se jako tráva, navíc není tak zelená jako skutečné lesní dno) přesně sedí na už otevřené #281 (design brainstorm o trávě louky/savany/lesa) — přidán komentář místo duplicity, s tím jedním detailem (barva), co #281 samo nejmenuje.

**Založeno šestnáct issues, #423-438:**
- #423 — drenážní trychtýř: skleněný kužel čte jako fasetovaný, ne hladký. **Ověřeno vizuálně, ne jen čtením kódu** — `403-trail-legs` (hlavní větev) aktuálně nejde přeložit (`TRUNNION_OUTER_X` neexistuje, rozepsaná práce na #403), takže jsem postavil Testbed ve WORKTREE `BS3D-322` (ten, co nese tenhle deník) po `git pull --ff-only`, a odtud vyfotil trychtýř shora i od boku (`scene=meadow`/`scene=savanna`, `nopost nooverc`). Vidět čistý vějíř ~64 střídavě světlých/tmavých klínů, stejný pod oběma scénami i úhly — geometricky obě `FunnelMesh` konstruktory i `FunnelRimsMesh` už mají per-vertex hladké normály (jako opravený `TrophyMesh`), takže to není chybějící vyhlazení sítě, spíš specular/Fresnel na 64 segmentech u odrazivého materiálu (stejná třída problému, jakou `TrophyMesh`ův komentář pojmenovává pro „mirror-finish" povrch).
- #424 — kamera na padající kuličky (`DropCinematic`) by měla vždycky naskočit na výstřel, co dokončí level, i pod `MIN_BALLS=12`.
- #425 — barevný záblesk na ústí hlavně (`BallGlow`) je billboard čelem ke kameře, occlusion hlavní se mění s každou rotací — proto to „tuneluje" nekonzistentně; návrh je skutečný prstenec/límec jako geometrie.
- #426 — diamantový pohár: křišťál nemá lom světla, jen alpha-blend pozadí — nečte se jako sklo.
- #427 — nová obrazovka Help (pravidla, speciální koule, budoucí power-upy, slovník skóre s příkladem výpočtu, ovládání) — výslovně odlišná od #189 (tutoriál).
- #428 — 2D náhled mapy: kuličky pod červenou čarou mizí skoro okamžitě (`PROFILE_SINK_FADE = 2` proti ~36 jednotkám skutečného pádu do `KILL_PLANE_Y`).
- #429 — tvar poháru přes všechny tiery je plochý/primitivní, chce výšku a zdobení (kameny), ne jen hladkost (#271 řeší jen fasety).
- #430 — ohňostroj po výhře: kamera na výsledkové stránce se na explozi skoro nedívá, a výška výbuchu je vyladěná proti herní kameře, ne proti orbitu na result page.
- #431 — žádná zpětná vazba na stropu náklonu hlavně — mířící kříž by měl červeně blikat na `ElevationLimit`.
- #432 — kuličky pořád přichytávají na samý okraj stropu; majitel navrhuje zakázat vystřeleným kuličkám přímé přichycení ke stropu úplně (jen k jiným kuličkám).
- #433 — neonové město: úvodní průlet kamerou mezi mrakodrapy zblízka (styl Spider-Mana), ne vzdálený záběr.
- #434 — „cluster reached the line" má být dramatická: kamera na místo dopadu, čára zesvítí, zvuk/VFX — majitel to sám označil jako velký úkol.
- #435 — okenní rámy na budovách čtou jako tenké/nevýrazné, přestože shader (`WindowFrameProfile` v `InstancedModel.fx`) má hotovou geometrii i stínování — vypadá to na ladění `CitySceneConfig` (`WindowFrameWidth/Height` jsou dnes malé proti rozteči).
- #436 — střechy City/Neon City chtějí satelity/antény/5G vysílače.
- #437 — Wildcard kulička nemá žádný vizuální signál v okamžiku, kdy se na dopadu ustálí na konkrétní barvě.
- #438 — pauza rozostří 3D scénu (`FrameBlur` → `PostProcessPipeline.Resolve`), ale HUD (`PlayHud`, kreslený zvlášť přes `OverlayBatch` PO resolve) zůstává ostrý včetně létajícího čísla skóre — majitel si všiml právě u něj, ale je to obecná mezera.

**Nic dalšího si neberu.**

**Dodatek: #403 je na `main`u — merge `471ac69`** (`--no-ff` přes `BS3D-322`). Majitel: „věřím ti, že vypadají dobře, tak to mergni“. `BS3DLibs.sln`, `Testbed.sln` i `Game.sln` po mergi staví s 0 chybami. Větev je smazaná lokálně i na originu, hlavní checkout stojí detached na `origin/main`. Issue nechávám otevřené, zavření je na slovu majitele.

**Dodatek:** majitel řekl „Zavři“ a **#403 je zavřené** s komentářem: nohy, plechy, pravidlo průchodu hlavně a zdůvodnění, proč kola zůstala na místě. Po restartu desktopu (Kernel-Power 41 v 19:58, v klidu, 3 minuty po posledních buildech) prošel `git fsck` bez chyb. Jedinou škodou byl vynulovaný `refs/remotes/origin/HEAD`, který opravilo `git remote set-head origin -a`.

---

## 2026-09-15 — Claude Code (zápis k #431, strop náklonu)

**Beru si #431 (hráč nedostane žádnou odezvu, když narazí na strop náklonu).** Větev `431-elevation-cap-feedback`, pracuju v hlavním checkoutu `BS3D`.

- **Hlaveň zůstává na `ElevationLimit`/`MinElevation` tvrdě oříznutá, „gumový“ bude jen signál.** Kdyby hlaveň přejela přes limit, vrátily by se slepé rány do té části vysokého levelu, kterou limit zakazuje právě kvůli nim.
- **Signál se čte ze vstupu, ne z pózy.** `Cannon.Aim` (myš i pad) pozná, že hráč chtěl náklon za stropem. Chůze, plynulý návrat míření ani `aim=` signál nevyvolají. Doznívání se řídí časem, ne počtem snímků.
- **Kříž v ADS, a v přehledu nejspíš i paprsek.** `docs/game-session.md` říká, že v přehledu nese paprsek totéž, co v přesném míření kříž. Kdyby signál dostal jen kříž, hráč bez RMB by zůstal bez odezvy.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (zápis k #431, hotovo)

**#431 je na `main`u — merge `98b419b`** (`--no-ff` přes `BS3D-322`). Větev je smazaná lokálně i na originu a hlavní checkout `BS3D` stojí detached na `origin/main`. Issue nechávám otevřené, zavření je na slovu majitele.

- **Co to dělá:** když hráč tlačí mířením do stropu náklonu, kříž v ADS i paprsek v přehledu blikají červeně (`Crosshair.WARNING`, 4 Hz, pokles na 15 %). `Cannon.ElevationStrain` zvedá jen `Cannon.Aim`, tedy vstup, a jen ve směru, kterým se vstup pohnul. Plná hodnota drží 0,1 s po posledním zatlačení a pak během 0,25 s klesne na nulu. Hlaveň zůstává tvrdě oříznutá. `PREVIEW_REFUSED` je teď `Crosshair.WARNING`, takže odmítnutí i strop mají jednu červenou.
- **⚠ Testbedův `aim=` signál NEVYVOLÁ, a to je záměr:** jde přes `AimTo`, ne přes vstup. Kdo bude ověřovat něco, co čte vstup, musí do okna s fokusem posílat `mouse_event`: klik do titulku, kontrola `GetForegroundWindow`, `-12 px` každých 15 ms. `rmb=` přitom normálně funguje. Skript byl ve scratchpadu a zmizí.
- **Ověřeno:** bezgrafický rig 21/21, tj. časování na 30/75/240 Hz, 80Hz myš na 240Hz displeji a nic, co není vstup (traverz podél stropu, chůze, snížený limit, `AimTo`, `Restart`). Pak 1 běh Testbedu ze 3, které majitel povolil (`fpscap=75`, bez incidentu): v klidu bílá 213,224,217, u stropu vrchol 189,74,82 a dno 77,73,103, po puštění, s hlavní pořád na stropu, zase bílá 204,209,218. Game, Testbed i MapEditor staví s 0 chybami.
- **Neověřeno za běhu: paprsek a kříž ve hře.** Game nemá timeline a paprsek je jen v ní, takže ho zatím nikdo neviděl.
- **Mimo scope:** strop traverzu (±45°) je stejně tichý, ale issue mluví jen o náklonu. Rumble na padu patří k #378.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (zápis k #431, druhé kolo)

**Beru si druhé kolo #431.** Větev `431-elevation-rubber`, hlavní checkout `BS3D`. Po restartu (21:03:30) byl `git fsck` čistý a refy v pořádku. Majitel napsal: *„to červené by mělo pulzovat i velikostí - být větší, nechat uživatele přejet dál a viditelně/cítitelně ho stáhnout zase dolů. Ne takhle jednoduše. Opravdu důražně.“*

- **Majitel tím obrací moje rozhodnutí „gumový je jen signál, hlaveň ne“.** Hlaveň teď přetáhne přes strop s klesajícím přírůstkem a pružina ji stáhne zpátky. Kamera v přesném míření jde s ní, takže to hráč i ucítí. Cena: rána vystřelená během přetažení letí o pár stupňů nad limit, dokud pružina nevrátí hlaveň. Řeknu majiteli číslo, ne dojem.
- **Kříž bude pulzovat velikostí** a při plném napětí bude znatelně větší.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (zápis k #431, druhé kolo hotovo)

**Druhé kolo #431 je na `main`u — merge `10c7eee`** (`--no-ff` přes `BS3D-322`). Větev je smazaná lokálně i na originu a checkout `BS3D` stojí detached na `origin/main`. Issue nechávám otevřené, zavření je na slovu majitele.

- **Guma v hlavni:** tlak přes strop natáhne pózu s klesajícím přírůstkem až o `ELEVATION_OVERSHOOT` (0,10 rad, ~5,7°). Když tlak skončí, pružina (~3 Hz, tlumení 0,4) stáhne hlaveň asi o čtvrtinu švihu pod strop a pak ji na něj usadí. Je to jedna póza, takže s ní jde i kamera v ADS, paprsek, duch a rána. `AimTo`, `AimAt` a `Restart` gumu zahodí, strop pózy je `POSE_ELEVATION_CEILING` (1,52 rad). **Cena:** rána vystřelená během natažení letí až ~6° nad limit, přitom `TALL_AIM_MARGIN` vysokých levelů je jen 0,05.
- **Geometrii jsem ověřil čtením kódu, ne odhadem.** Konec závěru má na 80,2° nad kamenem vůli 0,29 a do svislé polohy klesne o méně než 0,05 (poznámka k #287 v `CannonRig`). Vůle lafety z #403 je pravidlo, které platí pro jakýkoli náklon.
- **Kříž:** při napětí je 1,5× větší a na světlém vrcholu blikání pulzuje na 2×.
- **Ověřeno:** rig 18/18 na 30/75/240 Hz. Pak 1 běh Testbedu ze 2 povolených (`fpscap=75`, bez incidentu). Výpisy `C` ukázaly při tlačení 85,7°, po puštění 78,9°, pak 80,5° a nakonec přesně 80,2°. Kříž na 900 px měřil při tlačení 14/39/4 px (odstup/délka/tloušťka) v červené, po usazení 7/20/2 px v bílé. Game, Testbed i MapEditor staví s 0 chybami.
- **⚠ Past z paměti se zopakovala:** měřicí funkce PowerShellu pojmenovaná `Diff` ve skutečnosti volala alias `Compare-Object` ([[powershell-alias-beats-function]]). Funkce pojmenovávej ve tvaru sloveso-podstatné jméno.
- **Neověřeno za běhu:** paprsek a kříž ve hře, protože Game nemá timeline.

**Nic dalšího si neberu.**

---

## 2026-09-15 — Claude Code (zápis k #431, třetí kolo)

**Beru si třetí kolo #431.** Větev `431-refuse-over-cap`, checkout `BS3D`. Majitel: *„mě hra nechá vystřelit, když mířím výš než nejvýš - v ten vrcholný gumový moment. Toho jde zneužít. Jakmile mířidlo bliká červeně nebo jsme jinak v maximu, nesmí jít vystřelit. Mohl by se ozvat nějaký negativní/pesimistický zvuk.“*

- **Pravidlo patří do `Cannon`, platí stejně pro Game i Testbed.** Výstřel se odmítne, dokud trvá napětí (kříž bliká) nebo dokud póza stojí za svorkou. Tím zmizí cena přetažení, kterou jsem ve druhém kole jen pojmenoval.
- **Zvuk:** krátký, teplý a zamítavý, podle [[game-sfx-pleasant-over-credible]].

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (zápis k #431, třetí kolo hotovo)

**Třetí kolo #431 je na `main`u — merge `1f61fd2`** (`--no-ff` přes `BS3D-322`). Větev je smazaná lokálně i na originu a checkout `BS3D` stojí detached na `origin/main`. Issue nechávám otevřené, zavření je na slovu majitele.

- **Výstřel se odmítne, když platí `Cannon.ElevationRefusesShot`.** Platí po celou dobu napětí, tedy přesně když kříž nebo paprsek bliká, a jako pojistka i když póza stojí za svorkou o víc než `ELEVATION_FIRE_TOLERANCE` (0,01 rad). Tolerance není nula, protože druhý švih pružiny zpět přes strop přijde až po skončení blikání (změřeno 0,33°). Na stropu v klidu zbraň střílí. Game zahraje `PlayShotRefused` a skryje ducha, Testbed vypíše `[shot] refused`.
- **Zvuk:** suché nepoziční „bwom-bwoww“, dvě noty klesající o malou tercii (220→208 a 185→165 Hz), pod nimi subbas o oktávu níž, bez šumu a bez driveru. Změřeno z téže aritmetiky, protože `SynthShotRefused` je statická metoda: 0,50 s, těžiště spektra 186 Hz, 82 % energie ve 150–300 Hz, nad 600 Hz nic. **Majitel ho zatím neslyšel.**
- **Ověřeno:** rig 22/22 (odmítnutí končí 0,30–0,35 s po tlaku a nikdy nenastane bez blikání). V Testbedu `Space` při 85,7° dvakrát vypsal `[shot] refused`, po usazení na 80,2° rána vyletěla (snímek). Game, Testbed i MapEditor staví s 0 chybami.
- **⚠ Aider v hlavním checkoutu.** Majitel dnes v `BS3D` zkoušel lokální AI (Aider). Ta mi mezi 11:51 a 12:17 nacommitovala tři commity přímo na moji větev `431-refuse-over-cap`, pod majitelovým jménem. První z nich smazal z `PlayHud.cs` 1727 řádků a Game přestala jít sestavit. Majitel řekl, ať je zahodím. Moje soubory jsem uložil do stashe, větev posunul `checkout -B` na `origin/main` a stash vrátil, takže commity zůstaly jen v reflogu. Aiderovu úpravu `.gitignore` (`.aider*`) jsem nechal necommitnutou. **Než začneš buildit nebo mergovat, zkontroluj `git log` a reflog, jestli ti na větvi nepřibylo něco cizího.**

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (lokální AI: nástroj SemanticSearch)

**Beru si nový nástroj `Tools/SemanticSearch`.** Větev `semantic-search-tool`, checkout `BS3D`. Majitel: *„Chci to nachovat jako nástroj a zkoumej další možnosti lokální AI pro práci na naší hře.“*

- **Co předcházelo (změřeno dnes, podrobnosti v paměti `local-llm-lm-studio`):** DeepSeek-Coder-V2-Lite přes Aider pro úpravy kódu nestojí za to, udělal polovinu zadání a přepsal konce řádků. Embeddingy `nomic-embed-text-v1.5` z LM Studia našly u 4 ze 7 známých dvojic issue protějšek na 1.–2. místě. Pro český deník jsou slabé (odpovědi na 62., 201., 70. a 9. místě z 532).
- **Nástroj bude hledat hlavně v issue** (kontrola duplicit před založením). Deník půjde přidat přepínačem s varováním a model půjde vyměnit za vícejazyčný.
- **Potom prozkoumám Gemmu 4 (VLM):** jestli z obrázku levelu pozná, co má tvar představovat. Na to si playtesty stěžují opakovaně (#360, #415, #418, #421).

**Nic dalšího si neberu.**

**Dodatek: `Tools/SemanticSearch` je na `main`u — merge `7e2ea2c`**, větev smazaná. Dokumentace je v „The semantic search“ v `docs/formats-and-tools.md` a `CLAUDE.md` teď uvádí čtyři nástroje. Issue se stahují živě přes `gh`, vektory jsou v cache po modelech a korpusech v `%LOCALAPPDATA%\BS3D-Tools`. První běh s `--journal` trvá 31 s, další 4–7 s. **Před založením issue spusť `dotnet run --project Tools\SemanticSearch -- --file draft.md`** (potřebuje LM Studio s `text-embedding-nomic-embed-text-v1.5`).

---

## 2026-09-16 — Claude Code (lokální AI: co funguje a co ne)

Majitel chtěl prozkoumat, k čemu se lokální modely z LM Studia hodí pro práci na hře. Všechno jsem měřil proti předem známé odpovědi. Podrobnosti jsou v paměti `local-llm-lm-studio`, zkušební nástroje zůstaly ve scratchpadu a zmizí.

- **Úpravy kódu (DeepSeek-Coder-V2-Lite přes Aider): ne.** Udělal polovinu zadání a přepsal konce řádků. Kontrola stála víc než samotná práce.
- **Hledání v issue podle významu (embeddingy nomic): ano.** Z toho vznikl `Tools/SemanticSearch` (merge `7e2ea2c`). V českém deníku je slabé.
- **Gemma 4 na malé detaily ve snímku:** na výřezu kolem kříže 7/7, na celém zmenšeném snímku 5/7. Na přesné kontroly zůstává pixelové měření.
- **Gemma na čitelnost tvaru levelu: ne.** Testoval jsem 18 levelů: 9, které podle playtestu nečtou, a 9 bez výtky. Snímky byly z `BS3D.exe play level=X shot=14`. Naslepo pojmenovala správně jen Heart, Star a Smiley, souměrné vzory označuje za „butterfly“. Hodnocení 1–5 skupiny neodděluje: 2,0 proti 2,8, na plochém 2D náhledu 2,1 proti 2,9. Jediná zajímavá shoda: Chest dvakrát nazvala vlajkou, přesně jako majitel v #418. ⚠ Kontrolní skupina je slabá, protože „bez výtky“ neznamená „čte“. Žirafu v levelu Giraffe nevidím ani já.
- **Gemma na otázku „co se změnilo“ mezi dvěma snímky: nejslibnější.** U dvojice se známým rozdílem správně popsala zvětšený červený kříž. Na tentýž soubor dvakrát odpověděla „vypadají stejně“ a žádný rozdíl si nevymyslela. Na výřezu vyjmenovala změnu barvy, tloušťky i délky ramen. Je ale pomalá (20–80 s na dvojici) a směr pohybu kamery popsala špatně.
- **⚠ Technika:** `"reasoning_effort": "none"` vypíná Gemmě přemýšlení (1,3 s místo 8,5 s). S kontextem 16k a plným snímkem model padal. Během testu LM Studio jednou nahlásilo `vk::Queue::submit: ErrorDeviceLost` a model se sám znovu načetl s kontextem 65k. V systémovém logu není reset ovladače (4101) ani restart (41/6008).
- **Majitelovy soubory:** `Settings.json` i `Progress.json` mají po všech 19 spuštěních hry stejné otisky.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (skill `local-ai`)

**Beru si skill `.claude/skills/local-ai`.** Větev `local-ai-skill`, checkout `BS3D`. Majitel: *„udělej si na použití tohohle AI skill, když jsi na tomhle stroji. Potom se podívej po dalších AI modelech, které by nám mohli v něčem týkajícím se vývoje hry pomoci.“* Skill popíše, co se z lokálních modelů v LM Studiu změřilo jako užitečné. Místo dočasného klienta ze scratchpadu dostane skript `vision.ps1`. Potom udělám průzkum dalších modelů, zatím bez stahování.

**Nic dalšího si neberu.**

**Dodatek: skill `local-ai` je na `main`u — merge `94d6b48`**, větev smazaná. `vision.ps1` jsem ověřil v PowerShellu 5.1 na dvojici snímků se známým rozdílem: za 16,7 s správně vyjmenoval barvu, tloušťku i délku ramen kříže.

---

## 2026-09-16 — Claude Code (zápis k #395, druhá půlka)

**Beru si druhou půlku #395 (inkousty bloku Eruption), na majitelův výběr ze shortlistu.** Větev `395-eruption-inks` z `origin/main`, hlavní checkout. Začínám u „CO TO NEOPRAVILO“ z předávky (osmnáctý zápis 15. 9.) a u majitelova komentáře k Sill/Fume/Caldera.

- **Pracovní hypotéza z kódu, zatím NEzměřená:** láva ukáže jen **odstín** švu (`tint / peak`, pak `LavaHuePower`) a jas podle luminance tintu. Tím padá celý „čedičový registr“ bloku do dvou tříd: **hnědá = tmavší oranžová** a **černá, stříbrná i bílá = světlá neutrální**. Sill (černá/hnědá/stříbrná + oranžová/žlutá) má tedy jen tři čitelné odstíny na pět inkoustů, což sedí na „nedá se dohrát“.
- **Nejdřív měřím** jádra všech třinácti pod sopkou a kopulí 9 (nejjasnější desetina disku, několik fází). Teprve z matice vyberu inkousty a přepíšu zákon bloku v `Block08_Eruption.cs`.
- **Měním level soubory**, takže brány LevelGenu, ScoreSim a sonda. Před mergem to ukážu majiteli, protože je to nová barevnost celé kapitoly.
- Zastínění kulky v děle (bod 2 předávky) zůstává na majitelově vkusu, nesahám na něj.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (průzkum dalších lokálních modelů)

Průzkum jen přes web, nic jsem nestahoval ani neinstaloval. Kandidáti pro vývoj BS3D na majitelově RX 6900 XT (16 GB, Windows, AMD):

- **Qwen3-Embedding-0.6B (GGUF):** vícejazyčné embeddingy, umí i češtinu. Je to nejlevnější pokus a opravuje jedinou slabinu `SemanticSearch`, český deník: stačí položit tytéž české dotazy se známým pořadím přes `--model`. ⚠ Qwen3-Embedding chce u dotazu instrukční prefix, který nástroj zatím nezná.
- **Whisper large-v3-turbo přes whisper.cpp s Vulkanem:** poznámky z playtestu by šlo diktovat česky. Na RX 6800 přepsal 17,5s klip za 434 ms. Voxtral z LM Studio Bionic češtinu neumí.
- **Qwen3-VL-8B (katalog LM Studia):** druhý vision model, lze ho porovnat s Gemmou na téže sadě snímků se známou odpovědí.
- **Obrázky jako reference (FLUX/SDXL):** oficiální ComfyUI s ROCm řadu RX 6000 na Windows nepodporuje, zbývá komunitní build s RDNA2 nebo AMD Amuse. U 6900 XT jsou hlášené pády. Hodilo by se jen na reference k #429, #404 a #436, ve hře zůstává procedurální generování.
- **Stable Audio 3.0 Small-SFX:** otevřené váhy, 459M parametrů, vygenerovaný zvuk lze komerčně použít. Bez ROCm pro RX 6000 na Windows by běžel nejspíš jen na CPU. Zvuk ve hře je procedurální, takže nanejvýš jako reference. Hudbu majitel řeší lidskými skladbami (#391).
- **Hlas:** Piper má český hlas „jirka“, Kokoro anglický. Jen kdyby hra chtěla hlasové hlášky.

**Stahování a instalace jsou na slovu majitele.**

**Dodatek:** majitel diktování i hlas vyloučil natrvalo: *„Diktovat nikdy nebudu chtít místo psaní. Vždycky budu psát na klávesnici. Hlas neřeš.“* Pro zbylé modely jsou založená issues k otestování na tomhle stroji:
- #439: Qwen3-Embedding pro `SemanticSearch` na českém deníku.
- #440: Qwen3-VL proti Gemmě na téže sadě snímků se známou odpovědí.
- #441: generování obrázků jako reference k #429, #404 a #436.
- #442: Stable Audio Small-SFX jako reference pro procedurální zvuk.

⚠ #442 stojí vedle majitelova rozhodnutí z #187 (žádná AI generace hudby), proto issue před instalací žádá jeho potvrzení. Duplicity jsem před založením ověřil i přes `SemanticSearch --file`. Nejbližší issue měla skóre 0,74–0,79 a týkala se jiných témat.

---

## 2026-09-16 — Claude Code (lokální AI: generovaná hudba, #443)

**Majitel změnil rozhodnutí o hudbě.** Po poslechu lokálně vygenerovaných skladeb chce procedurální hudbu nahradit generovaným audiem a procedurální kusy nechat jako easter egg na About (přehrávač: pauza, další skladba, jednoduchý 2D vizualizér). Založeno jako **#443**. Tím se pro **hudbu** obrací #187 („žádná AI generace“) ze 14. 9.; osud #391 (lidské skladby z OpenGameArt) je na majiteli. Věta „Hudbu majitel řeší lidskými skladbami (#391)“ z předchozího zápisu už pro hudbu neplatí; k efektům (#442) #443 nic nového neříká.

- **Engine:** ACE-Step 1.5 přes `acestep.cpp` v0.0.5 (hotové Windows binárky s Vulkanem) na RX 6900 XT, bez ROCm. Modely Q8_0 GGUF (~7,7 GB). Skripty jsou mimo repo v `C:\Users\panrd\AI` (`generate-music.ps1`, `loop_crossfade.py`). **Licence MIT** (kód i váhy, ověřeno přes GitHub API a Hugging Face). Můj první commit uváděl Apache 2.0, opraveno v `5b098a7`.
- **Reference na `main`u:** `Research/AI-Music/` — protějšky všech pěti `MusicTheme` a menu plus jedna nová skladba, každá se sidecarem `.json` (prompt, co z něj LM udělal, střih loopu, měření). Merge `9029e8c` a `5b098a7`, ~166 MB WAV (32-bit float).
- **⚠ Pasti, změřené:**
  - VAE dekóduje napevno na **48 kHz**; `wav32` je přesnost vzorku, ne vzorkovací frekvence.
  - Délka renderu: 150 s = 5 dlaždic VAE (163 s dekódování, prošlo), **199 s = 6 dlaždic a `ace-synth` spadl** (0xC0000409). Bohemia je proto renderovaná jen na 120 s.
  - **ComfyUI node `acestep-cpp-comfyui` s binárkami v0.0.5 nefunguje** (volá novější CLI), binárky se volají přímo.
  - **Request JSON nesmí mít BOM:** `Set-Content -Encoding utf8` v PS 5.1 ho přidá a parser spadne.
  - `wav32` je IEEE float (format tag 3) a stdlib `wave` ho nepřečte.
  - **LM přepisuje zadání:** Mural, definovaný tím, že kick NEhraje na všechny čtyři doby, dostal „steady four-on-the-floor“; z Emberu (power ballada) udělal virtuózní kytarové sólo s potleskem. Krátký prompt na efekt („UI chime“) skončil jako sólové piano.
  - **Loop přehnutím konce do začátku nefunguje:** vygenerovaná skladba *končí*, na švu game-track-01 spadla úroveň z 1,0 na 0,04 mediánu. Loopy se proto stříhají z těla renderu na celé takty (tempo sedí na ±1 % zadání), zarovnané na milisekundu, s kontrolou rytmu pod crossfadem proti tomu, jak skladba navazuje sama na sebe o takt dál. Pod ní zůstává jen Bohemia (r 0,43 proti 0,53): render kolem 76 s mění rytmus.
- **Koordinace:** hlavní checkout `BS3D` jsem nechal session s #395, commity šly přes dočasný worktree a `BS3D-322`. Session bs3d-49 dostala o změně rozhodnutí zprávu.

**Nic dalšího si neberu.** Implementace #443 čeká na slovo majitele.

**Dodatek: tři punkové variace Emberu jsou na `main`u — merge `2466a75`**, větev smazaná. Majitel chtěl Ember „ve stylu punk rocku, jako Green Day – Boulevard of Broken Dreams“. Prompt styl popisuje, ale kapelu ani píseň nejmenuje, aby model nekopíroval konkrétní nahrávku. LM ani jednu variantu nenechal jako punk: vyšel alt rock/grunge, metal s double-kickem a blues-rock se sóly (podrobnosti v sidecarech). Všechny tři loopy drží rytmus. Pro věrnější styl zbývá nevyzkoušená páka `use_cot_caption: false`, kdy DiT dostane caption tak, jak je napsaný. GPU jsem si předem vyžádal od bs3d-49 (uvolnila Gemmu) a po dokončení jí ho zprávou vrátil. Nechat ACE-Step (~6 GB) a načtenou Gemmu 4 (12,8 GB) běžet na 16GB kartě naráz nejde. S volnou GPU trvalo dekódování VAE (4 dlaždice) 35–36 s, u odpoledních témat 68–137 s, jenže tehdy jsem nekontroloval, co v paměti karty drželo LM Studio.

**Dodatek 2: punk-04 a punk-05 jsou na `main`u — merge `16f1c58`**, větev smazaná, shrnutí v komentáři k #443. Majitel chtěl dvě skladby, jejichž prompt Green Day jmenuje („děláme to jenom tady doma“). Proto vznikly nejdřív jen lokálně: repo je **veřejné**. Po poslechu majitel rozhodl, že ani jedna nezní poznatelně jako Green Day ani jako konkrétní píseň, a nechal je přejmenovat a použít ve hře. Sidecary uvádějí přesný prompt i tento verdikt. Změřeno: **`use_cot_caption: false` funguje**, LM pak caption předá doslova a doplní jen bpm, tóninu a délku. **Tempo není zaručené:** punk-05 měl zadáno 180 BPM a vyšel na ~96 (cítěno ~192). Loop stříhaný na mřížce 180 byl rytmicky pod skladbou samotnou, přestřižený na půlčasové mřížce drží (r 0,40 → 0,83).

**Dodatek 3: #391 je zavřené.** Majitel: *„Zavři #391, jdeme cestou generované hudby.“* Věta výše, že osud #391 je na majiteli, tím neplatí. Tělo #443 je opravené. #280 a #292 zůstávají otevřené, ale vznikly pro procedurální hudbu, takže je čti ve světle #443.

---

## 2026-09-16 — Claude Code (zápis k #440)

**Beru si #440 (Qwen3-VL-8B proti Gemmě 4).** Větev založím, až bude co měnit v repu: skill a výchozí model `vision.ps1` se změní podle výsledků.

- Oba modely pojedou přes `vision.ps1` na **totožných obrázcích**: devět snímků kříže, tři dvojice před/po a 18 snímků levelů z dnešního odpoledne. Hru znovu nespouštím, takže se majitelova uložená hra nezmění. Gemmu přeměřím celou, protože její test kříže běžel ještě se zapnutým přemýšlením.
- Stahuju `qwen/qwen3-vl-8b@q8_0` (~9 GB, stejná kvantizace jako Gemma). Oba modely se do 16 GB nevejdou naráz, poběží tedy postupně.
- Majitel chce výsledek vidět: udělám stránku s každým testovaným obrázkem, správnou odpovědí a odpověďmi obou modelů.
- ⚠ V LM Studiu je načtená druhá instance `nomic-embed-text` s TTL 1 h, kterou jsem nenačítal já, nejspíš jiné sezení. Uvolňuju jen to, co jsem načetl sám.

**Nic dalšího si neberu.**

**Dodatek: #440 je hotové a skill je na `main`u — merge `8af8001`**, větev smazaná. Oba modely prošly stejných 96 otázek přes `vision.ps1` (Q8_0, kontext 8k, bez přemýšlení, teplota 0), každý zvlášť.
- **Kříž:** na výřezu 256 px Gemma 7/7, Qwen 6/7. Qwen u bílého kříže nad pestrým clusterem odpověděl „none“, odpovídá ale za 0,3 s, Gemma za 1,3 s. Na celém snímku oba 5/7, koule v letu oba 2/2.
- **Dvojice před/po:** Gemma 3½/4, Qwen 3/4. Tentýž soubor dvakrát oba označili za identický. Qwen jako jediný popsal, že se hlaveň zvedla k obloze, a všiml si žluté koule u spodního okraje, která tam opravdu přibyla (ověřeno výřezem). Zvětšení kříže ale vynechal.
- **Levely:** naslepo oba 3–4 z 18. Qwen hodnotí štědře a na 3D snímku skupiny neodliší (3,8 proti 3,7). Často odpovídá „cannonball pattern“.
- **Plné snímky:** Qwen s kontextem 16k zvládl snímek 1600×900 i jejich dvojici bez pádu (12 s a 29 s), Gemma na tomtéž dřív spadla.
- **Verdikt:** výchozí zůstává Gemma. Qwen je ve skillu jako volba pro sdílenou kartu (9,9 GB proti 12,8 GB) a pro plné snímky. Čísla jsou v komentáři v #440. Stránka s každým testovaným obrázkem je majitelův artefakt: https://claude.ai/artifact/EPYRKr3q4VfHhSSor93qiv. Zavření #440 nechávám na majiteli.
- **GPU:** kartu jsem dvakrát zprávou předal bs3d-81 na generování hudby a dvakrát ji dostal zpátky. Druhá instance `nomic-embed-text` patřila bs3d-81.

---

## 2026-09-16 — Claude Code (zápis k #395, druhá půlka hotová)

**Inkousty bloku Eruption jsou na `main`u — merge `8f1528b`**, větev smazaná lokálně i na originu. Pracoval jsem na notebooku (ThinkPad, `C:\GitHub`), ne v desktopovém `BS3D`. **#394 je zavřené, #395 nechávám na majitelův pokyn otevřené** kvůli zbytku s dělem. Oba komentáře v issues nesou čísla.

- **Příčina:** lávový styl ukáže barvu jen jako **odstín** švu. Hnědá je tmavší oranžová a černá, stříbrná i bílá jsou stejná světlá neutrální síť. Osm inkoustů bloku tak dávalo pět barev.
- **Měření:** snímky `Thirteen_Colors` pod sopkou a kopulí 9, sedm fází srdečního tepu, nejjasnější čtvrtina a polovina disku, světlost s poloviční vahou. Pět nejtěsnějších dvojic celé palety leželo uvnitř bloku: oranžová/hnědá 6,1, červená/oranžová 6,8, červená/hnědá 7,7, černá/stříbrná 8,4, bílá/žlutá 12,0. Sill měl na pět inkoustů tři barvy.
- **Nová paleta**, konstanty `ERUPTION_*` v `Block08_Eruption.cs`:
  - horké: červená a žlutá,
  - studené: černá, azurová a tmavě modrá,
  - šestá: fialová, jen kde je nutná.
  - Level má nejvýš 6 barev. Nejtěsnější dvojice je 25,2, se šestou 21,4.
  - Majitel vybral tuto variantu ze srovnání Sillu ve hře, proti variantě se zelenou (20,6). Čistě teplá paleta udělat nejde: na lávě jsou jen tři teplé nebo neutrální rodiny.
- **Skupiny:** devět levelů má stejné skupiny, zatížení kotev i dosažitelnost. **Meander:** jezy mají celé šestou barvu, 36 → 33 skupin. **Fume:** pořadí barev v trubkách jsem vybral výpočtem nad skutečnými kontakty kuliček, protože dvě první pořadí slepila trubky (35kuličková skupina).
- **⚠ Past:** „trubky se nedotýkají“ platí jen pro některé páry. Trubky 0/2 a 2/4 se dotýkají, 2/4 i napříč patry. U každého přebarvení s méně barvami porovnej počty skupin v reportu LevelGenu před a po.
- **Ověřeno:** LevelGen exit 0, změnilo se jen 10 souborů levelů a v nich jen hodnoty `"t"`. ScoreSim hodnotí ve správném pořadí. Čtyři solutions bez chyb. Sonda dala na všech deseti stejné verdikty: Breach a Causeway na setinu stejně, Caldera stejná hra rána po ráně. Běhy hry: Testbed 1×, Game 14× (prototypy a porovnání před a po), všechny s `fpscap=75`.
- **⚠ Nalezeno mimo zadání:** Caldera má na `main`u **2 z 5 už před změnou**, jedno prověšení u 4. rány při stojícím skle. Dokumentace uvádí 1 z 5. Zapsáno u `CALDERA_BENCH`, neopraveno. Může to vysvětlovat hodnocení „Nulová/Frustrující“.
- **Nástroj:** `palette.ps1` má nové volby `-Cores` a `-LightnessWeight`. Na stejném snímku dávají stejná čísla jako skript ve scratchpadu. Souřadnice řady pod sopkou jsou v hlavičce skriptu.
- **Zbývá na #395:** zastínění kulky v děle (rozhodnutí podle majitelova oka), měření vinylu před a po, snímky s drženým pravým tlačítkem a výkon `LavaPS`. U #394 zůstává otevřená otázka zasypaného jádra: zásobník nabízí barvy, které na začátku nejsou vidět. Je to rozhodnutí o obtížnosti, případně na samostatné issue.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (zápis k #389, merge s #396)

**Beru si #389 (výbuch bomby), na majitelův výběr ze shortlistu.** Pracuju na notebooku (`C:\GitHub`) na větvi `389-bomb-detonation`. Vzhled výbuchu majitel schválil 14. 9. a zvuk (verze jen s basy, `955d2e4`) čeká na jeho ucho. Úkol je dostat větev do stavu, kdy jde mergnout.

- **Do větve mergnu `origin/main`** (bez rebase a bez force-push, větev je pushnutá). Konflikty s #396 řeším podle instrukce ze šestnáctého zápisu 15. 9.: smyčka `ResolveDisconnected` z #396, do ní z #389 `links`, `blasts`, odhoz od těla a `ThrowOrphan`, a `detonationsInto?.Add(...)` tam, kde stojí `detonatedInto?.Add(bomb)`. Jinak osiřelá bomba vybuchne bez záblesku a bez zvuku.
- **Ověřím** osiřelou detonaci se zábleskem a zvukem, LevelGen beze změny, ScoreSim, sondu na Ventu, Sillu a Paroxysmu a výbuch ve hře.
- **Prosím do merge nesahat na** `BallsConstraintsBuilder.cs`, `BallContactEventHandler.cs`, `BallLanding.cs`, `GameplayScreen.Rules.cs` a `ProceduralAudio.cs`. ⚠ Týká se i #443 (generovaná hudba), pokud by sahala do `ProceduralAudio`.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (skill `capture-review`)

**Beru si nový skill `.claude/skills/capture-review`.** Větev `capture-review-skill`, checkout `BS3D`. Majitel po #440: *„udělej skill pro použití toho lepšího modelu tam, kde se to bude hodit, když se to bude hodit.“*

- Skill se má načíst při kontrole snímků před a po změně (vykreslování, HUD, scény) a při kontrole jednoho detailu na mnoha snímcích. Platí jen tam, kde běží LM Studio.
- Skript nejdřív spočítá přesný pixelový rozdíl po blocích, takže zrnění nezapočítá, a identické dvojice pustí bez volání modelu. U rozdílných najde oblast změny a nechá Gemmu 4 popsat celý snímek i výřez té oblasti, protože na výřezu byla v #440 výrazně přesnější. Qwen3-VL použije, když už je načtený.
- GPU koordinuju zprávou s bs3d-81.

**Nic dalšího si neberu.**

**Hotovo, mergnuto do main (2cc711b).** `.claude/skills/capture-review/review-captures.ps1` a `SKILL.md`, v `local-ai/SKILL.md` na něj odkaz.

- **Rozdíl po blocích 16×16** (průměr bloku, ne pixel): zrnění má nulový průměr a v bloku se vyruší. Změněné bloky se spojí do oblastí a řadí se podle **nejostřejšího** bloku, ne podle součtu, jinak by široce posunuté mraky přebily tenký zaměřovač. Na čtyřech dvojicích z #431 (1600×900, City) trval rozdíl 0,6 s.
- **Práh 12 z 255** je změřený: mraky mezi dvěma snímky vzdálenými sekundu se hýbou o 4–10 v průměru bloku (8/12/16/24 → 133/71/27/5 bloků), hlaveň měla špičku 159. **Tenká značka HUD je u prahu:** oblast samotného zaměřovače měla špičku jen 16 a byla čtvrtá; popsal ho jen proto, že výřez kolem hlavně byl dost široký. Na jemné značky `-BlockThreshold 8` a víc `-Regions`.
- **Gemma 4:** stejný soubor dvakrát → model se nevolá; dvojice sekundu od sebe → „změnil se FPS counter“ (opravdu); zaměřovač bílý → červený a dvakrát větší → celý snímek i výřez správně („zčervenal a zvětšil se, hlaveň se posunula“); náklon kamery nahoru → správně „cluster zmizel“, ale chybně „dělo je blíž“. Celý běh 70 s, z toho 12 s načtení modelu.
- Bez LM Studia (notebook) skript řekne, že model neběží, a vydá jen rozdíl, který sám odpoví „změnilo se něco a kde“.
- Gemma je po testu vyložená a bs3d-81 ví, že GPU je volné.

---

## 2026-09-16 — Claude Code (zápis k #443)

**Beru si #443 (generovaná hudba místo procedurální).** Větev `443-generated-music`, vlastní worktree `BS3D-443`, hlavní checkout `BS3D` nechávám ostatním. Majitel: *„Pusť se do #443.“* Rozhodnutí k obsahu: **levely s Emberem budou střídat šest variant** (Ember a punk 01–05), level soubory se nemění. **Bohemia jde do hry tak, jak je.** `game-track-01` zůstává v Research.

- **Plán:** skladby půjdou do `Game/Music` jako 16bit WAV 48 kHz. Kopírují se do výstupu bez MGCB a načítají se na pozadí přímo do 16bit PCM, které řetěz `DynamicSoundEffectInstance` už dnes dostává. Fronta, fady (#211), bezešvé opakování (#212) a výběr v Nastavení (#279) tak zůstávají. **Fanfáry zůstávají procedurální.** About dostane přehrávač procedurálních skladeb: pauza, další skladba a jednoduchý 2D vizualizér. Opravím dokumentaci (`game-feedback.md`, `game-shell.md`, `formats-and-tools.md`, CLAUDE.md).
- **Nesahám** na `ProceduralAudio.cs` ani na soubory fyziky, které #389 zamyká.
- ⚠ Commit `a196cf6` se zprávou „claim #443“ obsahuje ve skutečnosti **dodatek bs3d-49 ke `capture-review`**. V `BS3D-322` jsme commitovali ve stejnou chvíli a můj `git add` vzal její necommitnutý text. Obsah je správný, popis ne. Historii `main` nepřepisuju, tenhle zápis je skutečný claim.

**Nic dalšího si neberu.**

**Dodatek: #443 je na `main`u, merge `40df9d5`.** Větev je smazaná lokálně i na originu a worktree `BS3D-443` je odstraněný. **Issue nechávám otevřené**, dokud si majitel hudbu ve hře neposlechne.

- **Co hraje:**
  - `GameMusic` přehrává `Game/Music/*.wav`: 11 souborů, 16bit PCM, 48 kHz, stereo, 116,6 MB. Řetěz `DynamicSoundEffectInstance`, fady a výběr v Nastavení zůstaly.
  - Slot Ember střídá šest nahrávek (`ember.wav`, `ember-punk-01` až `05`) seřazených podle jména. Čítač žije jen v procesu, takže **první Ember po startu je vždy `ember.wav`**.
  - Fanfáry jsou pořád procedurální. `ProceduralMusic` teď drží jen je a statické `Render`/`RenderMenu`/`ToPcm`.
- **About:**
  - `ProceduralJukebox` a `MusicVisualizer` přehrávají původních šest skladeb: Play/Pause, Next, 24 pásem.
  - Dokud přehrávač drží skladbu, hudba hry se ztiší (`GameMusic.Yielding`).
  - Testovací argumenty `about` a `about=play`.
- **`MusicBake --tracks`** zapisuje `Game/Music` z masterů v `Research/AI-Music`:
  - RMS −15,0 dBFS, menu −19,5.
  - Tanh koleno nad −1 dBFS zasáhlo 0,134 % vzorků Nocturne.
  - Bez `--tracks` nástroj dál renderuje procedurální skladby.
- **Merge s `main`em:**
  - Konflikty byly jen v seznamech argumentů. `detonate=` z #389 zůstává vedle `about=`.
  - Opravil jsem čtyři komentáře, které po #389 ukazovaly na `ProceduralMusic`: `Level.cs`, `BallStyle.cs` a dva v `ProceduralAudio.cs`. Jeden z nich je důvod, proč jsou popy noise, a transpozici skladeb už neodpovídal.
- **Ověřeno po merge:**
  - Tři solutions bez chyb. Tři varování v `Game.sln` jsou starší (`CameraInputHelper`, `LevelGen`).
  - `about=play shot=8,12`: „Pulse · 1 / 6“ a sloupce se hýbou.
  - `play level=Basket`: log `[music] Ember: ember.wav`.
  - Hashe `Settings.json` a `Progress.json` se nezměnily.
- **Neověřeno:**
  - Přechod Emberu na druhou variantu za běhu, protože level se skriptem restartovat nedá.
  - Nic ušima: běhy šly s `mute`.
- ⚠ **Velikost:** repo i distribuce jsou o 116,6 MB WAV větší. Pro srovnání: poslechová stránka měla 12 skladeb v Ogg Vorbis za 34 MB.
  - **Majitel chtěl issue, založeno jako #444:** Ogg Vorbis v `Game/Music`, při načtení dekódovaný do stejného PCM, hudba mimo MGCB.
  - Změřeno přes libsndfile na všech 11 skladbách: kvalita 0,4 / 0,6 / 0,8 vychází na 10,2 / 14,9 / 19,9 MB. Délka po dekódování sedí na vzorek.
  - Kandidáti jsou NVorbis 0.10.5 a OggVorbisEncoder 1.2.2, oba pod MIT.
  - Neověřeno: délka po dekódování přes NVorbis, čas dekódování a jestli je šev smyčky slyšet.

---

## 2026-09-16 — Claude Code, bs3d-49 (#441 lokální generování obrázků)

**Beru si #441.** Majitel: *„Vyber další AI task, co použije grafiku.“* V repozitáři nic neměním, kromě tohoto deníku a na konci případně skillu `local-ai`.

- **Cesta:** issue počítá s AMD Amuse nebo ComfyUI s ROCm, které na řadě RX 6000 pod Windows padá. Zkouším místo toho **stable-diffusion.cpp přes Vulkan**. Je to stejná knihovna ggml, na které tu už spolehlivě běží `acestep.cpp` (#443), a nepotřebuje ROCm ani instalátor. Binárky a modely leží v `C:\Users\panrd\AI\sd`, mimo repo.
- **Model:** Z-Image-Turbo Q8_0 (6B, 8 kroků, Apache 2.0) s textovým enkodérem Qwen3-4B Q8_0. FLUX.2 klein 4B (také Apache) jen jako srovnání, pokud Z-Image nevyjde.
- **Změřím:** čas na obrázek, VRAM a stabilitu (každý `ErrorDeviceLost`, reset ovladače nebo restart zapíšu hned). Obrázky budou reference k #429 (pohár), #436 (střechy City) a #404 (ostrov podle scény). Majiteli je ukážu na stránce, protože verdikt, jestli pomáhají, je jeho.
- GPU sdílím s bs3d-81 (ACE-Step, #443), domlouváme se zprávami.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (zápis k #389, sloučeno s #396, čeká na ucho)

**#389 je sloučené s `main`em na větvi `389-bomb-detonation` (merge `621742f`, pushnuto), NEmergnuto do `main`u.** Čeká se na majitelův poslech zvuku (verze jen s basy). Pracoval jsem na notebooku v `C:\GitHub`.

- **Sloučení podle instrukce ze 15. 9.:** kostrou je smyčka `ResolveDisconnected` z #396. Do ní jsem vložil z #389 `links`, `blasts`, odhoz od **těla** bomby a `ThrowOrphan` (při finálním uvolnění, jen pokud v témže průchodu něco vybuchlo). `Detonation` se zapisuje na jediném řádku, kde se rozhoduje, že bomba vybuchla. Osiřelá bomba tak má záblesk i zvuk.
- **Jeden seznam `List<Detonation>` nahradil seznam buněk z #396.** Plní ho všechny čtyři odstraňovací cesty a handler ho vyčistí jednou za dopad v `CollectArmedSpecials`.
- **⚠ Past, kterou auto-merge vložil potichu:** `_detonations.Clear()` z #389 stálo těsně před `DetonateBombs`. Po #396 by smazalo bomby, které odpálil match, zap nebo kyselina. Odstraněno. Testovací páka `detonate=` si teď seznam čistí sama.
- **Článek řetězu pro bombu odpálenou kontrolou odpojení:** o jeden víc než nejhlubší článek, který v průchodu už vybuchl. Pokud ještě nic nevybuchlo, dostane 0 (odpálil ji match, zap nebo kyselina, tedy událost samotného dopadu).
- **Ověřeno:**
  - Čtyři solutions bez chyb.
  - Bezgrafický test proti skutečné knihovně 11/11: bomba osiřelá matchem má záznam s článkem 0 a všechno, co vzala, odletí nejméně 0,8 j/s. Bomba osiřelá výbuchem jiné bomby mimo jeho dosah dostane článek 1. Obyčejný match bez bomby nedá záznam a sirotek padá z klidu. Seznam se přes dvě volání jen doplňuje.
  - LevelGen exit 0, `Game/Levels` beze změny. ScoreSim „right way round“.
  - Sonda `--sag=Vent,Sill,Paroxysm`: všechny tři 0 z 5 jako na `main`u (Vent 26 ran/2,49, Sill 35/−0,00, Paroxysm 9/−0,53; na `main`u 23/2,70, 35/0,08, 9/−0,48). Jiná čísla vznikají tím, že teď letí i sirotci.
  - Ve hře `level=Sill detonate=10`: „5 went off, 117 destroyed“, výbuch se vykreslí.
- **Zvuk:** vyrenderovaný přes reflexi z `BS3D.dll` do WAV ve scratchpadu. RMS 0,282 a crest 3,37 sedí na verzi 5 z patnáctého zápisu 14. 9.
- **Pořád platí prosba nesahat do merge** na `BallsConstraintsBuilder.cs`, `BallContactEventHandler.cs`, `BallLanding.cs`, `GameplayScreen.Rules.cs` a `ProceduralAudio.cs`.

**Nic dalšího si neberu.**

**Dodatek: #389 je na `main`u — merge `172a92e` — a zavřené.** Majitel si poslechl zvuk: „Zní dobře, mergni a zavři.“ Po mergi se BS3DLibs i Game sestaví bez chyb. Větev je smazaná lokálně i na originu, až poté, co prošel push `main`u. Tím **padá prosba nesahat na pět souborů** výše. Komentář v issue shrnuje příčinu (střed odhozu v mřížkovém rámci, ne sirotci), co se dodalo, sloučení s #396 a co zůstává: poznámka majitele z playtestu Paroxysmu, že bombu je lepší představit na začátku kapitoly, a ScoreSim, který výbuch pořád nemodeluje.

**Claude Code, bs3d-49: #441, výsledek měření (verdikt je na majiteli).** Navazuje na můj claim výše.

- **stable-diffusion.cpp přes Vulkan na RX 6900 XT funguje.** Použil jsem build `master-869-07a85c7` pro win-vulkan, Z-Image-Turbo Q8_0, Qwen3-4B-Instruct-2507 Q8_0 jako textový enkodér a `ae.safetensors`, dohromady 11,2 GB stahování a nic se neinstalovalo. Spouštím `sd-server.exe` (API `/sdapi/v1/txt2img`) na portu **7860**, protože výchozí 1234 je port LM Studia.
- **Nastavení:** když je všechno na kartě, nevejde se. Váhy mají 10,5 GB a výpočet difuze chce 4,3 GB. S `--offload-to-cpu` trval obrázek 832×1216 **62,8 s**, protože dekódování přeteklo na CPU (25 s). S `--offload-to-cpu --vae-tiling` trvá **33–37 s**, z toho dekódování 3,8 s, a švy nejsou vidět. Proces bral až 10,5 GB a celá karta měla obsazeno až 12,8 GB, takže **s hrou ani s Gemmou 4 se nevejde**.
- **Stabilita:** 20 obrázků bez chyby. V System logu mezi 20:10 a 20:31 není 4101, 41 ani 6008.
- **Obrázky** (5× pohár k #429, 3× střechy k #436, 6× ostrov k #404) sedí na zadání. Odchylky: safíry na stříbrném poháru jsou jinde, než chtěl prompt, a na rozpisu střešních prvků je nápis „5G“, přestože ho prompt zakazoval. **Poučení k promptům:** první verze ostrovů popisovala „glass funnel drain“ a všech šest obrázků postavilo na plošinu sklenici na martini. Když jsem odtok popsal tvarem (díra zapuštěná do podlahy, lícující okraj, „Nothing stands on the platform“), bylo to se stejnými seedy správně na všech šesti.
- Skripty `gen.py` (dávka promptů přes server) a `vram-watch.ps1` a prompty `prompts-441*.json` jsou v `C:\Users\panrd\AI\sd`, obrázky v `out\zimage`. Server je vypnutý a karta volná.
- **Čeká se na majitele:** pomáhají obrázky při navrhování? Podle toho postup zapíšu do skillu `local-ai`, nebo #441 uzavřu jako „nestojí za to“.

---

## 2026-09-16 — Claude Code (série malých oprav: #422, #424, #428, #405)

**Beru si čtyři malé issue, na majitelův výběr ze shortlistu.** Každé dostane vlastní větev a pojedou po sobě na notebooku v `C:\GitHub`:

1. **#422** — hlavička `Block11_Mirage.cs` pořád popisuje pravidlo skla z doby před #344. Opravím jen dokumentaci. Facet je ověřený už z #344 (zápis ze 3. 9.).
2. **#424** — rána, která vyčistí level, spustí kameru pádu i pod hranicí `MIN_BALLS`, pokud ji hráč nevypnul v Nastavení. Soubory `GameplayScreen.Rules.cs` a `DropCinematic.cs`.
3. **#428** — v 2D náhledu mapy mají padající kuličky mizet až dole u počitadla, ne hned pod čarou. Soubor `PlayHud.cs`.
4. **#405** — výběr levelu v menu má na pozadí ukázat scénu a styl kuliček toho levelu. Soubory `LevelSelectPage.cs` a `BackdropScreen.cs`. ⚠ #443 upravuje About a `docs/game-shell.md`; na About nesahám, v dokumentaci se případně sejdeme.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #441 je hotová a zavřená.** Majitel po prohlédnutí stránky s obrázky: *„Ano, ty obrázky jsou skvělé!“*

- **Nový skill `.claude/skills/design-references`** (merge `c29df94`). `render-references.ps1` spustí `sd-server` s nastavením, které jsem změřil (`--offload-to-cpu --vae-tiling`, port 7860). Vykreslí prompt nebo soubor promptů přes několik seedů, ke každému obrázku zapíše `.txt` s promptem a server na konci vypne, aby karta byla zase volná. Výstup jde do `C:\Users\panrd\AI\sd\out`, **ne do repa**, protože repo je veřejné. V `local-ai` přibyl řádek s verdiktem a zmínka, že kartu sdílí i generování obrázků.
- **Ověřeno skriptem:** zlatý pohár se seedem 4411 vyšel **bajtově shodně** s obrázkem z prvního měření, a to za 37,6 s včetně startu serveru. Server se po běhu vypnul. Mezi 20:10 a 20:43 není v System logu 4101, 41 ani 6008.
- Komentář s měřením je na #441. Obrázky k #429, #436 a #404 leží jen na desktopu v `C:\Users\panrd\AI\sd\out\zimage`.

---

## 2026-09-16 — Claude Code, bs3d-49 (#429 pohár: vyšší a zdobený)

**Beru si #429.** Majitel: *„Pusť se do #429 s těmi obrázky poháru.“* Jako předloha poslouží reference z #441 (zlatý pohár, pohled zepředu, bronz, stříbro, křišťál), uložené v `C:\Users\panrd\AI\sd\out\zimage`.

- **Beru na sebe:** `BS3DLibs/Prazsky.Core/Render/TrophyMesh.cs`, `Game/Effects/TrophyPodium.cs`, `MeshBuilder.cs` (16bitový strop se posune z 32 767 na 65 535, stejně jako to už dělá `LatheMesh`), případně nové meshe ozdob v `Prazsky.Core/Render` a sekci poháru v `docs/game-feedback.md`.
- **Plán:** štíhlejší a vyšší profil se členěnými lištami. Ozdoby přibývají se stupněm: kabošony v obrubách, perlovec, kameny pod okrajem a v kalichu. Rozhodnutí z #271 platí dál: tělo zůstává hladké, bez faset. Ozdoby jsou instancované meshe, takže jeden druh znamená jedno volání kreslení.
- Ověřím to snímky výsledkové stránky (`result stars=1..4`) ve světlé a tmavé scéně, i na blízkém konci dolly kvůli ořezu. Save majitele před během zahashuju.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (série malých oprav hotová: #422, #424, #428, #405)

**Všechny čtyři jsou na `main`u a zavřené.** Každá měla vlastní větev, smazanou až poté, co prošel push `main`u. Pracoval jsem na notebooku v `C:\GitHub`.

- **#422** (merge `f94fce0`): hlavička `Block11_Mirage.cs` teď popisuje pravidlo skla z #344. Dopad obarví každé skleněné těleso, kterého se dotýká, a to celé. Hlavička vysvětluje, co přesně odmítá brána „glass in bodies of two or more“ a proč Facetových 64 samostatných tabulí projde. ⚠ „best glass landing pays“ počítá **obarvené sklo**, ne kuličky, které odpadnou. Ty oceňuje drop test. Změna je jen v komentáři, LevelGen se nezměnil.
- **#424** (merge `dc586f9`): rána, po které nezbude nic k sestřelení (`GetRemovableBallsCount() == 0`, stejný počet, jaký hned potom čte `CheckLevelCleared`), obejde `MIN_BALLS` i `MustBeatBestBy`. Nastavení „Drop camera“ a ostatní pojistky platí dál. **A/B ve hře** na testovacím levelu s bombou a čtyřmi kuličkami, odpálenou přes `detonate=`: bez změny se kamera nespustí, se změnou vypíše `[cinematic] 5 balls … cleared the level`. ⚠ Past: `levelfile=` se tváří jako level „One“, takže nejdřív poběží úvodní prohlídka kapitoly (9,5 s) a kamera se během ní nespustí. Odpal nastav až po ní.
- **#428** (merge `2b8d9f7`): padající kulička v bočním náhledu se kreslí až těsně nad počitadlo „balls left“ (`BallsLeftTop`). Délka pádu je ve světových jednotkách, nejméně stará 2, nejvýš po `KILL_PLANE_Y`. Prvních 60 % pádu je kulička plně viditelná (`PROFILE_SINK_HOLD`). Ve hře při 1920×1080 zhruba 11 jednotek / 230 px místo 2 / 40 px. ⚠ Na notebooku při nízkém FPS fyzika nestíhá reálný čas, takže pád je na snímcích z `shot=` vidět až v nízké kvalitě.
- **#405** (merge `b27c294`): když kurzor nebo myš spočine na odemčené dlaždici výběru levelu, `BackdropScreen.RequestPreview` po 0,35 s zavěsí ten level v jeho scéně, obloze, počasí a stylu kuliček. Let kamery se nerestartuje. Náhodný náhled v menu scénu dál nemění (#249). Ověřeno klávesami na `pick=1`: náhodný `One` ve scéně Mountain se změnil na `Toadstool - 389 balls, bubble, Meadow`. ⚠ **Klávesy vlevo a vpravo na výběru levelu stránkují kapitoly**, neposouvají kurzor po dlaždicích. Kapitoly za 4. jsou na notebooku zamčené (save z 26. 8.).
- **Majitelovy soubory:** `Progress.json` (26. 8.) a `Settings.json` (3. 9.) mají po všech bězích nezměněné časové razítko.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code, bs3d-81 (#444 hudba jako Ogg Vorbis)

**Beru si #444.** Majitel: *„Pusť se do #444.“* Větev `444-ogg-music`, vlastní worktree `BS3D-444`, desktop.

- **Beru na sebe:**
  - `Game/Audio/GameMusic.cs`, nový sdílený dekodér v `Game/Audio`, `Game/Game.csproj` a `Game/Music/*`.
  - `Tools/MusicBake` (Program i csproj).
  - Hudební části `docs/game-feedback.md` a `docs/formats-and-tools.md`, odstavec o nástrojích v CLAUDE.md, `Research/AI-Music/README.txt` a sidecary.
- **Plán:**
  - `MusicBake --tracks` bude zapisovat `.ogg` přes OggVorbisEncoder. Hra je dekóduje přes NVorbis do stejného 16bit PCM jako dnes.
  - MusicBake zkompiluje dekodér hry do sebe, takže délku, chybu na švu smyčky i čas dekódování změří stejným kódem, jaký běží ve hře.
- GPU potřebuju jen na krátké běhy hry, domluvím se s bs3d-49.

**Nic dalšího si neberu.**

**Dodatek: #444 je na `main`u, merge `a4b75f6`.** Větev je smazaná lokálně i na originu a worktree `BS3D-444` je odstraněný. **Issue nechávám otevřené, dokud majitel neposoudí sluchem švy smyček.**

- **Výsledek:**
  - V `Game/Music` je 11 souborů `.ogg` v kvalitě 0.59, dohromady 14,3 MB místo 116,6 MB WAV.
  - `OggTrack` dekóduje přes NVorbis 0.10.5 do stejného PCM, řetěz přehrávání se nezměnil.
  - Menu se načítá jako první.
  - Na desktopu se dekódování všech skladeb najednou vejde do 0,3–0,7 s. WAV se z cache systému četly ~30 ms.
- ⚠ **Dvě pasti, obě chytila kontrola v `MusicBake --tracks`** (délka plus zarovnání začátku a konce proti masteru, jinak exit 1):
  - OggVorbisEncoder 1.2.2 zahazoval prvních 1024 vzorků, protože jeho buffer začíná tam, kde má libvorbis pre-roll. Enkodér teď dostane nejdřív půl bloku ticha.
  - NVorbis vrátil Ember o 80 vzorků delší, protože neumí ořezat do předposledního paketu. `OggTrack` proto končí na počtu vzorků, který stream sám deklaruje.
  - Nakonec všech 11 sedí na vzorek v NVorbis i v libsndfile.
- ⚠ **Kvalita 0.6 a vyšší je v OggVorbisEncoder 1.2.2 rozbitá.** Tabulka coupled high residue je kopie low (upstream PR #24, zatím otevřený). Při 0.8 vyšel medián SNR 9 dB proti 32 dB z libvorbis, poškozené je všechno pod 2 kHz. Pod 0.6 se oba enkodéry shodují. MusicBake hodnoty od 0.6 výš odmítá. Kdo bude chtít vyšší kvalitu, potřebuje jiný enkodér.
- **Past při měření:** okno 8192 vzorků na Nocturne (dlouhý basový tón) našlo falešný posun 726 vzorků. Proto se zarovnání měří na celé sekundě.
- **Ověřeno:**
  - Tři solutions bez chyb.
  - `about=play`: přehrávač hraje.
  - `level=Basket` loguje `[music] Ember: ember.ogg`, `level=One` loguje `[music] Pulse: pulse.ogg`.
  - Žádná chyba při načítání, hashe savu beze změny.
  - Opakované zapečení z nezměněných masterů dá bajtově stejné soubory.
  - Běhy hry proběhly před merge #429 a #432. Po merge prošly jen buildy; strom `main`u je stejný jako strom ověřené větve po merge.
- **Neověřeno:**
  - Sluchem nic. Na krajích smyčky je šum kódování 0,09–1,64× zbytku skladby, výjimky jsou Pulse 2,11× a Bohemia 1,89×. Pro majitele je A/B stránka se švy (artifact „BS3D Music Loops“, v4).
  - Rychlost dekódování na notebooku.
- `--no-wav` se přejmenoval na `--no-write`. Opravena i chyba, kdy `--tracks --no-wav` stejně zapisoval.

---

## 2026-09-16 — Claude Code (zápis k #432, rány se nechytají stropu)

**Beru si #432 na majitelův výběr ze shortlistu.** Větev `432-no-ceiling-attach`, notebook v `C:\GitHub`.

- **Zjištěno z kódu:** náhled dopadu (`TryFindFirstHitCurved` a `TrySolveAgainstBall`) ani sonda prověšení se sklem nepočítají. Přichycení na strop existuje jen v handleru (`TrySolveAgainstCeiling`). Zamítnutí tedy srovná hru s tím, co ukazuje náhled a co sonda měřila, a sondu přeměřovat nebude potřeba.
- **Plán:** náraz do skla nic nerozhodne. Kulka se odrazí, poslouchá dál, a pokud pak narazí do kuličky, přichytí se k ní. Nic dalšího nerozhodne pořadí kontaktů ve stejném kroku.
- **⚠ Po cestě nalezená chyba:** rána odražená od skla se dnes nikdy nezapočítá jako minutí. Posluchač se odregistruje už při kontaktu se sklem, takže pozdější dopad na kámen ani propad pod kill plane nic nenahlásí. Tímhle řešením se to spraví: posluchač zůstane a ránu vyřeší kámen nebo kill plane.
- **Ověření:** bezgrafický test s handlerem nad skutečnou simulací.
- **Soubory:** `BallContactEventHandler.cs`, `ShotPlacement.cs` a `BallsMap.cs` (odstraním nepoužívanou cestu ke stropu), `docs/game-session.md`.

**Nic dalšího si neberu.**

**Dodatek: #432 je na `main`u (merge `dcf86d3`) a zavřené.** Kontakt se sklem teď neudělá nic, jen se zaloguje `[shot] bounced off the glass`, jednou za ránu. Kulka dál poslouchá. `TrySolveAgainstCeiling` a `TryFindEmptyCeilingCell` jsou odstraněné. Horní vrstva levelu i rána do buňky v horní vrstvě vedle kuličky si strop drží dál, jinak by se fyzika rozešla s `GetCellsDisconnectedFromCeiling`.
- **Ověřeno bezgrafickým testem** se skutečným handlerem, simulací i trychtýřem. Rána kolmo vzhůru do volného skla: 9 kuliček zůstalo 9, žádný dopad, jedno minutí. Stejný test na starém handleru zapsal desátou kuličku do skla. Rána do spodku kuličky se dál přichytí (9 → 10). Čtyři solutions bez chyb, LevelGen beze změny, ScoreSim v pořádku.
- **Neověřeno ve hře:** Game nemá skriptované míření, takže skutečný výstřel do skla zbývá majiteli. Test zůstal ve scratchpadu (`glassrig`) a zmizí.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (zápis k #410, náhled slíbí přichycení, rána se odrazí)

**Beru si #410 na majitelův výběr ze shortlistu.** Notebook v `C:\GitHub`. Větev založím, až bude co měnit.

- **Nejdřív měřím.** Bezgrafický test se skutečným handlerem a simulací na skutečných levelech. Dělo míří jako v sondě (`OrbitToFace`, `AimAt`), náhled se spočítá stejnými voláními jako ve hře (`TryFindFirstHitCurved`, `TrySolveAgainstBall`) a pak se vystřelí skutečná rána.
- **Každou ránu, kde náhled slíbil buňku, zařadím podle výsledku:** přichycená (stejná nebo jiná buňka), nebo odražená s důvodem z handleru (volná kulička v cestě, plné oba prstence, sklo, odražení bez dotykové události). Porovnám rychlou kadenci (střelba do padajících uvolněných kuliček) s klidnou.
- Soubory fyziky (`BallContactEventHandler`, `ShotPlacement`) prosím zatím nechte být.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #429 je hotová, mergnutá (`4ccba08`) a zavřená.** Majitel po stránce se srovnáním před a po: *„Vypadá to úplně perfektně! Vidím, že s použitím lokálního AI ti jde grafická práce mnohem lépe!“*

- **Tvar podle referencí z #441:**
  - Kalich je široký 0,64 místo 0,88 při stejné výšce.
  - Podstavec je stupňovitý s bubnem na kameny.
  - Dřík má 12 kanelur mezi dvěma prstenci, nodus 16 žeber. Obojí jsou hladké vlny a kolem osy je 96 segmentů.
  - Kalich má pod okrajem vystouplý pás.
  - Ucha jsou kubická Bézierova křivka. Změřené zapuštění kořenů je 0,0174 a 0,0134, od dutiny 0,0135.
- **Výzdoba** jsou instancované meshe (`TrophyOrnaments`) a přibývá se stupněm: bronz má jantary, stříbro safíry a perlovec, zlato a křišťál navíc rubíny, smaragdy a pás pod okrajem. `MeshBuilder` teď bere 32bitové indexy. Dělo, lafeta a kola vypadají v obou verzích stejně (ověřeno snímkem a blokovým rozdílem ze skillu `capture-review`).
- **Výkon:** párová měření proti `main` (meadow, high, 1600×900 ssaa 2, nocap) leží v šumu 0,3 ms, který mají i dva běhy stejné verze bez poháru.
- **Velikost a umístění** zůstaly. Majitel je spokojený s tím, jak to je.

---

## 2026-09-16 — Claude Code, bs3d-49 (#436 střechy City a Neon City)

**Beru si #436.** Majitel: *„Pusť se do #436 s referencemi střech.“* Předlohou jsou reference z #441 (střechy ve dne, Neon City v noci a rozpis prvků) v `C:\Users\panrd\AI\sd\out\zimage`.

- **Beru na sebe:** novou sdílenou třídu v `Prazsky.Core/Render` (střešní prvky: paraboly, příhradové stožáry s majákem, 5G sloupy, klimatizace), nastavení v `CitySceneConfig.cs` a zapojení do Game (`BS3DGame.Scene.cs`, `BS3DGame.Quality.cs`), Testbedu (`Testbed.cs`, `Testbed.Draw.cs`) a MapEditoru. Dokumentaci v `docs/scenes.md` (město) a případně `docs/rendering.md`.
- **Plán:** prvky se rozmístí deterministicky podle každé budovy. Každý druh je jeden instancovaný mesh s jedním voláním kreslení a ořezem podle frustumu a vzdálenosti. Majáky na stožárech blikají, v Neon City je prvků víc a paraboly mají neonové okraje.
- Výkon změřím v Testbedu s pevnou kamerou (páry proti `main`) a vzhled ověřím snímky z Game. Na #399 (budovy do prázdna) nesahám.

**Nic dalšího si neberu.**

---

## 2026-09-16 — Claude Code (#410 hotové: rána, které náhled slíbil buňku, se přichytí)

**#410 je na `main`u (merge `fc140ad`) a zavřené.** Soubory fyziky, které jsem blokoval, jsou volné.

- **Měření:** bezgrafický test ve scratchpadu (`bouncerig`, zmizí) se skutečným handlerem a simulací na 12 levelech. Dělo míří jako sonda, náhled se ptá stejnými dvěma voláními jako hra, pak padne skutečná rána.
  - **Před opravou:** zamítnuto 3,0 % ran, kterým náhled slíbil buňku (43 z 1441 při pauze 2,5 s), a 4,6 % při rychlé kadenci (42 z 905). Plné okolí zasažené kuličky nezamítlo ani jednu.
  - **Po opravě:** 0,8 % (8 z 987) a 1,0 % (9 z 913). Podíl ran ve stejné buňce jako náhled se nezhoršil.
- **Příčina 1, suť:** rána narazila do kuličky, kterou předchozí rána právě uvolnila. Náhled vidí jen zavěšený cluster. **Oprava:** handler uvolněné kuličky označí (`ContactEvents.MarkLoose`), `NarrowPhaseCallbacks.AllowPair` mezi letící ranou a volným tělem kontakty negeneruje a `PhysicsWorld.RetireBall` označení smaže, než Bepu handle recykluje. ⚠ Volná je i dead weight (#342), takže jí rána proletí. Je to zdokumentované v `game-feedback.md`.
- **Příčina 2, těsný průlet:** náhled počítá zásah už při dotyku povrchů, `OnTouching` až při překryvu. **Oprava:** `OnContactAdded` bere i kontakt kulička–kulička do `SPECULATIVE_MARGIN` (0,1). Sweep tolerance: 0 → 17 zamítnutí, 0,03 → 10, 0,08 → 3–5, 0,1 → 4 (při zhruba 440 ranách).
- ⚠ **Past při vyhodnocení:** první porovnání „stejná buňka 39 % → 50 %“ byl jen rozptyl mezi běhy. Sečteno přes víc běhů je to 43–45 % proti 48–50 %, tedy v rozptylu. Pokud chceš tvrdit zlepšení přesnosti, potřebuješ víc běhů.
- **Ověřeno:** čtyři solutions bez chyb, LevelGen beze změny, ScoreSim v pořádku, test skla z #432 prochází, krátký běh hry na Sillu s výbuchem proběhl bez chyby. **Neověřeno ručně:** rychlá střelba do sutě a těsné zásahy ve hře.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #436 je hotová, mergnutá (`72edda4`) a zavřená.** Majitel spí a nechal mě pracovat samostatně („pracuj sám pořád dál… s využitím lokálního generativního AI“). Mergoval jsem podle stálého pokynu a majitel má stránku se srovnáním před a po.

- **Kód:** nová třída `CityRooftops` a meshe `RooftopMesh` (stožáry s majákem, paraboly, 5G sloupy, klimatizace, neonové obruče), nastavení `CitySceneConfig.Rooftops`, zapojení do Game, Testbedu i MapEditoru. Popis a měření jsou v `docs/scenes.md` (město).
- **Past:** `HashCode.Combine` se v .NET seeduje v každém procesu jinak. Rozmístění pak nebylo deterministické (7 880 proti 7 800 prvkům). Opraveno vlastním celočíselným hashem, teď je to vždy 7 075.
- **Výkon:** Testbed s pevnou kamerou, 3 páry proti `main`. Z herního pohledu +0,01 ms (City) a +0,04 ms (Neon), shora +0,03 a +0,06 ms.
- ⚠ **`MapEditor.cs` má v merge diffu změněný celý soubor.** Byl jako jediný uložený v indexu s CRLF (`i/crlf`), ostatní soubory mají LF, a commit ho sjednotil. Obsahově se v něm změnilo jen zapojení střech (7 řádků).

---

## 2026-09-16 — Claude Code, bs3d-49 (#281, louka: tráva jako tráva)

**Beru si z #281 louku (Meadow).** Majitel spí a zadal: *„vylepšit scény, které to potřebují, aby vypadaly realističtěji a lépe“*. Ze snímků všech scén je louka spolu s Marsem nejplošší. Tráva tam čte jako zelený kámen, přesně jak #281 popisuje.

- **Beru na sebe:** `Testbed/Content/Shaders/Meadow.fx`, `MeadowSceneConfig.cs`, meadow část `SceneRenderer.cs` (`ApplyMeadowParameters`, `DrawMeadow`) a sekci louky v `docs/scenes.md`. Savanu a les zatím nechávám volné.
- **Plán:** udělat referenční obrázky louky (`design-references`) a dát trávě vlastní materiál: trsy s barevnou variací, světlé špičky a tmavé mezery podle reliéfu, sametový lesk při pohledu pod ostrým úhlem a prosvícení v protisvětle. Každý krok změřím v Testbedu s pevnou kamerou proti `main` (poučení z lesa: měřit kombinace, ne jednotlivé členy).

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #281, louka hotová a mergnutá (`a3f06c7`).** Issue zůstává otevřená kvůli savaně, lesu a zabydlení scén.

- **Tráva má vlastní materiál** podle 5 referencí z `design-references`:
  - trsy (spáry z nulových přechodů šumu),
  - špičky a prohlubně podle reliéfu,
  - stébla (šum natažený směrem ke kameře, takže se promítne svisle),
  - suché skvrny,
  - sametový lesk,
  - prosvícení proti slunci.
  Všechno má ovladač v `MeadowSceneConfig`.
- **Výkon** (Testbed, 3 páry, 3200×1800): +0,19 ms z herního pohledu, +0,36 ms zblízka. `Voronoi2` byl dražší (+0,43) a nahradil ho jeden hřebenový šum. Redukovaný program `MeadowReduced` pro Low stojí +0,06 / +0,11 ms a snímkem s `detail=0` jsem ověřil, že se kreslí.
- **Pro další práci na #281:** reference „kameny v trávě“ leží v `C:\Users\panrd\AI\sd\out\281`.

---

## 2026-09-17 — Claude Code, bs3d-49 (Mars: obzor a zem)

**Beru si Mars** (bez issue, větev `mars-skyline-and-ground`). Majitel spí a zadal vylepšení scén, které to potřebují. Na přehledu všech scén je Mars nejplošší: rovný obzor bez tvarů a zblízka hranatá mozaika (hashe po buňkách `floor(xz*0.75)` a `floor(xz*0.18)` v zrnu).

- **Beru na sebe:** `Testbed/Content/Shaders/Mars.fx`, `MarsSceneConfig.cs`, Mars část `SceneRenderer.cs` a sekci Mars v `docs/scenes.md`.
- **Plán podle referencí z `design-references`** (vrstevnaté stolové hory, tmavé čedičové písky, desky podloží s prasklinami):
  - prstenec vrstevnatých stolových hor v prachovém oparu,
  - tmavé písečné pásy,
  - světlé desky podloží s prasklinami,
  - hladké zrno místo hranatých buněk.
  
  Změřím to v Testbedu proti `main`.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: Mars je hotový a mergnutý (`fb16203`).**

- **Obzor:** prstenec vrstevnatých stolových hor (`MesaField`) s pruhy sedimentů.
- **Zem:** tmavé písečné pásy s čeřinami a světlé popraskané desky podloží. Hranatou mozaiku dělalo zrno z hashů po buňkách; jedno je teď gradient noise, druhé je pryč.
- **Ovladače** jsou v `MarsTerrainConfig`/`MarsSurfaceConfig` a popis v `docs/scenes.md` (Mars).
- **Výkon** (Testbed, 3 páry, 3200×1800): +0,39 ms z herního pohledu, +0,85 ms shora, po třech měřených kolech osekávání (původně +0,60 / +1,20).
  - Většinu zbylé ceny nesou hory. Přesun mimo `MarsHeight`, aby je blízká rovina vůbec nepočítala, neušetřil nic měřitelného.
  - Redukovaný program `MarsTerrainReduced` pro Low (hory zůstávají) stojí +0,22 / +0,49 ms.
- **Poučení k nástroji:** v bash heredocu s Pythonem se rozbíjí `\"` a `'\'`. Úpravy s uvozovkami je lepší dělat přes Edit.

---

## 2026-09-17 — Claude Code, bs3d-49 (#404 ostrov podle scény, první materiálová vrstva)

**Beru si #404.** Majitel spí a zadal vylepšení scén, a ostrov je ve všech dvaceti scénách stejný šedý disk. Issue je brainstorm. Tohle bude jeho první konkrétní krok: **materiál** ostrova podle scény (barva víka a těla, lesk, velikost desek), žádná nová geometrie.

- **Beru na sebe:** `BS3DLibs/Prazsky.Core/Render/ArenaIsland.cs` (tabulka vzhledů podle `SceneKind`, přebarvení při kreslení), dvě volání `DrawIsland` v Game a v Testbedu a sekci ostrova v `docs/scenes.md`.
- **Předloha:** reference ostrovů z #441 (sopka, led, tropy, Mars, město, vesmír) v `C:\Users\panrd\AI\sd\out\zimage`.
- Ověřím to snímky menu všech scén před a po a cenu změřím v Testbedu.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #404, první krok mergnutý (`0531dff`).** Issue zůstává otevřená kvůli tvaru a výbavě ostrova.

- **Materiál ostrova podle scény** (`ArenaIsland.LookFor`: barva víka a těla, lesk, velikost desek). Louka a moře zůstávají beze změny.
- **Barva** jde přes přebarvení při kreslení; `DrawIsland` teď dostává scénu.
- **Ladění:** menu všech 20 scén před a po, pět přepálených nebo příliš sytých vzhledů jsem ztlumil.
- **Výkon:** ±0,01 ms (poušť, polární ledovec).
- Komentář s dalšími kroky je na #404.

---

## 2026-09-17 — Claude Code, bs3d-49 (#401 záblesky ve vesmíru)

**Beru si #401.** Při hře se na obloze ve vesmíru objevují a mizí oranžové záblesky s velkým halem.

- **Nejsou to hvězdy z `Stars.fxh`.** Testbed s pevnou kamerou (snímky 0,1 s po sobě) ukazuje body, které blikají a přeskakují jen s časem. Jsou to singularity pochodu `StarNestVolume`: když se bod iterace přiblíží k nule, `activity` vyskočí a po umocnění na třetí dá HDR hodnotu až 19,8 (práh glare je 0,55). Proto záře, velikost i blikání.
- **Model v numpy:** vlastní síť má 99,9. percentil jasu 0,27 a aktivitu do ~63. Oříznutí aktivity kroku na 65 nechá všechny percentily sítě beze změny a maximum spadne z 19,8 na 0,75.
- **Beru na sebe:** `Testbed/Content/Shaders/Space.fx` (`StarNestVolume`), případně `SpaceSceneConfig.SpaceVolumeConfig` a sekci Space v `docs/scenes.md`.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #401 hotová, dva mergy (`0f173d8`, `535b785`), issue zavřená.**

- **Příčina** nebyly hvězdy, ale singularity iterace v `StarNestVolume`. Při pevné kameře blikají jen s časem.
- **Oprava:** strop na jeden krok iterace je `VOLUME_JUMP_CAP` 8, těsně nad 99,9. percentilem běžného kroku (7,8 v modelu na CPU přes 24 kamer). První krok pochodu má `VOLUME_JUMP_CAP_EYE` 30, protože dělá měkkou záři kolem kamery.
- **Kotouček zůstává, jen ztlumený.** Po odletu jdou všechny pixely uvnitř po stejné dráze, takže je to struktura fraktálu.
- **Poučení 1:** strop na součet kroku nepomohl (jádro zploštil, kotouček blikal dál).
- **Poučení 2:** první merge se stropem rostoucím jako 1/s vypadal v Testbedu dobře. Až zvětšenina ze hry ukázala, že nejbližší kotouče jsou nejjasnější. Proto druhý merge. Zvětšeninu ze hry dělat vždycky před mergem, ne až pro stránku.
- **Výkon:** +0,03–0,04 ms při 3200×1800.
- **Stránka:** https://claude.ai/artifact/Rx5fu9CaAnJUYeQEUyhsBV

---

## 2026-09-17 — Claude Code, bs3d-49 (#399 ulice pod městem)

**Beru si #399.** Věže obou měst dnes končí 420 jednotek pod ostrovem nad oblohou a pod nimi nic není. Shora jsou v kaňonech vidět světlé pruhy oblohy.

- **Plán:**
  - `CitySceneConfig.BaseY` posunout na úroveň ulice, kterou jde ještě vidět.
  - Nakreslit zem města: asfalt s pruhy a přechody, chodníky kolem bloků, dlažba tam, kde blok chybí. Vzor jde ze stejné mřížky jako rozložení (`BlockPitch`, `StreetWidth`), takže se s budovami nemůže rozejít.
  - Kaňon dostane zastínění, aby dno ulice nesvítilo plným sluncem.
  - Neonové město dostane tmavý asfalt.
- **Beru na sebe:** nový shader ulic a jeho kreslení v `SceneRenderer`, `CitySceneConfig`, volání v Game/Testbed/MapEditor po budovách a střechách a sekci města v `docs/scenes.md`.
- **Předloha:** reference z lokálního generátoru (pohled shora do kaňonů ve dne i v noci, detail křižovatky).
- **Pozor na #275:** pod ostrovem nebudou žádné budovy. Zem pod ním ale bude, protože díra v zemi s oblohou by byla přesně to, na co si majitel stěžuje. Ověřím, jak se na to dívá závěrečný průlet (`OpenBelow`).

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #399 hotová a mergnutá (`4a7243b`), issue zavřená.**

- **Co je hotové:** `BaseY` −100 a nový `CityStreets` s asfaltem, přechody, chodníky, auty a náměstím se stromy. V neonovém městě lampy, světla aut a neonová záře u paty věží.
- **Stín kaňonu:** dělá ho rozmazaná textura obsazenosti bloků.
- **Výkon:** herní pohled −0,31 ms, pohledy dolů −0,08 až −0,12 ms. Staré věže se stínovaly hluboko pod ulicí.
- **Poučení 1:** opar od oka udělal na asfaltu hnědý závoj. Věže opar nemají, takže ani zem uvnitř města nesmí.
- **Poučení 2:** `NoiseHash22` vrací −1..1, ne 0..1.
- **Poučení 3:** závěrečný průlet se dívá nahoru a zem nevidí. Ulici ukazuje úvod kapitoly The Spectrum (`play level=Icicle` ho spustí).
- **Stránka:** https://claude.ai/artifact/C4s8MvLZKuJoozRe5T591S

---

## 2026-09-17 — Claude Code, bs3d-49 (#435 okenní rámy ve městě)

**Beru si #435.** Majitel opakovaně hlásí, že rámy oken na věžích nejsou vidět.

- **Příčina:** `WindowFrameWidth` 0,1 se počítá v polovinách buňky, takže rám je široký asi 0,085 jednotky. Při 1600×900 je to jeden pixel na 60 jednotek, dál už nic. `resolvable` ho navíc na dálku úplně vypne.
- **Plán:**
  - Rám rozšířit a dát mu vlastní tón proti omítce.
  - Přidat stín ostění (sklo zapuštěné ve zdi) a parapet.
  - Když je rám pod pixel, nenechat ho zmizet, ale přejít na jeho průměrný vliv.
  - Posoudit na herních vzdálenostech ve městě i v neonovém městě.
- **Beru na sebe:** blok oken v `InstancedModel.fx` (`WindowFrameProfile`, rám, stín), `CitySceneConfig` (parametry rámu) a sekci města v `docs/rendering.md` / `docs/scenes.md`.
- **Předloha:** reference fasád z lokálního generátoru v `C:\Users\panrd\AI\sd\out\435`.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: #435 hotová a mergnutá (`85faecc`), issue zavřená.**

- **Co přibylo:** ostění ve vlastním tónu, parapet se stínem, stín nadpraží na skle a příčky. Všechno je box-filtrované přes pixel (`WindowSpan`).
- **Výkon:** +0,05 ms ve městě, +0,06 ms v neonu. Na vzdálených věžích nebliká víc než na `main`.
- **Poučení:** tenký detail filtrovat boxem, ne smoothstepem. Smoothstep přes užší prvek než pixel dává půl jasu a bliká.
- **Poučení:** úroveň kvality Game přepisuje `WindowFrameWidth` (0,1 / Low 0), takže výchozí hodnota v configu na Game nemá vliv.
- **Stránka:** https://claude.ai/artifact/UQWCH2K6Tk1AbbQmBQ94xY

---

## 2026-09-17 — Claude Code, bs3d-49 (#281 lesní podlaha)

**Beru si z #281 lesní podlahu.** Majitel po hraní hlásil, že tráva v lese vypadá jako „čáry a vlny“ a že skutečná lesní podlaha není tak zelená.

- **Příčina čar:** `NeedleRelief` v `Forest.fx` je součet čtyř rovinných sinusovek. To jsou přesně ty vlny.
- **Plán podle referencí** (`C:\Users\panrd\AI\sd\out\281-forest`):
  - rezavě hnědý koberec jehličí, tmavší rozložené skvrny a holá hlína;
  - polštáře mechu s lehkým vyvýšením;
  - tmavé skvrny borůvčí;
  - světlá suchá tráva na slunci v mýtině;
  - reliéf z izotropního šumu.
- **Beru na sebe:** `Testbed/Content/Shaders/Forest.fx` (podlaha), `ForestSceneConfig` (barvy a pokrytí), jejich push v `SceneRenderer` a sekci lesa v `docs/scenes.md`.
- **Nesahám na:** `Aurora.fx` (má vlastní kopii reliéfu) ani na savanu.

**Nic dalšího si neberu.**

**Claude Code, bs3d-49: lesní podlaha z #281 je hotová a mergnutá (`a0f6dd4`).** Issue zůstává otevřená kvůli savaně.

- **Co je hotové:** jehličí, hlína, polštáře mechu, borůvčí, suchá tráva a pramínky jehličí zblízka. Reliéf je šum místo sinusovek.
- **Výkon:** plná verze −0,27 až −0,44 ms. Redukovaná +0,12 / +0,16 ms.
- **Poučení 1:** první verze s vlastním šumem pro každou vrstvu stála +0,64 až +1,04 ms. Sdílet pole mezi maskami vyjde levně a obraz je stejný. Změřit hned po první verzi, ne až na konci.
- **Poučení 2:** `VaryNormal` (16 šumových vzorků na pixel) byla jen záplata na sinusový reliéf.
- **Stránka:** https://claude.ai/artifact/KNjDrWqknBvPPNkEGWGt1d

---

## 2026-09-17 — Claude Code, bs3d-49 (#281 savana)

**Beru si z #281 savanu**, poslední otevřenou část.

- **Příčina:** tráva je tři barevné tóny nad česaným šumovým reliéfem, tedy stejný recept, jaký měla louka, než dostala vlastní materiál. Proto vypadá jako hladký zelený koberec.
- **Plán:** přenést z louky trsy, špičky, stébla, sametový lesk a prosvícení, upravené na trsnatou trávu savany. Mezi trsy bude prosvítat červená hlína.
- **Barvy:** poměr zelené a zlaté zůstává, protože zelenání savany v `ffb5c2b` bylo záměrné.
- **Nízká kvalita:** redukovaný program pro Low, stejně jako u louky.
- **Předloha:** reference v `C:\Users\panrd\AI\sd\out\281-savanna`.
- **Beru na sebe:** `Testbed/Content/Shaders/Savanna.fx`, `SavannaSceneConfig`, push a volbu techniky v `SceneRenderer`, sekci savany v `docs/scenes.md` a `docs/game-shell.md` (redukované programy).

**Nic dalšího si neberu.**
