# Design system

Calm, paper-like interface (SPEC §11.4). Tokens are defined **once**, in the `@theme` block of
`src/index.css`. Tailwind's default palette is removed, so only these tokens exist as color
utilities (`bg-primary`, `text-text-muted`, `border-border`, …). Never use raw hex in components.

| Token          | Hex       | Use                                                        |
| -------------- | --------- | ---------------------------------------------------------- |
| `primary`      | `#2F5D50` | Primary buttons, active nav, links, focus rings            |
| `primary-dark` | `#1F4037` | Hover/pressed, header/footer, emphasis text                |
| `secondary`    | `#C9785D` | Secondary CTAs ("Request this book"), tags, selected chips |
| `accent`       | `#D9A441` | Rating stars, unread badges, reputation and credit markers |
| `background`   | `#F7F4ED` | Page background                                            |
| `surface`      | `#FFFDF8` | Cards, modals, inputs                                      |
| `text`         | `#252525` | Body and headings                                          |
| `text-muted`   | `#706D67` | Metadata, timestamps, placeholders (≥ 14 px only)          |
| `border`       | `#DED9CF` | Borders, dividers                                          |
| `success`      | `#3E7C59` | Success, Accepted, Completed                               |
| `error`        | `#B94A48` | Errors, Rejected, destructive actions                      |

## Text on filled backgrounds

| Fill                                                        | Text token |
| ----------------------------------------------------------- | ---------- |
| `primary`, `primary-dark`, `success`, `error`, `text-muted` | `surface`  |
| `accent`, **`secondary`**                                   | `text`     |

`secondary` differs from SPEC (which says `surface`): surface on secondary is only 3.26:1 and
fails WCAG AA; text on secondary is 4.62:1. See CLAUDE.md, "Design fix".

## Status badges

Always a filled badge with a label, never colored text alone.

| Status                           | Fill         | Text      |
| -------------------------------- | ------------ | --------- |
| Exchange `Pending`               | `accent`     | `text`    |
| Exchange `Accepted`, `Completed` | `success`    | `surface` |
| Exchange `Rejected`              | `error`      | `surface` |
| Exchange `Cancelled`, `Expired`  | `text-muted` | `surface` |
| Exchange `Disputed`              | `secondary`  | `text`    |
| Listing `Active`                 | `primary`    | `surface` |
| Listing `Reserved`               | `accent`     | `text`    |
| Listing `Exchanged`, `Archived`  | `text-muted` | `surface` |

## Rules

- Proportions per screen: ~70 % background/surface, ~20 % primary, ~10 % secondary/accent.
- Every text/background pair above meets WCAG AA (4.5:1); `src/design/tokens.test.ts` enforces it
  and fails if a token value drifts from this table.
- Focus: 2 px `primary` ring with a `surface` offset (global `:focus-visible` style).
- Mobile first; check layouts at 375 px and 1280 px. No dark theme in v1.
