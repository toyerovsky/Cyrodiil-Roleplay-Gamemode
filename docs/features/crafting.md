# Crafting

## 1. What it is

Characters will make the game's items in one of nine trades. So far only the ground under it is built: every item
of the game's files has a class, and every product has a trade and the level it takes. Nothing a player sees uses it
yet.

## 2. Rules

- **Trades:** blacksmith (melee weapons, tools, metal ware), armourer (metal armour and shields), woodworker (bows,
  arrows, wooden ware, the torch), tailor (clothes, fur and leather armour, cloth), jeweller (jewelry, cut gems,
  pottery, silver, glass, alchemy apparatus, empty soul gems), alchemist (potions and poisons), cook (food and drink),
  enchanter (enchanted gear, staves, filled soul gems), scribe (books, scrolls, spell tomes).
- **Levels and ranks:** a trade's level runs from 0 to 100, in five ranks: novice from 0, apprentice from 25,
  journeyman from 50, expert from 75, master at 100 alone (`TradeRules`).
- **Classes** (`ItemClass`):
  - product: a trade makes it, from a level. Some take a pattern besides the level: the gear of a faction (the
    Legion, the town guards, the Blades), of the Shivering Isles and of the Dremora, skill books, skooma.
  - material: found or gathered and used up by the trades: ores, nuggets, flawed gems, pelts, ingredients, rusty
    iron, sigil stones.
  - relic: a quest's or a unique item. No trade makes it.
  - exempt: the crafting has no say over it: gold, keys, conjured gear, the game's test forms, paper clutter.
- Every trade has products without a pattern from level 0 and in every rank below master, so a character always has
  something to make and to learn on.

## 3. Ways in

None yet.

## 4. How it works

- `Crafting/classification.json` holds the rules in order. The first rule that fits an item classes it. A rule names
  the item's record type, a regular expression over its editor id, or both. Matching minds the letter case, and a
  rule turns it off where the game's ids are not consistent, with `(?i:...)`.
- A product's rule names its trade and either a `level` or a `table`. A table gives the level by the part of the id
  the rule's group `key` takes: the metal of a weapon, the rank of a scroll, the grade of a soul gem.
- `CraftCatalog.Shipped` reads the file against the items the mode ships (`Admin/items.json`) and answers
  `Find(form)`. A form the game's files lack, as a custom item's, has no class.
- Loading throws on an item no rule classes, a rule that classes nothing, a table no rule names, a key a table
  lacks and a rule that does not hold together. `Crafting/CraftCatalogTests` loads the shipped file, so a mistake in
  it fails the build, as does a new export of the items that brings forms no rule knows.

## 5. Changing the classification

Move an item to another trade or level by a rule of its own above the one that takes it now, or by the level in the
table. The levels are the mode's own, not the game's: they set how far into a trade a product lies.
