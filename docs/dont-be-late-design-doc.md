# Don't Be Late — Game Design Doc

Oct 23, 2026 · @ahmet

## Concept

*Don't Be Late* is a chaotic, low-poly comedy game about getting to work on time, by any means necessary. Each run is one frantic morning: wake up, get out of the apartment, catch the bus, and reach your desk before 9:00.

Every morning looks normal, but something is always different. Like *Exit 8*, the player must spot small clues and figure out what today's problem is. Realism is not the goal. If the neighbor won't stop talking, finding a gun is a valid solution.

## Core loop

The game is a single continuous run of about 30 minutes (exact length to be decided). Failing at any point sends the player back to bed to start over.

The run passes through four stages:

1. **Home:** wake up, get ready, find your things.
2. **The street:** get from the building to the bus stop.
3. **The bus:** board, pay, survive the ride, get off at the right stop.
4. **The office:** get into the building, up to your floor, and to your desk by 9:00.

Each failure teaches the player one new thing. What they learn is not a fixed route but what a clue means, so the knowledge carries into future runs even though the scenarios change.

## Wake-up mini game

Every run opens with a short wake-up mini game. The player's performance sets the starting time: a perfect wake-up starts the morning at 7:30, a poor one at around 7:38.

Candidate mechanics:

- **Snooze fight:** the character's hand drifts toward the snooze button and the player fights it off.
- **Heavy eyelids:** a timing or balance challenge to keep the eyes open while the view blurs.
- **Dream escape:** a 20-second dream sequence where the player runs for the exit to wake up.

## Random scenarios

Every run draws random scenarios from a large pool, so no two mornings play the same way. Each scenario has a clue the player can spot early, a requirement they must meet, and chaotic ways to get around it.

Missing a requirement does not fail the player on the spot. The failure arrives later as a consequence: skip the umbrella at 7:40, slip in a puddle at 8:05, arrive soaked. The player only realizes what the clue meant after it's too late.

| Clue | What it means | What the player must do |
| --- | --- | --- |
| Radio mentions a transit announcement | Bus fare went up | Top up the bus card or bring cash |
| Neighbor's door is open | He's waiting to chat | Avoid the hallway or deal with him |
| Puddles outside the window | It rained overnight | Take an umbrella and the right shoes |
| Different number on the bus stop sign | The route changed | Take another bus |
| Calendar alert on the phone | Meeting with the CEO | Wear a tie and a clean shirt |
| Cat is not on its bed | The cat ran off with the keys | Chase the cat |
| Sirens in the distance | Police are checking IDs | Bring ID or use back alleys |
| Strange smell from the fridge | Spoiled food | Skip breakfast |
| Construction noise | The street is blocked | Leave by window, roof, or basement |
| Roommate's sticky note | They took the bus card | Find another way to pay |

**Design rules:**

- **Normal mornings exist.** Sometimes nothing is wrong, and overreacting wastes time.
- **Clues are subtle but fair.** Players miss them the first time and recognize them after failing.
- **Scenarios stack.** Early runs have one; later runs combine two or three.
- **Requirements can chain.** Fixing one problem can reveal the next: no bus card, then no balance on it, then the kiosk only takes cash.
- **A fridge of sticky notes** records what the character has learned, acting as a journal of discovered scenarios.

## Obstacles and chaotic solutions

Each obstacle is a small puzzle box with several solutions, and the most extreme ones should be the funniest.

| Stage | Obstacle | Possible solutions |
| --- | --- | --- |
| Home | Keys are missing | Search the apartment, or kick the door down |
| Home | Cat sitting on your shoes | Bribe it with tuna, fight it, or go in slippers |
| Home | Roommate in the bathroom | Wait, pick the lock, or use the plant pot |
| Home | Coffee machine explodes | Drink it off the floor for a speed boost, or skip it and get a sleepy debuff |
| Home | Elevator is broken | Stairs, slide the banister, or rope down with bedsheets |
| Street | Chatty neighbor | Find a gun, pull the fire alarm, fake a phone call, or jump off the balcony |
| Street | Pigeon swarm | Push through and lose time, or fight the pigeons |
| Street | Wet cement | Get stuck, or steal a worker's plank |
| Street | Vendor cart rolling downhill | Ride it: huge speed, no steering |
| Street | Lost tourist | Help, mislead (they return angry), or steal their bike |
| Street | Dog walker with 12 dogs | Jump the leashes like a jump rope |
| Street | Protest march | Join it with a sign, or go under through a manhole |
| Bus | Bus arrives full | Push people off, ride the roof, or cling to the bumper |
| Bus | No ticket money | Rob a vending machine, sell a shoe, or hide in a bag |
| Bus | Driver takes a wrong turn | Hijack the bus |
| Bus | Smelly food next to you | Endure a nausea meter, or throw it out the window |
| Bus | Your stop is skipped | Jump out the window |
| Office | Forgot your badge | Disguise as a delivery driver, or climb the building |
| Office | Elevator full of coworkers | Fire alarm, stairs, or ride on top of the elevator |
| Office | Boss next to your desk | Crawl under desks, or make a distraction |

Chaos piles up within a run: shoot the neighbor and police patrol the street; steal a scooter and an angry kid chases you.

## Consequences and carryover

What the player does in one run can show up in later runs, but not always. Each consequence has a chance to appear next time, and that chance fades over the following runs. This creates recurring jokes without locking the world into one state.

Examples:

- The neighbor you shot might greet you with a shotgun, a bandage, or a restraining order, or act as if nothing happened.
- The tourist you misled might return as a police officer.
- The kid whose scooter you stole might be waiting with friends.
- The cat you fought might be missing, and now there are rats in the apartment.

## Art style and tone

The game uses low-poly 3D art. The tone is absurd and chaotic, in the spirit of *Goat Simulator* and *Untitled Goose Game*. Violence stays cartoonish: bright colors, ragdoll physics, and characters who come back with a bandage. This keeps it funny rather than grim and helps with store ratings and streaming.

## Open questions

- [ ] Exact run length: is 30 minutes right?
- [ ] Does the in-game clock run in real time (1:1), or is the morning compressed?
- [ ] Does the player keep anything between runs besides knowledge (unlocks, shortcuts)?
- [ ] Is there a score beyond arriving on time, such as a chaos or style rating?
- [ ] What is the ending once the player finally makes it?
- [ ] Target platform and engine
