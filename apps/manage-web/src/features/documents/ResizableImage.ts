import { Image as TiptapImage } from '@tiptap/extension-image'

const MinWidthPx = 48

/**
 * Image extension that persists a `width` attribute on the `<img>` element and
 * renders a bottom-right drag handle so editors can resize images directly.
 * Width is stored as an HTML attribute (not inline style) so it survives the
 * server-side Ganss sanitizer, which strips all CSS except color/text-align.
 */
export const ResizableImage = TiptapImage.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      width: {
        default: null,
        parseHTML: (element) => {
          const raw = element.getAttribute('width')
          if (!raw) {
            return null
          }
          const parsed = Number.parseInt(raw, 10)
          return Number.isFinite(parsed) ? parsed : null
        },
        renderHTML: (attributes) => {
          if (!attributes.width) {
            return {}
          }
          return { width: attributes.width }
        },
      },
    }
  },

  addNodeView() {
    return (props) => {
      const { editor, getPos } = props
      let currentNode = props.node

      const container = document.createElement('div')
      container.className = 'resizable-image'

      const img = document.createElement('img')
      applyImageAttributes(img, currentNode.attrs)
      container.appendChild(img)

      const handle = document.createElement('span')
      handle.className = 'resizable-image__handle'
      handle.setAttribute('aria-hidden', 'true')
      container.appendChild(handle)

      let startX = 0
      let startWidth = 0

      const onPointerMove = (event: PointerEvent) => {
        const next = Math.max(MinWidthPx, Math.round(startWidth + (event.clientX - startX)))
        img.setAttribute('width', String(next))
      }

      const onPointerUp = () => {
        window.removeEventListener('pointermove', onPointerMove)
        window.removeEventListener('pointerup', onPointerUp)
        const finalWidth = Number.parseInt(img.getAttribute('width') ?? '', 10)
        if (typeof getPos === 'function' && Number.isFinite(finalWidth)) {
          editor
            .chain()
            .focus(undefined, { scrollIntoView: false })
            .command(({ tr }) => {
              const pos = getPos()
              if (pos === undefined) {
                return false
              }
              tr.setNodeAttribute(pos, 'width', finalWidth)
              return true
            })
            .run()
        }
      }

      handle.addEventListener('pointerdown', (event) => {
        if (!editor.isEditable) {
          return
        }
        event.preventDefault()
        event.stopPropagation()
        startX = event.clientX
        startWidth =
          img.offsetWidth || (typeof currentNode.attrs.width === 'number' ? currentNode.attrs.width : 0) || MinWidthPx
        window.addEventListener('pointermove', onPointerMove)
        window.addEventListener('pointerup', onPointerUp)
      })

      return {
        dom: container,
        selectNode: () => container.classList.add('resizable-image--selected'),
        deselectNode: () => container.classList.remove('resizable-image--selected'),
        update: (updatedNode) => {
          if (updatedNode.type.name !== currentNode.type.name) {
            return false
          }
          currentNode = updatedNode
          applyImageAttributes(img, updatedNode.attrs)
          return true
        },
        destroy: () => {
          window.removeEventListener('pointermove', onPointerMove)
          window.removeEventListener('pointerup', onPointerUp)
        },
      }
    }
  },
})

function applyImageAttributes(img: HTMLImageElement, attrs: Record<string, unknown>) {
  img.src = typeof attrs.src === 'string' ? attrs.src : ''
  img.alt = typeof attrs.alt === 'string' ? attrs.alt : ''
  if (typeof attrs.title === 'string' && attrs.title.length > 0) {
    img.title = attrs.title
  } else {
    img.removeAttribute('title')
  }
  if (attrs.width) {
    img.setAttribute('width', String(attrs.width))
  } else {
    img.removeAttribute('width')
  }
}
