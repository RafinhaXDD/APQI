# AQPI design system

Brand by Duda (official guidelines), adopted 2026-10-02. Light, warm and "tech-premium": cream background by
default, navy for structure, terracotta / gold / sage as accents. Tagline: **Troque. Leia. Repita.**

Tokens are defined **once**, in the `@theme` block of `src/index.css`. Tailwind's default palette is removed, so
only these tokens exist as utilities (`bg-primary`, `text-ink`, `border-border`, …). Never use raw hex in
components. `src/design/tokens.test.ts` checks every pair below against WCAG AA and scans components for misuse.

## Colors

| Token            | Hex                  | Use                                                                          |
| ---------------- | -------------------- | ---------------------------------------------------------------------------- |
| `primary`        | `#1D3D5C` navy       | Headings, links, primary buttons, Active badge, focus ring                   |
| `primary-dark`   | `#0F1F2E`            | Header/footer, cool sections, hover                                          |
| `secondary`      | `#D4936D` terracotta | Fills only: selected chips, secondary CTAs, Disputed badge                   |
| `accent`         | `#D0B088` gold       | Fills only: Pending/Reserved badge, ficha counter, secondary buttons on dark |
| `sage`           | `#8B9D83`            | Fills only: "Bom para começar" tag and highlight block                       |
| `background`     | `#F5F3EF` cream      | Page background                                                              |
| `surface`        | `#FFFFFF`            | Cards, inputs, modals                                                        |
| `text`           | `#4A4A4A` charcoal   | Body text                                                                    |
| `ink`            | `#0F1F2E`            | Text on terracotta, gold and sage fills                                      |
| `text-muted`     | `#6A6A6A`            | Metadata, timestamps (≥ 14 px)                                               |
| `secondary-text` | `#9E5A36`            | Terracotta-coloured labels (eyebrows) as readable text                       |
| `sage-text`      | `#5E6F57`            | Sage-coloured labels as readable text                                        |
| `border`         | `#E2DFD6`            | Borders, dividers                                                            |
| `success`        | `#3E7C59`            | Accepted/Completed                                                           |
| `error`          | `#B94A48`            | Errors, Rejected, destructive actions                                        |

### The one rule: accents are fills, not text

Terracotta, gold and sage as text on cream are 2.3:1, 1.9:1 and 2.6:1 (AA needs 4.5:1). Charcoal on terracotta
(3.5:1) and on gold (4.3:1) also fail. So:

| Fill                                                        | Text on it                                                              |
| ----------------------------------------------------------- | ----------------------------------------------------------------------- |
| `primary`, `primary-dark`, `success`, `error`, `text-muted` | `surface` (white)                                                       |
| `secondary`, `accent`, `sage`                               | `ink`                                                                   |
| `background`, `surface`                                     | `text`, `primary`, `text-muted`, `secondary-text`, `sage-text`, `error` |

Decorative graphics (the logo mark, blurred mesh blobs) may use accent colours directly; a graphic that carries
meaning needs 3:1 against its background.

## Status badges

Always a filled badge with a label, never coloured text alone.

| Status                           | Fill         | Text      |
| -------------------------------- | ------------ | --------- |
| Exchange `Pending`               | `accent`     | `ink`     |
| Exchange `Accepted`, `Completed` | `success`    | `surface` |
| Exchange `Rejected`              | `error`      | `surface` |
| Exchange `Cancelled`, `Expired`  | `text-muted` | `surface` |
| Exchange `Disputed`              | `secondary`  | `ink`     |
| Listing `Active`                 | `primary`    | `surface` |
| Listing `Reserved`               | `accent`     | `ink`     |
| Listing `Exchanged`, `Archived`  | `text-muted` | `surface` |
| Tag "Bom para começar"           | `sage`       | `ink`     |

## Type

Two free fonts (SIL Open Font License), self-hosted from `@fontsource-variable/*`, replace the paid **All Round
Gothic**:

- **Quicksand** (`--font-display`, `font-display`): headings, buttons, the wordmark and big statements. Its
  geometric round letters are the closest free match to All Round Gothic. Weights 300–700, so headings top out at 700.
- **Nunito** (`--font-sans`): body text, labels and forms; stays readable at small sizes where Quicksand's thin
  strokes would not.

Headings are navy, bold, with tight tracking; eyebrow labels are 12 px, bold, uppercase, wide tracking.

## Brand assets

- **Logo mark** (`src/components/AqpiLogo.tsx`): the speech-bubble book with a bookmark, redrawn as SVG from
  `aqpi-simbolo.png`; takes `currentColor`. App icon: `public/icon.svg` (gold mark on navy).
- **Mascot** (`public/images/mascot-*.webp`): the navy worm with white gloves. A complementary character, not the
  logo. Emotion comes from posture. Used in the hero, the four steps, empty states and 404. Images have white
  backgrounds and use `mix-blend-multiply` to sit on cream.

## Motion and surfaces

- Animate only `transform` and `opacity`. `animate-rise` (staggered entrances) and `animate-drift` (hero mesh).
- Everything stops under `prefers-reduced-motion`.
- Cards: white surface, `rounded-2xl`, two-layer `shadow-card` (tight edge + soft spread).
- Section temperature: warm cream sections, one cool navy section (manifesto / closing CTA).
- Focus: 2 px `primary` ring with a white offset (global `:focus-visible`).
- Mobile first; check 375 px and 1280 px. Dark mode is out of scope for v1.
