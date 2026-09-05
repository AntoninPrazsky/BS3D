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
