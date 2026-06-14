import { EditorContent, useEditor, type Editor } from '@tiptap/react'
import { StarterKit } from '@tiptap/starter-kit'
import { Image as TiptapImage } from '@tiptap/extension-image'
import { Link as TiptapLink } from '@tiptap/extension-link'
import { Color } from '@tiptap/extension-color'
import { Highlight } from '@tiptap/extension-highlight'
import { Table } from '@tiptap/extension-table'
import { TableCell } from '@tiptap/extension-table-cell'
import { TableHeader } from '@tiptap/extension-table-header'
import { TableRow } from '@tiptap/extension-table-row'
import { TextAlign } from '@tiptap/extension-text-align'
import { TextStyle } from '@tiptap/extension-text-style'
import { Underline } from '@tiptap/extension-underline'
import {
  AlignCenter,
  AlignJustify,
  AlignLeft,
  AlignRight,
  Bold,
  Code as CodeIcon,
  Code2,
  Eraser,
  Highlighter,
  Image as ImageIcon,
  Italic,
  Link as LinkIcon,
  List,
  ListOrdered,
  Minus,
  Quote,
  Redo2,
  Strikethrough,
  Table2,
  Underline as UnderlineIcon,
  Undo2,
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
const DefaultTextColor = '#1f1b17'
type BlockStyle = 'paragraph' | 'heading-1' | 'heading-2' | 'heading-3'
type TextAlignment = 'left' | 'center' | 'right' | 'justify'

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
        link: false,
        underline: false,
      }),
      Underline,
      TextStyle,
      Color.configure({ types: [TextStyle.name] }),
      Highlight,
      TextAlign.configure({
        types: ['heading', 'paragraph'],
      }),
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

  function setBlockStyle(style: BlockStyle) {
    if (!editor) {
      return
    }

    const command = editor.chain().focus()
    if (style === 'paragraph') {
      command.setParagraph().run()
      return
    }

    const level = Number(style.replace('heading-', '')) as 1 | 2 | 3
    command.toggleHeading({ level }).run()
  }

  function setTextColor(color: string) {
    if (!editor) {
      return
    }

    editor.chain().focus().setColor(color).run()
  }

  function unsetTextColor() {
    editor?.chain().focus().unsetColor().run()
  }

  function toggleHighlight() {
    editor?.chain().focus().toggleHighlight().run()
  }

  function setTextAlignment(alignment: TextAlignment) {
    editor?.chain().focus().setTextAlign(alignment).run()
  }

  const currentTextColor = normalizeTextColor(editor?.getAttributes('textStyle').color)
  const currentBlockStyle = getCurrentBlockStyle(editor)
  const currentTextAlignment = getCurrentTextAlignment(editor)

  return (
    <section className="html-editor" aria-label={t('documents.rich_editor_label')}>
      <div className="editor-toolbar" aria-label={t('documents.editor_toolbar')}>
        <div className="toolbar-group">
          <ToolbarButton
            label={t('documents.toolbar_undo')}
            disabled={!editor}
            onClick={() => editor?.chain().focus().undo().run()}
          >
            <Undo2 size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_redo')}
            disabled={!editor}
            onClick={() => editor?.chain().focus().redo().run()}
          >
            <Redo2 size={16} />
          </ToolbarButton>
        </div>

        <div className="toolbar-group">
          <select
            className="toolbar-block-style"
            aria-label={t('documents.toolbar_block_style')}
            value={currentBlockStyle}
            disabled={!editor}
            onChange={(event) => setBlockStyle(event.target.value as BlockStyle)}
          >
            <option value="paragraph">{t('documents.toolbar_paragraph')}</option>
            <option value="heading-1">{t('documents.toolbar_heading_1')}</option>
            <option value="heading-2">{t('documents.toolbar_heading_2')}</option>
            <option value="heading-3">{t('documents.toolbar_heading_3')}</option>
          </select>
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
            label={t('documents.toolbar_blockquote')}
            active={editor?.isActive('blockquote') ?? false}
            disabled={!editor}
            onClick={() => editor?.chain().focus().toggleBlockquote().run()}
          >
            <Quote size={16} />
          </ToolbarButton>
        </div>

        <div className="toolbar-group">
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
            label={t('documents.toolbar_strike')}
            active={editor?.isActive('strike') ?? false}
            disabled={!editor}
            onClick={() => editor?.chain().focus().toggleStrike().run()}
          >
            <Strikethrough size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_inline_code')}
            active={editor?.isActive('code') ?? false}
            disabled={!editor}
            onClick={() => editor?.chain().focus().toggleCode().run()}
          >
            <CodeIcon size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_underline')}
            active={editor?.isActive('underline') ?? false}
            disabled={!editor}
            onClick={() => editor?.chain().focus().toggleUnderline().run()}
          >
            <UnderlineIcon size={16} />
          </ToolbarButton>
        </div>

        <div className="toolbar-group">
          <ToolbarButton
            label={t('documents.toolbar_align_left')}
            active={currentTextAlignment === 'left'}
            disabled={!editor}
            onClick={() => setTextAlignment('left')}
          >
            <AlignLeft size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_align_center')}
            active={currentTextAlignment === 'center'}
            disabled={!editor}
            onClick={() => setTextAlignment('center')}
          >
            <AlignCenter size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_align_right')}
            active={currentTextAlignment === 'right'}
            disabled={!editor}
            onClick={() => setTextAlignment('right')}
          >
            <AlignRight size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_align_justify')}
            active={currentTextAlignment === 'justify'}
            disabled={!editor}
            onClick={() => setTextAlignment('justify')}
          >
            <AlignJustify size={16} />
          </ToolbarButton>
        </div>

        <div className="toolbar-group">
          <ToolbarButton
            label={t('documents.toolbar_code_block')}
            active={editor?.isActive('codeBlock') ?? false}
            disabled={!editor}
            onClick={() => editor?.chain().focus().toggleCodeBlock().run()}
          >
            <Code2 size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_horizontal_rule')}
            disabled={!editor}
            onClick={() => editor?.chain().focus().setHorizontalRule().run()}
          >
            <Minus size={16} />
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
        </div>

        <div className="toolbar-group">
          <label className="toolbar-color-picker">
            <span className="sr-only">{t('documents.toolbar_text_color')}</span>
            <input
              aria-label={t('documents.toolbar_text_color')}
              type="color"
              value={currentTextColor ?? DefaultTextColor}
              disabled={!editor}
              onChange={(event) => setTextColor(event.target.value)}
            />
          </label>
          <ToolbarButton
            className="toolbar-highlight-button"
            label={t('documents.toolbar_highlight')}
            active={editor?.isActive('highlight') ?? false}
            disabled={!editor}
            onClick={toggleHighlight}
          >
            <Highlighter size={16} />
          </ToolbarButton>
          <ToolbarButton
            label={t('documents.toolbar_clear_text_color')}
            active={Boolean(currentTextColor)}
            disabled={!editor || !currentTextColor}
            onClick={unsetTextColor}
          >
            <Eraser size={16} />
          </ToolbarButton>
        </div>

        <div className="toolbar-group">
          <ToolbarButton
            label={t('documents.toolbar_insert_image')}
            disabled={!editor || isUploadingImage}
            onClick={runImageCommand}
          >
            <ImageIcon size={16} />
          </ToolbarButton>
        </div>
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

function getCurrentBlockStyle(editor: Editor | null | undefined): BlockStyle {
  if (!editor) {
    return 'paragraph'
  }

  if (editor.isActive('heading', { level: 1 })) {
    return 'heading-1'
  }

  if (editor.isActive('heading', { level: 2 })) {
    return 'heading-2'
  }

  if (editor.isActive('heading', { level: 3 })) {
    return 'heading-3'
  }

  return 'paragraph'
}

function normalizeTextColor(value: unknown) {
  if (typeof value !== 'string') {
    return null
  }

  const normalized = value.trim()
  return /^#[0-9a-f]{6}$/i.test(normalized) ? normalized : null
}

function getCurrentTextAlignment(editor: Editor | null | undefined): TextAlignment {
  if (!editor) {
    return 'left'
  }

  const textAlign =
    editor.getAttributes('heading').textAlign ?? editor.getAttributes('paragraph').textAlign

  if (textAlign === 'center' || textAlign === 'right' || textAlign === 'justify') {
    return textAlign
  }

  return 'left'
}

function ToolbarButton({
  active = false,
  children,
  className,
  disabled = false,
  label,
  onClick,
}: {
  active?: boolean
  children: ReactNode
  className?: string
  disabled?: boolean
  label: string
  onClick: () => void
}) {
  return (
    <Button
      aria-label={label}
      className={['icon-button', className].filter(Boolean).join(' ')}
      data-state={active ? 'active' : 'inactive'}
      disabled={disabled}
      type="button"
      onClick={onClick}
    >
      {children}
    </Button>
  )
}
