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
export { Header } from './components/Header';
export { Sidebar } from './components/Sidebar';
export { DarkModeToggle } from './components/DarkModeToggle';
export { CommandPalette } from './components/CommandPalette';
export type { CommandGroup, CommandItem } from './components/CommandPalette';
