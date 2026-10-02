import type { SVGProps } from 'react'

/**
 * The official AQPI mark (Duda's guidelines): a speech bubble shaped like a closed book with a bookmark ribbon.
 * Redrawn as SVG from aqpi-simbolo.png; it takes the current text color.
 */
export function AqpiMark(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="370 560 825 440" fill="none" aria-hidden="true" {...props}>
      <rect
        x="401"
        y="597"
        width="762"
        height="244"
        rx="122"
        stroke="currentColor"
        strokeWidth="38"
      />
      <path
        d="M560 676H1005M560 719H1005M560 762H1005"
        stroke="currentColor"
        strokeWidth="10"
        strokeLinecap="round"
      />
      <path
        d="M893 841V950C893 985 898 990 912 975L944 937C950 930 953 930 959 938L982 975C992 990 998 985 999 970L1006 841Z"
        fill="currentColor"
      />
    </svg>
  )
}

/** Mark + wordmark, for the header. */
export function AqpiLogo({ className = '' }: { className?: string }) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      {/* Decorative graphic, not text: gold on navy-dark is 8.2:1 (needs 3:1 for graphics). */}
      <AqpiMark className="h-5 w-auto" style={{ color: 'var(--color-accent)' }} />
      <span className="font-display text-lg font-bold tracking-tight">AQPI</span>
    </span>
  )
}
