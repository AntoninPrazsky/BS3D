---
title: Two and a half hours of play in under eighteen minutes
date: 2026-10-27T12:00:00Z
topics: [Online scores, Fair play]
summary: "One evening a script took first place on every online board. Each of its clears passed every check on its own; together they claimed two and a half hours of play in under eighteen minutes, and stars their scores could not have earned. What the score service checks now, and what it still cannot."
thumb: /img/blog/thumbs/two-and-a-half-hours-of-play.jpg
---

Bubble Shooter 3D sends every cleared level to a small score service of ours, and every level has an online board. On 26 October Antonín asked about one player: "Is there a way to tell whether they played fairly? They lead on every level, and it seems to me they finished the levels suspiciously fast."

They had not played at all. Between 19:42 and 20:00 UTC the evening before, something sent the service 150 clears, one on each of 150 boards, and took first place on every one. That is every level in the game: a level has a board for each version of it, and these 150 boards are 140 levels. 63 of the boards already had a clear from another player; 87 had none.

## What the submissions said

None of it came from the game.

- The game introduces itself as `BS3D/<version>`, and a build made from the source sends the version `dev-<commit>`. These said `BS3D-Local-Score-Demo/1.0` and `dev`.
- One clear every 7.1 seconds, in alphabetical order from Amphora to Zigzag. That is just under the service's limit of ten submissions a minute for one player.
- Every clear had one star, 20 shots and 60.00 seconds.
- On 43 of the 63 boards that already had a clear, the score was the leader's plus one.
- A few minutes earlier the service had refused the same run twice, for its shot count, on Amphora at the same score, 50,991.

## Every clear passed on its own

The service checks each clear against a table the game's own score simulator writes at every release, one row for each level. A clear must score no more than the level's ceiling, which is a bound above every score the rules can produce. It must use no fewer shots than the least the level can be cleared in, and no more than its budget. And it must take long enough for those fewest shots to be fired. Sixty seconds and 20 shots passed all of it, 150 times.

Each of those checks looks at one submission. Nothing looked at a player's submissions together, and together they did not add up.

## Two and a half hours in under eighteen minutes

The game times a level by the real seconds of its frames, and only while the level is being played. So a player who plays online can never send clears whose times add up to more than the time that passed between them. 150 clears of 60 seconds claim two and a half hours of play. They arrived in 17 minutes 44 seconds.

The obvious answer would have been a refusal, and it would have been wrong. The game keeps an outbox: a clear played while the service cannot be reached waits, and goes with the others at the next start, all at once. A refusal tells the game to drop a clear for good, so a player coming back online would lose real clears to a check meant for a script. The game already treats one answer as no answer at all: HTTP 429, "too many requests". The clear stays in the outbox and goes again later.

So each player now has a budget of seconds. It fills at one second per second, up to ten minutes, and a new player starts with it full.

- **What a submission costs:** its duration. A clear and an unfinished attempt both count, since both are time played.
- **When the budget is short:** a submission that claims more than the budget holds gets a 429, with `Retry-After` saying how long until the budget covers it.
- **One rule for real play:** a single level longer than the whole ten minutes is let through on a full budget and leaves the budget in debt. Otherwise it would wait at the head of the outbox for ever, with every clear behind it.

We replayed the database through it: 279 submissions from five players. Not one of the 129 submissions of the other four players would have waited. 123 of the 150 scripted ones would have.

<figure>
  <picture>
    <source media="(max-width: 600px)" srcset="/img/blog/two-and-a-half-hours-of-play/time-phone-400.png 400w, /img/blog/two-and-a-half-hours-of-play/time-phone-800.png 800w" sizes="calc(100vw - 32px)" width="400" height="474">
    <img src="/img/blog/two-and-a-half-hours-of-play/time-800.png" srcset="/img/blog/two-and-a-half-hours-of-play/time-800.png 800w, /img/blog/two-and-a-half-hours-of-play/time-1600.png 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="440" loading="lazy" alt="A chart of minutes of play against minutes since the first scripted clear. The play the scripted clears claimed, in orange, climbs to 150 minutes in under 18. The time that passed, in grey, reaches 17 minutes 44 seconds. What a ten-minute budget lets through, in blue, runs about ten minutes above the grey line and ends at 27.">
  </picture>
  <figcaption>The play the scripted clears claimed, added up as they arrived, against the time that passed. A ten-minute budget takes the first 11 at once, then about one a minute: 27 of the 150 by the end. Drawn by a script from the score service's database.</figcaption>
