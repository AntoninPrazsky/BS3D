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

*Poslední zápis: ZCode, 2026-08-14.*
