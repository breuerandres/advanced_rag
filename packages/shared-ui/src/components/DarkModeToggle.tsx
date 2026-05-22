import { Moon, Sun } from 'lucide-react';
import { Button } from './Button';
import { useTheme } from '../hooks/useTheme';

interface DarkModeToggleProps {
  /** Accessible label, used for screen readers and tooltip. Defaults to 'Toggle theme'. */
  label?: string;
}

/**
 * One-click theme toggle. Resolves to light or dark; system-preference users can
 * still cycle with the button.
 */
export function DarkModeToggle({ label = 'Toggle theme' }: DarkModeToggleProps) {
  const { resolved, toggle } = useTheme();
  const Icon = resolved === 'dark' ? Sun : Moon;
  return (
    <Button variant="ghost" size="icon" onClick={toggle} aria-label={label} title={label}>
      <Icon className="h-4 w-4" aria-hidden="true" />
    </Button>
  );
}
