import { expect, test } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from './App'

test('renders the management shell label', () => {
  render(<App />)

  expect(
    screen.getByRole('heading', { name: 'Instruction Management' }),
  ).toBeInTheDocument()
})
