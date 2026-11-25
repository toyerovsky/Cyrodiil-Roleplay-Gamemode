# Talking to the mode's people

## 1. What it is

A player reaches the people the mode stands (traders, bankers, stablemasters, mentors, the Imperial Registrar,
auctioneers, fishers) and the Jobs mod's foremen the way the game reaches anyone: by talking to them. The game's
dialogue offers one topic of the mode on them, "Services", and picking it opens that person's window. Before, the
interaction key (H) opened it.

## 2. Rules (as the player sees them)

- Activating one of the mode's people starts the game's own dialogue with them. Among the topics stands "Services"
  ("Usługi" in Polish).
- Picking it makes the person answer ("Of course. We will get to it as soon as we are done talking."). The window
  opens when the player leaves the dialogue: the shop, the bank, the stable, the mentor's, the auction house, the
  fisher's or the foreman's.
- The topic can be picked again every time. It is the same on every person of the mode, whatever they do.
- The topic is in the language the player joined with. A language chosen in the F10 menu shows on the topic after the
  next join.
- H no longer opens these windows. It still opens the wheel over a player and a notice board's window.
- The window still closes when the player walks more than 5 m away, leaves the world, or the person is taken away.

## 3. Ways in

| Way in | What it does | Code |
|---|---|---|
| The game's dialogue, topic "Services" | Opens the window of the person spoken to | `Talk/Conversations.cs`, each kind's `TryTalk` |

There is no command and no key.

## 4. How it works

- **The topic** (`TalkTopics`, server): when an account has loaded, the player's entity gets a quest in its `Journal`
  (`TalkRules.QuestId`) and a topic with one reply in its `Dialogue` (`TalkTopic.PeopleId`), in the player's
  language (`locales/*/talk.json`). The topic is offered by the speaker's base form, `TraderRules.BaseNpc`. The Jobs
  mod names the base of its foremen (`IJobForemen.NpcBase`): when it is another one, they get a second topic with the
  same words (`TalkTopic.ForemenId`, `TalkOffer.ListFor`).
- **The pick** (`SpokenTopicWatch`, client): the game adds a spoken topic of a mod to the player's `SpokenTopics`, with
  the ref id of the speaker in that player's game. The client takes the mode's entry off the list at once, finds the
  person among the `Npc` entities by that local ref id, and keeps the ref id the server gave them.
- **Waiting for the dialogue to close** (`GameCursor`, client): the game draws its cursor exactly while one of its
  menus is up, and hands the cursor to every page each tick. The chat's page reports when it comes and goes
  (`ui/shared/cursor.js`, `GameCursor` message). Once no menu of the game is up, the client sends `TalkTo(npc)`. A load
  in between drops it.
- **The window** (`Conversations`, server): `TalkTo` goes to the kinds registered with `Conversations.AddTarget`
  (`TraderShops`, `Bankers`, `Stables`, `Mentors`, `AuctionHouse`, `FisherShops`, `ForemanWindows`). The first whose
  person stands under the ref id opens its window, after its own check that the player is in the world and within 5 m
  (`CheckWindow`), with its own notice when not. A ref id that is nobody's is logged as a warning.
- **Trust**: the server takes the client's word that the topic was spoken, as it took the press of H. A client that
  lies gets a window it could also have reached by walking up to the person.

## 5. What the platform sets

Each of these comes from how the platform's dialogue works, and is why the feature is built as it is.

- **One topic for every kind of person.** A topic is offered by the speaker's base form and nothing else, and every
  person of the mode is made from one base, because the game has two generic bases without services, factions,
  packages and lines of their own. So the topic cannot be "Trade" on a trader and "Bank" on a banker.
- **A quest under the topic.** A mod's topic exists only under a quest. The mode's has no text for any stage, so the
  journal should not list it.
- **Spoken once, unless taken off the list.** The game does not add a topic to `SpokenTopics` again while it stands
  there. Only the player's own game may take it off: a server's write to that list reaches the owner as an override,
  after which the owner's game stops filling the list.
- **The speaker's ref id is local.** A person the server made has another ref id in each player's game, and the
  spoken entry carries that one. So the client translates it.
- **The dialogue does not end on a reply.** A topic of a mod cannot say goodbye, so the player leaves the dialogue
  themselves and the reply says so in character.
- **No word about the game's menus.** Hence the cursor. A window of the mode opened over the dialogue would take the
  mouse from it for good when it closes: giving the input back sets the game to play, not to what it was.
- **A name that stays.** The game builds a topic once a session and never renames or removes it.

What would lift them, in the SDK: a topic offered by something the server sets on a person (its origin, the
`SpawnerRefId`), a reply that ends the dialogue, an event for a spoken topic with the server's ref id, a topic without
a quest, and a topic that can be renamed or taken back.

## 6. Not checked in the game yet

The code compiles against the SDK and its rules are tested, but none of this has run in the game:

- that the dialogue opens on a person the server made, for the cell's master and for the other players;
- that "Services" shows on them and only on them (the game's own dead miners share the base), with Polish letters;
- that the quest stays out of the journal and shows no "Quest updated" notice at the join;
- that the pick is told every time, not only the first;
- that the chat's page hears the cursor while the dialogue is up, so the window waits for the dialogue to close;
- that the crosshair names the person by what they do (`TraderCrosshair`), and what the dialogue's own heading calls
  them (the base's name is "Miner").
