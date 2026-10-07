# Agent notes — sdílený deník

Sdílený deník pro AI agenty pracující na tomhle repu. **Před začátkem práce si přečti poslední zápisy; po dokončení práce přidej vlastní záznam** (datum, kdo, co, stav). Nenahrazuje issues ani docs — je to provozní kronika „kdo co právě dělá / udělal / nechal ležet", aby se dva agenti nepřeskočili.

Pravidla:
- **Cizí rozepsanou práci v working tree nikdo nedotýká** — popiš ji tady a nech na rozhodnutí majiteli.
- Dokončená práce jde **okamžitě na main** (standing rule v `CLAUDE.md`): větev se mergne `git merge --no-ff` se zprávou `Merge branch '<větev>': <co dělá> (#NNN)` a hned se smaže. (Tady stávalo „squash-merge", což CLAUDE.md nikdy neříkal a historie nikdy nedělala.)
- **Nadpis zápisu má pevný tvar, aby šel najít grepem (#594):** `## YYYY-MM-DD — #NNN <krátký název> — <stroj/agent>`, například `## 2026-09-26 — #598 hash šumu — desktop, Claude Code`. Víc issues: `#598 #599`; bez issue `#none`. Pravidlo „před převzetím issue grepni deník“ stojí na tom, že číslo je v nadpisu: do září mělo 122 z 312 nadpisů jen „Claude Code (n-tý zápis dne)“. Starší zápisy se nepřepisují.
- **Zápis je krátký a věcný:** co je hotové a kde (merge), co je změřené a čím, pasti (⚠) a co zůstává. Dlouhé rozbory patří do issue nebo do `docs/`, sem odkaz.
- **Převzetí issue se píše do issue (od 2026-10-03):** štítek `in-progress` a jeden komentář — kdo (stroj a jméno sezení), co míním, datum; před začátkem `gh issue list --label in-progress`. Sem se píšou zjištění a merge. Podrobnosti: `CLAUDE.md`, „How work is run“.
- Vizuální změny ověřuj screenshoty (`.claude/skills/screenshot`), ne jen buildem.
- **Deník se rotuje po měsících (#358).** Tady je vždycky jen **aktuální měsíc**; starší jdou beze změny do `docs/agent-notes-archive/YYYY-MM.md`. Rotaci dělá ten, kdo píše první zápis nového měsíce. **Hledání jde přes obojí** — `grep -r "..." docs/agent-notes.md docs/agent-notes-archive/`.

---

## 2026-10-01 — #681 obloha pouště a savany do modra (claim) — desktop, Claude Code (bs3d-78)

- **Beru #681** (majitel 2026-10-01: „Přebarvit do modra“). Kopule 6 (poušť) a 14 (savana) vygenerovat z výškových bodů jako kopuli 8 v #661 podle 32 referencí (`C:\Users\panrd\AI\sd\out\sky-*`); spodní pětina zůstane teplá (barva slunce), modrá jen výš. Soubory: `BS3DLibs/Prazsky.Core/SkyDome.Data.cs`, `docs/scenes.md`.

## 2026-10-01 — #681 obloha pouště a savany do modra — desktop, Claude Code (bs3d-78)

- **Na mainu, `shipped-awaiting-verdict`.** Kopule 6 a 14 generované z výškových bodů jako kopule 8 (#661); spodní pětina beze změny do posledního znaku (barva slunce a odraz od země). Poušť 168° → 205° (sytost 0,60 → 0,23), savana 152° → 203°.
- **⚠ Herní kamera vidí kopuli jen asi do t ≈ 0,6** (deset stupňů nad obzorem): první verze měla bledý opar do 0,59 a obloha vyšla skoro bílá. **⚠ Sytá modrá jde přes tonemap do fialové**: savana se sytější modrou níž vyšla 235° a tráva zezelenala od modrého ambientu.
- **⚠ Skript na palety** nejdřív hledal konec bloku podle první uvozovky, a ta byla v komentáři — sežral paletu. Teď se hledá tvar řádku s paletou a kontroluje se, že jich je 20.

## 2026-10-01 — #542 #691 online skóre: veřejná adresa, cizí 404 — desktop, Claude Code (bs3d-a3)

- **Služba je venku na `https://scores.winphonew.eu/`**: doména majitele, DNS u Cloudflare (od 17:32), tunel `bs3d-pi` na Pi. Venkovní testy přes Cloudflare prošly (422/401, 31. POST z jedné adresy dostane 429, IPv6 hned potom ne, takže služba čte `CF-Connecting-IP`). `OnlineScores.DefaultServer` je na mainu (merge 80cd0238), hráči ho dostanou s dalším releasem. Na Pi je navíc tabulka stropů z mainu (`BS3D-dev-80cd023-ceilings.json`, 147 tabulek), aby šlo hrát vývojovou verzi. Před ostrým spuštěním smazat testovací DB a rozhodnout, jestli dev tabulka zůstane.
- **#691: klient zahazoval výsledek při jakékoli 4xx**, i když neodpovídala naše služba. Při přechodu DNS jeden resolver poskytovatele ještě vracel starý hosting (nginx 404) a majitel přišel o 8 výsledků. Teď platí jako odmítnutí jen 4xx s JSON `reason`, cokoli jiného je „no answer“ a výsledek zůstane v outboxu. ⚠ **Stará verze při cizí 404 na „Remove scores“ smazala `Online.json`** (identitu), i když na serveru nic smazáno nebylo; ověřeno se zástupným serverem, nová verze hlásí „NOT done“.
- ⚠ **Přechod DNS trvá až hodinu i po přepnutí u registru**: resolvery si drží NS Webglobe (TTL 3600) a ptají se dál starých serverů. `ipconfig /flushdns` nepomůže, chybná odpověď přichází od resolveru poskytovatele.

## 2026-10-01 — #687 online řádky v Nastavení (claim) — desktop, Claude Code (bs3d-a3)

- **Beru #687**: Off a Not set červeně, přezdívka jako textové pole s nápovědou Enter/Esc přímo na řádku, stav bez serveru na řádku Online. Soubory: `Game/Screens/SettingsPage.cs`, paleta v `Game/BS3DGame.Menu.cs`, `docs/game-shell.md`. #686 (stránky podle kategorií) ne.

## 2026-10-01 — #687 online řádky v Nastavení — desktop, Claude Code (bs3d-a3)

- **Na mainu (merge 3dcac6a9), `shipped-awaiting-verdict`.** Červeně (`MENU_TEXT_ALERT`, druhá výjimka z šedé palety) se kreslí Off, Not set a nové „No server“ přímo na řádku Online. Přezdívka se při psaní kreslí jako pole: text od levého okraje a blikající podtržítko. Poznámka začíná „Press Enter…“ jasným písmem. **Odchod na jiný řádek nebo ze stránky platné jméno uloží** (dřív se zahodilo), Esc ho zahodí.
- ⚠ **`|` v Antonu vypadá jako malé L** („Karell“), proto je kurzor podtržítko. ⚠ **Poznámka bez serveru přetékala** přes devět řádků a byla useknutá v půli věty; teď stojí sama, bez věty o tom, co se odesílá.
- **Testovací páka `settings=` umí `type:<text>`, `enter` a `esc`** (přes `OnTextInput`) a ostatní řádky jdou přes to, co dělá kliknutí. Takže `settings=online,type:Novak,intro` napíše jméno a odejde.

## 2026-10-01 — #682 přejmenovat level One (claim) — desktop, Claude Code (bs3d-a3)

- **Beru #682**: zobrazované jméno „One“ změním na „Cannonball“, soubor `One.json` zůstane (podle něj se ukládají hvězdy). Měním `Tools/LevelGen/Designs/Block01_Meadow.cs`, `Game/Levels` přegeneruji a upravím příklad v `HelpPage`.
- **Hotovo, merge 22710ccf, #682 zavřený.** Tabulka má nový klíč `One.json#1e913f0a50d358cb` (hash se počítá z obsahu souboru včetně jména). Na Pi leží v domovské složce nová dev tabulka stropů (`BS3D-dev-22710cc-ceilings.json`) a čeká na instalaci. ⚠ **LevelGen zapisuje CRLF**, takže `git status` po něm ukáže změněné všechny soubory, i když se nic nezměnilo. Rozhoduje `git diff`; soubory, které se liší jen konci řádků, se vrátí přes `git checkout -- Game/Levels`.

## 2026-10-01 — #683 signál u „Sending your score“ — desktop, Claude Code (bs3d-a3)

- **Na mainu (merge e9476d9d), `shipped-awaiting-verdict`.** Před řádkem stavu pod tabulkami na stránce výsledku je ikona signálu (`OnlineSignal`, čtyři sloupky). Během odesílání a načítání se sloupky postupně rozsvěcují, po přijetí jsou zlaté (barva vlastního řádku v tabulce), offline nebo při odmítnutí tlumené.
- **Ověřování:** testovací level z #546 (`Online546.json`, pět koulí s bombou, `play levelfile=… detonate=6`) proti zástupnému serveru v Pythonu se zpožděním. ⚠ **Odeslání startuje až se stránkou výsledku** (kolem 22. s od startu) a klient po 5 s vzdá, takže zpoždění serveru musí být pod 5 s, jinak se zachytí jen offline.
- ⚠ **Mimochodem nalezený pád (samostatný merge `music-theme-null`):** level bez pole `music` (mapa z editoru, testovací level) shodil herní smyčku v `GameMusic.SetTheme`, protože alias bohemia z #280 volal `StartsWith` na null.

## 2026-10-01 — #689 sklo láme ohňostroj — desktop, Claude Code (bs3d-a3)

- **Na mainu (merge 353e50ef), `shipped-awaiting-verdict`.** Ohňostroj se kreslí ve stejném místě jako ohně savany z #641: v `DrawTranslucentsBehindGlass` (dřív `DrawGroundedTranslucents`) hned po `BeginSceneDraw`, tedy ještě před pořízením kopie pro sklo. Dřív se kreslil až ve `FinishSceneDraw`, po skle.
- **Ověření:** levelem z #546 s `detonate=`, tři běhy na build, záběry z herní kamery, která se dívá na desku. ⚠ **Přední menu s `celebrate` se na to nehodí:** kamera tam desku vidí skoro z boku a výbuchy za ni nezalétnou. ⚠ **`shot=` každou půl vteřinu srazí hru na 2 FPS** (zápis PNG blokuje snímek), tím se posune celé načasování. Lepší jsou čtyři záběry po vteřině.

## 2026-10-01 — #688 kulaté duny — desktop, Claude Code (bs3d-a3)

- **Na mainu (merge 722c74eb), `shipped-awaiting-verdict`.** Hřebeny dun jsou kulaté: návětrná strana a závětrná stěna se spojují přes hladké minimum `CREST_ROUND` 1.0 (dřív to byl ostrý `min`). Obě sady dun se slévají přes hladké maximum `SET_BLEND` 0.35, takže na křížení je místo pyramidy sedlo. `TerrainMirror` počítá totéž.
- ⚠ **Plnější profil posunul střed pole, proto `DUNE_MEAN` 0.30 → 0.436.** Hodnota je navzorkovaná přes skutečný `TerrainMirror` (malá konzole s odkazem na Prazsky.Core a MonoGame DesktopGL 3.8.5; bez toho balíčku spadne při načítání Core), třicet milionů bodů daleko od mýtiny. Kdo změní tvar dun, musí střed přeměřit, jinak se ostrov posune v písku.
- **Ověření:** `mirrorcheck` PASS (max |dh| 0.0002), testy 428/428, Testbed ze tří pozic (`nopost nooverc sky=13 ssaa=2`, `at=2:F12`, aby záběr nezakrýval overlay) a průlet pouští ve hře. První pokus s 0.6/0.25 zakulatil málo.
- **Další páka:** zůstává čára světla a stínu nad závětrnou stěnou. Pokud bude majitel chtít duny ještě kulatější, je to `DUNE_WINDWARD`.

## 2026-10-01 — #686 nastavení po stránkách — desktop, Claude Code (bs3d-a3)

- **Beru #686**: místo jedné stránky se třemi sloupci stránky podle kategorií s lištou záložek, Online jako první a otevřená. Soubory: `Game/Screens/SettingsPage.cs`, případně nová třída pro lištu záložek, `docs/game-shell.md`.
- **Na mainu (merge a6b5ae4e), `shipped-awaiting-verdict`.** Záložky ONLINE · DISPLAY · AUDIO · CONTROLS · GAME, stránka se otevře vždy na ONLINE. Záložky nejsou v procházení padem: tlačítko bez `Action` v `Tag` `CollectNavEntries` nesbírá. Přepínají se doleva/doprava, LB/RB (host je teď čte na všech stránkách, které mají `PageSideways`), kolečkem nad lištou a kliknutím. Rumble se přesunul do CONTROLS, Tutorial, Intro logo a Drop camera do GAME nad CAMPAIGN.
- ⚠ **Myra počítá `Margin` dovnitř explicitní `Height`.** Mezera pod plochou stránek s pevnou výškou se vzala z řádků samotných a poslední řádek DISPLAY byl useknutý přesně o ni (vyfoceno při dvou velikostech mezery). Mezeru teď nese Back. Druhá past: Myra měří jen viditelné prvky, takže plocha se pevně měří ze všech stránek při stavbě stromu, a hodnoty mají do `Refresh` zástupný text „Off“.
- **Ověření:** všechny stránky při 1600×900 a 3840×1600 (`settings=tab:<jméno>`, nová páka) a průchod klávesnicí se skutečnými klávesami (`t686/keys.ps1` ve scratchpadu: fokus kliknutím do titulku, scan kódy). Pad (LB/RB) jsem nezkoušel, žádný tu není.
- ⚠ **Snímky ve 4K v okně se napoprvé neuložily** (`NO SHOT` u všech osmi při `shot=11`, běh trval 17 s). S `shot=9` a během 17 s prošly všechny. Příčinu jsem nehledal.

## 2026-10-01 — #692–#694 poznámky z hraní (Cut/Brake, HUD při průletu, čipy) — desktop, Claude Code

- **Založeno, nic nestaví:** #692 (Cut a Brake nejsou pochopené: řez uvolní jednu kouli nebo žádnou, účel brzdy není jasný; **nejdřív změřit**, na kolika levelech od The Tower má nějaký řez cenu — `docs/game-session.md` sám psal „na redundantně zavěšené struktuře řez skoro nic nedělá“ a „žádná karta to neučí“; čip Brake svítí naplno, i když ho `CanBrake` odmítne), #693 (celé HUD se kreslí přes úvodní průlet kapitoly: `GameplayScreen.cs` `overlayUp = !LevelOver`, nic se neptá `_chapterIntro.Engaged`; potvrzeno snímky `level=31 shot=1.5,4`; drop kamera a ztráta zůstávají mimo rozsah, #639), #694 (klávesy Cut/Brake/Swap nejsou v jednom sloupci: `PlayHud.DrawChip` zarovnává každý čip doprava podle šířky jeho popisku).
- Komentář na #213 (majitelův první verdikt na brzdu a řez; cena se nemá soudit dřív, než #692 řekne, co nástroje dělají). Duplicity: klíčové hledání v otevřených i zavřených issues + deník, jediný rodič #213; LM Studio bylo vypnuté.
- ⚠ „Break“ v poznámce čtu jako **Brake** (Q, brzda stropu) — jediné slovo podobné tomu ve hře; řečeno v #692, ať majitel opraví, pokud myslel jiné.

## 2026-10-01 — #684 nejlepší skóre na dlaždicích výběru levelů — desktop, Claude Code (bs3d-a3)

- **Beru #684.** Otevřenou otázku rozhoduji za majitele (pravomoc v jeho nepřítomnosti): na dlaždici je **vlastní nejlepší skóre hráče** (varianta (a) z issue, offline, z `Progress.json`), jen u odemčených a dohraných levelů. Online #1 zatím ne, protože kontrakt v1 nemá endpoint se seznamem (stejná otázka jako #685). Soubory: `Game/Screens/LevelSelectPage.cs`, accessor v `Game/BS3DGame.Menu.cs`, `docs/game-shell.md`.
- **Na mainu (merge b2fda176), `shipped-awaiting-verdict`.** Čtvrtý řádek pod hvězdami (`ScoreText`), jen kde je hodnocení. Dlaždice je o řádek vyšší (`TILE_HEIGHT` 210 → 260) a pruh s ní, takže spodní řady náhledu jsou víc za deskou. `PREVIEW_LIFT` jsem nechal, při 2,4:1 je horní hrana skla už na okraji snímku. Ověřeno snímky 1600×900 a 3840×1600 nad testovacím uložením (`t684/capture.ps1` ve scratchpadu).

## 2026-10-01 — #693 HUD při úvodu kapitoly, #694 klávesy čipů v jednom sloupci — desktop, Claude Code (bs3d-a3)

- **Beru #693 a pak #694** (domluveno zprávou se session bs3d-cd, která je založila). Soubory: `Game/Screens/GameplayScreen.cs` (bránu kreslení HUD), `Game/Screens/PlayHud.cs` (`DrawChip` a rozvržení čipů), případně test rozvržení v `Tests/BS3D.Tests`, `docs/game-feedback.md`.
- **#693 na mainu (merge 4b6fc277), `shipped-awaiting-verdict`.** `ChapterIntro.HudOpacity`: během letu 0, potom `1 − Blend`, takže je HUD celý přesně ve chvíli, kdy dělo znovu reaguje. Prolínání jde přes overlay vrstvu z #438 (`BS3DGame.FadeOverlayLayer`, předem vynásobená alfa, jeden tint). Drop kamera a ztráta na lince zůstaly beze změny (#639). Ověřeno snímky The Tower `level=31` (1,5/4/14,6 s bez HUD, 15,5 s napůl, 17,5 s celý) a skutečným mezerníkem uprostřed prologu.
- **#694 na mainu (merge f613a4c6), `shipped-awaiting-verdict`.** `ChipColumnLayout`: sloupec kláves podle nejširšího glyfu obou zařízení, popisky od jedné hrany podle nejširšího možného popisku. Test `ChipColumnLayoutTests` jsem viděl selhat. Ověřeno snímky `powerups=swap:3,brake:1,cut:2` proti obyčejným popiskům.
- ⚠ **Majitel u počítače odmítl běh, který bral fokus** (keybd_event se SetForegroundWindow). Když nespí, scénáře řídit bez kláves: `swap=`/`brake=`/`powerups=` a `shot=` přes `quiet.ps1`.

## 2026-10-01 — #692 Cut a Brake v hraní — desktop, Claude Code (bs3d-a3)

- **Beru #692**, nejdřív měření: `LevelGen` dostane přepínač se zprávou jen pro čtení, kolik koulí uvolní jeden řez na každém levelu od The Tower. Teprve podle čísel vyberu směr. Soubory: `Tools/LevelGen` (zpráva), případně `Game/Screens/PlayHud.cs` (čip Brake ztlumený, když `CanBrake` neplatí), `docs/game-session.md`.
- **Měření na mainu (merge 6972a6aa): `LevelGen --cuts`** (`CutProbe.cs`, pravidlo hry přes `GetCellsDisconnectedFromCeiling`). Na startovním clusteru **98 ze 100** levelů s řezem neuvolní žádný řez 3 a víc koulí a na 93 nejlepší řez uvolní jen zasaženou kouli. Něco dává jen Scales (20) a Grotto (3). Alternativa „prsten“ (koule i všichni sousedé): typicky 8, medián nejlepších 12, na čtyřech levelech masa (Kepler 227, Belfry 188, Comet 77, Organ 76).
- **Brake čip na mainu (merge 6529b995), `shipped-awaiting-verdict`:** ztlumený, dokud `CanBrake` neplatí. Ověřeno na Column (`fire=19..24`, světlý po prvním tlakovém kroku).
- **Nechávám majiteli:** co má řez dělat (silnější řez posune stropy ScoreSim a whitelist online tabulek, takže nová tabulka stropů musí na Pi ve stejném vydání) a karty tutoriálu pro Brake a Cut. Kartu pro řez bych dělal až po rozhodnutí o pravidle.

## 2026-10-01 — #690 krok 1: prototyp měkkosti — desktop, Claude Code (bs3d-a3)

- **Beru #690, jen krok 1 z issue:** prototyp měkkosti mřížky v Testbedu na dvou až třech ručně postavených tvarech (provaz mezi dvěma kotvami, hamaka, opona), změřit průvěs, houpání a stabilitu, ukázat majiteli. Formát levelu, LevelGen blok ani scéna zatím ne. Soubory: `BS3DLibs/Prazsky.BS3D.Physics/BallsConstraintsBuilder.cs` (pružina mřížky jako parametr), `Testbed` (páka `softness=`), `Tools/LevelGen/SagProbe.cs` (`--softness=`), mapy v `Testbed/Maps`.
- **Na mainu (merge ac9d2e43), `shipped-awaiting-verdict`.** `LatticeSoftness` přepíše sockety mezi koulemi na danou pružinu (kotvy ke sklu zůstanou). V Testbedu je `softness=<Hz>[:<tlumení>]` se stopou `[softness]` (pokles nejnižší koule, nejrychlejší koule), v LevelGenu `--softness=`. Tvary jsou `Testbed/Maps/Sag_Chain|Bridge|Hammock.json`.
- ⚠ **Hlavní zjištění: dnešních 15 Hz dlouhé tenké rozpětí už prověsí** do řetězovky a rozhoupe na sloupcích (0–4,5 j., perioda ~2,5 s). Provaz a látka jsou hlavně otázka tvaru. 6 Hz dá hluboké V, 3 Hz bungee a vlnění. Nic se nerozpadlo, nejrychlejší koule pod 15 j/s. Čára je limit: sonda se střelami při 14–16 prázdných patrech čáru zasáhla už při 15 Hz v 1–2 z 5 běhů.
- **Animace** (WebP, kamera `campos=0,6,27 camtarget=0,7,0`, `shotframe=` každý pátý snímek) jsou ve scratchpadu `t690/anim-*.webp`. Krok fyziky v Testbedu je omezený na 1/60 s, takže zápis PNG simulaci jen zastaví a nerozhodí ji.
- ⚠ **PowerShell nerozlišuje velikost písmen:** smyčková `$s` přepsala `$S` (cestu ke scratchpadu) a všech devět běhů skončilo na „term not recognized“.

## 2026-10-01 — #695 kapitola (block) v tabulce stropů — Pi, Claude Code (BS3D-API)

- **#695 na mainu (merge této větve), hotovo.** Řádek tabulky stropů (`ScoreSim --ceilings`) nese `block` svého záznamu z `Levels.json`, když ho set má; formát zůstává `bs3d-ceilings` verze 1, set bez bloků zapisuje přesně dosavadní tabulku. Služba pole čte od BS3D-API v0.1.11 a admin stránka podle něj dělí seznam desek na kapitoly (BS3D-API#5). Změněno jen `Tools/ScoreSim/Program.cs` a `docs/formats-and-tools.md`.
- **Ověřeno na Pi** (ScoreSim je čistý `net10.0`): tabulka před změnou a po ní na stejném commitu se shoduje řádek po řádku kromě `block` (130 řádků ve stejném pořadí, 13 kapitol po 10), každý `block` sedí s `Levels.json` a report je beze změny. S kopií `Levels.json` bez bloků je výstup bajt po bajtu stejný jako dnešní. Vydaná služba v0.1.11 novou tabulku načetla a ukázala všech 13 kapitol.
- **Co zůstává:** na Pi se tabulka s kapitolami dostane s příští verzí hry (release ji přikládá) nebo z ručně spuštěného `release.yml` (artefakt `BS3D-dev-<sha>-ceilings.json`), který majitel nainstaluje do `/var/lib/bs3d-api/ceilings`.

## 2026-10-01 — #685 obrazovka High Scores — desktop, Claude Code (bs3d-a3)

- **Beru #685.** Otevřené otázky rozhoduji za majitele (pravomoc v jeho nepřítomnosti):
  - (a) přehled levelů po kapitolách: u každého #1 tabulky a moje pořadí, měsíc a celá doba;
  - kontrakt: jeden nový souhrnný endpoint v BS3D-API, `GET /v1/boards?period=&player=`, místo 133 GETů, které by narazily na limit Cloudflare (50 za 10 s);
  - položka v menu zůstane i bez online skóre, s výzvou k zapnutí.
- Soubory: BS3D-API `Endpoints.cs`/`ScoreStore.cs`/`Contracts.cs` a testy (domluvě se session na Pi poslána zpráva). V BS3D `Game/Online/OnlineScores.cs`, nová `Game/Screens/HighScoresPage.cs`, `MainMenuPage.cs`, `docs/game-shell.md`. Nasazení API na Pi dělá majitel (`update.sh`), do té doby hra řekne, že server souhrn ještě nemá.

- **Na mainu (merge 1515c789), `shipped-awaiting-verdict`. API: BS3D-API#7 sloučené (253c131), neotagované a nenasazené.** Pro nasazení otagovat vydání API a spustit `update.sh` na Pi, což dělá majitel; do té doby stránka hlásí, že server souhrn nemá. Ověřeno proti lokálnímu API s dočasnou DB, naplněnému skriptem `t685/api/seed.py` (ve scratchpadu). ⚠ Hashe levelů se od v0.2.1 změnily (Pennant `ba38be2e0044accb`), aktuální dá `ScoreSim --ceilings`.
- ⚠ **Myra kreslí vypnutý `Button` vlastním světlým pozadím a šedým textem**, takže stránka bez online skóre byla deset nečitelných pruhů. Řádky proto mají `DisabledBackground`/`DisabledTextColor` nastavené ručně.
- ⚠ **Pozadí položky v procházení padem přepisuje `ApplyNavHighlight` při každém průchodu.** Vybrané období se proto odlišuje jasem textu, ne obrácenými barvami jako záložky v Nastavení.
## 2026-10-01 — kritické čtení nočních mergí (#684–#694, #690, #685, #688) — desktop, Claude Code (bs3d-a3)

- **Dva agenti přečetli všechny dnešní merge.** Opravy jsou v mainu:
  - `685-review-fixes`, merge 3c204d7c:
    - High Scores se po návratu ze Settings ptá znovu (dřív navždy „Loading“).
    - `Restart` odpoví čekajícím souhrnům.
    - Cache se zahodí po dohrání, přejmenování a nové identitě.
    - Na jedno období je venku jen jeden dotaz.
    - Status počítá jen tabulky aktuálních levelů.
    - `MENU_MIN_DESIGN_WIDTH` je 2600.
  - `688-true-peak`, merge be27f1e0: hřeben dělený skutečným maximem hladkého minima, `CREST_PEAK = 1 − k·W·(1 − W)`. **První verze #688 měla plochou plošinu 12 j. se dvěma zlomy.** `DUNE_MEAN` je 0,403.
  - `690-review-fixes`, merge acc3a7c1: start swing vrací tuhou pružinu jen socketům, které pořád patří kouli, ze které je vzal. Bepu recykluje uvolněná čísla constraintů. Test na recyklaci jsem viděl selhat.
- ⚠ **Ponaučení: merge bez nezávislého čtení nechal projít skutečné chyby ve třech z devíti mergí.** U větší změny se vyplatí pustit recenzenta hned po sloučení.

## 2026-10-01 — #230 červí díra po třech minech ze stejného úhlu — desktop, Claude Code (bs3d-a3)

- **Beru z #230 „nemožnou ránu“ (červí díru), jen kosmeticky** podle dělicí čáry z komentáře k #230: tři miny po sobě vystřelené ze stejného místa a úhlu (bez přistání mezi nimi) otevřou ve vzduchu na linii výstřelu malou vířící díru, ta nasaje právě ty minuté koule a s puknutím se zavře. **Bez bodů** (bonus by prošel kolem stropů `ScoreSim --ceilings`, které online tabulky hlídají) a bez zásahu do clusteru. Soubory: nový `Game/Effects/Wormhole.cs` (+ případně shader v `Prazsky.Shaders`), `GameplayScreen.Rules.cs`/`.Physics.cs`, zvuk v `ProceduralAudio`, `docs/game-feedback.md`, testy detektoru.

## 2026-10-02 — #495 hudba: řídká a plná varianta podle nebezpečí — desktop, Claude Code (bs3d-a3)

- **Beru #495.** Plán jinou cestou, než issue navrhuje: místo druhé generace z ACE-Step (jiná tónina/tempo, nesynchronní) **oddělím z každé nahrávky bicí** (Demucs na CPU, vlastní venv v `C:\Users\panrd\AI\stems`), takže řídká varianta je *tatáž nahrávka bez bicích* a sedí na vzorek. Hra pak míchá v softwaru `plná − (1 − nebezpečí) × bicí` do jednoho hlasu po kouscích místo přehrávání celé smyčky naráz; nebezpečí = jak blízko čáry visí cluster (totéž, co čte poplach stropu). Jedna kapitola nejdřív, pak majitelův sluch. Soubory: `Game/Audio/GameMusic.cs`, `OggTrack.cs`, `Tools/MusicBake` (zápis stopy bicích), `Game/Music/Drums/*.ogg`, `GameplayScreen` (hodnota nebezpečí), `docs/game-feedback.md`.

## 2026-10-02 — #230 červí díra na mainu — desktop, Claude Code (bs3d-a3)

- **#230 šesté vajíčko na mainu (merge `2087d40d`, opravy po kritickém čtení `1c07aef3`), `shipped-awaiting-verdict`; issue zůstává otevřené (tvar v troskách).** Tři rány po sobě ze stejné pózy (1,5°, 0,5 j. od *první*), každá ven za ostrov (`ESCAPE_RADIUS` 29, letí ven, pořád neurčená), otevřou díru před třetí; pohltí všechny venkovní rány v dosahu 250 (vyřazené jako běžné miny přes `RetireBall` + `OnShotSpent`), stočí je do hrdla a pukne. **Bez bodů** (stropy ScoreSim pro online tabulky). Díra se posune do záběru (`KeepInView`, 72 % poloviny šířky, mimo sloupce HUD) — bez toho byla při ráně pod 45° proužek na kraji obrazu. Nová páka `aim=<t>:<elev>:<trav>` v `ScriptedPlay`.
- ⚠ **Barvy přes 2 v HDR na jasné obloze zbělají a jejich záře zašedí černé hrdlo** — první záběr byl růžovobílá šmouha; `SWIRL_DIM` 0,85 + nízké radiance to spravily.
- ⚠ **Hash buněk v polárních souřadnicích skáče na řezu atan2 o počet buněk po obvodu**, i když je počet celé číslo (frac je spojitý, id ne) — hashovat id modulo počet. Našel recenzent.
- ⚠ **Snímek ve 4K zastaví hru na ~1,5 s a efekt na světovém čase poskočí o 0,5 s** (MonoGame `MaxElapsedTime`); sekvence se fotí v okně 1920×1080. Několik `shot=` časů splynulých za jednu zástavu dá jen jeden snímek.
- ⚠ **Reference: dva běhy `render-references.ps1` naráz na jednom serveru** (první pokus z bashe přežil zabití rodiče) — druhý „použil" klein server a podepsal snímky jako Z-Image. Než se pustí další běh, ověřit `tasklist | grep sd-server` a že předchozí skript doběhl.
- Neověřeno: zvuky (nikdo je neslyší), hraní rukou.

## 2026-10-02 — #495 bubny jako vrstva podle nebezpečí; #674 per-building hash; DocDrift — desktop, Claude Code (bs3d-a3)

- **#495 na mainu pro Louku (merge `1f21677e`, opravy po recenzi `34924a98`), `shipped-awaiting-verdict`.** Bubny každé nahrávky odděleny Demucsem (`C:\Users\panrd\AI\stems`: venv, `separate_drums.py`, `preview_layer.py`), uloženy `MusicBake --drums` do `Game/Music/Drums/` v poloviční úrovni; hra míchá `plná − (1 − g) × bubny` po čtvrtsekundových kouscích (8 ve frontě), `g` z výšky nejnižší koule nad čárou (7 kroků = klid, 0,2). 8 z 10 nahrávek Louky má vrstvu; pastoral (bez bubnů) a waltz (v klidu přebuzuje) odmítnuty. **Neslyšeno ve hře — v noci nebyl zvukový výstup** (Sound Blaster i monitory *Unknown*); poslechové rendery poslány majiteli.
- ⚠ **MonoGame `DynamicSoundEffectInstance.SubmitBuffer` KOPÍRUJE předaný buffer do vlastního poolu** (recenzent to přečetl z IL) — vlastní ring bufferů je zbytečný, stačí jeden scratch.
- ⚠ **`GameMusic` nikdy neuvolnil odehranou variantu rodiny** — kapitola na jedné rodině držela dekódované všechny nahrávky (Louka ~10× ~10 MB) už od #486; teď se drží jen hraná a další.
- ⚠ **Bash nástroj v heredocu i s `<<'EOF'` zhltne zdvojené zpětné lomítko na jedno** (`'\\'` v Pythonu dorazí jako `'\'`, a tenhle řádek sám to v první verzi předvedl) — Python skripty s cestami psát přes Write.
- ⚠ **Demucs na smyčku: separovat ji třikrát za sebou a vzít prostřední kopii**, aby šev smyčky viděl obě strany; výsledek má přesně délku originálu. ~0,9 s CPU na sekundu hudby. Stem může mít špičku nad 1 i tam, kde mix ne (bloom 1,10).
- **#674: per-building losy města** (tón fasády, neonová barva a odstín, pás a jeho výška) mají celočíselný `BuildingRoll` místo `Hash21` (perioda 50 × 100 buněk = 6 opakování přes město), merge `f1ce0f6d`; snímky před/po z Testbedu: den stejný, neon přeházený se stejným poměrem barev.
- **DocDrift**: 22 kandidátů, jediný skutečný drift `MENU_MIN_DESIGN_WIDTH` 2560 → 2600 v `game-shell.md` (merge `0e98daed`).
- **#230 tvar v troskách**: nestavěn — analýza v issue (náhodou se netrefí nikdy, nebo pořád; obrázek visí na pozadí), dvě levné varianty nabídnuty majiteli.

## 2026-10-02 — #257 bedna, krok 1 — desktop, Claude Code (bs3d-a3)

- **Beru #257: bednu** (majitelův souhlas 2026-09-29: „bedna první, pak buckshot v levném čtení“). Krok 1: statický kvádr v hracím prostoru, od kterého se rána odrazí **jako od zrcadla** (analyticky, ne Bepu kontaktem — odraz musí být čitelný a náhled ho musí spočítat stejně: jedna funkce pro fyziku i ducha), bedna nakreslená, pole `crates` v souboru levelu a testovací páka. Žádný dodávaný level ji zatím nedostane (brány `ClearProbe`/`AimReachability` o ní nevědí — to je krok 2 s levelem). Soubory: `Prazsky.BS3D.Physics` (odraz, `ShotPlacement`), `PhysicsWorld`/callbacky, `Prazsky.BS3D/Levels/Level.cs`, `Game` (kreslení, sezení), testy.

## 2026-10-02 — #257 bedna krok 1 na mainu; #696 duch na šikmých ranách — desktop, Claude Code (bs3d-a3)

- **#257 bedna krok 1 na mainu (merge `814bdef3`).** Odraz rány od bedny je analytický (`Crates`: `TryFindFirstFace`, `Reflect`, `BounceShots` v `PerStepForces` po studnách; posun koule na konec odraženého letu minus jeden krok nové rychlosti, gravitace složená dovnitř), Bepu dvojici letící rána–bedna nepáruje (`ContactEvents.IsCrate`). Duch na levelu s bednami krokuje celý let i s gravitací. Bedna nakreslená (`CrateMesh` ve 4 dílech, `CrateField`), pole `crates` v levelu, MapEditor je při uložení zachová. `CrateTests` (4): duch a simulace letí stejně na 5. desetinné místo. Žádný dodávaný level ji nemá; brány o ní nevědí (krok 2).
- **Nová páka `logshots`**: ke každé ráně řádek `[shotlog]` s dopadem/minem a buňkou, kterou slíbil duch při výstřelu. Nástroj na poctivost ducha obecně.
- ⚠ **#696 (nové issue): na šikmých ranách duch a dopad nesouhlasí — i bez bedny.** Bepu speculative contact s koulí *vedle* letu ránu ohne nebo ji přilepí (#410 near-touch) dřív, než doletí ke kouli před sebou. Zkoušel jsem tři změny handleru (řazení podle swept entry, přeskočit near-touch nedosažitelné koule, attach přes swept test z `PreviousPosition`); žádná to neřeší celé, žádnou jsem nenechal. Rozhodnutí o chování škrtnutí je na majiteli.
- ⚠ **Testovací runy: `levelfile=` zabírá slot prvního levelu → úvod kapitoly; přidat `level=3`.** Dva `shot=` argumenty — platí jen poslední. Game hodiny startují ~8 s po spuštění, okno quiet.ps1 dávat ≥ 26 s.
- ⚠ **Paralelně běžet 3 instance Game na desktopu je v pořádku** (single-shot měření po třech, `&` + `wait` v jednom bash volání).

## 2026-10-02 — #257 buckshot na mainu; oprava bedny po recenzi; sweep `down` — desktop, Claude Code (bs3d-a3)

- **#257 buckshot na mainu (merge `b92fa221`).** `BallKind.Buckshot = 12`: nematchovatelný, ale `Removable` — level se nedohraje, dokud shluk visí; padá jen odpojením (pravidla nic dalšího nepotřebují). Brána `LevelGates` „buckshot that can pour" přes `BallsMap.GetUncuttableFromCeiling` (shluk visící na skle jen přes kameny a buckshot se nikdy nesesype). Kreslený jako 20 kuliček (rohy dvanáctistěnu) v perleťově šedé glazuře; uvolněný shluk kreslený rozvolněný (×1,9), takže padá jako sprška. `BuckshotTests` (3). Žádný dodávaný level ho nemá.
- ⚠ **`DrawTintlessRegion` s „cizí" `BallShading` (např. Porcelain) na levelu jiného stylu kreslí černě** — uniformy toho stylu nikdo nenastavil; speciální kresba si je musí nastavit sama (`DrawBuckshot`).
- **Bedna po recenzi (merge `d0b177fc`):** pomalá rána (< 20 j/s) se neodrazí, ale skončí jako mine (bezztrátový odraz na vršku bedny by nikdy neutichl a držel level otevřený); krok, kde let doletí ke kouli dřív než ke stěně, se neodráží; paprsek minutí 24 j. místo 100; kroky daleko od clusteru se na cluster neptají.
- **`Tools/lattice-sweep.ps1` vantage `down` se díval kolmo dolů = degenerovaný look-at, Testbed kreslil jednu plochu** — všechny „down" snímky byly ničím; teď `camtarget 0,-12,58`. Louka je dnes ve všech pohledech 0–2 % (dřívějších 83 % „down" pochází z doby, kdy pohled ještě něco ukazoval; mezitím #609 louku předělal).
- ⚠ Smazal jsem větev `257-buckshot` přes `git branch -D` místo `-d` (byla už sloučená, nic se neztratilo) — pravidlo je `-d`.

## 2026-10-02 — #674 okna města: celočíselný hash okna — desktop, Claude Code (bs3d-a3)

- **Beru z #674 třídu „okna města“** (`City.fxh` `CityPS`): `Hash21(cell + buildingId*101 + k)` pro rytmus, neklid, rozsvícení, neonový kontrast a bzučení (zdegenerovaný hash u vzdálených věží: ~9 různých `rhythm` ze 400 oken při `buildingId` 80–150), a **teplá/studená lampa `Hash21(cell*1.7 + 11.3)` bez budovy** — každá věž má stejné rozložení teplých a studených oken v týchž buňkách. Plán: jeden celočíselný základ na pixel (budova + buňka okna, stejný mixer jako `BuildingRoll`), z něj sůl na každý los; měřit cenu (pevná kamera, střídavé páry), snímky před/po. Soubory: `City.fxh`, `docs/rendering.md`/`scenes.md`. Nesahám na `FacadeNoise` (hodnotový šum na fasádě) ani na ostatní třídy.
- **#674 okna města na mainu (merge `97b55139`), #674 zůstává otevřené.** `WindowSeed` (budova + buňka okna, celočíselně, mixer dvakrát) místo `Hash21(cell + buildingId*101)`; lampa teplá/studená dřív bez budovy (všechny věže stejné rozložení). Emulace: různé rytmy 9/400 → 94 %, shodné řady 20 % → 0,1 %. **⚠ Cena:** samostatný mix na každý los stál +0,11 ms den / +0,16 neon (3840×1600 ssaa 2, úzký pohled na fasády, 8/8 párů); losy s málo bity (rytmus, neklid, lampa, kontrast) se proto řežou z bitů semene → +0,02 den, 0 neon. Před/po: https://claude.ai/artifact/62E2uk55LLexF7enP74vFs. Sweep `down` po opravě vantage: nic nového (okna, čeřiny, sastrugi, kmen palmy).
- **#257 buckshot po recenzi (merge `af03a514`):** brána počítá s dosahem bomby a šachtou kyseliny (`LevelGates.BreakableByLayout`, kámen v dosahu není zeď) a odmítá buckshot spolu s infekcí (stopa kamene může shluk zazdít uprostřed levelu); shluk má `RockTurns`, v censu se počítá jednou, `TryParse` „buckshot“/„pellets“, řádek v Helpu. Ověřeno dočasnou sondou přes reflexi na syntetických rozloženích (obě větve viděny). Cena kreslení broků neměřena.

## 2026-10-02 — #674 moře: mřížka vln (swell aliasuje na mřížce vrcholů, málo vln, chop ze čtyř sinů) — desktop, Claude Code (bs3d-a3)

- **Beru z #674 třídu „voda“ (`Sea.fx`, sdílí moře a tropická laguna).** Změřeno předem (numpy port + Testbed): (1) swell se vyhodnocuje jen ve vrcholech mřížky 4,2 j. a nejkratší vlny (8,5 a 5,5 j.) jsou pod Nyquistem → shora vidět plástev v rozteči mřížky (snímek bez chopu, emulace to reprodukuje); (2) šest vln → interferenční mřížka, autokorelace sklonu 0,971 ve 68 j.; (3) chop ze čtyř sinů se opakuje přesně (1,000 ve 50,6 j.) a dva křížící se siny jsou mřížka kosočtverců. **Mraky zkontrolovány a nejsou vada** (anizotropie 0,96–1,09 vs bílý šum 1,00, žádná mřížka v renderu shora). Plán: swell 20 vln ze spektra (dominantní vlna 52 j. beze změny, zbytek rozprostřen, ±75° od větru), normála swellu per pixel (žádná interpolace mezi vrcholy), posun ve vrcholech jen pro vlny, které mřížka unese (`GridCell` z `TerrainPass`); chop z natažených gradientních šumů. ⚠ Pozor na okupační útes (Noise.fxh `WindGust`) — měřit moře i tropy. Před/po stránka pro verdikt.
- **Beru z #674 květy louky** (`Meadow.fx`: velké a malé květy, lekníny): všechny losy přes `Hash21(cell + k)`, který má na celých buňkách přesnou periodu 50 × 100 buněk — u malých květů (0,8 j.) se rozložení opakuje každých 40 j. Plán: celočíselný hash do `Noise.fxh` (mixer z `City.fxh`, přejmenovaný; město beze změny bajtů), z jednoho semene buňky bity pro losy. Měřit cenu, snímky před/po. Mraky a hvězdy v pásu Mléčné dráhy jsem zkontroloval a vada to není (anizotropie mraků 0,96–1,09; Clark–Evans v pásu 1,068 proti 1,003 ± 0,019).
- **#674 moře na mainu (merge `05554069`, opravy po recenzi `adae0c8a`).** 20 vln ze spektra (dominantní 52 j. beze změny), posun jen tam, kam ho mřížka unese (`GridCell` z `TerrainPass`), normála krátkých 12 vln per pixel, dlouhých 8 z vrcholů; hřeben normalizovaný rozptylem (`CREST_NORM`); chop z natažených gradientních šumů. Sonda: moře shora 71 → 32 %, laguna shora 29 → 7 %. Cena (3840×1600 ssaa 2): **moře +0,95 ms, tropy +0,26**; notebook neměřen. ⚠ `[loop]` s předčasným koncem byl dražší (+1,17 vs +0,53) — zabrání skládání konstant vln. ⚠ Pěna z Jacobiánu nikdy nevznikala (ani předtím). Úvod moře: mez hřebene z naměřeného dosahu (3,93 z 8 M vzorků → 4,2) + odstup 1,5, ať záběry #652 zůstanou. Před/po: https://claude.ai/artifact/1yfipDpcaty93cgk341HmA
- **#674 květy louky na mainu (merge `93a7bc9d`):** `Noise.fxh` má celočíselné `HashMix/HashUnit/HashBits/HashCell/HashSalt` (mixer z města, `InstancedModel` bajtově stejný); louka losuje ze dvou slov semene buňky. Malé květy se dřív opakovaly každých 40 j. Cena 14,34 → 14,25 ms (levnější).
- **#674 zkontrolováno a není vada:** mraky (anizotropie 0,96–1,09), hvězdy pásu Mléčné dráhy (Clark–Evans 1,068). Zbývá z inventáře: kamení ostrova (512² dlaždice po 4 j.), stromy na náměstí (záměrná mřížka), řeka v jeskyni (tři siny), polar, Mars/outback kameny, duny.
- **#674 reliéf ostrova na mainu (merge `7a78e7b9`).** Na víku ostrova bylo ve všech kamenných scénách jemné „tkané plátno“ — `SurfaceReliefWorld` (sedm sinů); vyloučeno snímky: bez reliéfu zmizí, bez textury kamene ne. Teď 4 oktávy `GradientNoise2` promítnuté do tří rovin jako textura (rovina pod `RELIEF_PLANE_FLOOR` se přeskočí bez švu, směs renormalizovaná), zisk podle sklonu sinů. ⚠ **3D šum (`GradientNoise3`) stál ve hře na High 3840×1600 Pennant +0,83 ms (12,97 → 13,80) — přes rozpočet 75 Hz**; promítnutý 2D +0,24 (13,11 → 13,34). ⚠ Pennant na High v nativním rozlišení majitele běží už **před** změnou 12,9–13,25 ms, tedy na hraně 13,33. Testbed (+0,74 pro 3D) a hra (+0,83) se shodují v přírůstku, ale **rozpočet 75 Hz ukáže jen hra v majitelově rozlišení** (Testbed na Full.json má jiný základ). Před/po: https://claude.ai/artifact/WrnGZJnAbuXuQzJNf5nQAS
- **#674 pěna kyseliny na mainu (merge `798a6c2d`):** `Hash21(cell + krok*7)` s krokem z wall clocku → smyčka každých 59 s a po ~50 min zamrznutí (float32). Teď `HashSalt(HashCell(cell), krok)`. Žádný dodávaný level kyselinu nemá.
- **#674 reliéf po recenzi (merge `f2a55cc7`):** zisk `RELIEF_NOISE_GAIN` 2,74 → 2,30 — ⚠ první kalibrace sklonu vzorkovala po 0,05 j., což je pod dva vzorky na buňku nejjemnější oktávy, a sklon šumu podhodnotila (při 0,004 j.: siny 2,299, šum 0,999). Poučení: numerický sklon jemných oktáv vzorkovat aspoň 10× jemněji než nejmenší buňka. Zastaralé komentáře (sedm sinů, tři hrubé oktávy, `ReliefOctaveDirectional`) opraveny; 0,336 ms je označené jako cifra sinů. Riziko „plazení na šikmém víku pod pohybem“ (geometrický průměr footprintu) zapsané, neviděné.
- **#674 řeka v jeskyni na mainu (merge `4c7a39e8`):** tři siny se stejným sklonem → záře na hřebenech kreslila pravidelnou mřížku skvrn. Swell zůstal sinem, 8,8 a 3,1 j. jsou šum (`CloudNoiseD`, analytický sklon místo tří vzorků). Sonda cavern-out 11 → 0 %, graze 14 → 2 %; +0,09 ms.
- **Plný sweep 20 × 4 po dnešku** (`scratchpad/t674/sweep2`): přírodní scény 0–5 % kromě pruhů (polar sastrugi, čeřiny pouště a Marsu, moře), hran (sopka, outback) a záměrů (okna, Grid, kmen palmy). Mnohopíková mřížka v přírodní scéně už žádná. #674 nechávám otevřené pro majitelův verdikt k dnešním změnám.
- ⚠ **CI byl od včerejších 23:41 do rána červený a nikdo to neviděl** (14 mergí): `CrateTests.ThePreviewAndTheSimulationBankOffACrateIntoTheSameBall` (můj, #257) chtěl přesně tu kouli, kterou náhled jmenuje; na desktopu prošel, na runneru trefil sousedy 7/9/15 místo 3 (šikmý příjezd = graze z #696, závisí na usazené póze clusteru do posledního bitu). Opraveno (merge `1d186416`): test drží let po odrazu na náhledově odražené přímce (0,083 max, tolerance 0,15, viděno selhat při 0) a dotek na slíbené kouli nebo jejím sousedovi. **Po merge kontrolovat `gh run list`.**
- **#257 cena broků změřena** (merge `3bf1a511`): 193 shluků vs. dvojče, hra High 3840×1600: 13,92 → 14,38 ms (+0,46), ~2,4 µs na shluk.
- **#400 šestý průchod hygieny** (komentář na issue): 162 souborů od 28. 9., čisté.
- **#674 ostrov ve všech 20 scénách** před/po reliéfu zkontrolován (stránka doplněna), bez regrese.

## 2026-10-02 — #697 koruny stromů louky — desktop, Claude Code (bs3d-a3)

- **Založeno #697 z majitelovy poznámky** (obdélníky v korunách, listy nerostou z větví = „nalepené papírky“). **Beru část 1, chybu masky:** `BroadLeafMask` násobí stonek `step(p.x, 0.74)` → za koncem stonku 0, ne záporné, a `clip(0)` pixel nechá → celá špičková čtvrtina každé karty je plný pruh. Stejný vzor má akácie (`LeafMask`, 2 % u řapíku). Oprava: záporná hodnota za koncem. Část 2 (listy z větví) je návrh s referencemi, nechávám na potom.

## 2026-10-02 — majitelovy verdikty a další dávka — desktop, Claude Code (bs3d-a3)

- **Verdikty zapsány:** OK a zavřeno #674, #686, #687, #685, #694, #693, #669, #675, #95, #280. OK, ale otevřené kvůli dalšímu kroku nebo rozhodnutí: #257 (krok 2), #230 („tvar v troskách“), #690 (krok 2), #692 (co dělá řez, karty).
- **Beru:**
  - #495: bicí pro zbylých 12 rodin, `C:\Users\panrd\AI\stems\batch_drums.py`, vrstva jen při bicích nad −18 dB;
  - #488: záběry do ulic výš, protože ulice a auta jsou 2D;
  - #684: online žebříček **vpravo** od okna výběru levelu, jako na stránce výsledku;
  - #688: duny s ostrými přeryvy, zakulatit jen špičky;
  - #697 část 2: koruna z větví s listy, trsy jen pro nižší kvalitu.

## 2026-10-02 — #684 žebříček ve výběru levelu, #688 ostré duny — desktop, Claude Code (bs3d-a3)

- **#684 na mainu (merge `5a959e41`, opravy po recenzi `ef2cad68`).** Deska s online žebříčkem vpravo od okna levelů, `BoardView` jako na stránce výsledku: tento měsíc a celkově, top 5 a hráčovo místo, nadpis jmenuje level. Jde za kurzorem (poslední odemčená dlaždice), před prvním najetím ukazuje frontu kampaně; tlačítko „Board: …“ jmenuje tentýž level hned.
  - Na cestě je vždy jen jedna dvojice dotazů. Mezipaměť `OnlineSession` (60 s) odpovídá na dlaždici, na kterou se hráč díval znovu.
  - ⚠ **Okno levelů je na 16:9 široké kvůli 13 puntíkům kapitol** (nefitované, 2378 j. i s paddingem). Vedle vycentrovaného okna zbývá 731 j. a deska potřebuje 892, takže se okno posune doleva (`BuildBoards`, šířka okna se měří přes `Widget.Measure`). Na 2,4:1 zůstává uprostřed.
  - **Recenze našla pět chyb, všechny opravené:**
    - mezipaměť žebříčku přežila zápis: stránka výsledku i výběr chtějí stejných top 5, takže po dohrání mohly ukázat staré řádky pod novým pořadím; teď `ForgetBoards` při `SubmitClear`;
    - menu se přestavuje jen při změně VÝŠKY: zúžené okno nechalo desku přes dlaždice a vypnuté online prázdnou desku; stránka se teď přestaví sama;
    - znovu zamčený level zůstával desce;
    - `pickfocus=` vystřelil dřív, než byl výběr nahoře.
  - **Nová páka `pickfocus=<level>@<s>`** posadí kurzor výběru na dlaždici, protože myš hry žádný skript nepohne.
  - ⚠ **Rainbow, Zigzag, One a Shuttle mají v dev buildu jiný hash než majitelovy zápisy na serveru** (soubory změněné od v0.2.1), takže jejich žebříček je v dev buildu prázdný. Tak je to navržené. Diabolo, Five, Saturn a Amphora sedí.
- **Scratch profil pro online snímky:** `settings=online,type:Probe684,enter` (první přezdívka jen lokálně vytvoří identitu, server nic nedostane) a v Settings.json `"server": "https://scores.winphonew.eu/"`, protože lokální build bez jmenovaného serveru neposílá nic.
- **#688 na mainu (merge `f7221e64`).** Ostré hřebeny dun zpět, zakulacené jen špičky. Dva zdroje špiček, nalezené ve výřezu z pohledu `above`:
  - pyramida, kde se hlavní a vedlejší řada potkají ve stejné výšce; poloměr hřebene se teď zvětšuje jen tam (mezera výšek pod `CREST_REACH`, součin výšek přes `CREST_GATE`);
  - pilka, kde ostrý hřeben šikmo křižuje mřížku vrcholů 2,78 j.; tvar se zakulatí na dvě buňky (`CREST_GRID_WIDTH` 5,6, platí i pro zrcadlo), normála jen na dva pixely.
  - Derivace přesné (numpy port, 40 000 bodů), `DUNE_MEAN` 0,337, `mirrorcheck` 0,00022, cena nulová (11,40 proti 11,39 ms).
  - ⚠ Gradient pole bez brány měl nenulový poloměr na 2/3 plochy: dvě řady blízko nuly nejsou setkání.
- ⚠ **Pády desktopu se vrátily i s podtaktovanou GPU** (Kernel-Power 41, bugcheck 0, bez WHEA a dumpu; dnes 10:18, 11:35, 11:36). Obě poslední přišly do minuty od spuštění sd-serveru, když Demucs vytěžoval všech 12 jader. Každá zátěž zvlášť běžela čistě. **Těžké úlohy proto pouštím po jedné.**
  - Po pádu byl `Desert.xnb` nulový: mezivýstup v `Prazsky.Shaders/Content/bin` i kopie v Testbedu, Testbed padal na `ContentLoadException`. Smazat a přestavět.
  - Kontrola po pádu: hlavička každého `.xnb` (`XNB`) a `.dll` (`MZ`), `git fsck`.
- **#495:** 50 vrstev bicích commitnuto na větvi `495-drums-rollout`, aby je pád nevzal; dávka běží dál. Vrstva rozepsaná v okamžiku pádu (nebula-shoegaze) neměla řádek v logu a vznikla znovu.
- **#688 po recenzi (merge `eeb584dc`):** poloměr se počítal z OSTRÝCH výšek, které mají roh na vlastním hřebeni, takže sklon poloměru skočil přesně na čáře, kterou měl zakulatit, a plocha záhyb zdědila. ⚠ Hladká funkce nestačí, hladké musí být i její vstupy. Test záhybu v numpy portu (normála přes 0,002 j. na hřebeni v místě setkání): sloučená verze medián 2,9°, max 38°, oprava 0,006°. Mezera se teď čte z výšek na poloměru mřížky. Normála pixelu je omezená na `CREST_GRID_WIDTH`, protože daleko footprint přerostl mřížku.
- **#697 část 2 na mainu (merge `4a802a55`).** V korunách luk nejsou koule:
  - každý trs je sprej 5–8 větviček z konce větve (`AddSpray`, vlastní síť `Twigs`, jen na plný detail) a karty visí stopkou na větvičkách;
  - jádro se kreslí jen do stínové mapy (`ScatterBucket.ShadowOnly`), koule zůstávají jen na Low;
  - cena: Pennant High 3840×1600, 14,31 → 14,11 ms, každý pár levnější.
  - ⚠ Zblízka to připomíná spíš jasan než dub (zpeřené snítky z `BroadLeafMask`); maska s dubovými laloky je možný další krok.

## 2026-10-02 — beru #698–#702 (majitelovy poznámky z dneška) — desktop, Claude Code (bs3d-1b)

- **Beru #701** (deska žebříčku na výsledku uprostřed pruhu vedle sloupce; zkontroluji i desku ve výběru levelu z #684), **#699** (nápověda přeskočení u každé animace), **#700** (karta tutoriálu problikne po animaci), **#702** (zvuk laserů trvá na stránce výsledku) a **#698** (plot na louce v měřítku stromů). Pořadí přesně takhle.
- **#701 na mainu (merge `8908737f`):** deska žebříčku na výsledku je uprostřed pruhu mezi sloupcem a okrajem. Okraj se dřív uzavíral na 150 j., takže na 3840×1600 bylo 407 / 59 px, teď 234 / 233. Totéž ve výběru levelu: když se deska vedle vycentrovaného okna nevejde, okno se posune a tři mezery jsou stejné.
- **#699 na mainu (merge `41441044`):** nápověda přeskočení u každé přeskočitelné animace, pokaždé. Průlet kapitoly skrývá celé HUD (#693), takže tam se nápověda kreslí samostatně (`DrawSkipHintAlone`).
- **#700 na mainu (merge `ac48ca93`):** pod převzetím kamery stály i hodiny odcházející karty, která se pak po animaci vynořila na 0,35. Odcházející karta teď doběhne neviditelně a rozhodnutý level kartu rovnou zahodí. Tři testy v `TutorialTests`, dva na starém kódu padají.
- **#702 na mainu (merge `a4dc9b26`):** bzukot laserů pod stránkou výsledku po prohře na čáře.
  - ⚠ Jedna smyčka pulzovala (obálka 32 %, r 0,96 po periodě); teď dvě smyčky (0,7 a 0,53 s), zploštělé a srovnané na RMS.
  - Hlasitost −39 dBFS proti hudbě −23.
  - Mrtvý muž: `HoldLineHum` každý snímek z `UpdateUnderResult`. Inscenovaná prohra potřebuje `lasers`, jinak síť nesvítí.
  - Ověřeno loopbackem (`soundcard`, skript `669/rec.py` ze session d2900955).
- **#697 po recenzi (merge `5ce6972f`):** třetina karet stínovala dovnitř trsu → snítky podél slupky, líc ven; třetina listů ležela v neviditelném jádru → jádro pryč, stín vrhají listy. 13,63 → 13,48 ms proti verzi před #697.
- **#495 uzavřeno (merge `e7b29e39`):** 107 ze 118 nahrávek má vrstvu bicích, 3 slabé, 8 odmítnuto kvůli přebuzení. +150 MB.
  - ⚠ Přepnutí z větve, kde byly vrstvy commitnuté, na main je z pracovního stromu smaže (jsou v commitu, ne ztracené).
- **Majitel 2. 10.:** #677 jen duny (zavřeno), #257 a #690 nová kapitola s menší prioritou, #696 doporučeno (b) až po releasu. **Priorita dne: první release s online skóre** (v0.3.0). Návrh poznámek ve scratchpadu `release/whats-new-v0.3.0.md`. Zkouška workflow Release spuštěná na `e7b29e39`.

## 2026-10-02 — v0.3.0, první release s online skóre — desktop, Claude Code (bs3d-1b)

- **Vydáno:** https://github.com/AntoninPrazsky/BS3D/releases/tag/v0.3.0, tag na `7a25c3e0` na majitelovo „ano“. Workflow 4 min, zip 397 MB (115 vrstev bicích). Stažený zip: SHA256SUMS OK, sada shaderů `3e8fbc4a` stejná jako lokálně, roh menu „v0.3.0“, klient hlásí „submitting to https://scores.winphonew.eu/ … game v0.3.0, rules v1, the built-in server“.
- **Pi (session „Update API script on Pi“, SendMessage):**
  - snapshot DB ve 13:21;
  - majitel smazal testovací skóre ve 13:24 (bokem v `/var/lib/bs3d-api/test-data/`);
  - `update-ceilings.sh` ve 13:31: v0.3.0 má 130 tabulí, všechny s `block`, health hlásí 148;
  - tři dev tabulky odstraní majitel.
  - ⚠ **Od smazání jsou tabule skutečných hráčů: žádné testovací zápisy z dev buildů ani ze scratch profilů** (moje mají online vypnuté).
- **Poznámky k releasu:** `release.yml` vkládá volitelný `Images/releases/<tag>.md` mezi obrázek a text ke stažení, takže v0.3.0 od první minuty začíná rámečkem „The first release with online scores“ s postupem Settings → ONLINE → Nickname → Online scores On. Totéž je v README.
- **Obrázek README i releasu:** louka, Cannonball, 3840×1600, majitelova volba. Vyfoceno buildem orazítkovaným v0.3.0 v okně, které nebere fokus, s `tutorial: false` ve scratch profilu.

## 2026-10-02 — beru #690 krok 2: nová kapitola v cirkusovém šapitó — desktop, Claude Code (bs3d-1b)

- **Majitelova rozhodnutí:** scéna je cirkusový šapitó, hudba ve stylu Colossu, bedny a broky z #257 patří do téže kapitoly.
- **Beru:** pole měkkosti v levelu, návrhy LevelGen (Block14), reference a scénu, hudbu a úvodní průlet. Dílčí kroky slučuji průběžně.

## 2026-10-02 — #690 krok 2: kapitola Šapitó je v main — desktop, Claude Code (bs3d-1b)

- **Pole měkkosti** (`0418b4a2`): `"softness": {"hz", "damping"}` v levelu (`SoftnessSpec`, meze 0,01–60 Hz a 0,01–20). Čte ho hra, Testbed, sonda prověšení i editor map, který teď při uložení přenáší i `weather`.
- **Scéna šapitó, 21. `SceneKind`** (`f3d266a1`, revize `8893abba`):
  - celá analytická v jednom průchodu přes obrazovku;
  - hloubku zapisuje podlaha a čtyři stožáry;
  - šest rampových světel jako `SceneLights`;
  - cena jako jeskyně a louka (7,0 ms proti 6,9 ms při 2560×1440, supersampling 2).
  - Ukázka: https://claude.ai/artifact/CFNe2FdZr65BrGVomq7FAF
- **Uvolnění čeká na kameru** (`1a5a900b`): při úvodním průletu visí shluk tuhý a měkkost i zhoupnutí z #617 se spustí, až průlet předá kameru. Platí pro všechny kapitoly.
- **Hudba `bigtop`** (`dae3b557`): 10 skladeb ve stylu Colossu (eurodance s kolovrátkem), všechny s vrstvou bicích. Mastery jsou v `C:/Users/panrd/AI/output/masters-690`. Stránka na poslech: https://claude.ai/artifact/NCXsJ9uhGosEv925rUGawY
- **Kapitola, 12. blok před Gridem** (`57030b4e`):
  - deset levelů: Tightrope, Bunting, Juggler, Trapeze, Hammock, Sandbags, Footbridge, Chandelier, SafetyNet, BigTop;
  - Grid a Mirage mají bránu o 20 hvězd výš.
  - Pravidla kapitoly:
    - žádný úsek mezi kotvami delší než 7 sloupců;
    - sloupky 2×2 ve dvou barvách po úhlopříčkách;
    - pole hluboké 18 úrovní.
  - Každý návrh prošel sondou prověšení 5 z 5. Broky jsou v Sandbags a Juggler.
- **Zbývá:**
  - bedny z #257 (brány je zatím neznají, to je #257 krok 2);
  - majitelův verdikt na scénu, hudbu a levely;
  - ⚠ **při příštím releasu spustit na Pi `update-ceilings.sh`**, jinak online tabulky nových levelů odmítnou zápisy.

## 2026-10-02 — Šapitó po majitelových verdiktech, bedny z #257 — desktop, Claude Code (bs3d-1b)

- **Majitel:**
  - scéna „vypadá skvěle“;
  - reflektory musí být vidět a pomalu se točit, geometrie co nejjednodušší, „podstatné je světlo“;
  - hudba: Juggler, Ringmaster, Spotlight, Swing, Finale a Carousel OK;
  - Calliope, Trapeze a Tightrope OK až ve 2. kole;
  - Clowns až ve 3. kole: původní „infantilní“, prostorová verze bez „high“ momentu;
  - přeje si víc sterea a prostoru (zadání to dnes žádá, model poslechne zčásti, +0,5 až 4,4 dB šířky).
- **Lampy** (`4d98eee6`): válec s čočkou, tyč a kruh. Čočka svítí nad prahem bloomu, kolem je záře a v kuželu prach. Všech pět se točí, cena se nezměnila.
- **Opravy z revize kapitoly** (`111eb749`):
  - Sandbags měl trám 9 sloupců mezi kotvami a pozdě přeseknutý sloupek znamenal prohru, teď je uprostřed ukotvený;
  - sloupky dělené po řadách (`PostInk`), protože úhlopříčka dávala 3 skupiny;
  - Hammock má sloupky ve dvou barvách a 8 kotev;
  - během průletu se fyzika nekrokuje.
- **Hudba:** detektory jsou ve scratchpadu `690music`:
  - `drumonly.py`: úseky jen s bubny proti Demucs stopě, zabral na „bušení“ 36 % / 28 s;
  - `bands.py`: podíl energie pod 100 Hz, na „prdění“ basů 90 % proti 57–69 %;
  - `peak.py`: kontrast obálky jako „high“ moment.
- **#257 krok 2** (`8a84fde5`):
  - `Crates.AddSpecs` je jediná instalace beden pro hru i sondy;
  - sonda prověšení se od beden odráží, sonda příletu je bere jako zarážku;
  - Trapeze má dvě plošiny.

## 2026-10-02 — Šapitó po třetí revizi, cena reflektorů; beru #716 — desktop, Claude Code (bs3d-1b)

- **Oprava předchozího záznamu:** „cena lamp se nezměnila“ platilo jen v Testbedu při 2560×1440, kde snímek omezovalo něco jiného. Ve **3840×1600** (ssaa 2) stály lampy **5,4 ms** (19,74 proti 14,33 ms). Rozpad podle vypínání při kompilaci:
  - prach v kuželech 2,9 ms;
  - průsečíky lamp 2,95 ms;
  - záře 0,55 ms.
  - Úspory se sčítají, takže tenhle průchod je omezený výpočtem, ne obsazeností GPU (na rozdíl od lesa, jeskyně a snu) a každý řez se vyplatí sám.
  - Teď se prach počítá jen v kuželu (`outer > 0`), rampa a záře jen za `NearRig` a každá lampa za testem vzdálenosti od čepu. Výsledek je **+0,3 až 0,7 ms** proti buildu bez lamp.
  - Měřicí skripty jsou ve scratchpadu: `t690/ab.ps1` je A/B dvou buildů a `exp.ps1` řezy přes `#define`. Build bez lamp je ve worktree `../BS3D-prelamps`.
- **Třetí revize** (`7b501eb3`):
  - Tři ostrovní reflektory mají vlastní bodové světlo nad cílem. Bloudí nad kamenem v poloměru 14,5 až 23,5, ne nad sklem trychtýře.
  - Rampová světla jsou 4 místo 6, osmý slot zůstává záblesku výbuchu.
  - Lampa se otáčí kolem čepu a kruh rampy se odvozuje od čepů.
  - Hodiny hry nepočítají průlet.
  - Plošiny v Trapeze jsou na ±14 ve výšce 2,5.
- **Beru #716** (herní strana, neúplné pokusy s 0 hvězdami). Služba v0.1.18 už běží na Pi. Testovat jen proti lokálnímu BS3D-API, nikdy proti živému.

## 2026-10-03 — beru #712 #728 #729 #727 #719 #721 #722 #725 #723 (UI z majitelových poznámek) — desktop, Claude Code (bs3d-54)

- **Beru:** devět drobností z poznámek z 2. 10., každou na vlastní větvi a v tomhle pořadí: #712 (pruh na plechu), #728 + #729 (Help), #727 (High Scores bez zacyklení), #719 (drop cinematic o 20 % vzácnější), #721 + #722 (stránka výsledku), #725 (pohár u pořadí), #723 (glóbus).
- **Neberu:** #703, #715, #711, #710, #720, #704, #706, #726, #730, #714, #696; #716 a #707 mají bs3d-1b.
- Pracuji v worktree `../BS3D-ui`, hlavní checkout je na větvi `crate-test-deflake` (podle reflogu ho tam přepnula session, která sloučila #716, tedy nejspíš bs3d-1b). Kdo sahá do `HelpPage`, `HighScoresPage` nebo stránky výsledku, ať mi napíše.

## 2026-10-03 — #712 #728 #729 #727 #719 #721 #722 #725 #723 jsou na mainu — desktop, Claude Code (bs3d-54)

- **Na mainu, všechno `shipped-awaiting-verdict`:** #712 `954a0fdb`, #728 + #729 `dcc13de7`, #727 `4bfcd430`, #719 `4a10ea77`, #721 + #722 `fef5d043`, #725 `d0a54cbb`, #723 `cd5d4801`. Každé issue má komentář s měřením.
- **#712 měřením, ne podle hypotézy v issue:** pruh není okraj. `SolidBrush` i `FillRectangle` v Myře natahují jediný texel `WhiteRegion` (901, 1) z atlasu skinu a vedle něj je texel (27, 161, 226); pruh je ta modrá krát tón plechu (42/255 × modrá = (4, 26, 37) = naměřených (5, 27, 38)). Vlastní `FlatBrush` (1×1 textura) to opravuje, deska je o 2 px širší. ⚠ **Nová plocha v menu = `FlatBrush`, ne `SolidBrush`/`FillRectangle`** (`docs/game-shell.md`).
- **#728/#729:** `HelpPage.BiggestPage` staví každou stránku do vlastního scrolleru, změří (`Widget.Measure` je veřejné, jako u Settings) a dá zobrazenému `MinWidth` i `MinHeight`. Měnila se i šířka: stránka, která přetéká, dostane posuvník (17 px). Klávesy jsou sloupec široký jako nejširší dvojice a vedle slova se zalomením.
- **#727:** `Turn` ořezává, šipky se vypínají na koncích; kurzor z vypnuté šipky skočí na druhou. Pad/klávesy nezkoušeny v běhu, jen z kódu (`PageSideways` vrací false na konci).
- **#719:** pravidlo spouštění je teď `Prazsky.BS3D.DropTrigger` (jedna kopie pro hru, testy a sondu) a **`LevelGen --drops`** přehraje 140 levelů hladovým a nahodilým hráčem na mřížce (6 rozdání) a vyčíslí každé nastavení nad stejnými hrami. Sonda čte, co četl #615 (5 %/×2: 1,34 a 1,25 proti 1,34 a 1,23; staré 12/×1,25: 1,94 a 2,16). Podíl 5 → **9 %**: 1,11 a 0,96 (−17 / −23 %). Krok 4 z issue (spočítat `[cinematic]` v opravdové hře) **nedělán**: Hra nemá skript, který by level odehrál.
- **#721/#722:** zpoždění rozostření 8 → 16 s. Pohled nahoru **naměřen: 52° na objektivu 60°, horní okraj snímku 82°** (orbita v klidu < 4°); `GLANCE_HEIGHT` 46 → 28 a objektiv se na vrcholu rozšíří o 8° (pitch 38°, horní okraj 72°). Čtyři kandidáty a stránka s nimi: https://claude.ai/artifact/7fLbyByxXX9P14VYhmtKCH (pod účtem, pod kterým běžela tahle session).
- **#725:** pohár U+1F3C6 (náhradní pár, jediný glyf mimo BMP) FontStashSharp i Myra kreslí bez náhrady; sloupec rezervovaný na každém řádku, zlato/stříbro/bronz z hvězd. **#723:** drátěný glóbus, pás 20 snímků vypečený při prvním použití (9–13 ms), klidové stavy stojí na prostředním snímku (snímek 0 měl poledník na hraně a vypadal jako záměrný kříž).
- ⚠ **#725 a #723 jsem neověřoval proti službě ani zástupnému serveru**: dočasná páka (proměnná prostředí) postavila desku s pevnou tabulkou, resp. s vybraným stavem signálu, a pak jsem ji odstranil. První pohled na skutečná jména a na stránku tabulky v pickeru je na majiteli.
- ⚠ **Pasti:** `LevelGen` na Windows zapisuje levely s CRLF, takže `git status` ukáže všech 140 jako změněné, ale `git diff -w` je prázdný (`git checkout -- Game/Levels`). `sceneseed=` pevně nedrží druh scény; `level=<jméno>` s `result` drží scénu levelu (jinak náhodná). `scene=` v `LaunchOptions` je `Row.Value`, hledání `Row.Int/Text` ho nenajde. `ClearProbe` je teď partial (`ClearProbe.Drops.cs`).
- ⚠ **CI:** `CrateTests.ThePreviewAndTheSimulationBankOffACrateIntoTheSameBall` spadl na `956b0dd4` i `d0a54cbb`, stejný nestálý test, kterému se věnuje větev `crate-test-deflake` (bs3d-1b); s mými změnami nesouvisí.

## 2026-10-03 — červené CI: nestálý crate test opraven, zapomenutá větev `crate-test-deflake` sloučena; `Tools/play-latest.ps1` — desktop, Claude Code (bs3d-54)

- **Příčina nestálého testu (merge `5ba98ac3`):** bank shot narazí v jednom kroku na **čtyři koule** (slíbená, dvě vedle ní o úroveň výš a jedna o dvě úrovně výš než slíbená) a `FirstTouch` bral první callback, jenže Bepu pracovní vlákna hlásí kontakty kroku v pořadí, v jakém doběhnou. Na desktopu (24 jader) 30 běhů téhož kroku dalo pokaždé stejné čtyři koule a slíbenou první 21krát; koule o dvě úrovně výš je jediná ze čtyř, která není soused, což odpovídá tomu, jak padá runner. Hra to vyřešila už v #578 (kontakty kroku řadí podle polohy podél letu), test ne.
- **Oprava:** `FirstTouch` zaznamená každý dynamický kontakt a assert je, že slíbená koule nebo její soused je mezi koulemi dotčenými v tom kroku, tedy bez požadavku na pořadí. Hlášení teď jmenuje buňky. Ověřeno: 300 běhů testu v jednom procesu při zatíženém CPU, selhávací větev vyvolaná schválně (porovnání se špatnou buňkou, v hlášení všechny čtyři dosažené buňky) a **CI 10 ze 10 zelená** na deseti jednorázových větvích se stejným stromem (předtím padal zhruba každý pátý běh od 2. 10.).
- **„Zapomenutá rozdělená práce“:** větev `crate-test-deflake` (jediný commit `8ce52e29`, jen pojmenování buněk v hlášení, z relace Opus 2. 10. v 18:41) zůstala nesloučená a hlavní checkout na ní stál; teď je sloučena (merge, ne cherry-pick), větev smazaná na originu i lokálně a checkout stojí na `main`. Ostatní větve na originu jsou všechny sloučené; tři zbylé worktrees ve scratchpadech jiných relací (`wt609-main`, `wt671`, `t684/before-wt`) jsou čisté a jejich HEAD je v mainu, nic v nich nechybí.
- ⚠ **Past, na kterou se narazilo při vzorkování CI:** push **commitu bez změny souboru** na novou větev workflow nespustí (`paths-ignore` ve `build.yml` vidí prázdnou sadu změn jako „vše ignorováno“), a stejný SHA na druhé větvi taky ne. Pro vzorkování je potřeba commit, který mění soubor mimo ignorované cesty.
- **`Tools/play-latest.ps1`** (merge `e32411a2`): načte origin, postaví nejnovější `main` v **druhém checkoutu `..\BS3D-play`** (git worktree) v Release a spustí hru; pracovní složka zůstane na své větvi i se změnami (`-Ref <větev>`, `-NoRun`, `-NoFetch`). První běh 84 s, další 5 s. Hra běží na vlastním savu hráče. Řádek v `CLAUDE.md`.

## 2026-10-03 — beru #708 #724 #703 #730 — desktop, Claude Code (bs3d-54)

- **Beru:** #708 (sklo děla zapuštěné do stěn), #724 (korálky na poháru až k trubce), #703 (HUD čipy výš a menší), #730 (barevný vizualizér s padajícími špičkami). Každé na vlastní větvi, v tomhle pořadí. Pracuji ve worktree `../BS3D-ui`. Majitel povolil pro #725 a #723 poslat testovací skóre na živý server pod hráčem „Test“ a brát GPU úlohy.
- **Hotovo, na mainu, komentář na issue a štítek `shipped-awaiting-verdict` u všech čtyř** (před/po: https://claude.ai/artifact/9ZaPTJafFiPSrSHa7VNoYM, soukromá stránka pod tímhle účtem):
  - **#708** (`b241fe7d`): sklo okna děla je zapuštěné do stěny hlavně, `GLASS_EMBED` 0,06, spodek 0,004 nad vývrtem proti z-fightingu; pryč jsou okrajové čtyřúhelníky a stavitel bočních hran. Měřeno v Testbedu na sedmi pevných polohách hlavně.
  - **#724** (`af66820a`): mezeru korálků u ucha čte z jeho vlastní osy a poloměru trubky `TrophyMesh.HandleHalfAngle` (400 vzorků, sjednocení koulí), na každém prstenci zvlášť, o poloměr jedné korálky od povrchu trubky. Dřív pevných 0,16 rad na obě strany obou řad, tedy dvojnásobek toho, co potřebuje horní řada, a celé zbytečně na dolní. Čtyři testy.
  - **#703** (`3fe6c09a`): písma čipů 182/96 (karta 228/120 krát 0,8, poměr 1,90 zachován), mezery 11/6, spodní řádek čipů stojí `HUD_MARGIN` nad hlavou fronty (bylo 26 nad a 92 pod). Karty beze změny.
  - **#730** (`eba93575`): reference **Z-Image i klein**, pět promptů každý, v `C:\Users\panrd\AI\sd\out\730-zimage` a `…\730-klein` (mimo repozitář). Vizualizér = 18 LED segmentů na sloupec, barva podle výšky (zelená, žlutá od 40 %, oranžová od 62 %, červená od 78 %; hodnoty z referencí), segment se rozsvěcí plynule, pod rozsvícenými je halo, nad hladinou tlumené sklo. **Špičky** (`Prazsky.Core.Tools.PeakHold`): drží 0,33 s, pak padají z klidu s 5,5 výšky sloupce za s², přesně integrované v sekundách (60/240/1000 Hz stejné, sedm testů). Paleta `Led` je výchozí, `Spectrum` (odstín na pásmo) je vlastnost pro budoucí Jukebox (#704). Barva je **záměrná výjimka z pravidla „jas, nikdy odstín“** podle majitele, řečeno ve třídě i v `docs/game-shell.md`. Ověřeno v Game na About (`about=play`) při 1600×900, 1920×1080 a 3840×1600, šest snímků špiček v držení i v pádu, a `about` bez spuštěného přehrávače (tlumené sklo, žádné špičky). Testy 503, čtyři solutions 0 varování.
  - ⚠ **Horní pásma jsou v původních skladbách tichá** (posledních pět šest sloupců sedí u podlahy): je to vážení pásem v `ProceduralJukebox`, nedotčeno; v komentáři na #730 je to otevřená otázka na majitele spolu s volbou palety.
  - **Příhoda s komentáři:** skript na úpravu textu komentářů spadl na zpětném lomítku v bash heredoc, ale `gh issue comment` pokračoval a vložil neopravená tvrzení („méně trojúhelníků“, „precise aim“, „záběr kořene“); opraveno `gh issue comment --edit-last` a ověřeno grepem. Neověřený detail do komentáře o výsledku nepatří.
- **#725 a #723 ověřeny proti ŽIVÉ službě** (majitel povolil hráče „Test“): scratch profil (`userdata=`) s `Settings.json` `online:true` + `server` a ručně napsaným `Online.json`, ztráta čárou (`level=<název> lineloss=3`, skutečný 0hvězdičkový pokus, `201`, desky odpověděly). **Amphora** (bez desky) → #1 z 1, zlatý pohár u řádku Test; **Anchor** (RDT 21 905) → #2 z 2, stříbrný; **Belfry** (RDT, Tereza) → #3 z 3, bronzový, na 1920×1080, 1600×900 a 3840×1600; zeměkoule v `Done` je zlatá. **`Working` na skutečné síti se nepodařilo zachytit** (odezva přes tunel je kratší než první snímek) — to zůstává jen z lever běhů. Test hráč byl **smazán** (`DELETE v1/players/<id>` → 204; živé desky přesně jako předtím: 42 desek, totéž `total` všude, žádný řádek Test v měsíčním ani celkovém pohledu). Skutečný clear skriptem nejde (míření hry se nedá skriptovat), takže zůstává jen ztráta. `Online.json` = `{"format":"bs3d-online","version":1,"playerId":<guid>,"token":<32 náhodných bajtů base64url>,"name":"Test"}`, úklid `DELETE` s `Authorization: Bearer <token>`. Stránka před/po: https://claude.ai/artifact/9ZaPTJafFiPSrSHa7VNoYM.

## 2026-10-03 — poznámky majitele ke scénám a UI → issues #738–#762, uzavřeno #722 a #728 — desktop, Claude Code (bs3d-85)

- **Zadání majitele:** založit/uzavřít issues, **implementace až později**. Nic jsem neimplementoval a **nic jsem si nebral**; všechna nová issues jsou volná.
- **Zavřeno na jeho verdikt:** #722 („úhel je nyní výborný“), #728 („vypadá to dobře“; jeho přání k tlačítkům Previous/Next/Back je nové **#738**). **Komentáře s jeho odpovědí:** #703 (klávesy menší asi o 20 %, bez kola s kandidáty) a #730 (reference barevného analogového vizualizéru si má vygenerovat AI sama). Obě issues má v journalu zabrané bs3d-54; poslal jsem mu odpovědi zprávou, #703 už je na mainu (`3fe6c09a`).
- **Nová issues (1 poznámka = 1 issue, u každé co kód říká, odděleně od hypotéz):** #738 Help pager do jednoho řádku, #739 barva „tohle jsi ty“ v žebříčcích (zlatá se plete s poháry, #725), #740 Scene screen: zvýrazněná položka mimo okno, #741 gravitace per scéna, #742 The Quarry (Měsíc) levely, #743 The Reveal (jeskyně) jako designová reference, #744 Phobos na Marsu, #745 stopy vozítka na Marsu, #746 stopy vozítka a vlajka na Měsíci, #747 Polar objekty, #748 Volcano krajina za kuželem, #749 Outback vegetace, #750 Storm blesk, #751 Forest listí a jehličí na High/Ultra, #752 Big Top prologue, #753 Tropical zelené „bochánky“, #754 Tropical paty palem, #755 Cavern hloubka (ostrov přes krápník), #756 Cavern krystaly, #757 Cavern krápníky u stropu, #758 Savana spoj kmen–větve, #759 Sea střih mezi dvěma skoro stejnými záběry, #760 Sea rybky, #761 Sea ohňostroj pod vodou, #762 trychtýř: koule klipují sklem na špičce. Komentáře s odkazem na #718 (Polar jako pilotní scéna zvířat), #733, #725, #95, #398, #736, #483, #670.
- **Odpovědi na jeho otázky (z kódu, ne z běhu):** (1) **Gravitace je všude zemská** (`new PhysicsWorld()` bez parametru; `gravityY` v konstruktoru je připravený; #95 per-scene gravitaci výslovně nechalo „not done“ kvůli `--sag` přes celý balík). Měsíc = The Quarry (61–70), vesmír = The Nebula (71–80), **Mars nemá žádný level**. (2) **Tmavý disk na Marsu je Phobos**, záměrně (`MarsMoons`, `Mars.fx`), ale bez barvy slunce v osvětlení a na světlé obloze čte jako díra; docs/game-feedback.md to zná. (3) **Storm blesk se kreslí** (`StormBolts`, vertex shader), takže #750 začíná otázkou „proč ho hráč nevidí“ (záběr, okluze, nebo je tupý), ne přidáváním. (4) **Jeskynní kapitola nemá žádné speciální koule** (6 206 koulí, všechny Normal); „speciální kuličky“ ve verdiktu majitele jsou nejspíš žárové koule payloadu nebo čipy Swap/Brake/Cut: ověřit u něj, až se bude aplikovat (#743).
- ⚠ **Neověřeno během běhu** (čtení kódu třemi agenty + moje kontroly vybraných míst): příčina #755 (jeskyňový průchod nezapisuje hloubku, `DepthStencilState.None` ověřeno; že stalaktit stojí před objektivem na snímku 0 ne), #740 (pět kandidátů, žádná reprodukce), #762 (vizuál a kolize splývají, nulová tloušťka; měření průniku chybí), #761 (nenašel jsem cestu ve hře, kdy je objektiv pod vodou, když hrají ohňostroje).

## 2026-10-03 — beru #734 (plate „Sending your score...“ s hotovou stránkou) — desktop, Claude Code (bs3d-54)

- **Beru:** #734. Stav řádku plate (signál + „Sending your score...“) má být od prvního snímku výsledku, ne po `_revealSettled` (~3 s); desky se dosunou po odhalení jako dosud. Reprodukce bez živé služby: `userdata=` s `"online": true` a `"server": "http://127.0.0.1:9/"`.

## 2026-10-03 — beru #731 (průhledné středy písmen ve startovním logu) — desktop, Claude Code (bs3d-54)

- **Beru:** #731. Znovu vyříznout z masteru `logo-9110-x4.png` tak, aby devět středů (B,B,B,O,O,R) bylo průhledných a **fialový obrys zůstal** (rozhodnutí majitele v `Images/logo/README.md`). Skript `cutout-alpha.py` ze skillu `design-references`, ComfyUI venv; před/po snímky splashe přes světlou a tmavou scénu.

## 2026-10-03 — #734 a #731 jsou na mainu — desktop, Claude Code (bs3d-54)

- **#734** (`3567aed3`): plate „Sending your score...“ stojí od prvního snímku stránky, řádky desek čekají na dokončení odhalení (dřív celá plate čekala na `_revealSettled`, tedy ~2 s po ztrátě a ~3 s po clearu). Odpověď dřív než hvězdy: „Your clear/attempt was sent.“ ve zlaté zeměkouli. Build s přepínačem ON a bez serveru (lokální build, tedy i `play-latest`) dřív nabízel „Online leaderboards are off. Turn on Online scores...“, teď „Online scores are on, but this build has no score server to send to.“; to je pravděpodobný kandidát na majitelovo „plate už nevidím“, **nepotvrzeno** (potvrdil by to řádek `[online]` v `Logs\last-run.log`). Měřeno proti loopback stand-inu (`Tools/score-standin.py`, nový), jeden snímek za sekundu: stránka nahoře ve 4 s, plate na `main` v 6 s, po změně na prvním snímku. Nezkoušeno: skutečný clear s rozpisem (skriptem nejde), 3840×1600.
- **#731** (`d26476a9`): devět středů písmen (B,B,B,O,O,R) průhledných, obrys zůstal. Nový průchod `counters 0.65` v `cutout-alpha.py` (skill `design-references`): otevření masky uzavřených oblastí kotoučem 20 px nechá jen tlusté skvrny (drážka je tenká), vyjasnění přes 6 px smoothstep, tenký tmavý lem kolem každého zůstává jako vnitřní obrys. **Past:** našlo 13 skvrn, ne 9 — čtyři stíny číslic v odznaku „3D“ jsou stejně tlusté a nic v nich je neodliší (okolní jas čte 84/80/190), proto se rozsah písma zadává argumentem (0,65 výšky mastera). Skript bez nových argumentů dává původní soubor bajt po bajtu (ověřeno). Alfa ve středech devíti: 0 (bylo 255), změněno právě devět oblastí a nic jiného, pixel-for-pixel kontrola splashe znovu spuštěna: 7 784 448 vzorků, max 1, průměr 0,034.
- Před/po u obou: https://claude.ai/artifact/9ZaPTJafFiPSrSHa7VNoYM (soukromá stránka pod tímhle účtem). Obě issues mají štítek `shipped-awaiting-verdict` a čekají na jeho oko.

## 2026-10-03 — beru #711 (Exposure jako jas v %, 100 % = výchozí) — desktop, Claude Code (bs3d-54)

- **Beru:** #711. Řádek Exposure v Settings jako „Brightness“ v procentech (70…130, výchozí 100 uprostřed), vnitřní násobič `DEFAULT_EXPOSURE × procento`; jedno `DEFAULT_EXPOSURE` v Core místo tří; uložené `exposure` se mapuje na nejbližší příčku; `exposure=<f>` na příkazové řádce zůstává syrový násobič.

## 2026-10-03 — beru #710 (milost čáry delší než 1 s) — desktop, Claude Code (bs3d-54)

- **Beru:** #710. `ClusterHang.BELOW_LINE_GRACE` z 1,0 na 1,5 s (start, který issue navrhuje; ladí se hrou, takže zůstane otevřené pro majitele), `SHOT_SECONDS` v SagProbe zůstává 1,6 (0,1 s rezerva). Brána `--sag` se pustí **sama o sobě** (jedna těžká úloha najednou) před a po v oddělených worktrees; je to „ranking, not a verdict“, takže se porovná seznam úrovní „worth a look“ a devět známých neřešitelných + tři známé řešitelné z majitelova playtestu.

## 2026-10-03 — #711 a #710 jsou na mainu — desktop, Claude Code (bs3d-54)

- **#711** (`5a23d471`): řádek Exposure je **Brightness** v procentech autorského vzhledu, 70 až 130 po desítkách, 100 % uprostřed, cyklí nahoru a obtáčí (řádek nemá krok zpět). Logika v `Prazsky.Core.Render.BrightnessLadder` (12 testů), jedno `PostProcessPipeline.DEFAULT_EXPOSURE` místo tří kopií. `Settings.json` drží dál syrový násobič a starší hodnota se čte na nejbližší příčku (0,7 → 70, 0,9 → 80, 1,1 → 100, 1,3 → 120, 1,5 → 130); `exposure=<f>` zůstává syrový násobič (`exposure=1.0` ukáže 91 % a klik dá 100). `settings=brightness` pro skriptované běhy. Ověřeno v Game: průměrná světlost scény 93 / 115 / 121 / 127 / 132 při 70 / 100 / 110 / 120 / 130 %, nic nezaklipované. Nezkoušeno: pad.
- **#710** (`9d2ddc31`): `BELOW_LINE_GRACE` 1,0 → **1,5 s** (start z issue, ladí se hrou, takže zůstává otevřené pro majitele), klauzule hloubky (1 jednotka) beze změny, `ClusterLineWatchTests` (10). **Brána `--sag` přes 141 úrovní, sólo, třikrát**: 73 sagged běhů při 1,0 s, 75 při 1,5 s (v rámci ±1 chvění sondy), 71 při 1,5 s i s hloubkou 1,5 (neshipnuto). **44 z 45 úrovní, jejichž nejhorší běh je sag, končí klauzulí hloubky**, jen Bolt časem — takže delší čas je vidět jen na mělkém držení čáry a hluboký průnik se ztrácí stejně rychle jako předtím („patchy“ riziko z issue); jestli má hluboký průnik dostat okno, je rozhodnutí majitele a brána ho nerozhodne. Stará kalibrace (tři řešitelné, devět ne, „čtyři z pěti“) na dnešních úrovních nereprodukuje při žádné hodnotě; brána je pořadí, ne verdikt, nic se nepřekalibrovalo. Nezkoušeno: skriptované držení čáry ve hře (potřebuje ručně postavený řetěz v mřížce) a čitelné okno.
- Past, na kterou jsem narazil: `LevelGen --sag` přepisuje všech 141 `Game/Levels/*.json` s CRLF konci (obsahově totéž, `git diff -w` nula) — po běhu `git checkout -- Game/Levels`, nikdy ne do commitu. A `git branch -D` jsem jednou použil na čerstvou větev bez vlastních commitů místo `-d`; nic se neztratilo, ale pravidlo je `-d`.
- Před/po u #711: https://claude.ai/artifact/9ZaPTJafFiPSrSHa7VNoYM (soukromá stránka pod tímhle účtem). Obě issues mají `shipped-awaiting-verdict`.

## 2026-10-03 — beru #714 (zip vydání: obvyklá cesta dovnitř) — desktop, Claude Code (bs3d-54)

- **Beru:** #714. Pořadí z issue: (1) uklidit složku (`SatelliteResourceLanguages=en`, cizí `apphost.exe` a ladicí zbytky 2.1), (2) vyzkoušet single-file self-contained bez trimování lokálně a přes „Run workflow“ (nic se nepublikuje), (3) jinak podsložka s launcherem. Zip se zkouší lokálně a z workflow artefaktu; **skutečně stažený zip s mark-of-the-web ověřit neumím sám**, to zůstane jako otevřené pro majitele.

## 2026-10-03 — #714 je na mainu — desktop, Claude Code (bs3d-54)

- **#714** (`b517dc46`): zip vydání je teď **`BS3D.exe` + `Content`, `Levels`, `Music`, `Sfx`** (5 položek místo 299 souborů a 17 složek). Single-file self-contained **bez trimování** (bundling bez trimu neodstraní typy), `-p:DebugType=embedded` na příkazové řádce (dosáhne i na tři knihovny), `SatelliteResourceLanguages=en` (52 překladových souborů pryč) a cíl v `Game.csproj`, který z publikace vyřadí balíčky `runtime.win-x64.Microsoft.NETCore.App` 2.1.30 (49 souborů: `api-ms-win-*`, `ucrtbase`, `clrcompression`, `dbgshim`, `sos`, `mscordaccore`) a `…DotNetAppHost` (druhý `apphost.exe`) — táhne je `MonoGame.Framework.WindowsDX` přes starou závislost, nikdy se nenačítají. Crash zprávy drží soubor a řádek (ověřeno na **skutečném exe z runneru**: schovaný shader → `crash-*.txt` s `BS3DGame.cs:line 987`), start beze změny (3 střídavé běhy).
- **Zkouška na skutečné pipeline:** ruční „Run workflow“ na větvi (nic se nepublikuje): 456 souborů, 491 MB, **397,9 MB zipu** proti **417,0 MB** ve starém rozložení se stejným obsahem; stažený a rozbalený zip (s ručně přidaným `Zone.Identifier`) hraje Pennant, načte úrovně i hudbu, ukáže splash i About. Kontrola ve workflow („Check the published folder is playable“) teď selže na čemkoli jiném na nejvyšší úrovni; viděna při selhání na starém rozložení (312 položek) i na single-file s 2.1 zbytky. `CLAUDE.md` přepsáno: číslo „218,1 MB“ bylo z 23. 9., hudba mezitím vyrostla (`Music/` 350 MB).
- ⚠ **Nezkoušeno a co udělat nemohu:** skutečně stažený zip z GitHub Release a dialog SmartScreen (skript ho neovládne). **První tag po tomhle mergi je první skutečné publikování z tohoto rozložení — sledovat ten běh `release.yml`.**
- Poznámka k „Hotovo“ z issue: launcher (možnost 3) se nestavěl, nebyl potřeba.

## 2026-10-03 — beru #713 (varování HLSL) — desktop, Claude Code (bs3d-54)

- **Beru:** #713. Sedmnáct odlišných varování HLSL, u každého buď oprava s důkazem, že se obraz nezměnil (shodný zkompilovaný `.xnb` před/po, jinak párové snímky), nebo ponechat s důvodem v komentáři na místě. Základ: zkompilované `.xnb` všech shaderů před změnou uložené mimo repozitář.

## 2026-10-03 — #713 je na mainu (částečně, zdokumentováno) — desktop, Claude Code (bs3d-54)

- **#713** (`134c08f3`): z **19 odlišných varování HLSL** (31 řádků; issue mělo 17, Circus přidal dvě X4000) zůstalo **11** (20 řádků). **Opraveno a ukázáno beze změny obrazu:** `Aurora`, `Glowworms`, `Earth` (X4000, vzor z #587: tělo za `[branch]` je `…Lit`, původní funkce jen volí a vrací jednou; Aurora se v disassembly liší jen porovnáním `ge`→`lt` a `if`→`else`, Earth je pixel po pixelu shodná, glowworms ±1 jako základní běh proti sobě), `SampleScene` v Tonemap (shodná pixel po pixelu při ssaa 1/2/3), `FireworkVS` (X3578, jeden `mov`), porcelánový `HilbertPoint` (X3556, `uint`, ověřeno na CPU pro všechny indexy do 64×64) a lávový `pow` (`max(hot,0)`). **Ponecháno s důvodem na místě:** 3× X3557 (konstantní počet oktáv), 2× X3571 (Blast, Flame: nezápornost je vlastnost dat z CPU), 6× X4000, které se nepodařilo ukázat beze změny obrazu: **Space (`StarNestVolume`, `Planet`)** — jakékoli překompilování `Space.fx`, i rozdělení samotné `Planet`, posunulo mlhovinu až o 19 úrovní z 255 (základní běh proti sobě o 1; pozdní základní běh se shodoval s prvním, takže to není časový drift), `ForestShadow` (vítr), `MoonDisc` (měsíc není v základním pohledu) a Circus `ShadeRoof`/`ShadeSeats` (vnořené returny, nezkoušeno).
- **Past, na kterou se narazilo:** dva běhy Testbedu téhož sestavení se liší skoro všude, a `nopost` to nespraví (jiggle clusteru, sníh, vítr, FPS text po snímku); proto kontrolní běh + pevné oblasti pozadí + srovnání instrukcí (`D3DDisassemble`, registry ignorovány). Samotné `at=:F12` muselo přijít **před** snímkem (`shotframe=` 120, F12 v 0,3 s). Nástroje (`xnbdiff.py` atd.) jsou ve scratchpadu relace, ne v repu. CI krok proti návratu varování **nepřidán** (potřeboval by být viděn při selhání; dnešní základ je 20 řádků / 11 odlišných).

## 2026-10-03 — verdikty majitele: #730 #708 #724 #703 #725 #723 #734 #731 #711 uzavřeno, nové #764 — desktop, Claude Code (bs3d-54)

- **Majitelovy verdikty (vložené do chatu):** #730 „Vypadá to skvěle!“ (+ nápad: paleta Spectrum jako easter egg, když má hra narozeniny), #708, #724, #703, #725, #723, #734, #731, #711 všechna „Super!“. Všech devět **uzavřeno** s komentářem a verdiktem, štítek `shipped-awaiting-verdict` sundán.
- **Nové #764** (easter egg: paleta Spectrum na narozeniny hry): **otevřená otázka, který den je narozeniny** — první commit repozitáře **30. 12. 2019**, první vydání v0.1.0 **18. 9. 2026**, v0.3.0 (online skóre) 2. 10. 2026; doporučení 30. 12., rozhodnutí je jeho. Do té doby se neimplementuje (`MusicVisualizer.Palette` už je vlastnost, nic ji zatím nenastavuje).
- Čeká na verdikt z mých: **#710** (milost čáry 1,5 s; potřebuje pocit z hraní), **#714** (zip: první skutečný tag po mergi je první skutečné publikování z nového rozložení, sledovat běh `release.yml`; SmartScreen jen člověk), **#713** (6 X4000 ponecháno s důvodem na místě).
- Odkaz na stránku s před/po (https://claude.ai/artifact/9ZaPTJafFiPSrSHa7VNoYM) majitel pod aktuálním účtem nevidí (artefakt „nenalezen“, účty se střídají); verdikty přesto přišly.

## 2026-10-03 — beru #763 (první spuštění se zeptá na přezdívku pro žebříčky) — desktop, Claude Code (bs3d-aa)

- **Beru:** #763 na majitelův pokyn („Vem #763 a udělej to“). Rozhodnutí majitele jsou v issue (souhlas je jen pole pro jméno, „online“ = připojení k internetu, druhé Esc = Skip). Soubory: `Game/GameSettings.cs` (`Online` na `bool?`, počítadlo zavření), `Game/Online/OnlineSession.cs`, nová deska nad hlavním menu a z ní vytažené pole přezdívky ze `Game/Screens/SettingsPage.cs`, `Game/LaunchOptions.cs`, `Game/BS3DGame.Menu.cs`, testy, `docs/game-shell.md`, `docs/formats-and-tools.md`.

## 2026-10-03 — #763 je na mainu (první spuštění se zeptá na přezdívku pro žebříčky) — desktop, Claude Code (bs3d-aa)

- **#763** (`2a3c0ed2`, oprava `b2b5726e`), `shipped-awaiting-verdict`. Po úvodním logu okno „NICKNAME / Pick a nickname for the leaderboards.“ s polem a jedním tlačítkem. **Souhlasem je jen pole pro jméno** (rozhodnutí majitele: žádná věta o soukromí, žádné „online“); „online“ = jen připojení k internetu (`InternetCheck`, `INetworkListManager`, ~5 ms, nic neposílá); druhé Esc = Skip. `GameSettings.Online` je `bool?` (chybí = nerozhodnuto), `nicknameDismissed` počítá Esc. Soubor s `"online": false` se čte jako rozhodnutí.
- **Tlačítko je OK nebo Skip podle pole** (majitel po prvním pohledu: napsal jméno a dole viděl Skip): OK při platném jménu, Skip při prázdném nebo neplatném; pad A dělá, co tlačítko říká.
- **Past, kterou našel review (opraveno):** `Manager.Pop()` jen zařadí operaci a `ApplyPending` pak sundá, co je nahoře, bez kontroly, co to je. Dvě odpovědi mezi dvěma snímky (Enter + klik, auto-repeat při pomalém snímku) sundaly i hlavní menu a zbyla scéna bez UI. Reprodukováno `nickprompt=type:Novak,enter,skip` na prvním sestavení, opraveno zámkem `_answered`. **Každá stránka, která se zavírá `GoBack()` z více vstupů, má stejnou díru.**
- **Páky:** `nickprompt[=type:<text>,enter,esc,skip]`, `nointernet`; `StartupScript.Drives` říká, že běh řídí skript (takový běh se neptá). Ověřeno v Game (Debug i Release) na scratch profilech se `server=http://localhost:5000` (nic neposlouchá, nic se neodešle). 552 testů, 27 nových; pravidlo Esc jsem schválně rozbil a dva spadly.
- ⚠ **Nezkoušeno:** skutečné klávesy, klik myší na tlačítko, skutečný pad, odpojení sítě (jen `nointernet`). Klávesnicový hráč nemá klávesu pro Skip (klávesnice je zachycená): odmítne myší, nebo Esc ve dvou spuštěních.
- Pozor při ověřování: `shot=` je v sekundách od prvního snímku po načtení (~6 s po startu procesu), takže `-WaitSeconds` pod ~16 s zabije hru uprostřed zápisu PNG a nechá 0bajtový soubor.

## 2026-10-03 — #none CLAUDE.md zkrácen a čtyři procesní pravidla (#768 cizí hráči) — desktop, Claude Code (bs3d-ec)

- **Souhlas majitele s přehledem procesu (všechny čtyři body).** Hotovo: **(1)** `CLAUDE.md` z 48 111 na 27 930 B — historie a měření slovo od slova do `docs/build-and-release.md`, `docs/libraries.md`, `docs/repo-conventions.md` (merge `52f8b9f2`; skript tvrdil, že je každý přesunutý odstavec v novém souboru). Sekce, které citují komentáře v kódu („Triangle winding“, „Constraint handle bookkeeping“, „The ball grid“, „Conventions“, pravidlo o mgcb manifestu), zůstaly. **(2)** Nová sekce „How work is run“ v `CLAUDE.md`: „hotovo“ = hráč to pochopí (návod/čip ve stejném issue, vlastní vzhled a zvuk, řádek „Not verified“), strop **15** čekajících verdiktů a verdikty po dávkách, převzetí issue štítkem **`in-progress`** + komentářem (kdo, co, kdy), kontrola CI po každém mergi. **(3)** Štítek `in-progress` vytvořen. **(4)** **#768** — tři až pět cizích hráčů dřív, než přibude další šířka (navržená závora: nové scény a kapitoly čekají na první kolo; opravy, učení a kvalita levelů ne — **majitel potvrdí**).
- **Změřeno 2026-10-03** (zapsáno v `docs/repo-conventions.md`): od 19. 9. 308 issues vzniklo a 287 se zavřelo, 1 564 commitů, 76 otevřených (6 starších než 30 dní), **21 s `shipped-awaiting-verdict`**, `docs/*.md` 3,9 MB, tento deník 7 760 řádků.
- ⚠ Strop je už teď překročen (21 > 15): další změny vzhledu, zvuku a pocitu čekají, dokud fronta neklesne. **Otevřené pro majitele:** ekonomika nábojů power-upů (#765 §5), den narozenin (#764), cena Swapu a Brzdy.
- Kód se nezměnil. `docs/rendering.md` teď u rozdělení renderer/host odkazuje na `docs/libraries.md`.

## 2026-10-03 — #696 je na mainu: rána se lepí do buňky ducha v 97,9 % místo 63,6 % — desktop, Claude Code (bs3d-aa)

- **#696** (`ffbe9f42`, opravy po recenzi `684fc1bf`), `shipped-awaiting-verdict`. Varianta (b) z issue, ale příčina byla větší: **`TryFindFirstHitOnSegment` ztrácel koule** (testoval nejbližší přiblížení proti segmentu, ne dotyk), takže krokovaný let (studny, bedny, nově gravitace) míjel kouli, jejíž povrch se dotkl uvnitř kroku; k tomu dotyk od Bepu chodí o krok později ze špatné koule, duch ignoroval gravitaci (0,03 až 0,06 jednotky, ne 0,004) a spekulativní kontakt ohýbal letící střelu. Rozhoduje teď stejný sweep jako náhled (`LandSweptShots`, `ContactEvents.TryGetStepStart`).
- **Měřeno novým přístrojem `ShotRig` / `ShotAgreementTests`** (skutečný level v simulaci se skutečným handlerem, náhled volaný jako ve hře, čerstvý level na každou ránu): 238 zaměření na šesti levelech, **152 z 239 před, 233 z 238 po**, odražené od bedny 23 z 24 (issue: 3 ze 7), ve skutečné hře 6 z 9 → 8 z 9. `BS3D_SHOT_RIG=<n>` spustí delší měření, `BS3D_SHOT_COST=1` měří cenu náhledu (45 až 74 µs místo 16 až 21). Každou část jsem schválně rozbil a testy spadly.
- ⚠ **Past z Testbedu (`autoshoot` na `Full.json`)**: odmítnutá rána (`no free cell`) zůstala zaklíněná v clusteru a odmítala se každý krok, 184 řádků za sekundu z jedné rány. Odmítnutá rána se vrací na místo doteku a je znovu obyčejné těleso (`ContactEvents.MarkBounced`), řádek jednou za ránu.
- **Recenze nezávislým agentem** našla únik značky `bounced` (rána po dotyku kamene přestala poslouchat, pozdější odmítnutí ji označilo a značka přežila tělo), spící střelu zametanou ze starého záznamu a zastaralé komentáře (#410). Opraveno s testy.
- ⚠ **Pracovní strom na tomhle stroji má 235 souborů s CRLF** (`git ls-files --eol`, `w/crlf`, index je LF): skript, který upravuje soubor, musí zachovat jeho konce řádků, jinak `git diff` ukáže celý soubor. `LevelGen` přepisuje `BigTop.json`, `Levels.json`, `Trapeze.json` s CRLF (obsah stejný): po běhu `git checkout -- <ty tři>`.
- **Nechávám majiteli:** duch **bliká** (`PREVIEW_BLINK_*`), protože býval častěji špatně než dobře; ten důvod je pryč. Bliknutí zůstalo, protože je to vzhled. A cíl 0.3.1: **hráči v0.3.0 s `"online": false`** nový dotaz na přezdívku nedostanou (soubor ten klíč zapisoval všem), a **Pi: `update-ceilings.sh` při vydání** (deset nových levelů Big Top). Číslo verze se rozhodne před vydáním.
- **Nezkoušeno:** pocit z hraní, pad, level se studnami (žádný vydaný nemá), odmítnutí na herním levelu, vítr (rig ho nemá, hra ano; 16 scriptovaných ran ve hře ho mělo a sedělo 8 z 9).

## 2026-10-04 — #739 je na mainu: „tohle jsi ty“ v žebříčcích je magenta; #696 a #542 zavřeny — desktop, Claude Code (6aa39589)

- **Před releasem (majitel chce vydat, tag bude `v0.3.5`, ale až po několika menších issues):** tabulka stropů z mainu proti assetu v0.3.0 = 140 proti 130 tabulím, **10 nových (Big Top), 0 změněných, 0 zmizelých**, takže skóre z v0.3.0 zůstávají na svých tabulích; formát odesílání i `Outbox.json` od v0.3.0 beze změny; živý server `health` = 148 tabulí; na žebříčcích 47 záznamů od RDT a jednoho cizího hráče. Při publikování spustit na Pi `update-ceilings.sh` (stahuje jen z GitHub releasů, dřív nejde). Majitel **#696 otestoval („je to ok“)** a nechal zavřít **#542**.
- **#739** (`739-board-you-colour`): majitel vybral **magentu (240, 110, 230)** ze tří kandidátů nafocených na skutečné desce (fialová 180,140,255 vyšla o stupeň tmavší než šedé řádky, modrá 100,170,255 nejblíž stříbru a diamantové azurové; červená nebyla na výběr, je to výstraha z #687). `BS3DGame.BOARD_YOU`, `BoardYouColor` už nealiasuje `STAR_GOLD`; komentáře v `BoardView`, `OnlineSignal`, `ResultPage`, `HighScoresPage`, `OnlineSession` a `docs/game-shell.md` (nová výjimka z šedé palety), `docs/game-session.md`.
- **Rozhodnuto místo majitele:** glóbus `OnlineSignal` ve stavu Done barvu řádku **následuje** (magenta), ačkoli issue doporučovalo nechat zlatou: na desce je pak všechno „ty“ jednou barvou a zlatá znamená jen pohár. Vratné jednou řádkou.
- **Ověřeno snímky** (lokální BS3D-API s dočasnou DB a vymyšlenými hráči, živý server nedotčen) při 1600×900 a 3840×1600: deska levelu na 1. místě (zlatý pohár, magenta řádek), na 7. z 9, High Scores, stránka výsledku po prohře (`level=Heart lineloss=3`, glóbus). 564 testů.
- **Lokální API pro snímky žebříčků:** `dotnet run --project src/BS3D.Api -- --urls http://127.0.0.1:5000 --Scores:Database=<scratch>/scores.db --Scores:CeilingsDirectory=<scratch>/ceilings --Scores:SubmissionsPerMinutePerAddress=1000 --Scores:SubmissionsPerMinutePerPlayer=1000` (v `ceilings` tabulka z `ScoreSim --ceilings`), hráči nasazení přes `POST v1/scores` s `Authorization: Bearer <token>`, profil `userdata=` se `Settings.json` `{"online":true,"server":"http://127.0.0.1:5000"}` a `Online.json`; páky `board=<n>`, `highscores=1`.
- ⚠ **Past:** spouštěč bez fokusu s `param([string]$Args)` předal hře **nula argumentů** (`$Args` je automatická proměnná PowerShellu) a hra běžela **na majitelově profilu** — jen do hlavního menu; `Progress.json`, `Online.json`, `Outbox.json` a `Settings.json` mají časy z doby před během, nic se neodeslalo, přepsal se jen `Logs\last-run.log`. Spouštěč teď běh bez `userdata=` odmítne.

## 2026-10-04 — #740 je na mainu: zvýraznění v seznamu už neodjede mimo obraz — desktop, Claude Code (6aa39589)

- **Příčina (změřeno):** `ScrollNavEntryIntoView` porovnával polohu položky v souřadnicích obrazovky (`ToGlobal`) s `ScrollViewer.ActualBounds`, které má Myra v souřadnicích widgetu (na výběru levelu `{0,0,355,139}`, grid přitom na obrazovce v `(462,325)`). Na Scene stránce při 1600×900 tak chůze dolů rolovala asi o 90 px dál a chůze nahoru přestala rolovat, když byla položka ještě skrytá nad seznamem. Další dvě díry: příchod na stránku (strom i odrolování se drží mezi návštěvami, kurzor na položce 0 mimo obraz) a kolečko (posune seznam, ne kurzor).
- **Oprava:** `NavViewport` (velikost z `ActualBounds`, roh z `ToGlobal`), `KeepNavFocusInView` jednou za snímek před čtením vstupu (nastavený kurzor se po rozvržení odhalí, seznam posunutý kolečkem vezme kurzor na nejbližší celou viditelnou položku), `MenuPage.NavArrival` a Scene stránka přichází na aktuální scénu. `docs/game-shell.md`.
- **Ověřeno** dočasným skriptem přes skutečné `StepNavFocus` (bez fokusu okna, smazán): 16× dolů, 12× nahoru, seznam na konec, zpět a znovu dovnitř, na `main` i na opravě. Před: zvýraznění mimo obraz ve třech ze čtyř stavů; po: ve všech čtyřech vidět. 564 testů.
- **Neověřeno:** skutečná klávesnice a pad, tažení posuvníku, skutečné kolečko (simulováno nastavením `ScrollPosition`).

## 2026-10-04 — #738 je na mainu: nápověda má Back, Previous a Next v jednom řádku — desktop, Claude Code (6aa39589)

- **#738** (majitelova poznámka): Back vlevo pod začátkem textu, Previous a Next vpravo, tři tlačítka po `WALK_BUTTON_WIDTH` (490) v řádku širokém jako rámec textu; uvolněný řádek dostal text (`BODY_SURROUNDINGS` 820 → 680: tlačítko 115 jednotek, změřeno, plus mezera 26). Kurzor přichází na Next, na poslední stránce na Previous (`HelpPage.NavArrival`, hák z #740), aby hráč nepřistál na odchodu.
- **Snímky** 1600×900 a 3840×1600, stránka 1 a Controls, před/po: deska stejně velká, Controls ukáže o řádek víc (Escape/F11/F12), posouvá se dál, ale méně. `shipped-awaiting-verdict` (vzhled).
- **Neověřeno:** klávesnice a pad v běhu (pořadí Back, Previous, Next je dané pořadím ve stromu).
- **Recenze #740** (nezávislý agent, Myra 1.6.3 dekompilovaná `ilspycmd`): žádná skutečná vada. `Bounds` je od nuly, `ActualBounds` = `Bounds` bez okrajů, takže `ToGlobal(ActualBounds.Location)` je správný roh; `ScrollPosition` je jen `-Content.Left/Top` a `ToGlobal` přepočítá transformaci hned, takže odhalení a krok v jednom snímku se nesečtou; nová cesta za snímek nealokuje. Opraveny jen formulace: posuvník kurzor nevede (tažení je pohyb myši, kurzor zmizí po `NAV_MOUSE_WAKE_PIXELS`), a „nejbližší“ položka je ve skutečnosti první/poslední celá viditelná v pořadí procházení.

## 2026-10-04 — v0.3.5 vydán — desktop, Claude Code (6aa39589)

- **Vydáno na majitelův pokyn:** https://github.com/AntoninPrazsky/BS3D/releases/tag/v0.3.5, anotovaný tag na `0fd280fb` (od zeleného `0d3d1851` se liší jen `Images/releases/v0.3.5.jpg` a `.md`). Workflow Release prošel; zip 417,2 MB (397,9 MB u zkoušky #714), rozbalený 493 MB.
- **Snímek:** louka, Pinwheel (majitelova volba), build orazítkovaný v0.3.5, 3840×1600 v okně přes spouštěč bez fokusu, `seed=917045606 sweep=0:40:0 quality=ultra`, scratch profil s `tutorial:false`. **Poznámky:** `Images/releases/v0.3.5.md` s rámečkem pro nové hráče (přezdívka při prvním spuštění) a pro hráče starších verzí (jak zapnout žebříčky v Settings → ONLINE: majitelovo rozhodnutí místo změny kódu) a „What's new“ (Big Top, #763, #716, #739, #696, #710 a menší).
- **Pi:** `update-ceilings.sh` spuštěn přes SSH (`rdt`, sudo bez hesla jen na ty skripty) hned po publikování: „Added BS3D-v0.3.5-ceilings.json“, služba restartována, `health` 158 tabulí (148 + 10 Big Top). Deska `Tightrope.json?hash=f5b77eae580cd35e&rules=1` odpovídá 200.
- **Ověřeno jako hráč (#714, první ostré publikování nového rozložení):** `gh release download`, `SHA256SUMS` OK, zip = `BS3D.exe` + `Content`, `Levels`, `Music`, `Sfx`; spuštěno na scratch profilu: sada shaderů `3fb6dca4` jako lokálně, Tightrope se načte s klíčem desky z tabulky, About ukazuje v rohu „v0.3.5“. Dialog SmartScreen jen majitel.

## 2026-10-04 — verdikty majitele (13 zavřeno), #769, #770, #704 (Track pryč), #764, #702 — desktop, Claude Code (6edf655a)

- **Fronta verdiktů 18 → 4 otevřené** (zbývá #257 Šapitó, #213 Swap, #520 a #378 na pad) + nově #769, #770, #702. Zavřeno s jeho OK: #712, #729, #727, #701, #721, #700, #719, #697, #698, #688, #488, #713 (šest X4000 zůstává zdokumentovaných, žádná CI hlídka) a #764 (hotovo). Nové issues z jeho poznámek: #769, #770, #771 (obrázky do Help, až později).
- **Otevřené otázky na něj:** #699 — „po dohrání levelu se to už nebude zobrazovat (přestože pořád půjde přeskočit)“: čtení „nápověda u průletu kapitoly jen do prvního dohrání úvodního levelu“, čeká na potvrzení, nic nepostaveno. #692: koule uříznutého patra se počítají jako **zničené**; ochrana horních pater až po měření. #765 rozhodne později. #704: po skladbě další, hrát všechny kapitoly (čteno včetně Unlock all).
- **#769 (merge `e9386660`):** věta o soukromí byla uříznutá za „Remove“ (9 × `LineHeight`, ale rozteč je větší a Myra počítá horní okraj do výšky → 8 řádků) a FontStashSharp láme i za `.` (`RichText.LayoutBuilder`: `IsWhiteSpace(c) || c == '.'`) → `scores.winphonew.` / `eu`. `SpaceWrap` (testy `SpaceWrapTests`) láme jen na mezerách, výška poznámky se měří z nejdelšího textu. ⚠ **`Scaled(0)` je 1 px** (`Math.Max(1, …)`), takže `ScaledThickness(0, …)` dává bokům pixel a label 473 px kreslí text do 471 → měřit na `MenuPage.LaidOutWidth`. Stejně About („MonoGame 3.8.5“).
- **#770 (merge `1ac24457`):** stránky Settings široké jako řada záložek, sloupec popisků `ProportionType.Fill`, tlačítka u pravého okraje. Nafoceno všech pět záložek, 900p/1080p/1600p, psaní přezdívky.
- **#704 krok (merge `5734620c`):** řádek Track pryč i s `CycleMusicTrack`/`NextMusicTrack`/`SoundingTrack`. Settings nad pauzou dál pouští téma levelu (řádek Music se musí slyšet).
- **#764 (merge `a74f5478`):** duhová paleta na About na `BS3DGame.Birthday` (30. 12., den čepičky); nafoceno s `about=play birthday` a bez.
- **#702 (merge `d004e4cb`):** brum je 100 Hz + harmonické, 89 % energie pod 300 Hz → na reproduktorech monitoru skoro nic. Smyčky přebarvené (HP 300 Hz 4. řádu, LP 3,5 kHz, 25 % suchého), filtry kruhově přes smyčku (`PeriodicBiquad`), `LINE_HUM_LEVEL` 0,03 → 0,04. Loopback proti mainu: 300 Hz–4 kHz **+12,5 dB**, celé pásmo −0,3 dB, šev bez lupnutí. ⚠ **Inscenovaná prohra potřebuje `lasers` i `nofocuspause`** — okno bez fokusu hru pauzne a pauza brum nedrží (první dva záznamy byly ticho).
- **Spouštěč bez fokusu + snímky:** `scratchpad/launch.ps1` (CreateProcess, SW_SHOWMINNOACTIVE), `cap.sh`, `hum.sh` v session 6edf655a; nahrávání `rec.py` ze session d2900955 (`soundcard`). Dekódování ogg: `C:\Users\panrd\AI\sfx\venv` má `soundfile`, systémový python má `scipy`.
- **Dodatek: #699 (merge `3d7db89e`), #770 zavřeno, #704 upřesněno.** Majitel: nápověda se neukáže nad padající animací **rány, která level dohrála** (přeskočit jde dál). `GameplayScreen` předá HUD padání nad rozhodnutým levelem jako neběžící. Nafoceno na zkušebním levelu (56 koulí, `detonate=18`, až po průletu kapitoly, jinak převzetí kamery padání zablokuje). Přeskočit jde dál, uprostřed levelu nápověda svítí. #704: Jukebox hraje **všechnu hudbu ve hře** bez ohledu na odemčení. #770: „vypadá to dobře“.
- **Dodatek: dávka poznámek majitele (4. 10. večer) založena jako #772–#784, jen založit, nic nestavět.** Hudba na Měsíci (#772: **`lunar-idm.ogg` má 18,25 s ticha** uprostřed smyčky, bicí taky; sken všech 140 skladeb: další nejdelší mirage-electronica 5,75 s, magma-doom 5 s), Scene v pořadí kapitol (#773; volné scény: Sea, Forest, Outback, Tropical, Mars, Storm, Polar), konec města v ulicích (#774), Mars (#775), Outback (#776), kameny na Měsíci = ikosaedry ze sopky (#777), oválná jeskyně (#778), ostrov v Dream (#779), střechy v neonovém průletu (#780, záběr `Roofs` existuje, ale je nečitelný), hory na High/Ultra (#781), listí vždy z větví (#782), ulice v průletu výš (#783, nízká priorita), symboly místo slov v přehrávači (#784). K existujícím: #751 (jehličí a listí), #759 a #760 (záběr pod vodou s rybičkami do průletu Sea), #704. Bod „skip na konci levelu“ = #699, hotovo.

## 2026-10-05 — noc bez majitele: #783, #784, #773, #772, #761, #762, #755, #715, #704 Jukebox, #782, #692 změřeno, #750, #743, #400 — desktop, Claude Code (6edf655a)

Majitel večer 4. 10.: „zpracovávej až do rána issues, které můžeš zpracovat bez mého inputu“. Všechno přes větev → `--no-ff` merge → CI zelená; vzhledové změny nesou `shipped-awaiting-verdict`. **Fronta verdiktů je teď 15** (750, 784, 783, 782, 773, 772, 769, 762, 761, 715, 704, 520, 378, 257, 213), tedy na stropu: další vzhledové změny až po verdiktech.

- **#783 (merge `28c106f2`):** záběr ulice v průletu města výš (`CityIntroShots.STREET_HEIGHT` 26, `STREET_PITCH_DEGREES` 36), město i neon nafoceno před/po.
- **#784 (merge `9cd2e9b2`):** symboly místo slov na tlačítkách přehrávače (`Game/Screens/MediaGlyph.cs`: Play, Pause, Next, Previous kreslené řádky `FlatBrush`, `MenuPage.ButtonGlyph`, `BS3DGame.MenuButton(Widget, Action)`).
- **#773 (merge `be39126a`):** obrazovka Scene v pořadí kapitol se jmény kapitol (`ScenePage.OrderByChapters`, `TryReadScene`), volné scény na konci; páka `scenes`.
- **#772 (merge `7b642831`):** `lunar-idm` přestřižena z druhého hlasitého těla renderu (12 taktů, 28,8 s); master vyměněn v `C:\Users\panrd\AI\output\masters-486`, starý zůstal jako `theme-lunar-idm.loop-2026-09-23.wav`; bicí znovu oddělené. **MusicBake odmítá ticho** (`LongestQuiet`/`QuietRefusal`: −35 dBFS déle než 6 s) v `--tracks` i `--shipped`. Neslyšeno.
- **#761 (merge `b7b0f1dd`):** pod hladinou žádný ohňostroj: `Fireworks.Draw(camera, visibility)`, uniform `Visibility` násobí premultiplikovaný výstup, předává se `1 − LensSubmergedAmount`.
- **#762 (merge `c021be81`):** kreslené sklo trychtýře stojí 0,1 nad kolizním kuželem (`ArenaIsland.FUNNEL_GLASS_CLEARANCE`, `FUNNEL_DRAWN_HOLE_RADIUS` pro sklo, okraje, plášť jámy a vodu moře); fyzika beze změny. Naměřená největší penetrace koule do kužele byla 0,025.
- **#755 (merge `4370cc9d`, zavřeno):** útvary jeskyně zapisují hloubku: `Cavern.fx` technika `CavernDepth` (`SV_Depth`, `discard`), `Backdrop.DrawDepth` volaný ze `SceneRenderer.DrawEnvironment`, jen na plném detailu a s čočkou mimo `_formationsClearRadius`, **scissor na box arény**: bez něj +1,37 ms, s ním +0,05 ms.
- **#715 (merge `d258f4ce`):** level ukáže své tutorial karty znovu při každém vstupu; Retry/Restart si pamatuje, co tento pokus už viděl (`BuildLevel(index, retry: true)` jen z `RetryLevel`).
- **#704 Jukebox (merge `b1cbe8a3`):** v hlavním menu „Extras“ místo „Scene“; Jukebox hraje **všechnu hudbu ve hře** po kapitolách (`RecordingPlayer`, `SpectrumAnalyser` + `IBandSource`, `ExtrasPage`, `JukeboxPage`), páky `extras`, `jukebox[=<kapitola>[:play]]`. ⚠ **V noci jsou audio výstupy desktopu vypnuté** (všechny endpointy „Unknown“), XAudio2 nemá zařízení a `DynamicSoundEffectInstance` hází NRE: ošetřeno, ale **přehrávání neslyšeno**.
- **Review nočních merge (merge `24441319`):** #715 pamatoval karty na celé spuštění (teď jen retry), Amphora při rozloučení vracela lekce, které save neměl; #704 rozbitá nahrávka zastavila celý přehrávač (teď se přeskočí) a dekódování běželo paralelně (teď jedno naráz); #755 komentář k poloměru.
- **#782 (merge `ae9f6283`):** listí savany roste z větviček: `AcaciaMesh`/`BaobabMesh` → `Build` bez zařízení, `LeafSprays.HangOnTwigs`. Test `LeafAttachmentTests` **před opravou vystřelil** (58–90 % karet listí mimo dřevo), po ní 0; hlídá i 16bitové indexy. Savana 3,29 → 3,34 ms.
- **#692 změřeno (merge `a8c2f522`, `LevelGen --cuts`):** řez celým patrem bez ochrany: jedna rána vyčistí 88 ze 110 levelů. S chráněnými horními 1/2/3 patry: 0 vyčištění, medián nejlepší rány uvolní 398/355/302 koulí, medián rány 190/153/129. Rozhodnutí je majitelovo.
- **#750 blesk v bouři (merge `448f19ae` a `fbb495de`).** Diagnóza ve hře (dočasná instrumentace, nikdy necommitnutá):
  - **kanál se kreslí pokaždé, ale utopí ho vlastní záře** (buňka je bílá a aditivní kanál přes bílou nic nepřidá; se `FlashGlow` 0 je vidět);
  - **ostrov ho schová** (všechno pod úrovní ostrova je z herní kamery za ostrovem);
  - **polovina úderů je mimo záběr**.

  Oprava:
  - úder se vybírá mezi buňkami, které kamera vidí, 14–28° od osy (mimo dělo a shluk), a **fixuje se při začátku úderu** v paměti osmi slotů;
  - kanál vychází z boku buňky kolmo k pohledu a padá mezerou;
  - šířka v obrazovce: bílé jádro asi 2,5 px při 1080p ve fialovém halu, šest větví z kloubů kanálu;
  - **záře mraku přijde 0,12 s po kanálu**.

  Reference: `C:\Users\panrd\AI\sd\out\750-zimage` a `750-klein`. ⚠ Review po merge našel regresi: výhled úvodu kapitoly (`StormIntroShots.FindStrike`) přepisoval jediný slot a úder v průletu šel jinam. Opraveno osmi sloty, ověřeno `tour scene=storm` na seedech 1–3. ⚠ **Testovat krátkou událost přes `shot=` nejde** (start levelu posune hodiny); pomohlo dočasné přepsání sekund od začátku úderu proměnnou prostředí.
- **#743 (merge `05fa782b`):** pravidla odhalení jsou **brána LevelGenu**. `Design.Payoff` (barvy odměny), `LevelGates.RevealFaults`:
  - odměna se bez těla nezřítí;
  - žádnou kouli odměny nejde na startu trefit (přes `ArrivalProbe`, 16 stanic na oběžné dráze).

  Výsledky:
  - Osm odhalení prošlo, kromě **kýlu lodi (Ship, 7 koulí zespodu)**: nádrž je dole otevřená záměrně, proto `PayoffInSight`.
  - **Viděno vystřelit:** srdce Onionu dočasně deklarované → 161 padá, 10 trefitelných, LevelGen exit 1.
  - ⚠ **Záplava prázdných buněk (jako v `ClearProbe`) je na „uzavřeno“ špatně:** krystal Sparku se zužuje o buňku na patro a záplava proklouzne mezi patry, kudy žádná koule neprojde. Hlásila 46 ze 46.
  - Body 3 (přenést vzor do dalších kapitol) a „speciální koule“ jsou majitelovy.
- **#400, sedmý průchod: čisté.** 66 nálezů hygieny snímku, žádný skutečný. Kontrola gradientů v `[branch]` byla viděna vystřelit na podstrčeném souboru. DocDrift: 21 kandidátů, žádný zastaralý. Další průchod od `448f19ae`.
- ⚠ **LevelGen na Windows zapisuje CRLF**, takže po běhu jsou všechny levely „modified“. Obsah je stejný (`git diff --numstat Game/Levels/` je prázdné) a vrací se `git checkout -- Game/Levels/` (ne clean, ne reset).
- **Dodatek (ráno 5. 10.): #743 opraveno po review, #759 + #760 hotovo, fronta verdiktů 17.**
  - **#743 review (merge `870b9a9c`).** Barvami definovaná odměna nestačila: tělo v barvě odměny, které se dotýká odměny nebo skla, s tělem nespadne, takže brána ho pustila. Review to změřilo: Chest 64 z 64, Mango 75 z 85, Spark 22 z 25 přebarvení prošlo. `Design.Payoff` je teď **predikát buňky** (z part funkce designu), emitor předá bráně masku a brána odmítne i kouli těla v barvě odměny. Viděno vystřelit: špička Sparku obarvená žlutě dala 24 koulí a exit 1. Ship: 7 trefitelných = pět kýlu plus příď a záď paluby.
  - **#759 + #760 (merge `179abc3c`):** v průletu Sea nahrazuje „swell“ záběr **pod vodou**. Kamera krouží pod ostrovem a dívá se nahoru na spodek, kužel trychtýře a shluk přes sklo.
    - **Hejno 110 rybiček:** `FishSchool` + `Fish.fx` + `FishMesh` (75 trojúhelníků, `CullNone`, jeden draw, pohyb ve vertex shaderu).
    - **Paprsky slunce:** `SeaLightShafts` + `LightShafts.fx` (40 aditivních quadů).
    - Obojí jen s kamerou pod hladinou; náklad nulový (2,80 ms s hejnem i bez).
    - Reference `C:\Users\panrd\AI\sd\out\760-*` (obě modely).
    - Nafoceno: `tour scene=sea` seed 1–3 před/po a padající animace pod vodu (rybičky u špičky trychtýře).
    - ⚠ **Pohled dolů pod vodou je plochá světlá pláň** (spodní půlka kopule přes jednotný zákal, žádné dno), proto se záběr dívá nahoru.
    - **Review po merge (merge `87d505a2`):** paprsky se kreslily před ostrovem, takže je kámen přemaloval; teď jdou v `DrawOverlays`. Útlum u kamery byl po vrcholech quadu s rohy jen na koncích; teď je po pixelech. Brána `LensSubmergedAmount > 0` se otevírá 0,5 *nad* hladinou a hladina nezapisuje hloubku, takže rybičky prosvítaly vodou (chyba #761); teď kreslit jen pod každým údolím vlny.
    - Rybičky jsou vidět i v padající animaci pod vodu (snímek z `detonate=17` na testovacím mořském levelu).
    - ⚠ **Ukazatel FPS na snímcích s `shot=` po sekundě lže:** ukazoval 42–48, měření ve Testbedu 2,8 ms; PNG ukládání průměr srazí.
  - **Stránka verdiktů:** https://claude.ai/artifact/6CtkPdXEenQk1GLK22Zhtr (Noční verdikty 5. 10.; patří účtu, pod kterým vznikla).

## 2026-10-05 — verdikty majitele ráno: 11 zavřeno, rybky, Jukebox bez ambience, řez po patrech (#692) — desktop, Claude Code (6edf655a)

- **Zavřeno jako OK:** #750 (napsal „758: OK“, ale #758 se nestavělo, čteno jako #750, řečeno na issue), #784, #773, #761, #715, #782, #772, #783, #759, #760, #704.
- **#759 + #760 (merge `3b801672`):** některé ryby „plavou pozadu“. Každá se pohybuje hlavou napřed, ale vlna těla běžela `sin(t − x)`, tedy k hlavě; teď `sin(t + x)`, od hlavy k ocasu.
- **#783:** OK; jeho poznámka (detailní záběr na vysílače na střechách, kroužit, spíš seshora) patří do #780. To rozšířeno na obě města a přejmenováno.
- **#704 (merge `a6082ed1`):** na obrazovce Jukebox musí být slyšet jen hudba. Ambience scény se tam ztlumí za 0,5 s (`ProceduralAmbience.Duck`), jednorázové zvuky počasí (hrom) se zahodí i s čekajícím.
  - Loopback (zvukový výstup tentokrát fungoval): menu −43,4 dBFS ambience, Jukebox digitální ticho, Jukebox hraje −17,9 dBFS. Tím je zavřená i noční mezera „přehrávání neslyšeno“.
- **#692 (merge `b1cfea1c`):** řez bere **celé souvislé patro** zasažené koule (zničené), pak padá, co nevisí. **Horní 4 patra jsou chráněná:** zaměřovač a paprsek blikají jako při míření moc vysoko a výstřel neodejde (`IsCutProtected`, `_cutterRefused`, `AimStrain`).
  - `--cuts` se 4 patry: 0 dohrání jednou ranou, nejlepší rána 240, běžná 105 (typický level asi 500 koulí).
  - Ve hře: Column zničeno 24 a spadlo 45; na krátkém levelu řez míří do horních pater a nevyletí (30 koulí zůstává, normální výstřel 30 → 29).
  - `LatticeSoftnessTests` potřeboval uvolnit jednu kotvu, proto zůstal interní `DestroyBall` (`InternalsVisibleTo BS3D.Tests`).
  - Otevřené: tutorial karta řezu (#705) a jestli žebříčky od The Tower začít znovu (`RulesVersion`), rozhodne majitel.
- **#743:** majitel myslí speciální koule v mapě, ale jeskyně (51–60) žádné nemá. Speciální koule jsou jen v Eruption (bomby, zapy), Big Top (broky) a Mirage (kameny, sklo). Otázka položena na issue.
- **Dodatek (dopoledne 5. 10.): #780 a review #692.**
  - **#780 (merge `f00323d0`):** poslední záběr průletu obou měst krouží 85° kolem jedné vybavené střechy, zblízka a seshora.
    - `CityRooftops.Showcase` hodnotí střechy: stožár 4, talíř 2, 5G 1,5, klimatizace 0,5.
    - Bere jen střechy ve 2–6 blocích, které převyšují ostatní v okolí 26 jednotek; jinak záložní průlet nad ulicí.
    - První verze bez podmínky převýšení koukala na polovině seedů do kaňonu na stěny.
    - Nafoceno: seedy 1–3 v obou městech. Před je ze stejného seedu na buildu před merge (dočasný worktree).
  - **Review #692 (merge po `b1cfea1c`):**
    - Zákaz chráněných pater se četl z náhledu o snímek starého, takže R + klik v jednom snímku vystřelil řez do horního patra. Teď si spoušť zaměření přepočítá.
    - Zbloudilý let do chráněného patra bere jen jednu kouli (`DestroyBall`).
    - Bomba v patře vybuchne až po vyhodnocení toho, co spadlo.
  - Fronta verdiktů: 8. Stránka https://claude.ai/artifact/1iHNpAZWYT4AVYaAvbPnr9 (nový účet).

## 2026-10-05 — #785 #786 port na Raspberry Pi 5: kostra GamePi je na mainu, issues #785–#793 — Pi (BS3DServer), Claude Code (296408e3)

- **Rozhodnutí majitele (#785):**
  - Hra poběží na Raspberry Pi 5 8GB (Linux ARM64).
  - Nový grafický stupeň **Potato**. V OpenGL buildu je jediný a nejde změnit.
  - V menu u verze bude „ARM64“.
  - Potato smí kreslit pod nativním rozlišením (výjimka z #298), cílem je ale 1920×1080 na 40–50 FPS.
  - Port zůstává **v tomhle repu**, žádný fork.
  - Potato dostane **vlastní sadu GL shaderů**. 43 efektů SM 5.0 se nepřevádí a `#if OPENGL` se nevrací.
- **#786 (merge `1a5cb00d`):**
  - `GamePi/GamePi.csproj` (net10.0, DesktopGL) překládá všechny zdroje z `Game/`.
  - Nahrazuje jen tři soubory z `Game/Platform`: `DisplayRefresh` přes SDL, `WindowIcon` jako no-op a nový `CrashDialog` jako SDL message box.
  - Ve sdíleném kódu: `RunLog` volá `CrashDialog`; `FrameLimiter`, `LoadingIndicator` a `InternetCheck` jsou za `OperatingSystem.IsWindows()`.
  - `Game.csproj` se nezměnil.
  - Hra na Pi doběhne k prvnímu `Load<Effect>` (`Shaders/InstancedModel`) a skončí crash reportem a SDL dialogem.
- **Změřeno na Pi:**
  - MonoGame DesktopGL 3.8.5 běží nativně: GL 3.1, HiDef, max textura 4096, max MSAA 4×.
  - Proxy scéna 1080p: 4,5–5,5 ms, s MSAA 4× 11–12 ms, přes RGBA16F 9 ms.
  - Bepu headless: medián 1,2 ms na krok, nejhorší Girandole 4,6 ms.
  - `Sleep(1)` trvá 1,06 ms. Logické testy prošly (588).
  - Podrobnosti jsou v #785.
- ⚠ **`bad-echo-mgcb` 3.8.2.1 na ARM64 nefunguje:** `libFreeImage.so` existuje jen pro x64. GamePi proto pinuje oficiální `dotnet-mgcb` 3.8.5, ten na Pi funguje.
- ⚠ **GL shadery nejde zkompilovat na Pi:** mgfxc potřebuje D3D kompilátor z Windows. Musí je kompilovat CI na windows-latest nebo desktop (#789).
- **Issues se štítkem `pi-port`:**
  - #785 je souhrnné.
  - #786–#793 jsou kostra, CI, Potato tier, shadery a renderer, zvuk, vstup, pacing a ikona, release.
- **Dál:** #787, GamePi v CI na ARM64 runneru.

## 2026-10-05 — #787 #788 #789 Potato na Pi: CI na ARM64, zamčený stupeň, vlastní GL shadery, 64 FPS na Girandole — Pi (BS3DServer), Claude Code (296408e3)

- **#787 (merge `8b9dadbd`):**
  - `build.yml` má druhý job `gamepi` na `ubuntu-24.04-arm`: GamePi.sln v Debug i Release, logické testy na ARM64 a kontrolu publish složky.
  - `GamePi.csproj` má CA1416 jako chybu. Viděno vystřelit: bez podmínky v `InternetCheck` build spadne.
- **#788 (merge `ff424e20`):**
  - `QualityLevel.Potato` je přidaný na konec výčtu. `QualityLevels.DropsSceneDetail` nahrazuje čtyři `== Low`.
  - Zámek je platformní šev `Platform/QualityLock`: na Windows null, v GamePi Potato.
  - V menu je `BuildVersion.DisplayName` s „ARM64“; `Name` zůstává pro server.
  - Na Pi viděno: „Potato (locked)“, „Off (locked)“, „dev-… ARM64“.
- **#789 (merge `f581b102`), Potato render path:**
  - `GamePi/Shaders` má 8 efektů SM 3.0: PotatoModel, PotatoSky a porty ShotTrail, LaserGrid, Blast, Wormhole, Fireworks a Confetti.
  - Kompilují se jen na Windows (`compile.ps1`, `gamepi-shaders.yml`) a **commitují se** do `Compiled/`; na Pi je stáhne `fetch.sh`. Kompilace je deterministická.
  - Hostitel je `BS3DGame.Potato.cs`: bez SceneRenderer, PostProcessPipeline, Sky.fx a mraků. `InstancedModelRenderer.DrawPotato` se zapne podle efektu.
- **Změřeno na Pi (1920×1080, bez stropu):**
  - Girandole: 30,8 → 15,5 ms (64 FPS). Příspěvky: LodBias 2 (25,0 ms), řazení odpředu dozadu a rozpad jako zmenšení místo `clip()` ditheru.
  - Pennant: 19,7 → 17,0 ms.
  - Profil přes `dotnet-trace` (uživatelsky, bez sudo): Pennant čeká ~79 % v Present, tedy na GPU. Girandole je fyzika ~14 ms plus GPU.
- ⚠ **MojoShader a mgfxc se neshodnou, kde jsou dvě dynamicky indexovaná uniform pole.** Fireworks a Blast četly cizí sloty, tiše a bez varování. Teď je jedno pole na shader a `compile.ps1` dvě odmítne.
- ⚠ **Obsahová úloha si sama najde každý `.mgcb`** a na Linuxu kompilace efektu chce Wine (MGFXC0001). GamePi proto má `EnableMGCBItems=false`.
- ⚠ **`BuildStamp` hledal `Content\Shaders` s backslashem**, na Linuxu to nefungovalo. Opraveno.
- **Revize (workflow, 4 pohledy a skeptik na každý nález):** pády 0; 7 nálezů opraveno (pole, světlo přidané na sklo, šev ostrova, žolík, koule bez barvy, expozice oblohy).
- **Majitel:** zvětšování a zmenšování místo ditheru chce i na Windows, odpojené koule zůstanou s ditherem (#794).
- **Nové issues:** #794, #795 (tmavé scény bez světel scény, sopka), #796 (řádky Aberration a Film grain), #797 (Bubble kreslí dvakrát).
- **#788 a #789 čekají na verdikt.** Dál: #790 zvuk, #791 myš.

## 2026-10-05 — Windows ověření Pi práce (#788, #789) a `compile.ps1` na PowerShellu 5.1 — notebook, Claude Code (github-cf)

- **Proč:** Pi session nemá Windows, takže sdílený kód po #788/#789 na Windows nikdo nespustil (CI ho jen staví a spouští testy). Notebook (Ryzen 7 5700U) po 6 dnech: `main` f581b102 byl o 271 commitů dál.
- **Na Windows čisté (Release, `main` f581b102):** `Game.sln` 0 varování 0 chyb, `BS3D.Tests` 597/597, `level=Girandole shot=12` se načte a běží (1204 koulí, 5093 vazeb, žádná výjimka, snímek v pořádku). Adaptivní sonda na APU 1080p High → Medium → Low, Low 55–58 FPS. Diff sdílených souborů přečtený: na Windows cestě jen bool `_potato` a null kontroly, `QualityLock.Tier` je null.
- **Opraveno na mainu (`c3069c46`):** `GamePi/Shaders/compile.ps1` padal na Windows PowerShellu 5.1 (stock Windows nemá `pwsh`): `[Text.Encoding]::Latin1` je .NET 5+. Teď `GetEncoding(28591)`. Kompilace i kontrola jednoho pole pod 5.1 prošly; kontrola nad vadným kompilátem z `0fcc82b5` pod 5.1 odmítla přesně Blast a Fireworks (PotatoModel ne).
- **Změřeno:** lokální kompilace všech osmi GL efektů 3,5 s a sha256 všech osmi `Compiled/*.xnb` **bajt po bajtu stejné** jako commitnuté z CI. Notebook tedy umí shadery pro Pi kompilovat sám.
- **APU: Pi práce #786–#789 Windows cestu nezpomalila.** `history-sweep.ps1 -From cc0b25d1 -To 80daa720 -Points 0 -Levels Girandole,Pennant -Tier low -Seconds 40`, Game Release, 1600×900, `limit off`, dopředný a zpětný průchod, minimum mediánů (obě půlky `shaders set 7b69f635`, `libraries` `c743d65a` → `2f57d10d`; mezi nimi jen Pi práce, 21 souborů): **Girandole 12,33 → 12,36 ms (+0,03), Pennant 13,26 → 13,22 (−0,04)**; průchody se shodují na 0,03–0,05 ms až na první průchod Girandole (12,50 / 12,90, drift). Jen Low a dva levely — Low je tier, kde se počet kreslicích volání a cena na kouli ukáže nejvíc. Stroj klidný (Teams v klidu, build servery vypnuté). #790 (`Game/Audio`) a novější merge měřením nepokryté.
- **Nechané Pi session:** dvojitý `<summary>` u `InstancedModelRenderer.PotatoBallColor` (ten první patří k `DrawPotato`).
- **Otevřené otázky:** #794 (grow/shrink na Windows) neberu, dokud neodpoví Pi session a majitel nepotvrdí, zda se křížové přechody barev mění taky; issue to sama označuje. (Odpověď: viz další záznam.)

## 2026-10-05 — #794 duch při míření je velikost, dither zůstává pro všechno ostatní — notebook, Claude Code (github-cf)

- **Rozhodnutí majitele (předala Pi session):** „ten efekt zmenšování bych chtěl jenom při tom míření. Odpojené kuličky by měly používat ten efekt ditheringu – pokud jde nějak snížit jeho náročnost – např. snížením jeho rozlišení, tak bych do toho šel.“ Čteno doslova: **jen duch** je velikost, mrtvá váha **i barevné přechody** (Transmute, žolík, infekce, rozmrazení, zámek, dutá koule) zůstávají dither. Na desktopu se mění jen duch.
- **Jak:**
  - Duch je `ModelInstance.Dissolve` pod −1 (`GhostDissolve(scale) = −(1+scale)`). Clip každého z 21 stylů (`-d - noise`) nechá u hodnoty pod −1 všechny pixely, takže **žádný pixel shader se nemění**; `PatternVS` v `BallCommon.fxh` kreslí ducha zmenšeného, jako přičtený rozdíl, takže ostatní koule jsou bit po bitu jako dřív.
  - `GameplayScreen`: `PREVIEW_SCALE` 0,5 ± `PREVIEW_BLINK_DEPTH` 0,22 (vzhled z Pi, který majitel viděl a nechal).
  - **Potato:** `PotatoBallVS` zmenšuje jen ducha; nová technika `PotatoBallDither` (hash desktopu, `VPOS`, `DissolvePixelSize`) pro dithrované koule. `InstancedModelRenderer.OrderForPotato` dá čisté koule (a ducha) první, od nejbližší, dithrované za ně; `DrawPotato` je kreslí jako dva běhy jednoho instance bufferu (druhý přes `VertexBufferBinding` s posunem, na GL 3.1 není base-instance). `clip()` je jen ve druhém běhu, takže ranní zisk z early-Z zůstává.
- **Ověřeno:** 617/617 testů (20 nových, `GhostDissolveTests`, viděné selhat při počítání ducha jako ditheru), čtyři solutiony 0 varování, CI větve zelená (Build i `gamepi-shaders.yml`, tedy `PotatoModel.xnb` z notebooku je bajtově stejné jako z CI). Na Windows ve hře i v GamePi. **Na Pi změřila Pi session A/B proti `15f07c79`:** Girandole 22,15/21,52 → 21,29/22,48 ms, Pennant 22,15/21,96 → 22,03/22,06, detonace na Sill beze změny (vrcholy 30,26 vs 30,32 a 36,85 vs 30,99) — vše v šumu; ⚠ absolutní hodnoty ~22 ms jsou z prostředí (VNC klient, plný `/tmp`), ne z větve.
- **GamePi se dá postavit a spustit i na Windows** (`dotnet build GamePi\GamePi.csproj`, DesktopGL): Potato zamčené, Girandole na APU 1080p ~7 ms. Potato obraz jde posoudit bez Pi (jen ne jeho cena na V3D).
- **Buňka ditheru zůstává 1 px** (`DISSOLVE_DISPLAY_PIXELS`, sdílená s desktopem): majitel hrubší chtěl jen kvůli ceně a ta se na Pi neukázala.
- **Neověřeno:** mrtvá váha na Windows (koule z Sill spadly do odtoku), kolik koulí šlo na Pi dithrovaným během, vzhled ducha u majitele — `shipped-awaiting-verdict`.

## 2026-10-05 — dělba pi-port issues, #796 řádky Aberration a Film grain na Potato — notebook, Claude Code (github-cf)

- **Dělba s Pi session:** já **#793** (linux-arm64 balík v `release.yml`) a **#796**; ona #792, #795, #797 a měření na Pi. Do `GamePi/Platform`, `Game/Audio` a `BS3DGame.Potato.cs` nesahám.
- **#796 na mainu:** na Potato cestě čtou řádky Aberration a Film grain `Off (tier)` (slovo, které už má Motion blur) a jejich přepínače se vrátí hned na začátku (`BS3DGame.HasLensLooks`, `!PotatoPath`), takže klik nic nedělá a uložená volba hráče zůstane. Vzor: zamčené řádky Quality (#788). Windows beze změny. Viděno na snímku Settings → DISPLAY: GamePi (přes DesktopGL na Windows) `Off (tier)` ×3, Windows hra `On` ×3. 617/617 testů.
- **#793 (větev `793-release-linux-arm64`, rehearsal prošel):** `release.yml` má tři joby: `release` (Windows zip + stropy, brány; nově brána, že commitnuté GL efekty GamePi jsou to, co jejich zdroje kompilují), `release-linux-arm64` (GamePi `-r linux-arm64 --self-contained`, ne single-file, na nativním ARM64 runneru, tar.gz) a `publish` (čeká na oba, jeden `SHA256SUMS` přes zip, tar.gz i stropy, `sha256sum -c`, release jen na tag). Ruční běh nic nepublikuje a nechá oba balíky a součty jako artefakty.
  - **Rehearsal 37314115805 (`dev-bac893b`), změřeno a ověřeno ze stažených artefaktů:** Linux složka 635 souborů, 471 MB, tar.gz 386 MB, `BS3D` v archivu `-rwxr-xr-x`, jedna horní složka; Windows zip 396 MB beze změny; `sha256sum -c` všech tří souborů OK.
  - **Nevyzkoušeno:** poslední krok `gh release create` (na ručním běhu se přeskakuje); jeho skript jsem spustil lokálně proti falešnému `gh` (čtyři soubory, poznámky, `--prerelease` u suffixového tagu). Tarball ověřila Pi session z čisté složky (`env -i`, minimální prostředí): součet OK, menu s `dev-bac893b ARM64`, level hraje (~43 FPS pod VNC), `ldd` nic nehlásí, libicu76 a libssl3t64 jsou v desktopovém obrazu Raspberry Pi OS (Lite nezkoušeno). **Nález mimo release:** bez existujícího `~/.local/share` vrátí `LocalApplicationData` na Linuxu prázdný řetězec a `UserData.Resolve` spadne na cestu apphostu (soubor), takže se nezapíše log ani save — Pi session to opraví v `Game/UserData.cs`.
  - **CLAUDE.md** popisuje release jen Windows zipem; věta o `release.yml` tam chce doplnit klauzuli (nesahal jsem na něj).

## 2026-10-05 — pad v ruce majitele: #378 #520 dvě nové vibrace a RT skip, #188 motory ve spouštích přes Windows.Gaming.Input — desktop, Claude Code (ce223402)

- **Majitel poprvé hrál s padem (Xbox One S, model 1708, USB).** Verdikt: výstřel a dopad cítit, menu i hra na padu fungují, krok stropu „trochu moc výrazný", chyběla vibrace na dorazu míření a na „Cluster reached the line", analogové přiblížení LT „hezké", ale má reagovat hned. A **RT nepřeskakoval animaci kamery, přestože nápověda „to skip" na padu kreslí právě RT** — přeskočit šlo jen A.
- **Merge `b345b481`** (#378, #520): strop 0,6/0,25/0,5 s → 0,45/0,18/0,4 s; prohra na čáře = nejtěžší puls (dva kicky v `BeginLineLoss`); doraz = jeden knock při `Cannon.ElevationStrain` = 1, znovu nabitý až po uvolnění; RT přeskakuje převzetí kamery a spotřebuje stisk jako výstřel; `PreciseAim.TRIGGER_REST` 0,08 → 0.
- **⚠ MonoGame 3.8.5 na WindowsDX nemá u spouští žádnou mrtvou zónu** (`GamePad.XInput.cs`: surový byte / 255), takže 0,08 byla mrtvá zóna navíc. Puštěná spoušť čte přesně 0.
- **#188 zodpovězeno na jeho padu:** WinForms sonda (`net10.0-windows10.0.19041.0`, nebalíčkované exe) — `Gamepad.Gamepads` je v Win32 procesu na startu prázdný a `GamepadAdded` přijde do ~30 ms; majitel cítil všechny čtyři motory včetně spouští. **Merge `ce8159bd`:** `Game/Platform/PadMotors.cs` (GamePi má prázdný jmenovec), mixer posílá všechny čtyři motory přes WGI, když pad odpoví, jinak tělo přes XInput — nikdy obojí v jednom snímku. RT: plný krátký zlom při výstřelu, suché cvaknutí při odmítnutém. LT: rohatka 0,75° při přiblížení, aretace na konci dráhy.
- **⚠ `TargetPlatformVersion` samotné nefunguje** — SDK ho z TFM přepíše (vrátilo `7.0`). TFM hry je teď `net10.0-windows10.0.19041.0` a `OutputPath` drží staré složky `bin\net10.0-windows` / `bin\Release\net10.0-windows`, takže skilly ani `play-latest.ps1` se nemění. Exe 116,2 → 141,6 MB, zip +6,3 MB (změřeno lokálním publishem).
- **⚠ LevelGen na Windows přepíše všech 141 levelů na CRLF** — obsah stejný (`git diff --ignore-cr-at-eol` prázdný); vráceno `git checkout -- Game/Levels`.
- **Neověřeno rukou:** zjemněný strop, puls prohry, knock dorazu, RT skip, čtyři efekty spouští, plynulé LT od nuly; release a hvězda z prvního kola. Bluetooth ani Series pad nezkoušeny. #188, #378 a #520 nesou `shipped-awaiting-verdict` + `needs-gamepad`.
- **Doplněk téhož dne — ikony a revize.** Majitel chtěl zkontrolovat ikony padu v UI. Vykreslil jsem PromptFont a podíval se: **čip Cut ukazoval PlayStation „R1" (U+21B1), ne RB (U+2199)** — doc tvrdil „ověřeno v cmapu", jenže cmap říká jen, že kód existuje, ne co kreslí. A **jedna obecná páčka bez písmene (U+21CD) stála za pravou i levou**; teď U+21BB / U+21C4 / U+21C5. Nová testovací páka **`pad`** (`Tutorial.PinDevice`) — bez ní běh, který nikdo nehraje, čte jako myš (merge `20050fc6`). **Revize tří merge našla dvě skutečné vady** v rohatce LT: četla pózu hlavně (pružina gumy po dorazu a chůze tikaly bez ruky) a tikala padu na stole pod myší s pravým tlačítkem; teď čte `Cannon.ElevationAim` (nové) a chce LT padu. Plus `TRIGGER_REST` 3/255 (opotřebená spoušť by držela defocus), znovuzápis stavu po návratu fokusu, zámek v `PadMotors` (merge `5b941aea`). Neřešeno: dva pady, cena `Vibration` setteru za snímek.

## 2026-10-05 — #790 #791 #792 #797 #798 na Pi: zvuk přes OpenAL, myš, ikona, Bubble jedna stěna, složka dat, zkouška tarballu #793 — Pi (BS3DServer), Claude Code (296408e3)

- **#790, zvuk (merge `c6e0531e` a `92b4dc58`):**
  - ⚠ **OpenAL Soft umístí hlas jen tehdy, když drží zdroj, a ten dostane až při `Play`.** `Apply3D` před `Play` se na GL nepoužije a výstřely hrály ze středu. Platformní šev `Platform/AudioBackend.PlacesAfterPlay`: v GamePi `ProceduralAudio.Speak` umístí hlas až po `Play`, na Windows se pořadí nemění.
  - ⚠ **Recyklovaný zdroj si nese `AL_PITCH` posledního hlasu.** Nový `DynamicSoundEffectInstance` na stejném zdroji ho zdědí. Změřeno: one-shot s pitch −0,7 nechal na zdroji 256 hodnotu 0,616 a nový stream ji převzal. Po `Pitch = 0` je 1,000. Opraveno u všech tří streamů (`GameMusic`, `ProceduralJukebox`, `RecordingPlayer`).
  - Majitel hlásil ticho ve hře, ale nahrávka výstupu (`pw-record` z HDMI sinku) zvuk měla: menu −30 dBFS, hudba v levelu −25, výstřely −14. Majitel pak napsal, že zvuky i hudbu slyší. Příčina byla nejspíš mimo kód (HDMI nebo monitor).
  - Čeká na verdikt, jestli zvuk jde ze správné strany.
- **#791, myš:** warp kurzoru pod Xwayland funguje a majitel míření ověřil. Zavřeno.
- **#792, ikona, načítání a tempo snímků (merge `834c38c7`):**
  - `GamePi/Icon.bmp` (128 × 128 z 256px rámce `Icon.ico`) je vložený jako `BS3D.Icon.bmp`. SDL okno si ho vezme samo a `_NET_WM_ICON` čte 128 × 128.
  - Od spuštění po první snímek 6,2 s (na desktopu 4,2).
  - Vsync přes Mesu (`vblank_mode=3`) je horší než limiter: 30 FPS, střídání 8 a 60 ms, odchylka 22 ms. Limiter drží ~45 FPS s odchylkou 1,4 ms, takže zůstává.
  - Zavřeno.
- **#789, obloha (merge `5a2e116a`):** tonemap oblohy po revizi běžel pro každý pixel a stál ~1 ms. Teď běží po vrcholech (gradient je stejně po vrcholech). Pennant 22,0 → 20,8 ms, Girandole 21,5 → 20,8 ms.
- **#793, tarball z čisté složky (pro notebook session):**
  - Součet sedí, menu ukazuje `dev-bac893b ARM64` a level hraje.
  - Spuštěno přes `env -i` a `ldd` nic nehlásí.
  - libicu76 a libssl3t64 jsou v desktopovém obrazu (datum dpkg souborů je den obrazu a instalace SDK přidala jen balíky dotnet-*).
- **#798, složka dat (merge `678b4a5c`):**
  - ⚠ **`GetFolderPath(LocalApplicationData)` vrátí na Linuxu prázdný řetězec, když `~/.local/share` ještě neexistuje.** Záložní cesta `AppContext.BaseDirectory/BS3D` je na Linuxu soubor apphostu `BS3D`, takže log ani save nešly zapsat.
  - Teď se cesta ptá s `DoNotVerify`. S prázdným HOME vznikne `~/.local/share/BS3D/Logs`. Zavřeno.
- **#797, Bubble jedna stěna (merge `5114210b`):**
  - Na Potato byly obě stěny bubliny neprůhledné, takže se každá koule kreslila dvakrát. `DrawShell` a `DrawHollow` teď vzdálenou stěnu přeskočí, když `InstancedModelRenderer.Potato`.
  - **Pennant je Bubble level**, tedy první level hráče: 22,2 → 18,7 ms (45 → 53 FPS). Girandole (porcelán, kontrola) beze změny.
  - Snímek Pennantu ve stejné sekundě z obou buildů ukazuje stejný shluk.
- ⚠ **Vlastní chyba:** při mergi #792 jsem poslal `git merge … | tail` a pak `&& git push --delete`. Konflikt neukončil řetěz (návratový kód měl `tail`), takže se větev na remote smazala nemergnutá. Hned jsem ji obnovil z lokální větve (`0b52eba8`), vyřešil konflikt v docs (věta „Six files“ s PadMotors z #188) a mergnul. Při merge nepoužívat rouru.
- **Absolutní čísla dnes kolísají o 3–5 ms** podle prostředí (VNC). Srovnávat jen A/B ve stejném sezení.
- **Dál #795 (tmavé scény):** na Pi jsou Caldera (sopka) a Cabinet (neon) skoro černé, Balloon (savana) a BigTop (cirkus) vypadají dobře. Potato nemá `SceneRenderer`, takže ani pozice světel sopky a ohňů. Návrh: dvě směrová difúzní „fill“ světla na scénu v PotatoModel. Desktopové referenční snímky stejných levelů pořídí notebook session.
- **Doplněk — #800 a RT skip.** Majitel: strop pořád moc, rohatka LT „hodně snížit", chce odezvu v menu, úvodní „podpis" při startu s padem, potvrzení připojení a drncání pojezdu. **Merge `be373d97`:** brána rumble je teď jen `IsActive` (menu vibrují), hru ztiší `GamepadRumble.Silence()` v snímku, kdy pauza nebo odchod ze hry; mixer umí vzory pulzů; drncání z `RollTravel`/`SlideTravel`. Strop 0,28/0,1/0,25 s, rohatka 0,15/0,035 s. **RT skip:** majitel hlásil, že nepřeskakuje (a že „asi X"); čtením jsem vadu nenašel, tak přibyla páka **`padrt=<od>:<do>`** (plný RT zapsaný do stavu padu snímku) a log `[skip] …` — skriptovaný RT úvod přeskočil (merge `9f72ca74`). Přeskakuje A (dolní tlačítko), X ne; zůstává ověřit u majitele. **⚠ Moje spuštění hry (`quiet.ps1`, SW_SHOWNOACTIVATE) se aktivovala** — v logu `[pad] rumble through WGI`, což jde jen s `IsActive` — tedy braly majiteli fokus; nespouštět, když hraje (`Get-Process BS3D`). Založeno **#799** (ikony kláves/tlačítek přímo v UI) a **#800**. Fronta verdiktů je na **15** — strop; další změny vzhledu/zvuku/pocitu až po verdiktech.
- **Doplněk — #802, čip RB, #803.** **#802** (merge `8d1a569f`): levá páčka jede dělem rychlostí podle vychýlení od prvního kroku za mrtvou zónou MonoGame (ta páčku přeškáluje od 0 nad 0,24), křivka |x|^1,5; `Cannon.Orbit/Advance` rampuje k velikosti požadavku (`RampTowards`), klávesa ±1 beze změny. **Čip Cut** (merge `711407c4`): ⚠ nebyl to problém jen RB — všechny klávesy/tlačítka čipů seděly ~12 px (4K) pod středem popisku, protože se centrovaly podle řádku dvou různých písem; u nízkého oválku RB je to vidět nejvíc. **Past: uložený box glyfu RB v PromptFontu sahá hluboko pod inkoust**, takže `TextBounds` i PIL `getbbox` lžou o jeho středu; vykreslené pixely mají střed RB přesně na úrovni tlačítek (127,5/200). Oprava: střed inkoustu tlačítka Y (těsný box) na střed velkých písmen popisku ("H"); změřeno RB 1091,5 = C 1091,5. **#803** (merge `cc5b5148`): pořadí za Quarry je teď Grid, Nebula, Big Top, Eruption, Spectrum, Arcade, Mirage — Nebula a Eruption od sebe, po těžké (Quarry, Arcade, Eruption podle LevelGenu) lehčí. Prvních sedm se nehnulo (Swap/Brake/Cut podle pozice 2–4, Colossus zavírá Quarry), ScoreSim 0, změnil se jen `Levels.json`. ⚠ `Designs/BlockNN_*.cs` čísla jsou pořadí vzniku, ne hraní.
- **Doplněk — #804, antialiasing pro Pi (návrh, ne stavba).** Majitel: zuby na Pi jsou největší bolest portu, FXAA nestačí, chce něco původního/předgenerovaného. Návrh je v `Research/pi-antialiasing/README.md` (merge `de9098f0`) se simulací v numpy a dvěma obrázky; issue **#804**. Jádro: **lem každé koule** (edge overdraw — po všem neprůhledném včetně oblohy tenký prstenec přes obrys, alfa 1→0 přes pixel ven, depth test bez zápisu; obrys koule je vzorec ve VS), **ploutve** pro dlouhé hrany (okraj ostrova, odtok, strop) a **rozpočet** (per-ball práce z `BallPS` do VS, případně předstínovaná koule v oktaedrické mapě). **Ověřeno čtením Mesa v3d (neměřeno):** existuje jen 4× MSAA; `v3d_tlb_blit_fast` zapisuje resolved obraz i surový 4× buffer a job odešle při blitu, takže invalidace MSAA hloubky musí být PŘED resolve blitem. **Rozdělení práce:** staví session „Hra port pro RaspberryPi" (notebook github-cf) na větvi `pi-aa-rims` nad `801-render-resolution`, měří Pi session; desktop (bs3d-7d) nestaví.

## 2026-10-05 — #795 světla scény na Potato, #801 rozlišení pro všechny verze, měření AA pro #804 — Pi (BS3DServer), Claude Code (296408e3)

- **Verdikty majitele:** duch (#794), strana zvuku (#790) a lávové koule (#795) jsou v pořádku. Všechny tři zavřené.
- **#795 (merge `80655071`), tmavá sopka a neon na Potato:**
  - Průzkum všech 14 scén na Pi: tmavé jsou jen sopka a neon.
  - `PotatoModel` přebírá desktopové uniformy `SceneLight*`. `SceneLights.Apply` bere null renderer: neon dostane světla z konfigurace, sopka z nové `VolcanoLava` (výpočet proudů vytažený z `VolcanoBackdrop` beze změny; notebook ověřil, že desktop je stejný a na APU nic nestojí).
  - ⚠ **Světla po pixelech stála 13 ms** (Cabinet a Caldera 21 → 34 ms). Po vrcholech jen ~1,5 ms; Girandole bez světel +0,7 ms (jedna větev místo osmi naměřila totéž, takže to nejsou vrcholy).
  - Sopka je hlavně **lávový styl**: `EmissiveStrength` 0, barvu dávají praskliny. Potato teď kreslí kůru a průměrnou záři (podíl 0,3 sedí s desktopem: jas shluku 0,147 proti 0,151).
- **#801 (merge `53962e2c`), řádek Resolution pro všechny verze:**
  - ⚠ **Menší back buffer v bezokrajovém fullscreenu v MonoGame 3.8.5 nejde na žádné platformě.** 3D se proto kreslí do vlastního targetu a roztáhne se; menu, HUD, logo a pohár zůstávají nativní.
  - Nabídka rozlišení (`RenderResolution`) je v poměru stran displeje.
  - Na Pi je **Auto**: nativně, a jeden krok na 1280×720, když se nedrží obnovovací frekvence. To je majitelovo pravidlo; ruční volba ho vypne.
  - ⚠ **V3D zapisoval hloubku offscreen targetu do paměti.** `glInvalidateFramebuffer` (nový šev `TargetDiscard`) srazil cenu roztažení z ~2,9 na ~1 ms.
  - Notebook ověřil desktop: `render=0` beze změny (±0,05 ms), 720p na všech stupních vypadá správně. APU Pennant: Low 18,1 → 10,0 ms, High 57,6 → 30,2 ms.
  - Notebook opravil pád GamePi na Windows (`e937b44d`).
- **AA na Pi, změřeno v 720p:**
  - FXAA na výstupu +3,3–3,8 ms, MSAA 4× +1,8 ms; ani jedno se nevešlo.
  - Majitel pak řekl, že **50 FPS stačí**.
  - **#804 dělá notebook**: lem koulí, tj. prstenec na přesné kružnici obrysu. Na Pi stojí +0,3–0,8 ms v 720p a +0,5–1,2 ms nativně, fotky má majitel.
  - Strop stínování koulí (`ballflat`): −2,4 až −2,5 ms v 720p a −4,3 až −4,8 ms nativně na Girandole a Cabinetu. Pixel koule je největší položka snímku.
  - `V3D_DEBUG=shaderdb`: FS koule, Lit a Textured mají 330–391 instrukcí a 2 vlákna (45–49 temps).
- ⚠ **VNC na Pi stojí 3–5 ms.** Odpolední absolutní čísla byla s připojeným klientem. Bez něj: Pennant nativně 14,4 ms, Girandole 16,3, Cabinet 16,9. Srovnávat jen A/B v jednom sezení.
- ⚠ **Online z Pi nezapnuto.** Lokální build nemá server. Auto mode classifier odmítl jak úpravu majitelova `Settings.json` (řádek `server`), tak testovací kopii API. Majitel ví, který řádek přidat sám.
- **Zlevněný ball shader z #804** (`10009913`: konstanty na draw na CPU, heartbeat ve vrcholu), změřeno na Pi nativně: Girandole 16,6 → 13,6 ms, Cabinet 16,9 → 14,0, Pennant 14,5 → 12,2.
  - S lemem je nativní 1080p pod 15 ms na všech třech levelech, bez VNC.
  - shaderdb: FS koule 391 → 250 instrukcí.
  - Snímky Caldery a Cabinetu jsou stejné; jeden odlehlý běh staré verze dělal šum.
- **Dál:** majitelův verdikt k lemu a k 720p (#801). #804 mergne notebook nad aktuální main.

## 2026-10-05 — #804 AA pro Pi: lem koulí, ploutve hran, konstanty na CPU; Windows ověření #801 — notebook, Claude Code (github-cf)

- **Zadání majitele:** hráč nesmí vidět hranaté pixely, ani rozmazané; měkký obraz nevadí. Návrh (lem, ploutve, rozpočet) napsala desktopová session do `Research/pi-antialiasing`, postavil ho notebook, měřila Pi session.
- **Proč notebook:** GL shadery kompiluje za 3 s a GamePi tu běží přes DesktopGL, takže Potato obraz jde posoudit bez Pi. Cenu na V3D umí změřit jen Pi.
- **Lem koulí (`rims=`, výchozí 1):** po všech koulích druhý průchod stejnými buckety, pásek na kružnici obrysu, alfa po pixelu z přesné kružnice, blend, depth test bez zápisu, bez biasu.
  - ⚠ První řez měl dvě vady, obě vidět až na snímku: barva lemu na okrajové normále je vrchol Fresnelu (bílá linka), a normála otáčená přes pásek dělala tmavou linku. Správně: normála půl pixelu uvnitř obrysu pro obě řady.
  - ⚠ Krajní pixel koule byl přesvícený už bez lemu (pátá mocnina šplhá k 1 v poslední pětině pixelu): `facing` je teď zdola omezený polovinou své změny přes pixel (`fwidth`).
- **Konstanty na CPU:** MonoGame nemá preshader, takže výraz jen z uniformů běží na každém pixelu. Pixel koule 391 → 250 instrukcí, nativně −2,3 až −3,0 ms (čísla Pi session výše). Rovnost obrazu: `Research/pi-port/804/ball-pixel-before-and-after.py` (20 000 náhodných vstupů, 5,5e-14; na pokaženém znaménku selže).
- **Ploutve (`fins=`, zatím výchozí 0):** `EdgeFinMesh` pro ostrov, desku, hlaveň, límec a lafetu; obrys se hledá po hranách ze dvou stěn.
  - ⚠ Ploutve na všech sítích: 142 000 hran a 177 ms načítání; válečky kol (desítky instancí) přes milion vrcholů a +1 ms i na notebooku. Proto jen uvnitř `EdgeFins.Wanted()`.
  - ⚠ Práh „skoro v rovině“ 1,5° sebral ploutve oblým stěnám (ostrov má 512 segmentů, 0,7° na fazetu). Je 0,25°.
  - ⚠ Test pravidla pro záhyb na krychli prošel i s obráceným pravidlem (u pravého úhlu ustupují obě stěny). Rozliší to až tupý hřeben.
- **Windows ověření #801** (větev je na mainu): regrese při `render=0` šest buněk v ±0,05 ms; 720p na Low/Medium/High, pohár, sklo, motion blur, jeskyně, sen, menu, řádek Resolution, F11, alt-tab, pauza v pořádku; APU Low 18,06 → 10,03 ms, High 57,58 → 30,22.
  - Nález a oprava (`e937b44d`): GamePi na Windows padal s `render=720` (`TargetDiscard` volal SDL pod linuxovým jménem).
- ⚠ **Bash heredoc v téhle session padá na apostrofech v delším textu** („unexpected EOF“). Skripty psát přes Write a pak spustit.
- ⚠ **Rebase už pushnuté větve jsem lokálně udělal a vrátil** (`git switch -C` na origin). Větev stojí na commitu, který je v mainu, takže stačí `--no-ff` merge.
- **Ploutve na Pi změřeny (session na Pi, `2adfe52a`): +1,05 až +1,43 ms, nativně i v 720p stejně** - cena za vrcholy, ne za pixely. Shaderdb: binning pouští coordinate shader ploutví (243 instrukcí) pro každý vrchol každého zkolabovaného quadu. Doba do prvního snímku se nehnula (3,9 s). Nativně s lemem i ploutvemi Pennant 13,9, Girandole 15,5, Cabinet 16,0 ms proti 16,1 limiteru, takže **ploutve zůstávají vypnuté** (`POTATO_FIN_PIXELS` = 0).
  - Další krok (nová větev): konkávní hrany vůbec nestavět (konkávní hrana uzavřené sítě není nikdy viditelný obrys) a živé hrany vybírat na CPU do dynamického index bufferu, aby binning viděl stovky hran místo desítek tisíc.
- **`upscale=soft`** (`5e9c8652`): kubický B-spline ve čtyřech bilineárních tapech pro cestu pod nativním rozlišením; bilineární zvětšení dělá z tenké světlé linky korálky na mřížce zdroje. Výchozí je bilineár, cena na Pi se měří.
- **Neověřeno:** cena `upscale=soft` na Pi, švy mezi dvěma sítěmi (zlatý pás odtoku proti kameni a šachtě) a vnitřní tvrdé hrany děla zůstávají schodovité, kola a válečky nemají ploutve, jiný poměr stran než 16:9 u #801.

## 2026-10-05 — poznámky majitele z hraní založeny jako #805–#811 a sedm komentářů (jen založit, nic nestaveno) — desktop, Claude Code (bs3d-a3)

- Majitel poslal osm poznámek se zadáním „založ issues“. Výsledek: **7 nových issues a 7 komentářů**, žádná větev, žádný claim, nic se nespouštělo ani nefotilo; všechno je přečtené z kódu na `main`. Před založením prošly všechny issues (otevřené i zavřené) a žurnál; čerstvé issues se před založením zkontrolovaly ještě jednou.
- **Nová issues:**
  - **#805 (bug):** na About a v Jukeboxu nefunguje na padu ◀ ▶. `AboutPage` nepřepisuje `PageSideways` (výchozí `false`, `MenuPage.cs:172`), host krokuje fokus jen svisle, a tlačítka Play a Next stojí vedle sebe v jedné řadě. Návrh: obecné pravidlo pro řady vedle sebe, ne přepis na každé stránce.
  - **#806:** typografie pro všechny obrazovky. Žádné issue o pravidlech sazby neexistovalo, jen jednotlivé příznaky (#769, #479, #461…). Obsahuje otázku, co umí FontStashSharp 1.5.6 (kerning, OpenType funkce), než se cokoli slíbí.
  - **#807:** About rozdělit na stránky jako Help, přehrávač první. Dnes je to ~160 slov v jedné velikosti, jedné barvě a vystředěné, bez nadpisů. **Otevřená otázka majiteli:** „Jukebox“ = přehrávač originální procedurální skladby na About (#443), nebo Jukebox v Extras (#704)?
  - **#808:** Potato volitelné z Windows, tedy odpověď na otevřenou otázku v #788 („Potato on Windows too?“). ⚠ Potato cesta se volí **při startu** (`QualityLock` → `PotatoPath`), takže je to argument nebo restart, ne řádek, který se přepne hned. **Neznámé, zda se `GamePi/Shaders` (SM 3.0, dnes přeložené jen pro DesktopGL) přeloží pro DirectX.** To je první věc ke zjištění.
  - **#809:** mraky letí moc rychle. Sopka má výchozí počasí Storm: vítr 12,1 u/s ve výšce 140, tedy **4,9 °/s nad hlavou, 3,3× rychleji než Scattered** (1,5 °/s). Druhá oktáva jede 1,6× rychleji než velké tvary (`Clouds.fxh`), což čte jako „závod“. Spočítáno z kódu, **ne změřeno ve snímku**.
  - **#810:** vibrace při uvolnění skupiny jsou moc silné. Váha je přímka `0,22 + 0,04 × koule` oříznutá na 1, takže **od 20 koulí je to vždy plná síla těžkého motoru**; s ťuknutím přistání ve stejném snímku se oba motory na papíře žádají naplno. Návrh: konkávní křivka s ceilingem hluboko pod plnou, případně delší místo silnější.
  - **#811:** zvuk, když míření narazí na zarážku. Zamítnutý *výstřel* už zvuk má (`PlayShotRefused`) a majitel ho chválí; zarážka má jen blik zaměřovače a od dneška ťuknutí padu. Majitel chce vrznutí a vzdušné povzdechnutí v duchu Half-Life 1, procedurálně. ⚠ Pravidlo „žádné syčení“ a jeho „pfft“ se tu tahají; rozhodne ucho.
- **Komentáře:**
  - **#748 (sopka):** je to to issue, které myslí. Reference pro něj **nejsou vyrenderované** (v `C:\Users\panrd\AI\sd\out` je jen #679) a nikdo ho nemá. Majitel přidal **hory a lidská obydlí**; přidán štítek `local-ai`.
  - **#765 a #767 (efekty power-upů):** haptika je další kanál, který nepopisuje ani anatomie, ani definice hotového. Pozor na klouby (#810). Jeho „nejvýš jednou za kapitolu“ se **liší od kódu**: náboje se dávají **za level** (`GrantPowerupCharges`). To patří k otevřené otázce §5 v #765; neptal jsem se.
  - **#788, #378, #431, #463:** zpětné odkazy a jeho odpověď na otázku Potato.
- Majitel šel spát a **prosil, ať je desktop k dispozici session na Pi**, kdyby potřebovala větší stroj. V okamžiku zápisu žádná jiná session na tomhle stroji neběžela (`ListAgents` prázdný). Tahle session nedrží GPU ani nic těžkého.

## 2026-10-05 (noc) — claim #808; #804: lem je na mainu, druhý řez ploutví čeká na měření Pi — notebook, Claude Code (github-cf)

- **CLAIM #808** (Potato volitelný ve Windows buildu), na žádost session na Pi; majitel spí a řekl, ať jsem jí k dispozici s Windows. První krok podle issue: zjistit, jestli se osm zdrojů z `GamePi/Shaders` přeloží pro WindowsDX. Větev `808-potato-on-windows`.
- **#804, stav:** `pi-aa-rims` je na mainu (`23acaf10`): lem koulí výchozí zapnutý, konstanty na CPU, ploutve a `upscale=soft` za argumentem. Session na Pi potvrdila nativní 1080p při 60 FPS na těžkých levelech.
- **`upscale=soft` na Pi: +1,5 až +1,8 ms** (720p → 1080p). Celoobrazovkový průchod na V3D stojí zhruba 0,5–0,6 ms za každý tap navíc. Zůstává za argumentem.
- **Ploutve, druhý řez: větev `804-fins-live-edges` (`c8bdbc43`), NENÍ na mainu, čeká na měření Pi.** Hran na snímek: Pennant 18 507 → 6 762 kandidátů → 1 944 předaných GPU; Cabinet 11 671 → 5 511 → 1 828.
  - Konkávní hrana ploutev nedostane; šev je tam, kde se láme stínování; otevřený okraj se otvírá jen tam, kde se jeho stěna kreslí; sklo odtoku a šachta ploutve nemají; v síti složené z těles žádná hrana uvnitř společné stěny; CPU vybírá živé hrany (`EdgeFinMesh.SelectLive`), a když se nehne oko ani síť, nepočítá nic. `finsall` kreslí postaru.
  - ⚠ **Chyba prvního řezu, opravená:** stínovací normály stěny B se četly z opačného konce hrany. Vyšlo to najevo až s pravidlem pro šev: 497 hran podél profilu jednoho bubnu vyšlo jako šev.
  - ⚠ **Tmavá čárkovaná linka na bočnici lafety** byla ploutev hrany uvnitř společné zadní stěny bočnice a patky. Na snímku vidět, v testech ne, dokud nevznikl test se dvěma kvádry.
  - ⚠ **Límec ústí hlavně (#425) je vysoustružený naruby:** profil běží po ose soustruhu nahoru, takže `LatheMesh` dá normály i přední stěny dovnitř. Kreslí se vzdálená vnitřní stěna, což zezadu vypadá jako tentýž kroužek. `WindingCheck` to nepozná (vinutí souhlasí s normálami, otevřený pás nemá objem). Neopraveno, issue teprve založím.
  - Deset mutací pravidel v `EdgeFinTests`, všech deset padá.
- **Neověřeno:** cena druhého řezu ploutví na Pi (GPU i CPU při pohybu kamery). Švy u odtoku (zlatý pás proti kameni a šachtě), tmavé pruhy na hlavni, kola a válečky zůstávají schodovité.

## 2026-10-05 — noc na Pi: všech 14 scén nativně v 62 FPS, 10min zátěž, měření #804 (ploutve, měkké roztažení) — Pi (BS3DServer), Claude Code (296408e3)

- **Po #804 (merge `23acaf10`: lem koulí zapnutý a levnější shader koule) drží Pi nativní 1920×1080 na všech 14 scénách.** Jeden level z každé, čistý profil (Auto), s limitem, bez VNC, 45 s: 61,8–62,0 FPS v posledních 20 s a Auto nikde nesnížil.
  - Zapsáno do „The resolution“ v docs/game-shell.md (merge `9fc8cea8`) a na #801. Plán v #785 je aktualizovaný.
- **Pacing s limitem je klidný.** Rozptyl za sekundu je většinou pod 0,1 ms, občas jedno škubnutí za 20–40 s (sd 2,4–3,7 v té sekundě). Rozptyl ~5 ms při `nocap` je fronta bez limitu, ne hra.
- **Deset minut na Cabinetu:** 62 FPS od druhé minuty, 55–60 °C, `get_throttled` 0x0.
- **#804 ploutve (dělá notebook), měřeno nativně s `rims=1`:**
  - První řez: +1,05–1,4 ms nativně i v 720p. Byl to binning: VERTEX_BIN 243 instrukcí na každý vrchol zkolabovaného quadu.
  - Druhý řez (`c8bdbc43`: konkávní hrany pryč, švy podle stínování, CPU vybírá živé hrany): **+0,53–0,67 ms**. Starý způsob kreslení nad novými kandidáty +0,70; CPU výběr v obíhajícím menu v šumu.
  - Na fotkách z Pi je obzor ostrova a lafeta hladká, vnitřní elipsa odtoku a kola ne. Fotky má majitel.
- **`upscale=soft` (B-spline ve 4 tapech) stojí +1,5–1,8 ms v 720p.** Na V3D vychází ~0,5–0,6 ms za tap na 1080p výstupu, proto zůstává za argumentem.
- **Fronta verdiktů 17**, takže jsem nezačínal nic, co se posuzuje okem.
- **Čeká na majitele:** verdikty #801 a #804 (lem, ploutve) a starší pi-port (#788, #789, #793, #796); řádek `server` pro online.
- **#808** (Potato na Windows) si vzal notebook.

## 2026-10-05 (noc) — #808: Windows build umí `potato` (renderer Raspberry Pi na desktopu) — notebook, Claude Code (github-cf)

- **Hotovo, větev `808-potato-on-windows`:** `BS3D.exe potato` spustí Windows hru přes cestu Potato, drženou stejně jako GamePi (`[quality] Potato: held for this run by the potato argument, locked`). Nic se neukládá a ze hry se tam nedá dostat; bez argumentu je build stejný jako dřív. Issue zůstává otevřené se štítkem `shipped-awaiting-verdict`: varianta (b), řádek „Potato (restart)“ v Settings, je rozhodnutí majitele.
- **Odpověď na první otázku issue:** devět zdrojů z `GamePi/Shaders` se pro DirectX 11 nepřeložilo ani jeden (`Invalid profile 'vs_3_0'`). Stačily tři úpravy, všechny v `PotatoProfile.fxh`: jména profilů přes makro, dither čte pozici místo `VPOS`, a vstup pixel shaderu u šesti portů začíná pod DirectX pozicí.
- ⚠ **Past, která nic nehlásí:** DirectX 11 páruje výstupy vertex shaderu se vstupy pixel shaderu podle POŘADÍ, ne podle sémantiky. Šest portů má pro pixel shader vlastní strukturu bez pozice (SM3 mu ji nedovolí), takže každý vstup četl hodnotu souseda. Všech šest se přeložilo, načetlo a kreslilo; **konfety padaly černé** a jen podle nich se na to přišlo. `PotatoShaderSourceTests` to drží v textu zdrojů (pět pokažení, pět selhání).
- **GL xnb se nezměnily ani o bajt**, takže Pi se změna netýká a nic se necommituje do `GamePi/Shaders/Compiled`.
- **Jak se efekty dostanou k loaderům:** `Prazsky.Shaders` je překládá podruhé do `Content/Potato/Shaders` (zápis `/build:zdroj;cíl` v `.mgcb`) a `Game/PotatoContent.cs` se dívá do `Content/Potato` dřív než do `Content`. Šest z devíti má stejné jméno jako desktopový efekt a načítá se podle něj z tuctu míst; žádné jsem neměnil.
- **Čtvrtý řádek `[build]`** jen u takového běhu: `[build] potato shaders 9 set …`. Třetí řádek se do `Content\Potato\Shaders` nedívá, takže snímek by nesl otisk 41 efektů, které nenačetl.
- **Ověřeno na notebooku, DirectX vedle GamePi přes DesktopGL, stejná vteřina stejného levelu (`seed=5 sceneseed=1`):** obloha a obzor ostrova se shodují na 1/255 v každém pixelu (nativně, s ploutvemi, v 720p oběma zvětšeními). Po dvojicích prohlédnuto: zaměřovací paprsek a stopa střely, laserová síť, výbuch bomby (`level=Vent detonate=9`), červí díra (`aim=10:25:70 fire=11,11.6,12.2`), ohňostroj, konfety, ditherovaný přechod (`level=Facet fire=9`), ploché koule, menu s nápisem. Běží i z jednosouborového publishe (`release.yml` teď kontroluje, že `Content/Potato/Shaders` v něm je).
- **Neověřeno:** záložka Display v Settings pod `potato` (žádný argument záložku neotevře; řádky čtou stejný `IsQualityLocked` jako na GamePi). Jiná grafická karta než Vega tohoto notebooku.
- ⚠ **`CLAUDE.md` jsem neměnil** a dvě jeho věty už neplatí přesně: „three `[build]` lines“ (běh s `potato` má čtyři) a „There is no OPENGL build of any shader now, and the `#if OPENGL` branches are gone“ (platí pro `Prazsky.Shaders`; `GamePi/Shaders` jsou od #789 GL a od teď mají `#if OPENGL` v `PotatoProfile.fxh`). Je to na majiteli.
- ⚠ **Snímky během sekundy, kdy se ukládá PNG, kazí `[fps]`:** šest `shot=` po 0,1 s dalo řádek 137 ms při jinak rovných 16,67. Čas měřit z běhu bez snímků.
- **#804, měření Pi k druhému řezu ploutví (`c8bdbc43`):** +0,53 až +0,67 ms nativně (první řez +1,05 až +1,43), výběr na CPU není vidět ani v menu, kde kamera obíhá. Pokus `fins=0.01` rozdělil zbytek na ~0,25 ms pevné režie (drawy, stavy) a ~0,35 ms pixelů. Větev zatím není na mainu; další krok je osvětlení ploutve ve vertex shaderu a méně drawů.

## 2026-10-06 (noc) — #804: ploutve ve třetím řezu, výchozí zapnuté; #812 založeno — notebook, Claude Code (github-cf)

- **Ploutve (`fins=`) jsou na mainu a výchozí zapnuté** (`POTATO_FIN_PIXELS` = 1, `fins=0` vrátí snímek bez nich). Měření Pi (session na Pi, nativně, `rims=1`, dvě kola): druhý řez +0,53 / +0,55 / +0,67 ms na Pennant / Girandole / Cabinet; třetí řez **+0,42 (Pennant) a +0,48 (Cabinet)**, Girandole nestihla. Cabinet s lemem i ploutvemi 15,2 ms proti limitu 16,1.
- **Třetí řez:** ploutev se stínuje ve vertex shaderu týmž `Shade` jako pixely (dostane konstantu `onPixel`, protože `fwidth` ve VS není); pixel shader je pokrytí × barva (16 řádků GLSL místo 214), u kamene navíc detailní textura a křivka (85 místo 245). Devět ostatních programů vyšlo z MojoShaderu bajt po bajtu stejně (hash GLSL v xnb před a po, `glsl_programs.py` ve scratchpadu session). Obzor ostrova shodný na pixel.
  - `fins=0.01` na Pi rozdělilo zbytek druhého řezu na ~0,25 ms drawů a ~0,35 ms pixelů; třetí řez vzal většinu pixelů. Co zbývá, je šest instancovaných drawů navíc (15 atributů, vlastní uniformy) a to levně nejde.
  - Výběr živých hran na CPU na Pi vidět není ani v menu, kde kamera pořád obíhá.
- **#812 založeno** (límec ústí hlavně naruby, důkaz zboku na desktopové i Potato cestě). Oprava je na větvi `812-collar-inside-out`, **nemergovaná**: horní část kroužku po ní zesvětlá (ocel odráží oblohu) a tu barvu majitel ladil v #478. Před/po poslány majiteli.
- ⚠ **Při merge #808 jsem smazal vzdálenou větev dřív, než byl merge pushnutý** — řetěz s `;` místo `&&` po neúspěšném kroku. Nic se neztratilo (lokální větev to měla), ale pravidlo platí: smazání větve jen jako poslední článek `&&` za pushem mainu.
- ⚠ **Snímky v sekundě, kdy se píše PNG, kazí `[fps]`:** ten řádek čte z běhu bez `shot=`.
- **Fronta verdiktů má 18 položek.** Lem i ploutve jsou jeden verdikt na #804 (jeden pohled: hrany Potato cesty), ne dva.
- **Neověřeno:** Girandole ve třetím řezu, shaderdb třetího řezu (FS ploutví by měl spadnout z 329/274). Švy u odtoku (zlatý pás proti kameni a šachtě), tmavé pruhy na hlavni, kola a válečky zůstávají schodovité.

## 2026-10-06 — #804: švy odtoku (zlatý pás × kámen, límec × šachta) vyhlazené analyticky — notebook, Claude Code (github-cf)

- **Šev, podél kterého nevede hrana žádné sítě** (pás jde POD kámen a pod šachtu, #109), nenajde žádná ploutev. Každý vrchol pásu nese v texturové souřadnici **výšku nad plochou, na které leží** (offset prstence, `FunnelRimsMesh`) a druh švu (`SEAM_NONE / SEAM_SOLID / SEAM_PIT`); technika `PotatoBand` (`PotatoLit` + šest instrukcí) pás rozpustí přes poslední pixel nad plochou: pokrytí = (výška − `SEAM_HEIGHT`) / `fwidth(výška)`. `SEAM_HEIGHT` 0,004 je nad prohyby fazet vrtání (tisíciny) a pod zdvihem rtu (0,012).
- Límec se rozpouští jen ve scénách se šachtou (`PotatoBandUnderPit`); pod samotným sklem má zůstat vidět (#237). Prstenec u dna nikdy. Ret (obrys pásu na bližší straně díry) má ploutve: pás se staví v `EdgeFins.Wanted(twoSided: true)`.
- ⚠ **Dvě slepé uličky, obě viděné na snímku:** (1) napůl pokryté pixely límce zapisovaly hloubku nad sklem, sklo kreslené po nich na nich neprošlo testem a šachta prosvítala neprosklená — řada tmavých teček podél švu. (2) Pás bez zápisu hloubky to spravil, ale bližší stěna skleněného kužele pak překreslila ret. Řešení: ve scénách se šachtou jde na cestě Potato **sklo před pásem** (`ArenaIsland.DrawGlass`), pás zapisuje hloubku jako dřív. Desktop má pořadí beze změny a jeho odtok je stejný až na zrno.
- Třináct programů efektu zůstalo bajt po bajtu (hash GLSL v xnb), přibyly dva. Desktop texturovou souřadnici nečte (jeho vertex input ji nemá).
- **Neověřeno:** cena na Pi (pás je 2 560 trojúhelníků, čekám nic měřitelného); třetí řez ploutví na Girandole. Zbývá: tmavé pruhy na hlavni, kola a válečky.

## 2026-10-06 — #804: konkávní švy (tmavé pruhy hlavně) a kola mají ploutve; pás odtoku bez ploutví na okrajích — notebook, Claude Code (github-cf)

- **Konkávní šev** (hrana, přes kterou se láme stínování, ale obě stěny jdou od ní k oku): ploutev leží V ROVINĚ stěny přivrácené víc, posunutá podél ní tak, aby na obrazovce stála šířku rampy od hrany, v hloubce té stěny, s barvou druhé stěny — a kreslí se s hloubkovým posunem jako decal (`FinRasterizer`: dva kroky přesnosti + jeden sklon). Tmavé pruhy hlavně a prstenec závěru jsou hladké z obou stran. Konkávní hrana s hladkým stínováním ploutev dál nemá.
- ⚠ **`RasterizerState.DepthBias` je v [0, 1] hloubkového bufferu, MonoGame ho pro GL násobí 2^24** (a pro D3D na celočíselný bias). Posun −2 dal každou ploutev před všechno a dělo bylo pokreslené ploutvemi svých skrytých hran (viděno, dva snímky mě stály, než jsem to vyloučil vypnutím konkávních ploutví na CPU — artefakty zůstaly, takže to byl posun, ne nové ploutve).
- **Kola mají ploutve:** draw do čtyř instancí dostane ploutve po instancích, každá vybraná pro svoji stranu oka (`FIN_INSTANCES_APART`); válečky (desítky instancí) ne. **Pás odtoku nemá ploutve na okrajích** (`EdgeFins.Wanted(rims: false)`): všechny čtyři jsou zasunuté do kamene nebo skla a jako okraje byly obrysem odevšad — tisíc ploutví každý snímek schovaných testem hloubky.
- Počty na snímek (notebook): Pennant 8 322 kandidátů → 2 520 předaných (kola dvakrát), Cabinet 7 167 → 2 491; před švy a koly 1 944 / 1 828. Třináct mutací pravidel v `EdgeFinTests`, všech třináct padá. Třináct programů efektu beze změny, jen dva VS ploutví.
- **Neověřeno:** cena na Pi (session na Pi skončila s majitelovým limitem). Zbývá: válečky (záměrně), okno hlavně (šev sklo × ocel).

## 2026-10-06 — #811 vrznutí, když míření narazí na zarážku; odpovědi majitele k #807, #765 a #811 — desktop, Claude Code (bs3d-a3)

- **Odpovědi majitele na tři otevřené otázky ze včerejška** (každá zapsaná jako komentář do svého issue):
  - **#807 „oba“.** Čteno jako: oba přehrávače zůstávají. Přehrávač originální procedurální skladby jde na první stránku About, Jukebox v Extras se nemění. Komentář to říká otevřeně a nechává opravu, kdyby myslel něco jiného.
  - **#765 „myslel jsem jednou za level, dlouhý efekt si můžeme dovolit“.** Zodpovídá §5 (kód dává náboje za level, jak bylo uvedeno): efekt power-upového náboje smí být několikasekundový, jen se musí škálovat podle výsledku, protože se uvidí zhruba stokrát.
  - **#811 „udělej, co navrhuješ“.** Zvuk jako krátký dolně filtrovaný dech, ne syčení.
- **#811 hotovo, merge `40be61c6`, `shipped-awaiting-verdict`.**
  - `Game/Audio/AimStopSynth.cs` je čistá aritmetika v samostatném souboru jako `DrumLayer`, takže ji testy kompilují: stick-slip vrznutí (34→58 slipů za s, dvě rezonance 650→1100 Hz) a od 0,20 s pufnutí vzduchu (350–1700 Hz). `PlayAimStop` hraje jednou na hraně `_aimStopArmed`, na které už jede ťuknutí padu.
  - **Změřeno:** 95 % energie v 300–4000 Hz, 0,25 % nad 5 kHz, 4,3 % pod 300 Hz (odmítnutý výstřel jich má 93 % mezi 60 a 300 Hz); těžiště vrznutí stoupá 812→962 Hz. Test selhal na mutantu, který syčel a byl basový, a **první návrh selhal u „dvou událostí“**: pufnutí bylo neslyšné, jakmile vrznutí skončilo (0,013 proti 0,09 RMS), takže byl hotový jen 0,33 s dlouhý rachot. Proto se vrznutí zkrátilo na 0,28 s a pufnutí zesílilo.
  - ⚠ **Neslyšeno.** Spektrum říká jen, kde zvuk je, ne že je příjemný. Konstanty k ladění sluchem jsou nahoře v `AimStopSynth`: `PUFF_WEIGHT`, rychlost slipů, stoupání rezonance.
  - ⚠ **Hák se ve hře nespustil.** Nic nedokáže zatlačit míření do zarážky (`aim=` nastavuje pózu, a to napětí nezvedá). Hra se s novým bakem spustila a 14 s hrála level bez výjimky. Pokud se to bude hodit víckrát (zarážka, ťuknutí, blik), stačí `push=<od>:<do>` v `ScriptedPlay`, které volá `Cannon.Aim`.
  - Fronta `shipped-awaiting-verdict` je na **19** (strop 15); šlo se na to výslovným zadáním.
- **Past:** `python3 - <<EOF` v Git Bash visí (zástupný python z Windows Store, čeká na stdin) a zablokuje celé volání nástroje. Na úpravy souborů Edit nebo `sed -i`, bez skriptových heredoců.
- **Peer session mezitím udělala #808** (`f71cae14`: devět Potato efektů se překládá i pro DirectX 11 ze stejných zdrojů). Tím je zodpovězena otázka z mého issue, kterou jsem tam nechal jako první věc ke zjištění. Neřeším, patří jim.
- Pi session: celou dobu neběžela žádná jiná, nic těžkého jsem nedržel.

## 2026-10-06 — Pi: kurzor myši, online zapnutý, měření ploutví #804 — Pi (BS3DServer), Claude Code (296408e3)

- **Kurzor myši (merge `4f53370d`):** majitel hlásil kurzor na Pi jako „náhodnou změť pixelů“.
  - Xwayland držel šipku 39×39 v pořádku (`XFixesGetCursorImage`, šest spuštění po sobě).
  - ⚠ **Kurzorová vrstva Pi 5 je 64×64** (`DRM_CAP_CURSOR_WIDTH/HEIGHT` na card1). `PointerCursor` teď bitmapu průhledně doplní na násobek 64; šipka i hot spot zůstávají.
  - Majitel potvrdil, že je kurzor v pořádku.
  - ⚠ labwc kurzor do screencopy nevkládá (`grim -c`), takže kurzor na obrazovce se snímkem nedá zkontrolovat.
- **Online na Pi:** s majitelovým svolením jsem do jeho `~/.local/share/BS3D/Settings.json` přidal `"server": "https://scores.winphonew.eu/"`. Na kopii nastavení s `nickprompt` se hra zeptá na přezdívku a internet test na Linuxu projde. Odeslání skóre zatím nikdo neviděl.
- **#804 ploutve, měřeno pro notebook** (nativně, bez VNC):
  - Třetí řez (`0df9dcef`, stínování ploutve ve vrcholu): +0,28 až +0,48 ms; FS FinLit 15 a FinTextured 125 instrukcí, 4 vlákna.
  - Main s koly, konkávními švy a pásem odtoku: +0,66 až +0,78 ms. Rozklad přes `finsless`: offset ~0, konkávní švy ~0, kola ~0,1–0,2 ms, základ +0,5–0,6 ms.
  - Notebook nechal všechno výchozí. Cabinet nativně 15,5 ms.
- **#808** (Potato na Windows, notebook): na Pi ověřeno, že GamePi je beze změny (xnb bajt po bajtu stejné, A/B v šumu).
- **Pro měření** mám od rána oddělený worktree v scratchpadu (`measure-wt`), takže majitelův build v `~/BS3D-gamepi/GamePi/bin` se během měření nepřepisuje.

## 2026-10-06 — #none Pravidla verdiktů: co potřebuje majitele, jedna stránka, mlčení o vzhledu = souhlas — desktop, Claude Code

- **Majitel: úzké hrdlo je, že verdikty dává jen on; s návrhem souhlasil („ta pravidla se mi líbí a zavedeme je“).** Pravidla jsou v `CLAUDE.md` („How work is run“, dvě odrážky), důvody a čísla v `docs/repo-conventions.md` („What needs the owner's verdict“).
- **Co platí:**
  - **Objektivní** věc (zachycení, měření, test, běh workflow) agent zavře s důkazem a do fronty vůbec nejde.
  - **Rozhodnutí** dostane `owner-decision`.
  - **Verdikt** = `shipped-awaiting-verdict` + právě jeden druh: `verdict-eye`, `verdict-hands` nebo `verdict-newcomer`. Nováček se do stropu 15 nepočítá.
  - **Odpovídá se na jedné stránce** (OK / Vrať / Je mi to jedno). Každá odpověď se zkopíruje do issue.
  - `verdict-eye` se po **7 dnech** bez námitky (nebo vydáním, co dřív) zavře se štítkem `accepted-by-silence`. Podmínka: verdikt jde vynést do minuty a vlastní kontrola agenta nic nenašla. Pro dnešních 15 běží lhůta od dávkové stránky, takže nejdřív 2026-10-13.
- **Štítky založené:** `verdict-eye`, `verdict-hands`, `verdict-newcomer`, `owner-decision`, `accepted-by-silence`. Dotaz na strop: `gh issue list --label shipped-awaiting-verdict --search "-label:verdict-newcomer"` (dnes 15).
- **Dělba s relací bs3d-f3** (dávková stránka pro 15 čekajících): f3 roztřídí issues podle dosudů a skeptiků, dá štítky, objektivní zavře s důkazem a stránku vyvěsí na každé issue. Já jsem sahal jen na pravidla, štítky a tenhle zápis. Vlastního třídicího agenta jsem zastavil, aby se práce nedělala dvakrát.
- **Změřeno:**
  - Zip vydání byl stažen 0–4× na verzi (v0.1.0 3, v0.2.0 0, v0.2.1 2, v0.3.0 4, v0.3.5 2).
  - Od spuštění žebříčků (2. 10.) mají záznam na 66 deskách tři přezdívky: majitel (60) a dva další hráči (11 a 5).
  - #768 (cizí hráči) zůstává nedotčené.
- ⚠ **Python na tomhle stroji** odmítl certifikát `scores.winphonew.eu` jako prošlý. openssl ho vidí platný (Let's Encrypt YE1, do 30. 12. 2026) a curl i hra fungují. Dál jsem to nezkoumal.

## 2026-10-07 — #813 Send a Note: dokumentace a stav API; inventura rozdělané práce — desktop, Claude Code (bs3d-c5)

- **#813 je na mainu od 6. 10. (`59cf5aed`), chyběl jen ocas.** Do `docs/game-shell.md` jsem dopsal oddíl „Send a Note“: kde tlačítko je, snímek jako závěrka, kontext, podpis identitou, psaní, outbox a jeho pravidla a páka `note=`. Do `docs/formats-and-tools.md` přibyla složka `Notes/`.
- **API je nasazené:** BS3D-API v0.1.22 (2026-10-07 06:12) obsahuje poznámky (`8f17b2c`). Živá služba odpovídá na `POST /v1/notes` s prázdným tělem `400 bad-request`, neznámá cesta dává 404. BS3D-API#10 je ale pořád otevřené.
- **Neověřeno:** poznámka z release buildu do živé služby. Testovací poznámku jsem bez svolení majitele neposlal, stejně jako se neposílá testovací clear.
- **Inventura** (majitel se ptal na rozdělanou práci): kromě mainu žádná větev, žádný stash, CI zelené. Tři worktree ve scratchpadech starých sezení jsou čisté a detached na commitech mainu. Nemazal jsem je, protože v `bin` můžou mít snímky.
- ⚠ **Dávka verdiktů ze 6. 10. nemá štítky ani odkaz na issues.** Žádné z 15 issues nenese `verdict-*` ani `owner-decision` a stránka „Verdikty 6. 10.“ není odkázaná na žádném z nich. Klasifikace bs3d-f3 čeká na majitelovo svolení a databáze stránky (`verdicts`) je prázdná, takže majitel zatím neodpověděl.
- ⚠ **Deník za září se zrotoval až 7. 10.** (448 zápisů, #358, teď `docs/agent-notes-archive/2026-09.md`). Měl to udělat první zápis října, a nikdo to neudělal: deník narostl na 1,44 MB, teď má 175 kB. Obě půlky složené za sebe dají původní soubor bajt po bajtu (chybí jen prázdný řádek mezi zářím a říjnem).

## 2026-10-07 — dávka verdiktů ze 6. 10. provedena, #762 sklo 0,2, testovací poznámka #813 — desktop, Claude Code (bs3d-c5)

- **Majitel dal svolení („Ano, proveď to“), dávka ze 6. 10. je provedená.** Na každé z 15 issues je odkaz na stránku „Verdikty 6. 10.“ a druh.
  - `verdict-hands`: #188, #378, #800, #520, #802.
  - `verdict-eye`: #780, #811, #257. Mlčením se může zavřít jen #780, nejdřív 2026-10-14. #811 čeká, až někdo uvidí hák ve hře; #257 čeká na bedny.
  - `owner-decision` místo `shipped-awaiting-verdict`: #803, #213, #692, #808.
  - **Zavřeno s důkazem:** #769 (snímky bs3d-f3, `SpaceWrapTests` 8/8) a #793 (zkouška release na dnešním mainu, run 37582683923: tři zelené joby, oba balíky a SHA256SUMS, poprvé i s řádky z #808).
  - **Fronta mimo nováčka má teď 9 položek** (bylo 16 i s #813, které je `verdict-eye`).
- **#762 (merge `cfbec3de`): vůle kresleného skla 0,1 → 0,2.** Dočasná sonda (necommitnutá) měřila v každém kroku průnik každé padající koule do kolizní stěny a ke kreslenému sklu. Scénář: City `level=112 seed=3 powerups=cut:4`, čtyři trojice `aim=`/`cut=`/`fire=`, pády 240, 311 a 284 koulí. Průnik do stěny byl 0,055 a v dalším běhu 0,069 (řešič není deterministický), vždy v dolní třetině (r ≈ 4), kde se hromada vzpříčí v otvoru. Se starou vůlí chybělo ke sklu 0,007 a 0,069 by jím už prošlo. S 0,2 je nejblíž 0,060 a v 900 000 vzorcích sklem neprošla žádná koule. Snímky zespodu (průlet pádu v City) jsou čisté před i po.
  - ⚠ **`shot=` během průletu pádu:** uložení PNG ve 4K trvá asi 2 s a hodiny hry poskočí nejvýš o 0,5 s, takže série snímků se za průletem opožďuje. Pohled zespodu zachytilo `shot=13,14.5` při `fire=9`.
- **#813: testovací poznámka do živé služby prošla** (svolení majitele): `[note] Sent 0c20ef9b (201)`. Je to jediná poznámka v živé databázi (záložka Notes admin stránky přes `bs3d-admin`). **Smazat ji agent neumí:** admin CLI chce sudo heslo, bez hesla jdou jen `update.sh`, `update-ceilings.sh`, snapshot databáze a admin stránka, která jen čte. Smaže ji majitel (`admin notes`, pak `admin delete-note <id>`).
  - ⚠ **Admin stránka přes SSH tunel vrací 400, když lokální port není stejný jako vzdálený** (`-L 15008:127.0.0.1:5008`). Se stejným číslem portu (`-L 5009:127.0.0.1:5009`, `bs3d-admin --port 5009`) jde přihlášení klíčem z výpisu i stránka.
- BS3D-API#10 zavřené s důkazem nasazení (v0.1.22).

## 2026-10-07 — verdikty majitele zapsané; #814 #813 #817 #800c #819 #821 na mainu; #816–#821 založené — desktop, Claude Code (bs3d-c5)

- **Majitelovy odpovědi z dávkové stránky** (vložené i do chatu, v databázi stránky stejné) jsou opsané do všech 13 issues.
  - Přijaté a zavřené: #780, #188, #378, #520, #802.
  - „Je mi to jedno“: #803 nové pořadí zůstává; #213 platí rozvrh z #705 (Swap od 3. kapitoly, Brake od 4., Cut od 5.); u #692 b zůstává červené odmítnutí.
  - Vrácené do práce: #813 c, #811 a+c, #257 a, #800 c, #692 d, #808 a. Poznámky majitele jsou doslova v jejich komentářích.
- **Hotové a na mainu:**
  - **#814** (nápis „Click or Space to skip“ zůstával ostrý pod pauzou během průletu kapitoly): kreslí se jen, když je hra navrchu (`IsActive`). Zavřené se snímkem před a po.
  - **#813**: tlačítko je **Write to the Author** v tyrkysové (`MENU_TEXT_NOTE`), je i v Extras a pole ukazuje posledních šest *měřených* řádků (`FieldTail`, `FieldTailTests`, starý ocas 220 znaků v testech 7 z 8 selže). `note=` umí krok `back`.
  - **#817**: pad vibruje při herních událostech jen s rukou na padu (`GamepadRumble.HandOnPad` z `GameplayScreen.NoteDevice`). Podpis, puls po připojení a menu jdou dál. Změřeno dočasnou sondou: bez ruky po podpisu nic, s `pad` 31 pulsů. Zavřené.
  - **#800 c**: otočení záložky má sílu pro každý motor (těžký 0,26, lehký 0,55) a trvá 0,1 s.
  - **#819**: pravá páčka se v přiblížení zpomalí podle objektivu (`CursorRateScale`) a má křivku 1,5 (`PadAimRate`, `PadAimRateTests`, lineární verze 5 z 8 selže).
  - **#821**: každý druhý přechod duhové koule jde opačným směrem a na koncích zpomalí (`WildcardCycle.Returning`, `WildcardReturning` v shaderu, `WildcardCycleTests`; směr podle indexu barvy selže při 3 a 5 barvách).
  - **#808 b**: opravené řádky o Potato v CLAUDE.md.
- **Založené z majitelových poznámek:** #816 checkbox, #817, #818 Konami na padu, #819, #820 paprsky z ústí a značka ve frontě u speciální koule, #821. Karta pro chameleona je komentář v #735, které ji už obsahovalo. Majitel říká wildcardu „chameleon“ nebo „duhová kulička“.
- **Stránka verdiktů má verzi 3:** na majitele čekají #813 a #821 (oko) a #800 a #819 (ruce). Fronta mimo nováčka má 4 položky.
- ⚠ **Pad majitele je u desktopu připojený.** Každé spuštění hry posílá úvodní podpis, a dokud neplatilo #817, vibrovaly v něm i skriptované výstřely (`fire=`, řezy u #762).
- ⚠ **`shot=` ve 4K stíhá jen asi jeden snímek za 0,4 s** (z 31 plánovaných jich vzniklo 7) a hra při tom běží na 2 fps. Na pohyb kratší než ~1 s se snímkovou řadou měřit nedá; `width=`/`height=` velikost snímku nezmenší.
- ⚠ **Heredoc v jednom Bash volání s několika `cat > … <<'EOF'` spadl na „unexpected EOF“** a nic se nezaložilo. Těla issues jdou spolehlivě přes Write do souborů a `gh issue create --body-file`.

## 2026-10-07 — první poznámky z hraní přes #813: #823 a #824 založené — desktop, Claude Code (bs3d-0a)

Majitel poslal odkaz na admin stránku poznámek (`127.0.0.1:5002/notes`, jednorázový `login?key=`; vlastní SSH tunel na Pi zamítl klasifikátor oprávnění, takže se čekalo na odkaz). Poznámek bylo pět: č. 1 a 2 jsou testovací (agent a About), č. 3 „I like! :)“ u Hilbertu (úroveň 75) je pochvala bez parametrů, **bez issue a bez komentáře** (žádný otevřený rodič, ambiguous praise), č. 4 a 5 jsou nové issues. Nic se neimplementovalo, žádná větev, žádný claim.

- **#823** (č. 5, Gyroid, úroveň 78): vyčerpaná kulička Cut zůstává uvnitř shluku. `LandCutter` ji vloží do `_fallingBalls` s rychlostí 1,6 u/s a vyřadí ji jen kill plane (`RemoveFallenBalls`: „no sleep cull here, deliberately“); `MarkLoose` jen propouští střely (`Callbacks.cs`), s mřížkou koliduje dál. **Změřeno skriptem hry** (`level=78 seed=1 aim=0:E:T cut=0.4 fire=1.5`): výchozí zaměření `destroyed 62, orphaned 277` (cutter padá s uvolněnou polovinou, vidět), ale 12°/0°, 15°/±10°, 18°/0° dají `destroyed 58, orphaned 0` — nic nepadá, cutter nemá s čím odejít. **Nevidět ho přímo:** zepředu je uvnitř shluku skrytý a nic neloguje jeho polohu. #692 (d), kterou má `bs3d-c5` rozdělanou (řez nejvýš dolní půlka), takových řezů přidá; kolega dostal zprávu, komentář je v #692.
- **#824** (č. 4, Phyllotaxis, úroveň 77): hudba Gridu „občas příšerně“. Všech deset úrovní Gridu (71–80) hraje rodinu `pulse` (deset nahrávek z #486), která se střídá při každém otevření úrovně, a **poznámka neříká, která nahrávka hrála** (`DescribeForNote` nemá stopu). Issue žádá `music` v kontextu poznámky, pak majitelův poslech v Jukeboxu (#704) a výměnu jako u #706. Jediné číslo, které `pulse` odlišuje: šum kódování na švu smyčky 2,11× (`docs/game-feedback.md`), jen holé `pulse.ogg`. Komentáře v #706 a #813.
- ⚠ **Skript `aim=0:E:T` při elevaci ≥ 25° řez Cutem tiše nevystřelí** (žádný `[shot] cutter struck`; chrání horní čtyři patra, #692), při 55° `bounced off the glass`. Na Gyroidu zasáhne patro 4 elevace 12–18°.
- ⚠ **Hra běží v 3840×1600 i s `width=1920 height=1080`** a snímek má ~15 MB; na prohlížení je zmenšit (`System.Drawing`, Pillow ve venv chybí).

## 2026-10-07 — #692 řez nejvýš do poloviny s náhledem, #823 odklizení řezací koule, kontrola po mergi našla chybu — desktop, Claude Code (bs3d-c5)

- **#692 (merge `8cdd8e0c`, oprava `780d2dfe`): řez vezme nejvýš dolní polovinu shluku a koule, které by odřízl, se před výstřelem zesvětlí** (majitelovo Vrať u d).
  - Pravidlo je `CutReach.IsProtected`: zasažené patro a vše pod ním smí být nejvýš polovina pater od nejnižší koule po nejvyšší. Nahradilo čtyři chráněná patra pod sklem.
  - `LevelGen --cuts` (nový řádek „half“) na 110 levelech: nejlepší rána typického levelu uvolní 154, běžná 77 (se čtyřmi patry 240 a 105). Jednou ranou se nedohraje žádný level.
  - Náhled: `CutReach.Measure` udělá na logické mapě stejné dvě procházky jako `CutBall`. `PhysicsBall.CutPreviewGlow` jde kanálem vlny (`Ripple`), nic nealokuje a pomalu dýchá. `AnchorCutTests` ho porovnávají se skutečným řezem na třech náhodných shlucích. Mutant, který vynechá padající koule, selže ve všech třech.
  - Ve hře vyfoceno v City: zesvětlená část je přesně to, co rána vzala (61 zničených a 179 spadlých).
- ⚠ **Kontrola po mergi (agent nad sedmi dnešními merge) našla skutečnou chybu v #692**: háček vlny bral větší z vlny a záře. Výstražná vlna stropu má ale záporné znaménko, takže nula u každé nerozsvícené koule ji přebila a červená vlna zhasla na celém shluku. Na mainu to bylo asi 45 minut.
  - Oprava: `ClusterRipple.WithCutPreview`, kde výstraha vždy vyhraje. `ClusterRippleTests` ji hlídají; původní `max()` selže v obou případech s výstrahou.
  - Ze stejné kontroly: `HandOnPad` se nastaví z tutoriálu nového levelu už při jeho stavbě (#817), a opravená dokumentace u `HighestOccupiedLevel`. Ostatní dnešní merge kontrola prošla bez nálezu.
- **#823 (merge `e1718de8`, zavřené)**: vystřelená řezací koule se odklidí hned v kroku zásahu (`RemoveFallenBalls(…, retireSpentCutters: true)`, jen ze seznamu padajících). Na Gyroidu u řezu, který nic neuvolnil, sonda zapsala „retired at y −2,71“. Na problém upozornila session bs3d-0a zprávou.
- Stránka verdiktů je ve verzi 5 a čeká na ní pět věcí: #813, #821 a #692 na oko, #800 a #819 na ruce. BS3D-play má build `dev-780d2df`.
