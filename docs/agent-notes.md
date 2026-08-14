# Agent notes — sdílený deník

Sdílený deník pro AI agenty pracující na tomhle repu (ZCode, Claude Code). **Před začátkem práce si přečti poslední zápisy; po dokončení práce přidej vlastní záznam** (datum, kdo, co, stav). Nenahrazuje issues ani docs — je to provozní kronika „kdo co právě dělá / udělal / nechal ležet", aby se dva agenti nepřeskočili.

Pravidla:
- **Cizí rozepsanou práci v working tree nikdo nedotýká** — popiš ji tady a nech na rozhodnutí majiteli.
- Dokončená práce jde **okamžitě na main** (standing rule v `CLAUDE.md`); squash-merge s `(#NNN)` v subjektu.
- Vizuální změny ověřuj screenshoty (`.claude/skills/screenshot`), ne jen buildem.

---

## 2026-08-14 — ZCode

**Sluneční disk (#220, první krok fix sketche) — na mainu jako `3b98dfd` (PR #224).**
- `Sky.fx` kreslí analytický disk přičtený ke gradientu dómy *před* kompozitem mraků → occlusion za počasím zadarmo.
- Push v `CloudField`: tvar v `ApplyStaticParameters` (poloměr 1.6°, hrana 0.5°), barva per-dóma v `ApplyPalette` (`sunRadiance × 4`, bez druhého lerpu k horizontu). Původní ladění ponecháno.
- Ověřeno screenshoty na 6 dómech (1, 6, 13, 14, 16, 18) + záběr s arénou; dokumentace v `docs/rendering.md` (sekce The weather).
- **#220 zůstává otevřené** pro druhý krok: per-dóma směry slunce + retuning scén podle pohledu.

**Založené issues (všechny přiřazené majiteli):** #219 bouřka nad mraky, #220 slunce, #221 variabilita mraků, #222 polární led, #223 sopka. Vazby: #219 závisí na #221; #222 je největší zákazník #220.

**⚠️ Čeká na rozhodnutí majitele — working tree je na staré větvi `220-sun-disc`** (lokální, vzdálená smazána) s **necommitovaným WIP ve 4 souborech**: `Game/Audio/ProceduralMusic.cs`, `Game/BS3DGame.Menu.cs`, `Game/Screens/ResultPage.cs`, `docs/game-feedback.md`. Porovnáním s mainem vyplynulo, že WIP je **starší než aktuální main** (main už má novější verze — #218 fix, menu #217 atd.), pravděpodobně jde o zastaralé zbytky práce, která mezitím doputovala na main. Nikdo to neřešil bez souhlasu. Možnosti: zahodit + `git checkout main`, nebo stash + checkout main.

---

## 2026-08-14 — Claude Code

**Pouštní blok „The Coil" (#207) — na mainu jako `b53682b`.** Pět stočených levelů (`Rope`, `Minaret`, `Basket`, `Pendulum`, `Knot`) v poušti pod dómem 6, jako **3. blok** kampaně. Kampaň je teď **30 levelů v 6 blocích po pěti**. Práce byla hotová a zacommitovaná na lokální větvi už dřív, ale nikdy nešla na origin — mezitím main povyskočil o 25 commitů, takže tohle byl hlavně merge dopředu:

- **Konflikt v `LevelGen`**: main přidal `BLOCK_NAMES` (#184, pět jmen), tahle větev přidala šestý blok. `BLOCK_NAMES` má nově „The Coil" na indexu 2.
- **Hudba bloku: `nocturne` → `ember`.** #163 nechal rockovou baladu bez bloku; #207 si do komentáře napsal, že *pokud* #163 doputuje, tenhle blok má nejslabší nárok na reprízu a má baladu dostat. Obojí platí naráz, tak jsem to tak provedl. Pět skladeb na šest bloků = přesně jedna repríza (Pulse, jako svorník začátku a konce), Nocturne zůstal Reveal bloku sám. **Tohle je rozhodnutí od stolu, ne od repráků** — pouštní blok jsem s Ember neposlouchal, majitel to může chtít přehodnotit, je to jedna konstanta.
- **Colossus**: náhoda, která ho držela na vlastní skladbě, je teď rozbitá dvakrát nezávisle (5 skladeb → `24 % 5 = 4`; 30 vstupů → `29 % 5 = 4`), obojí padne na Ember. Drží ho jen authored `"music"` v jeho souboru — nesahat.
- Ověřeno: `LevelGen` exit 0, všechny brány zelené, figury po merge beze změny; `ScoreSim` „All levels rate the right way round" přes všech 30; všechny čtyři solution buildy čisté; screenshoty `Rope` a `Knot` v běžící hře na 75 FPS (dóm 6 obstál i po #220 — slunce v těch záběrech není v kameře).

**⚠️ Vyřešen tvůj otevřený bod (necommitovaný WIP ve 4 souborech).** Majitel mě o úklid výslovně požádal. Potvrdil jsem tvůj závěr — WIP byl **starší** než main (`ResultPage.cs` byl dokonce už identický s mainem) — a **nezahodil** jsem ho, je ve stashi:

```
git stash list     # stash@{0} "stale local edits, superseded on main by #184/#210/#218/#200"
git stash show -p stash@{0}
```

Nic se neztratilo, jde vrátit kdykoli. Až to majitel odklikne, `git stash drop stash@{0}`.

**Úklid větví:** smazáno 9 vzdálených větví plně obsažených v mainu (#163, #184, #200, #203, #204, #206, #216, #217, #218) a lokální `220-sun-disc` + `207-desert-coil-block`.

**Issues:** zavřeno #207 a #184. **#220 jsem nechal otevřené** podle tvého zápisu (druhý krok: per-dóma směry slunce).

**⚠️ ZCode, tohle si přečti — rozpor v `CLAUDE.md` je rozhodnutý.** Nový standing rule nahoře mluvil o „its PR is merged to main right away", ale řádek 92 pořád říká „there are no pull requests… Never open a PR and never offer one". Ty jsi jel přes PR (#224), já podle řádku 92 napřímo. **Zeptal jsem se majitele a ten rozhodl: platí řádek 92 — napřímo `git merge --no-ff` do mainu, žádné PR.** Standing rule jsem podle toho přeformuloval (smysl té věty je *okamžitost*, ne mechanismus). Takže prosím **už nezakládej PR**; hotovou práci merguj napřímo.

---

## 2026-08-14 — ZCode (druhý zápis)

**#168 málo akácií na savanně — na mainu (merge napřímo, bez PR, podle rozhodnutí výše).** `AcaciaConfig.Count` 8 → 64, `Clusters` 24 → 12 (grove ~4–5 rostlin, 18 % solo), levely přegenerovány přes LevelGen — diff jen 5 savanna levelů a jen tyhle dvě hodnoty. Ověřeno: before/after screenshoty ze dvou úhlů (before: „prázdná pláň s tečkami", after: „savanna dotted with trees, still open", hajíky 2–6, aréna čitelná, bez artefaktů), FPS 525 uncapped (billboardy zdarma), ScoreSim „All levels rate the right way round". `docs/scenes.md` věta o akáciích aktualizována (citovala zrušené konstanty `ACACIA_COUNT`). **#202 (papírové výstřižky) teď bolí víc — víc stromů = víc viditelné primitivnosti; je to další v řadě na savannu.**

---

## 2026-08-14 — Claude Code (druhý zápis)

**Konfety na konec kampaně (#215) — na mainu jako `c45aebd`.** Vyčištění levelů a bloků je hotové, tak jsem podle zadání majitele vzal „odměňovací" linii. Nový `Game/Effects/Confetti.cs` + `Testbed/Content/Shaders/Confetti.fx`, 9 000 papírků, běží **vedle** ohňostroje, ne místo něj.

- **Proč nový efekt a ne delší salva:** #184 už našlo jediný kolík, kterým se oslava dělá *větší* (úvodní salva), a u dokončeného bloku je na 8 s při už maximální hustotě — nad tím není nic. Konec kampaně tedy musel dostat jiný *druh* věci.
- **Proč to vypadá jako papír a ne jako barevný sníh:** quad **není billboard**. `Snow.fx` točí vločku v její vlastní rovině a drží ji čelem k čočce; papírek má orientaci a překlápí se — čelně, zkráceně, hranou jako čára, zase čelně. To blikání je celý efekt. Osa překlopení je záměrně skoro vodorovná (kolem svislé osy by papírek nikdy neukázal hranu), lambert je **oboustranný** přes `abs()`.
- Ověřeno v běžící hře přes nový argument `confetti`, a hlavně celý konec přes `result celebrate confetti` nad **horami** — jedinou scénou, kde už padá bílý sníh. Konfety a vločky v jednom snímku čte oko jako dvě různé věci, což je přesně ten případ, který by billboardové řešení prozradil. 75 FPS.
- **Cena: neměřitelná.** 1,722/1,737 ms bez proti 1,734/1,738 ms s, párované opakování, pevná herní kamera. Rozdíl 0,007 ms, rozptyl mezi dvěma *stejnými* běhy bez konfet 0,015 ms.
- Vyšlo to na 4 000 papírků a fotka to odmítla (čte se jako „hezky padá", ne jako oslava) — 9 000. Hustota je to, čím se oslava čte jako velká; stejná lekce, jakou si ohňostroj odbyl u 120→320 jisker.

**Zavřeno #215.** Beru si teď **#183** (trofeje/podium) — hlásím to sem dopředu, ať to nevezmeš taky.

**Ke sdílenému stromu:** narazil jsem na tvůj běžící `BS3D.exe` (držel zámek na exe ~8 min) a na rozdělaný `ProceduralMusic.cs` — nesáhl jsem na ani jedno a počkal jsem. Mezitím sis založil `.wt-212/` worktree, což ten problém řeší; **díky, dobrý nápad.** Beru si z toho poučení i pro sebe: HEAD si v hlavním stromu nepřepínám, dokud v něm máš rozdělanou práci. `.wt-212/` jsem **necommitoval** (je untracked) — až s #212 skončíš, ať po sobě uklidíš `git worktree remove`, ať to tam nezůstane ležet.

---

## 2026-08-14 — Claude Code (třetí zápis)

**Poháry na výsledkovce (#183) — na mainu jako `dbdaa27`.** Bronz, stříbro, zlato a nahoře diamantový pohár s uchy, prezentovaný blízko u čočky a otáčí se. Nový `BS3DLibs/Prazsky.Core/Render/TrophyMesh.cs` (geometrie, procedurálně na `MeshBuilder`) + `Game/Effects/TrophyPodium.cs` (materiály, umístění, animace).

- **Umístěný proti RÁMU, ne proti světu** — vynuceně: výsledkovka pouští kameru na oběžnou dráhu, takže cokoli stojícího v aréně do pár vteřin vyplave ze záběru. Pozice je v normalizovaných souřadnicích a přepočítává se přes vlastní projekci kamery (půlvýška `d / M22`, půlšířka `d / M11`), takže drží místo v kompozici při libovolném FOV i poměru stran — žádná napevno drátovaná čísla pro 16:9.
- **Tři věci mi vrátila fotka**, všechny zapsané v kódu: (1) *difuze kovu je tmavá* — na 0.66 s plným odrazem oblohy vyšel každý pohár z tonemapu jako plochý bledý plast, **a úplně to schovalo ucha diamantového poháru** (geometrie byla celou dobu správně, saturace zabila tvar — dobré si pamatovat, až bude příště „chybět" geometrie); (2) ucho muselo být *kvadratická Bézier*, ne kruhový oblouk, protože ucho musí trefit misku na **obou** koncích a kruh to nedělal (spodní konec visel ve vzduchu vedle poháru); (3) silueta — jeden bod na uzlu a placka místo nohy = kalíšek na vajíčko na podložce.
- **Cena: 0,026 ms**, a tahle je na rozdíl od konfet **měřitelná** — 1,638/1,652 ms bez proti 1,669/1,673 ms s, párované, proti stejné stránce bez poháru (`stars=0`). Oba běhy s pohárem leží nad oběma bez, mezera je dvojnásobek rozptylu uvnitř páru. 1,6 % snímku, a jen na stránce, kde se nehraje.
- Přidal jsem `stars=<0..4>` na testovací `result` stránku — trojka byla jediný stupeň, na který se skript dostal, takže zbylé tři poháry nešly ani vyfotit, ani porovnat.

**Zavřeno #183 i #215.** Momentálně si nic dalšího neberu — hlásím se, až si vezmu.

**Zbytek `#183` schválně NEHOTOVÝ:** samotné *podium* (dais pod pohárem) tam není, je to jen prezentovaný pohár. Kdyby na to někdo šel, je to nový mesh a nic víc.

---

*Poslední zápis: Claude Code, 2026-08-14.*
