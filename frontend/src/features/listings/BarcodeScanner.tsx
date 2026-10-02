import { useEffect, useRef, useState } from 'react'
import { useI18n } from '../../i18n/context'
import { isBookBarcode } from './isbn'

/** Why the camera couldn't start, from the getUserMedia error name. */
export type CameraProblem = 'denied' | 'notFound' | 'inUse' | 'insecure' | 'unknown'

export function cameraProblemFrom(error: unknown): CameraProblem {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) return 'insecure'
  const name = error instanceof DOMException || error instanceof Error ? error.name : ''
  switch (name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'denied'
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'notFound'
    case 'NotReadableError':
    case 'AbortError':
      return 'inUse'
    default:
      return 'unknown'
  }
}

type ScanControls = { stop: () => void }
type ScanResult = { getText: () => string } | undefined

/** The part of ZXing's reader the scanner uses; injectable so tests don't need module mocks. */
export type BarcodeReader = {
  decodeFromConstraints: (
    constraints: MediaStreamConstraints,
    video: HTMLVideoElement,
    callback: (result: ScanResult) => void,
  ) => Promise<ScanControls>
}

/** Loads ZXing on demand (kept out of the main bundle), restricted to EAN-13 book barcodes. */
async function createZxingReader(): Promise<BarcodeReader> {
  const [{ BrowserMultiFormatReader }, { BarcodeFormat, DecodeHintType }] = await Promise.all([
    import('@zxing/browser'),
    import('@zxing/library'),
  ])
  return new BrowserMultiFormatReader(
    new Map([[DecodeHintType.POSSIBLE_FORMATS, [BarcodeFormat.EAN_13]]]),
  ) as unknown as BarcodeReader
}

/**
 * Camera barcode scanner (ZXing; Safari has no BarcodeDetector). The library is loaded only when the
 * scanner opens, so it stays out of the main bundle. Only book barcodes (EAN-13, 978/979) are accepted.
 */
export function BarcodeScanner({
  onDetected,
  onError,
  createReader = createZxingReader,
}: {
  onDetected: (isbn: string) => void
  onError: (problem: CameraProblem) => void
  createReader?: () => Promise<BarcodeReader>
}) {
  const { t } = useI18n()
  const video = useRef<HTMLVideoElement>(null)
  const [starting, setStarting] = useState(true)

  useEffect(() => {
    let stop: (() => void) | undefined
    let cancelled = false

    void (async () => {
      try {
        const reader = await createReader()
        // React StrictMode (dev) mounts twice; the first mount is already cleaned up by now. Opening
        // the camera from both would fail on Windows, where a webcam can't be opened twice at once.
        if (cancelled) return

        const controls = await reader.decodeFromConstraints(
          // "ideal" so laptops (front camera only) still work; phones get the rear camera.
          { video: { facingMode: { ideal: 'environment' } } },
          video.current!,
          (result) => {
            const text = result?.getText()
            if (text && isBookBarcode(text)) {
              controls.stop()
              onDetected(text)
            }
          },
        )
        stop = () => controls.stop()
        if (cancelled) stop()
        else setStarting(false)
      } catch (error) {
        console.warn('Barcode scanner could not start the camera:', error)
        if (!cancelled) onError(cameraProblemFrom(error))
      }
    })()

    return () => {
      cancelled = true
      stop?.()
    }
  }, [onDetected, onError, createReader])

  return (
    <div className="space-y-2">
      <video
        ref={video}
        muted
        playsInline
        className="bg-text aspect-[4/3] w-full rounded-md object-cover"
      />
      <p className="text-text-muted text-sm">
        {starting ? t('common.loading') : t('create.scanHelp')}
      </p>
    </div>
  )
}
