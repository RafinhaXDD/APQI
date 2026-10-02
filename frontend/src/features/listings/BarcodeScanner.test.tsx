import { render, waitFor } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { I18nProvider } from '../../i18n/I18nProvider'
import { BarcodeScanner, cameraProblemFrom, type BarcodeReader } from './BarcodeScanner'

const decodeFromConstraints = vi.fn<BarcodeReader['decodeFromConstraints']>()
// Stable reference, like the real default: a new function per render would restart the camera.
const createReader = () => Promise.resolve<BarcodeReader>({ decodeFromConstraints })

describe('BarcodeScanner', () => {
  beforeEach(() => {
    vi.stubGlobal('isSecureContext', true)
    vi.stubGlobal('navigator', { ...navigator, mediaDevices: { getUserMedia: vi.fn() } })
    decodeFromConstraints.mockReset()
  })
  afterEach(() => vi.unstubAllGlobals())

  it('opens the camera only once under StrictMode (Windows webcams cannot be opened twice)', async () => {
    decodeFromConstraints.mockResolvedValue({ stop: vi.fn() })

    render(
      <StrictMode>
        <I18nProvider initialLanguage="en">
          <BarcodeScanner onDetected={vi.fn()} onError={vi.fn()} createReader={createReader} />
        </I18nProvider>
      </StrictMode>,
    )

    await waitFor(() => expect(decodeFromConstraints).toHaveBeenCalled())
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(decodeFromConstraints).toHaveBeenCalledTimes(1)
  })

  it('hands a scanned book barcode to the page and stops the camera', async () => {
    const stop = vi.fn()
    decodeFromConstraints.mockImplementation(async (_constraints, _video, callback) => {
      setTimeout(() => {
        callback({ getText: () => '7891000315507' }) // grocery EAN: ignored
        callback({ getText: () => '9788535914849' }) // book
      })
      return { stop }
    })
    const onDetected = vi.fn()

    render(
      <I18nProvider initialLanguage="en">
        <BarcodeScanner onDetected={onDetected} onError={vi.fn()} createReader={createReader} />
      </I18nProvider>,
    )

    await waitFor(() => expect(onDetected).toHaveBeenCalledWith('9788535914849'))
    expect(onDetected).toHaveBeenCalledTimes(1)
    expect(stop).toHaveBeenCalled()
  })

  it('reports why the camera failed instead of a generic error', async () => {
    decodeFromConstraints.mockRejectedValue(new DOMException('Device in use', 'NotReadableError'))
    const onError = vi.fn()

    render(
      <I18nProvider initialLanguage="en">
        <BarcodeScanner onDetected={vi.fn()} onError={onError} createReader={createReader} />
      </I18nProvider>,
    )

    await waitFor(() => expect(onError).toHaveBeenCalledWith('inUse'))
  })

  it.each([
    ['NotAllowedError', 'denied'],
    ['NotFoundError', 'notFound'],
    ['OverconstrainedError', 'notFound'],
    ['NotReadableError', 'inUse'],
    ['SomethingElse', 'unknown'],
  ])('maps %s to %s', (name, expected) => {
    expect(cameraProblemFrom(new DOMException('x', name))).toBe(expected)
  })

  it('explains that the camera needs https or localhost', () => {
    vi.stubGlobal('isSecureContext', false)
    expect(cameraProblemFrom(new DOMException('x', 'NotAllowedError'))).toBe('insecure')
  })
})
