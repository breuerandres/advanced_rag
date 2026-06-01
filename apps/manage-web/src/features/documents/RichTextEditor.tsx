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
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@helpcenter/shared-ui'

interface RichTextEditorProps {
  documentId?: string
  value: string
  onChange: (value: string) => void
  onUploadImage?: (file: File, altText: string) => Promise<{ url: string; altText: string }>
}

const MaxDocumentImageSizeBytes = 5 * 1024 * 1024

export function RichTextEditor({ documentId, value, onChange, onUploadImage }: RichTextEditorProps) {
  const { t } = useTranslation()
  const lastEditorHtml = useRef(value)
  const imageInputRef = useRef<HTMLInputElement | null>(null)
  const [imageError, setImageError] = useState<string | null>(null)
  const [isUploadingImage, setIsUploadingImage] = useState(false)
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
        'aria-label': t('documents.content_field'),
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
    const href = window.prompt(t('documents.link_url_prompt'), previousHref ?? 'https://')
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

    setImageError(null)
    if (!documentId || !onUploadImage) {
      setImageError(t('documents.image_save_before_upload'))
      return
    }

    imageInputRef.current?.click()
  }

  async function uploadSelectedImage(file: File) {
    if (!editor || !onUploadImage) {
      return
    }

    setImageError(null)
    if (file.size > MaxDocumentImageSizeBytes) {
      setImageError(t('documents.image_too_large'))
      return
    }

    const alt = window.prompt(t('documents.image_alt_prompt'), '') ?? ''
    setIsUploadingImage(true)
    try {
      const result = await onUploadImage(file, alt.trim())
      editor.chain().focus().setImage({ src: result.url, alt: result.altText }).run()
    } catch {
      setImageError(t('documents.image_upload_error'))
    } finally {
      setIsUploadingImage(false)
    }
  }

  return (
    <section className="html-editor" aria-label={t('documents.rich_editor_label')}>
      <div className="editor-toolbar" aria-label={t('documents.editor_toolbar')}>
        <ToolbarButton
          label={t('documents.toolbar_bold')}
          active={editor?.isActive('bold') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleBold().run()}
        >
          <Bold size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_italic')}
          active={editor?.isActive('italic') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleItalic().run()}
        >
          <Italic size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_underline')}
          active={editor?.isActive('underline') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleUnderline().run()}
        >
          <UnderlineIcon size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_heading_2')}
          active={editor?.isActive('heading', { level: 2 }) ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleHeading({ level: 2 }).run()}
        >
          <Heading2 size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_bullet_list')}
          active={editor?.isActive('bulletList') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleBulletList().run()}
        >
          <List size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_ordered_list')}
          active={editor?.isActive('orderedList') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleOrderedList().run()}
        >
          <ListOrdered size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_code')}
          active={editor?.isActive('codeBlock') ?? false}
          disabled={!editor}
          onClick={() => editor?.chain().focus().toggleCodeBlock().run()}
        >
          <Code2 size={16} />
        </ToolbarButton>
        <ToolbarButton label={t('documents.toolbar_link')} disabled={!editor} onClick={runLinkCommand}>
          <LinkIcon size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_insert_table')}
          disabled={!editor}
          onClick={() => editor?.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()}
        >
          <Table2 size={16} />
        </ToolbarButton>
        <ToolbarButton
          label={t('documents.toolbar_insert_image')}
          disabled={!editor || isUploadingImage}
          onClick={runImageCommand}
        >
          <ImageIcon size={16} />
        </ToolbarButton>
      </div>
      <input
        ref={imageInputRef}
        className="file-input-native"
        type="file"
        accept="image/png,image/jpeg,image/webp,image/gif"
        onChange={(event) => {
          const file = event.target.files?.[0]
          event.target.value = ''
          if (file) {
            void uploadSelectedImage(file)
          }
        }}
      />
      {imageError ? (
        <p className="status-message error" role="alert">
          {imageError}
        </p>
      ) : null}
      <EditorContent editor={editor} />
      <details className="html-source">
        <summary>{t('documents.generated_html')}</summary>
        <textarea
          aria-label={t('documents.generated_html')}
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