</figure>

## Stars that do not add up

The second sign was in the stars. A clear's stars come from its score and nothing else. The game divides the score by the level's floor, the least any clear can score: 10 points for each of the level's balls. From 1.8 times the floor a clear earns two stars, from 4.0 three and from 6.0 four. So on one board a higher score never has fewer stars.

Every scripted clear said one star. On all 63 boards that had another player's clear, that put one star on a score above someone else's four. No other submission in the database does anything like it.

The service could not see this, because it did not know where the stars change. Now the simulator writes each level's three star floors into its table: the least score for two, three and four stars. The service refuses a clear whose stars are not the ones its score earns.

Each floor is found by asking the game's rating itself, never by doing its arithmetic again. The rating compares a floating-point number, and a floor one point off would refuse a real clear that lands exactly on it. To see that the check can fail, we moved the floors by one point. Six tests failed, and so did the simulator's new gate, with 420 refusals: three floors on each of 140 levels.

The thresholds have been retuned before: three stars moved from 3.0 to 4.0 times the floor, and four stars from 4.5 to 6.0. A release already out keeps rating by the floors it shipped with. So the service accepts a clear whose stars match the floors of any table that names its board, not only the newest one.

On the boards of the levels as they ship today, all 96 real clears match their floors, and all 133 scripted ones would be refused.

<figure>
  <picture>
    <source media="(max-width: 600px)" srcset="/img/blog/two-and-a-half-hours-of-play/stars-phone-400.png 400w, /img/blog/two-and-a-half-hours-of-play/stars-phone-800.png 800w" sizes="calc(100vw - 32px)" width="400" height="468">
    <img src="/img/blog/two-and-a-half-hours-of-play/stars-800.png" srcset="/img/blog/two-and-a-half-hours-of-play/stars-800.png 800w, /img/blog/two-and-a-half-hours-of-play/stars-1600.png 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="470" loading="lazy" alt="A chart of stars against score as a multiple of the level's floor. Shaded bands mark where each number of stars begins and ends. The clears from the game, in blue, all sit in the band their score earns, most of them on the four-star row from six times the floor up. The scripted clears, in orange, all sit on the one-star row, at six to thirteen times the floor.">
  </picture>
  <figcaption>Every clear on the boards of the levels as version 0.5.0 ships them, by its score as a multiple of the level's floor. The real clears sit where their scores put them: 86 with four stars, 9 with three, 1 with two. Every scripted clear scored at least six times its floor, which is four stars' worth, and claimed one. Drawn by a script from the score service's database.</figcaption>
</figure>

The service has the check now. It starts refusing once the table of the game's next release is installed, the first table with the floors in it.

## Dev builds keep to themselves

The scripted clears said `dev`, the version of a build made from the source rather than downloaded. Antonín ruled on that the same day: "In release versions of the game we never want to show scores from dev versions. Scores sent from dev versions may be shown only in dev versions, never in official releases."

So a board now depends on who asks for it.

- **What the game sends:** the game introduces itself as `BS3D/<version>` when it asks for a board, and every release since 0.3.0 has.
- **Dev builds** (`BS3D/dev…`) get every score.
- **Anyone else** gets only the scores releases sent: a release, a browser, or a script.
- **A release's own clear** is ranked among releases only.

Nothing in the game had to change, and no release needs an update.

It makes the release boards emptier. Of the 279 submissions, 37 came from releases, on 32 boards. On the day the change went live, a release saw 32 boards with a score on them and a dev build saw 63.

## The player

We hid the player rather than delete them. Hiding takes a player off every board and every count and deletes nothing. The leader on Amphora is the real one again, at 50,990; the script had sent that score plus one.

## What this does not stop

Both checks stop a careless script, not a careful one. A script that waits out its own durations and sends the stars its scores earn gets through. The budget only makes the same run take two and a half hours instead of under eighteen minutes. The game is open source, so anything it knows about a clear, a script can know too. Only a record of the shots, replayed by the service, would prove that a clear was played, and we have not built that.

What the evening did leave is a list of what gave the script away, and each item is one query on the database:

- an agent that is not the game's;
- the same shots, time and stars on every clear;
- more play claimed than time passed;
- scores of exactly the leader's plus one;
- refusals just before the first clear went through.
