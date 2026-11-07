# Draho – Visual Art Direction & Unity Implementation Brief

> The owner's brief (November 2026), kept as written. [ART_STYLE.md](ART_STYLE.md) turns it into rules,
> palette values and Unity settings, and tracks the phases.

## Objective

Transform the existing Draho Unity mobile game into a visually cohesive, stylized 3D game built around the perspective and visual characteristics of a cat.

The desired aesthetic is a combination of:
- Cozy low-poly 3D environments.
- Atmospheric blue-hour and twilight lighting.
- Muted, natural colours.
- Warm yellow light sources and highlights.
- Soft shadows and subtle environmental animation.
- Strong visual readability, especially on mobile screens.

The game should feel like a charming, slightly mysterious world experienced by a cat.

**Do not turn the game into a horror game, a photorealistic game, or a generic cartoon.**

## 1. Core visual identity

Establish the following visual principles throughout the game:

### Colour palette

Use a restrained palette inspired by feline colour perception.

- Blue-grey: ambient lighting, shadows, distant environments and atmosphere.
- Muted blue: sky, environmental depth and cool surfaces.
- Soft yellow: sunlight, windows, lamps, interactive elements and highlights.
- Muted olive green: grass, trees, plants and vegetation.
- Warm grey: stone, walls and architecture.
- Natural brown: wooden furniture, doors and environmental props.
- Charcoal black: Drahomil and darker objects.

Avoid highly saturated rainbow colours.

Suggested colour references:

- Deep blue-grey: #38465C
- Muted blue: #687D9B
- Soft yellow: #E8C77D
- Pale warm yellow: #F3DFA7
- Muted olive: #858B69
- Warm grey: #A7A29B
- Charcoal: #292B32
- Warm off-white: #DED9CF

These are starting references, not mandatory material colours.

The overall world should remain colourful, but subdued and harmonious.

### 2. Lighting and atmosphere

Lighting is one of the most important parts of the art direction.

Use a combination of:

- Cool blue ambient lighting.
- Warm yellow directional light.
- Soft shadows.
- Subtle atmospheric fog where appropriate.
- Warm light leaking from apartment windows.
- Gentle contrast between illuminated areas and shadows.

The environments should look particularly attractive during dawn, dusk and early evening.

Avoid excessively dark scenes. The player must always be able to distinguish the cat, platforms, obstacles and interactive objects.

Create a consistent lighting setup using Unity URP and its Volume system.

### 3. Environment design

Preserve the existing low-poly geometry and environment assets wherever possible.

Improve their appearance through:

- Consistent materials.
- Harmonised colours.
- Better lighting.
- Subtle surface variation.
- Improved environmental composition.
- More natural vegetation placement.

The apartment should feel warm, lived-in and inviting.

The courtyard should feel slightly mysterious, with cool blue ambient lighting and warm illuminated windows.

The garden should feel alive, with muted vegetation, flowers, insects and environmental movement.

Do not replace existing environments with unrelated asset packs.

### 4. Drahomil – The cat

Drahomil must remain visually readable against every environment.

Pay particular attention to the black silhouette.

Use:
- Subtle rim lighting where necessary.
- Soft highlights along the ears, back and tail.
- Clear separation between the character and dark backgrounds.
- Appropriate shadow contrast.

Do not make the character artificially glow.

The cat should feel like a natural part of the environment.

### 5. Environmental animation

Make the world feel alive through subtle movement.

Prioritise:
- Grass moving gently.
- Leaves swaying.
- Curtains moving near open windows.
- Small insects flying around light sources.
- Flowers reacting to environmental movement.
- Small objects responding to the cat's proximity.

These animations should be lightweight and suitable for mobile hardware.

### 6. Feline perception

Use feline visual characteristics as an artistic inspiration, not as a strict scientific simulation.

In particular:
- Emphasise silhouettes and movement.
- Use strong visual contrast for important objects.
- Make small moving objects noticeable.
- Give nighttime environments a distinctive atmosphere.
- Use subtle visual cues rather than excessive UI markers.

Potential future feature: a temporary hunting-focus mode that makes moving prey and environmental disturbances easier to identify.

Do not implement this feature unless the existing gameplay architecture supports it or it is explicitly requested.

## 7. Unity implementation requirements

Before making changes:

1. Inspect the existing Unity project and its architecture.
2. Identify the current render pipeline and URP configuration.
3. Inspect existing materials, shaders, lighting, volumes and environment prefabs.
4. Identify existing scripts that generate or modify environments.
5. Review the existing apartment, courtyard and garden scenes.
6. Determine which existing assets can be reused.

Then implement the art direction incrementally.

Technical requirements:

- Maintain compatibility with Unity 6.
- Use URP-compatible materials and shaders.
- Prefer reusable material configurations.
- Avoid unnecessary custom shaders.
- Avoid introducing expensive real-time effects.
- Consider mobile GPU and battery constraints.
- Preserve existing gameplay, colliders, physics and animations.
- Avoid modifying unrelated scripts.
- Keep changes reversible and organised.

Use a global Volume for colour grading and post-processing where appropriate, but do not rely on a global colour filter to achieve the entire look. Materials and lighting must be individually coherent.

## 8. Visual quality priorities

Prioritise these in order:

1. Consistent colour palette.
2. Attractive lighting.
3. Readable black cat silhouette.
4. Coherent environment materials.
5. Environmental atmosphere.
6. Subtle environmental animation.
7. Additional visual polish.

The result should look intentional and professionally art-directed, not like a collection of unrelated low-poly assets.

## 9. Working methodology

Work incrementally.

**Phase 1 – Audit**
Inspect the current project and report the existing rendering and material setup.

**Phase 2 – Visual prototype**
Use the apartment as the first test environment.

Establish:
- Global colour palette.
- Lighting configuration.
- URP Volume settings.
- Material palette.
- Cat silhouette readability.

**Phase 3 – Review**
Capture before-and-after screenshots from comparable camera positions.

Evaluate:
- Colour consistency.
- Lighting quality.
- Visual readability.
- Character separation.
- Mobile performance.

**Phase 4 – Expansion**
Once the apartment looks correct, apply the same art direction to the courtyard and garden.

**Phase 5 – Polish**
Introduce subtle environmental animation and additional visual details.

Do not make sweeping changes to every scene at once.

## 10. Important instructions

- Do not redesign the gameplay.
- Do not replace the existing cat model.
- Do not delete or replace existing environment assets without justification.
- Do not introduce a completely different art style.
- Do not sacrifice performance for visual effects.
- Do not assume that adding more post-processing automatically improves visual quality.
- Prefer small, measurable improvements over large unverified changes.

If you can generate visual references, create a concept image of the existing apartment environment using this art direction before implementing substantial changes.

The final goal is a distinctive Draho visual identity: a cozy, atmospheric, low-poly world with cool blue-grey surroundings, warm yellow highlights, subdued natural colours and a strong sense of feline perspective.
