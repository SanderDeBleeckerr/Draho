# Draho: Game Design Document

> Working title: **Draho**. Platform: iPhone (landscape). Engine: Unity.
> Status: pre-production. Version 1 scope is in [V1_PLAN.md](V1_PLAN.md).

## 1. Pitch

You play **Drahomil**, a black house cat. People hire him to hunt mice.
He starts at home, in a cozy apartment. Later, neighbors, shops and other
buildings hire him. Each finished job unlocks a new place to explore.

## 2. Pillars

1. **Feel like a real cat.** The movement, the animations and the scale of the
   world come from a cat's point of view. Furniture is big. Gaps under the sofa
   are hiding places.
2. **Cozy with bursts of chaos.** Calm spaces, soft light and purring are the
   base. Hunts and knocked-over objects give short moments of chaos.
3. **Short sessions on mobile.** A job takes 3–8 minutes. You can play with
   two thumbs and stop at any time.

## 3. Target

- **Players:** casual and mid-core mobile players, cat lovers, age 10+.
- **Platform:** iPhone. The minimum device is the iPhone 11 (A13 chip).
  The screen is landscape only.
- **Language:** English at launch. Keep text in localization tables from day one.
- **Business model:** free to play with in-app purchases (see §8).

## 4. Art and audio direction

- **Realistic** cat and interiors, made from bought assets.
- Mood: warm, natural light, a slightly soft and filmic look.
- A black cat is hard to read on screen. Use a rim light, a fur sheen, and a
  slightly lifted albedo instead of pure black.
- Audio: real cat sounds (paw steps on different floors, purr, meow, chirp).
  Ambient apartment sounds come later. There is no music in version 1.

## 5. Controls and camera

- **Left thumb:** a floating virtual joystick. Push it a little to walk and
  push it far to run.
- **Right thumb:** swipe to orbit the camera.
- **Camera:** third person, behind and above the cat, with collision against
  walls and furniture. The camera height is low, near a cat's eye level.
- Later versions add context buttons (jump, pounce, meow) on the right side.

## 6. Core loop (long term)

```
Accept a job (mouse problem at a client)
        ↓
Travel to the location (new building or room)
        ↓
Explore → find signs (droppings, noise, holes) → stalk → pounce
        ↓
Get paid (coins + reputation), and maybe cause chaos for a bonus or a penalty
        ↓
Unlock new clients and locations, buy upgrades and cosmetics
        ↺
```

### Planned systems (after version 1)
| System | Description |
|---|---|
| Jump and climb | Jump onto sofas, tables, shelves and window sills. Jumps have a target. |
| Stealth and stalking | A crouch-walk is quieter. Mice hear and see you. |
| Mouse AI | Mice flee, hide in holes, and come back out when it is quiet. |
| Jobs / bounty board | Each client has a building, a mouse count, a time limit and a bonus goal. |
| Chaos meter | Knocking things over is fun, but some clients do not like it. |
| Progression | Reputation unlocks clients. Coins buy collars, beds and abilities. |
| Locations | Home apartment → neighbor's flat → bakery → library → warehouse → … |

## 7. Version roadmap

| Version | Content |
|---|---|
| **v0.1 (Version 1)** | One room. Walk, run and idle. Joystick and camera. Collisions. Cat sounds. Runs on the iPhone 11. |
| v0.2 | Jump onto furniture. Sit, lie and sleep when idle. Full apartment (more rooms). |
| v0.3 | A first mouse with simple AI, a pounce, and one hunt inside the apartment. |
| v0.4 | Job board, a second location, coins and save data. |
| v0.5 | Physics props (chaos), start menu, settings, music. |
| v1.0 (store) | 3–5 locations, IAP store, onboarding, App Store release. |

## 8. Monetization (draft)

Free download with these in-app purchases:
- **Cosmetics:** collars, bells, beds, name tags. These do not change gameplay.
- **Location packs:** new buildings with new jobs, as optional paid expansions.
- **No pay-to-win** and no energy timers. Avoid ads, because they break the cozy mood.

## 9. Open questions

- Is Drahomil a silent cat, or does he have a voice or inner thoughts?
- Who gives the jobs? A phone, a notice board, or a human character?
- Do failed jobs have a cost?
- How do we build the iOS app? We decide before the first on-device test.
