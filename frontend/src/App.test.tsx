import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('App shell', () => {
  it('renders the header and main heading', () => {
    render(<App />)

    expect(screen.getByRole('link', { name: 'Book Exchange' })).toBeTruthy()
    expect(screen.getByRole('heading', { level: 1, name: 'Find books near you' })).toBeTruthy()
  })
})
