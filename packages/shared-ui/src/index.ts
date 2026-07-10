// Barrel exports for @helpcenter/shared-ui.
// Add new components/hooks here as they are created.

// Hooks
export { useTheme } from './hooks/useTheme';
export type { Theme } from './hooks/useTheme';
export { useShortcut } from './hooks/useShortcut';

// Lib
export { cn } from './lib/cn';

// Components
export { Button } from './components/Button';
export type { ButtonProps, ButtonVariant, ButtonSize } from './components/Button';
export { AppShell } from './components/AppShell';
export { AuthCardHeader, AuthFrame, AuthShell, AuthSurfaceControls } from './components/AuthShell';
export type {
  AuthCardHeaderProps,
  AuthFrameProps,
  AuthShellProps,
  AuthSurfaceControlsProps,
} from './components/AuthShell';
export { Header } from './components/Header';
export { Sidebar } from './components/Sidebar';
export { DarkModeToggle } from './components/DarkModeToggle';
export { LanguageSelect } from './components/LanguageSelect';
export type { LanguageSelectOption, LanguageSelectProps } from './components/LanguageSelect';
export { CommandPalette } from './components/CommandPalette';
export type { CommandGroup, CommandItem } from './components/CommandPalette';
export { Input } from './components/Input';
export type { InputProps } from './components/Input';
export { Textarea } from './components/Textarea';
export type { TextareaProps } from './components/Textarea';
export { Select } from './components/Select';
export type { SelectOption, SelectProps } from './components/Select';
export { Checkbox } from './components/Checkbox';
export type { CheckboxProps } from './components/Checkbox';
export { RadioGroup } from './components/RadioGroup';
export type { RadioOption, RadioGroupProps } from './components/RadioGroup';
export { Switch } from './components/Switch';
export type { SwitchProps } from './components/Switch';
export { Dialog } from './components/Dialog';
export type { DialogProps } from './components/Dialog';
export { Drawer } from './components/Drawer';
export type { DrawerProps } from './components/Drawer';
export { Tooltip } from './components/Tooltip';
export type { TooltipProps } from './components/Tooltip';
export { HoverCard } from './components/HoverCard';
export type { HoverCardProps } from './components/HoverCard';
export { Popover } from './components/Popover';
export type { PopoverProps } from './components/Popover';
export { DropdownMenu } from './components/DropdownMenu';
export type { DropdownMenuItem, DropdownMenuProps } from './components/DropdownMenu';
export { Badge } from './components/Badge';
export type { BadgeProps } from './components/Badge';
export { Avatar } from './components/Avatar';
export type { AvatarProps } from './components/Avatar';
export { Pagination } from './components/Pagination';
export type { PaginationProps } from './components/Pagination';
export { DataTable } from './components/DataTable';
export type { DataTableColumn, DataTableProps } from './components/DataTable';
export { ToastViewport, notify } from './components/Toast';
export { Skeleton } from './components/Skeleton';
export type { SkeletonProps } from './components/Skeleton';
export { EmptyState } from './components/EmptyState';
export type { EmptyStateProps } from './components/EmptyState';
export { Markdown } from './components/Markdown';
export type { MarkdownProps } from './components/Markdown';
export { ChatMessage } from './components/ChatMessage';
export type { ChatMessageAuthor, ChatMessageProps } from './components/ChatMessage';
export { ChatComposer } from './components/ChatComposer';
export type { ChatComposerProps } from './components/ChatComposer';
export { ConversationList } from './components/ConversationList';
export type { ConversationListItem, ConversationListProps } from './components/ConversationList';
export { CitationCard } from './components/CitationCard';
export type { CitationCardProps } from './components/CitationCard';
export { CitationDrawer } from './components/CitationDrawer';
export type { CitationDrawerItem, CitationDrawerProps } from './components/CitationDrawer';
