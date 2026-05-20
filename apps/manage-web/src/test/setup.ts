import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

const testRect: DOMRect = {
  bottom: 1,
  height: 1,
  left: 0,
  right: 1,
  top: 0,
  width: 1,
  x: 0,
  y: 0,
  toJSON: () => ({}),
}

function testClientRects(): DOMRectList {
  return [testRect] as unknown as DOMRectList
}

if (!document.elementFromPoint) {
  Object.defineProperty(document, 'elementFromPoint', {
    configurable: true,
    value: () => document.body,
  })
}

Object.defineProperty(window, 'scrollBy', {
  configurable: true,
  value: () => undefined,
})

if (!Element.prototype.getClientRects) {
  Object.defineProperty(Element.prototype, 'getClientRects', {
    configurable: true,
    value: testClientRects,
  })
}

if (!Range.prototype.getClientRects) {
  Object.defineProperty(Range.prototype, 'getClientRects', {
    configurable: true,
    value: testClientRects,
  })
}

if (!Range.prototype.getBoundingClientRect) {
  Object.defineProperty(Range.prototype, 'getBoundingClientRect', {
    configurable: true,
    value: () => testRect,
  })
}

if (typeof Text !== 'undefined' && !('getClientRects' in Text.prototype)) {
  Object.defineProperty(Text.prototype, 'getClientRects', {
    configurable: true,
    value: testClientRects,
  })
}

if (typeof Text !== 'undefined' && !('getBoundingClientRect' in Text.prototype)) {
  Object.defineProperty(Text.prototype, 'getBoundingClientRect', {
    configurable: true,
    value: () => testRect,
  })
}

afterEach(() => {
  cleanup()
})
