import { EditorContent, useEditor } from '@tiptap/react'
import { StarterKit } from '@tiptap/starter-kit'
import { Image as TiptapImage } from '@tiptap/extension-image'
import { Link as TiptapLink } from '@tiptap/extension-link'
import { Table } from '@tiptap/extension-table'
import { TableCell } from '@tiptap/extension-table-cell'
import { TableHeader } from '@tiptap/extension-table-header'
import { TableRow } from '@tiptap/extension-table-row'
import { Underline } from '@tiptap/extension-underline'
import {
  Bold,
  Code2,
  Heading2,
  Image as ImageIcon,
  Italic,
  Link as LinkIcon,
  List,
  ListOrdered,
  Table2,
  Underline as UnderlineIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { useEffect, useRef } from 'react'
import { Button } from '@helpcenter/shared-ui'

interface RichTextEditorProps {
  value: string
  onChange: (value: string) => void
}

export function RichTextEditor({ value, onChange }: RichTextEditorProps) {
  const lastEditorHtml = useRef(value)
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({
        heading: {
          levels: [1, 2, 3],
        },
      }),
      Underline,
      TiptapLink.configure({
        autolink: true,
        defaultProtocol: 'https',
        openOnClick: false,
      }),
      TiptapImage.configure({
        allowBase64: false,
        inline: false,
      }),
      Table.configure({
        resizable: true,
      }),
      TableRow,
      TableHeader,
      TableCell,
    ],
    content: value || '<p></p>',
    editorProps: {
      attributes: {
        'aria-label': 'Contenido del documento',
        class: 'tiptap-editor-surface',
        role: 'textbox',
      },
    },
    onUpdate: ({ editor: currentEditor }) => {
      const html = currentEditor.getHTML()
      lastEditorHtml.current = html
      onChange(html)
    },
  })

  useEffect(() => {
    if (!editor) {
      return
    }

    const nextHtml = value || '<p></p>'
    if (nextHtml !== lastEditorHtml.current && nextHtml !== editor.getHTML()) {
      editor.commands.setContent(nextHtml, { emitUpdate: false })
      lastEditorHtml.current = nextHtml
    }
  }, [editor, value])

  function runLinkCommand() {
    if (!editor) {
      return
    }

    const previousHref = editor.getAttributes('link').href as string | undefined
    const href = window.prompt('URL del enlace', previousHref ?? 'https://')
    if (href === null) {
      return
    }

    const trimmedHref = href.trim()
    if (trimmedHref.length === 0) {
      editor.chain().focus().extendMarkRange('link').unsetLink().run()
      return
    }

    editor.chain().focus().extendMarkRange('link').setLink({ href: trimmedHref }).run()
  }

  function runImageCommand() {
    if (!editor) {
      return
    }

    const src = window.prompt('URL de la imagen', 'https://')
    if (src === null || src.trim().length === 0) {
      return
    }

    const alt = window.prompt('Texto alternativo', '') ?? ''
    editor.chain().focus().setImage({ src: src.trim(), alt: alt.trim() }).run()
  }

  return (
    <section className="html-editor" aria-label="Editor TipTap">
      <div className="editor-toolbar" aria-label="Herramientas del editor">
        <ToolbarButton
          label="Negrita"
          active={editor?.isActive('bold') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleBold().run()}
        >
          <Bold size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Cursiva"
          active={editor?.isActive('italic') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleItalic().run()}
        >
          <Italic size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Subrayado"
          active={editor?.isActive('underline') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleUnderline().run()}
        >
          <UnderlineIcon size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Titulo 2"
          active={editor?.isActive('heading', { level: 2 }) ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleHeading({ level: 2 }).run()}
        >
          <Heading2 size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Lista con vinetas"
          active={editor?.isActive('bulletList') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleBulletList().run()}
        >
          <List size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Lista numerada"
          active={editor?.isActive('orderedList') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleOrderedList().run()}
        >
          <ListOrdered size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Codigo"
          active={editor?.isActive('codeBlock') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleCodeBlock().run()}
        >
          <Code2 size={16} />
        </ToolbarButton>
        <ToolbarButton label="Enlace" disabled={!editor} onClick={runLinkCommand}>
          <LinkIcon size={16} />
        </ToolbarButton>
        <ToolbarButton
          label="Insertar tabla"
          disabled={!editor}
          onClick={() => editor?.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()}
        >
          <Table2 size={16} />
        </ToolbarButton>
        <ToolbarButton label="Insertar imagen" disabled={!editor} onClick={runImageCommand}>
          <ImageIcon size={16} />
        </ToolbarButton>
      </div>
      <EditorContent editor={editor} />
      <details className="html-source">
        <summary>HTML generado</summary>
        <textarea
          aria-label="HTML generado"
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
      </details>
    </section>
  )
}

function ToolbarButton({
  active = false,
  children,
  disabled = false,
  label,
  onClick,
}: {
  active?: boolean
  children: ReactNode
  disabled?: boolean
  label: string
  onClick: () => void
}) {
  return (
    <Button
      aria-label={label}
      className="icon-button"
      data-state={active ? 'active' : 'inactive'}
      disabled={disabled}
      type="button"
      onClick={onClick}
    >
      {children}
    </Button>
  )
}
